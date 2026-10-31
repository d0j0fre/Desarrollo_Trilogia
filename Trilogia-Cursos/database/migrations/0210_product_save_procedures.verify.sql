SET NOCOUNT ON;

/*  Verificación de 0210. Sólo lectura: no ejecuta los procedimientos de
    escritura para no crear ni alterar productos. */

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrationHistory
               WHERE MigrationId = N'0210_product_save_procedures'
                 AND Status = N'Applied'
                 AND LEN(FileSha256) = 64)
    THROW 55410, N'0210 no figura aplicada correctamente.', 1;

IF (SELECT COUNT(*) FROM sys.parameters
    WHERE object_id = OBJECT_ID(N'dbo.sp_Admin_CreateProduct')
      AND name IN (N'@StockMinimo', N'@ImagenUrl', N'@EsDestacado')) <> 3
    THROW 55411, N'sp_Admin_CreateProduct no acepta @StockMinimo, @ImagenUrl y @EsDestacado.', 1;

IF (SELECT COUNT(*) FROM sys.parameters
    WHERE object_id = OBJECT_ID(N'dbo.sp_Admin_UpdateProduct')
      AND name IN (N'@StockMinimo', N'@ImagenUrl', N'@EsDestacado')) <> 3
    THROW 55412, N'sp_Admin_UpdateProduct no acepta @StockMinimo, @ImagenUrl y @EsDestacado.', 1;

IF NOT EXISTS (SELECT 1 FROM sys.sql_modules
               WHERE object_id = OBJECT_ID(N'dbo.sp_Admin_GetProductById')
                 AND definition LIKE N'%StockMinimo%')
    THROW 55413, N'sp_Admin_GetProductById no devuelve StockMinimo.', 1;

IF NOT EXISTS (SELECT 1 FROM sys.sql_modules
               WHERE object_id = OBJECT_ID(N'dbo.sp_Admin_GetProducts')
                 AND definition LIKE N'%StockMinimo%')
    THROW 55414, N'sp_Admin_GetProducts no devuelve StockMinimo.', 1;

SELECT
    ledger.MigrationId,
    ledger.Status,
    ledger.AppliedAtUtc,
    ledger.EnvironmentName,
    (SELECT COUNT(*) FROM sys.parameters WHERE object_id = OBJECT_ID(N'dbo.sp_Admin_CreateProduct')) AS ParametrosCrear,
    (SELECT COUNT(*) FROM sys.parameters WHERE object_id = OBJECT_ID(N'dbo.sp_Admin_UpdateProduct')) AS ParametrosEditar
FROM dbo.SchemaMigrationHistory ledger
WHERE ledger.MigrationId = N'0210_product_save_procedures';
