SET NOCOUNT ON;

/* Verificacion de 0252. Solo lectura. */
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0252_reparar_texto_mal_codificado' AND Status = N'Applied')
    THROW 56570, N'0252 no figura aplicada en el ledger.', 1;

DECLARE @Pares TABLE (Malo NVARCHAR(2) COLLATE Latin1_General_BIN2 NOT NULL PRIMARY KEY);
WITH Bytes AS (SELECT TOP (64) 127 + ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS B FROM sys.all_objects)
INSERT @Pares (Malo)
SELECT NCHAR(195) + CAST(CHAR(B) AS NVARCHAR(1)) FROM Bytes WHERE B NOT IN (129, 141, 143, 144, 157)
UNION ALL
SELECT NCHAR(194) + CAST(CHAR(B) AS NVARCHAR(1)) FROM Bytes WHERE B >= 161;

/* Ninguna de las columnas reparadas conserva un par dañado. */
IF EXISTS (SELECT 1 FROM dbo.Productos t JOIN @Pares p ON CHARINDEX(p.Malo, t.Nombre COLLATE Latin1_General_BIN2) > 0 OR CHARINDEX(p.Malo, t.Descripcion COLLATE Latin1_General_BIN2) > 0)
   OR EXISTS (SELECT 1 FROM dbo.MovimientosInventario t JOIN @Pares p ON CHARINDEX(p.Malo, t.ProductoNombre COLLATE Latin1_General_BIN2) > 0 OR CHARINDEX(p.Malo, t.Motivo COLLATE Latin1_General_BIN2) > 0)
   OR EXISTS (SELECT 1 FROM dbo.FacturaDetalle t JOIN @Pares p ON CHARINDEX(p.Malo, t.ProductoNombre COLLATE Latin1_General_BIN2) > 0)
   OR EXISTS (SELECT 1 FROM dbo.Usuarios t JOIN @Pares p ON CHARINDEX(p.Malo, t.NombreCompleto COLLATE Latin1_General_BIN2) > 0 OR CHARINDEX(p.Malo, t.Direccion COLLATE Latin1_General_BIN2) > 0)
   OR EXISTS (SELECT 1 FROM dbo.Pedidos t JOIN @Pares p ON CHARINDEX(p.Malo, t.DireccionEntrega COLLATE Latin1_General_BIN2) > 0 OR CHARINDEX(p.Malo, t.Observaciones COLLATE Latin1_General_BIN2) > 0)
    THROW 56571, N'Queda texto con doble codificación en una columna que repara la 0252.', 1;

/* Muestra de los textos demo afectados (si existen en esta base). */
SELECT N'MovimientosInventario' AS Tabla, ProductoNombre AS Texto FROM dbo.MovimientosInventario WHERE ProductoNombre LIKE N'Ron A_ejo%'
UNION ALL SELECT N'Productos', Descripcion FROM dbo.Productos WHERE Nombre IN (N'Whisky Black Label 750ml', N'Vodka Premium 750ml')
UNION ALL SELECT N'Usuarios', Direccion FROM dbo.Usuarios WHERE Direccion LIKE N'San Jos_ Centro';
PRINT N'Verificación 0252 completada.';
