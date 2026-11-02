SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

/* G2-42: textos con caracteres dañados en los datos demo («Ron AÃ±ejo 750ml»,
   «San JosÃ© Centro», «Whisky premium 12 aÃ±os»).

   Origen: database/00_todo_en_uno.sql traia esos textos guardados dos veces en
   UTF-8 (los bytes de «ñ», C3 B1, leidos como Windows-1252 y vueltos a guardar).
   El archivo ya se corrigio; esta migracion repara lo que se cargo con el.

   Que repara: solo los pares evidentes de esa doble codificacion,
     - Ã + caracter de 0x80-0xBF  ->  letra de U+00C0-U+00FF (á é í ó ú ñ ü Á É Ó Ú Ñ...)
     - Â + caracter de 0xA1-0xBF  ->  ese mismo signo (¡ ¿ º ª...)
   en estas columnas de texto de negocio y nada mas:
     Productos.Nombre, Productos.Descripcion, MovimientosInventario.ProductoNombre,
     MovimientosInventario.Motivo, FacturaDetalle.ProductoNombre,
     Usuarios.NombreCompleto, Usuarios.Direccion, Pedidos.DireccionEntrega,
     Pedidos.Observaciones.
   «Ã» y «Â» no aparecen en textos en español, asi que esos pares no tienen otra
   lectura. Los pares se arman con NCHAR/CHAR para que este archivo no dependa de
   su propia codificacion. Se repite hasta tres pasadas por si un texto se dano
   dos veces («AÃƒÂ±ejo»).

   No cambia montos, estados ni fechas; ningun disparador de esas tablas reacciona
   a estas columnas (TR_Pedidos_AnularCargoCredito solo actua si cambia Estado).
   Idempotente: una segunda pasada no encuentra pares. Rollback en
   0252_reparar_texto_mal_codificado.rollback.md. */
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL THROW 56520,N'Falta el ledger 0001.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0252_reparar_texto_mal_codificado' AND Status=N'Applied')
    THROW 56521,N'0252 ya figura aplicada.',1;
IF COLLATIONPROPERTY(CONVERT(sysname, DATABASEPROPERTYEX(DB_NAME(), 'Collation')), 'CodePage') <> 1252
    THROW 56522,N'0252 arma los pares con la pagina de codigos 1252 y la base usa otra: revisar antes de aplicar.',1;

DECLARE @Pares TABLE (Malo NVARCHAR(2) COLLATE Latin1_General_BIN2 NOT NULL PRIMARY KEY, Bueno NCHAR(1) NOT NULL);
WITH Bytes AS (SELECT TOP (64) 127 + ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS B FROM sys.all_objects)
INSERT @Pares (Malo, Bueno)
SELECT NCHAR(195) + CAST(CHAR(B) AS NVARCHAR(1)), NCHAR(192 + B - 128)
FROM Bytes
WHERE B NOT IN (129, 141, 143, 144, 157)          -- sin caracter en Windows-1252
UNION ALL
SELECT NCHAR(194) + CAST(CHAR(B) AS NVARCHAR(1)), NCHAR(B)
FROM Bytes
WHERE B >= 161;

DECLARE @Columnas TABLE (Tabla SYSNAME NOT NULL, Columna SYSNAME NOT NULL);
INSERT @Columnas VALUES
    (N'Productos', N'Nombre'), (N'Productos', N'Descripcion'),
    (N'MovimientosInventario', N'ProductoNombre'), (N'MovimientosInventario', N'Motivo'),
    (N'FacturaDetalle', N'ProductoNombre'),
    (N'Usuarios', N'NombreCompleto'), (N'Usuarios', N'Direccion'),
    (N'Pedidos', N'DireccionEntrega'), (N'Pedidos', N'Observaciones');
DELETE c FROM @Columnas c WHERE COL_LENGTH(N'dbo.' + c.Tabla, c.Columna) IS NULL;

DECLARE @Reparados INT = 0, @Pasada INT = 0, @EnPasada INT, @Filas INT;
DECLARE @Tabla SYSNAME, @Columna SYSNAME, @Malo NVARCHAR(2), @Bueno NCHAR(1), @Sql NVARCHAR(MAX);

BEGIN TRANSACTION;

WHILE @Pasada < 3
BEGIN
    SET @Pasada += 1;
    SET @EnPasada = 0;

    DECLARE pares CURSOR LOCAL FAST_FORWARD FOR
        SELECT c.Tabla, c.Columna, p.Malo, p.Bueno FROM @Columnas c CROSS JOIN @Pares p;
    OPEN pares;
    FETCH NEXT FROM pares INTO @Tabla, @Columna, @Malo, @Bueno;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @Sql = N'UPDATE dbo.' + QUOTENAME(@Tabla) + N' SET ' + QUOTENAME(@Columna)
                 + N' = REPLACE(' + QUOTENAME(@Columna) + N' COLLATE Latin1_General_BIN2, @Malo, @Bueno)'
                 + N' WHERE CHARINDEX(@Malo, ' + QUOTENAME(@Columna) + N' COLLATE Latin1_General_BIN2) > 0;'
                 + N' SET @Filas = @@ROWCOUNT;';
        EXEC sys.sp_executesql @Sql, N'@Malo NVARCHAR(2), @Bueno NCHAR(1), @Filas INT OUTPUT', @Malo, @Bueno, @Filas OUTPUT;
        SET @EnPasada += @Filas;
        FETCH NEXT FROM pares INTO @Tabla, @Columna, @Malo, @Bueno;
    END;
    CLOSE pares;
    DEALLOCATE pares;

    SET @Reparados += @EnPasada;
    IF @EnPasada = 0 BREAK;
END;

DECLARE @MigrationSha256 NVARCHAR(128)=N'$(MigrationSha256)';
IF LEN(@MigrationSha256)<>64 OR @MigrationSha256 LIKE N'%[^0-9A-Fa-f]%' THROW 56523,N'SHA-256 inválido para 0252.',1;
INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes)
VALUES(N'0252_reparar_texto_mal_codificado',N'0252_reparar_texto_mal_codificado.sql',UPPER(@MigrationSha256),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),
       CONCAT(N'G2-42: reparación de texto con doble codificación UTF-8 en columnas de negocio. Reemplazos aplicados: ', @Reparados, N'. Solo texto.'));
COMMIT TRANSACTION;
GO
