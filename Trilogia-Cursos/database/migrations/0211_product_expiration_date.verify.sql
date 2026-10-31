SET NOCOUNT ON;

/*  Verificación de 0211. Sólo lectura. */

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrationHistory
               WHERE MigrationId = N'0211_product_expiration_date'
                 AND Status = N'Applied'
                 AND LEN(FileSha256) = 64)
    THROW 55410, N'0211 no figura aplicada correctamente.', 1;

IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'dbo.Productos')
                 AND name = N'FechaVencimiento'
                 AND TYPE_NAME(user_type_id) = N'date'
                 AND is_nullable = 1)
    THROW 55411, N'Falta dbo.Productos.FechaVencimiento DATE NULL.', 1;

IF NOT EXISTS (SELECT 1 FROM sys.parameters WHERE object_id = OBJECT_ID(N'dbo.sp_Admin_CreateProduct') AND name = N'@FechaVencimiento')
    THROW 55412, N'sp_Admin_CreateProduct no acepta @FechaVencimiento.', 1;

IF NOT EXISTS (SELECT 1 FROM sys.parameters WHERE object_id = OBJECT_ID(N'dbo.sp_Admin_UpdateProduct') AND name = N'@FechaVencimiento')
    THROW 55413, N'sp_Admin_UpdateProduct no acepta @FechaVencimiento.', 1;

IF NOT EXISTS (SELECT 1 FROM sys.sql_modules WHERE object_id = OBJECT_ID(N'dbo.sp_Admin_GetProductById') AND definition LIKE N'%FechaVencimiento%')
   OR NOT EXISTS (SELECT 1 FROM sys.sql_modules WHERE object_id = OBJECT_ID(N'dbo.sp_Admin_GetProducts') AND definition LIKE N'%FechaVencimiento%')
    THROW 55414, N'Las consultas de productos no devuelven FechaVencimiento.', 1;

SELECT
    ledger.MigrationId,
    ledger.Status,
    ledger.AppliedAtUtc,
    (SELECT COUNT(*) FROM dbo.Productos WHERE FechaVencimiento IS NOT NULL) AS ProductosConVencimiento
FROM dbo.SchemaMigrationHistory ledger
WHERE ledger.MigrationId = N'0211_product_expiration_date';
