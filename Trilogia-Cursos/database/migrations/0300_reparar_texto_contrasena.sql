SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL THROW 58000,N'Falta el ledger 0001.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0300_reparar_texto_contrasena' AND Status=N'Applied') THROW 58001,N'La migracion ya fue aplicada.',1;
BEGIN TRANSACTION;
GO
/* Texto danado por doble codificacion en sp_Auth_UpdatePassword (guardado por la migracion 0022): la a con tilde de 'Credencial invalida.' quedaba como dos caracteres raros. Repara solo ese texto.
   Cambia solo textos: toma la definicion VIGENTE de cada objeto (sys.sql_modules),
   sustituye unicamente los fragmentos indicados y la vuelve a crear con CREATE OR ALTER.
   Archivo solo ASCII (los caracteres acentuados se arman con NCHAR) para no depender
   de la codificacion con que se ejecute. Si un fragmento ya no existe (porque otra
   migracion cambio el objeto), la migracion se detiene sin modificar nada. */
DECLARE @Frag TABLE (Orden INT IDENTITY(1,1), Objeto SYSNAME, Tipo NVARCHAR(10), Viejo NVARCHAR(400), Nuevo NVARCHAR(400));
INSERT @Frag (Objeto, Tipo, Viejo, Nuevo) VALUES
 (N'sp_Auth_UpdatePassword', N'PROCEDURE', N'Credencial inv' + NCHAR(195) + NCHAR(161) + N'lida.', N'Credencial inv' + NCHAR(225) + N'lida.');
DECLARE @o SYSNAME, @t NVARCHAR(10), @def NVARCHAR(MAX), @id INT, @v NVARCHAR(400), @n NVARCHAR(400), @p INT, @q INT, @sql NVARCHAR(MAX), @msg NVARCHAR(300);
DECLARE objs CURSOR LOCAL FAST_FORWARD FOR SELECT DISTINCT Objeto, Tipo FROM @Frag;
OPEN objs;
FETCH NEXT FROM objs INTO @o, @t;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @def = OBJECT_DEFINITION(OBJECT_ID(N'dbo.' + @o));
    IF @def IS NULL THROW 58002, N'No se encontro el objeto a corregir.', 1;
    DECLARE f CURSOR LOCAL FAST_FORWARD FOR SELECT Viejo, Nuevo FROM @Frag WHERE Objeto = @o ORDER BY Orden;
    OPEN f;
    FETCH NEXT FROM f INTO @v, @n;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        IF CHARINDEX(@v COLLATE Latin1_General_BIN2, @def COLLATE Latin1_General_BIN2) = 0
        BEGIN
            SET @msg = CONCAT(N'El texto a corregir ya no esta en ', @o, N'.');
            THROW 58003, @msg, 1;
        END;
        SET @def = REPLACE(@def COLLATE Latin1_General_BIN2, @v COLLATE Latin1_General_BIN2, @n);
        FETCH NEXT FROM f INTO @v, @n;
    END;
    CLOSE f; DEALLOCATE f;
    -- CREATE [espacios] PROCEDURE|TRIGGER dbo.<objeto>  ->  CREATE OR ALTER ...
    SET @p = CHARINDEX(@t + N' dbo.' + @o, @def COLLATE Latin1_General_BIN2);
    IF @p = 0 THROW 58004, N'No se reconoce el encabezado del objeto.', 1;
    SET @q = @p - CHARINDEX(REVERSE(N'CREATE'), REVERSE(LEFT(@def, @p))) - 4;
    IF @q < 1 OR SUBSTRING(@def, @q, 6) <> N'CREATE' THROW 58004, N'No se reconoce el encabezado del objeto.', 1;
    SET @sql = LEFT(@def, @q - 1) + N'CREATE OR ALTER' + SUBSTRING(@def, @q + 6, LEN(@def));
    EXEC (@sql);
    FETCH NEXT FROM objs INTO @o, @t;
END;
CLOSE objs; DEALLOCATE objs;
GO
DECLARE @sha NVARCHAR(128)=N'$(MigrationSha256)';
IF LEN(@sha)<>64 OR @sha LIKE N'%[^0-9A-Fa-f]%' THROW 58005,N'SHA-256 invalido.',1;
INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes) VALUES(N'0300_reparar_texto_contrasena',N'0300_reparar_texto_contrasena.sql',UPPER(@sha),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),N'Repara el texto con doble codificacion de sp_Auth_UpdatePassword (Credencial invalida).');
COMMIT TRANSACTION;
GO
