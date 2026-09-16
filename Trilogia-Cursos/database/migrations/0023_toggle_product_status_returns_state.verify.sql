SET NOCOUNT ON;

/*  Verificación de 0023. Sólo lectura: no modifica datos ni definiciones.
    No ejecuta el procedimiento para no alterar el estado de ningún producto. */

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrationHistory
               WHERE MigrationId = N'0023_toggle_product_status_returns_state'
                 AND Status = N'Applied'
                 AND LEN(FileSha256) = 64)
    THROW 55410, N'0023 no figura aplicada correctamente.', 1;

IF OBJECT_ID(N'dbo.sp_Admin_ToggleProductStatus', N'P') IS NULL
    THROW 55411, N'Falta dbo.sp_Admin_ToggleProductStatus.', 1;

IF (SELECT COUNT(*) FROM sys.parameters
    WHERE object_id = OBJECT_ID(N'dbo.sp_Admin_ToggleProductStatus')) <> 1
   OR NOT EXISTS (SELECT 1 FROM sys.parameters
                  WHERE object_id = OBJECT_ID(N'dbo.sp_Admin_ToggleProductStatus')
                    AND name = N'@ProductoId')
    THROW 55412, N'La firma esperada es un único parámetro @ProductoId.', 1;

IF NOT EXISTS (SELECT 1 FROM sys.sql_modules
               WHERE object_id = OBJECT_ID(N'dbo.sp_Admin_ToggleProductStatus')
                 AND definition LIKE N'%OUTPUT INSERTED.Activo%')
    THROW 55413, N'La definición no devuelve el estado resultante.', 1;

SELECT
    ledger.MigrationId,
    ledger.Status,
    ledger.AppliedAtUtc,
    ledger.EnvironmentName,
    (SELECT COUNT(*) FROM sys.parameters
     WHERE object_id = OBJECT_ID(N'dbo.sp_Admin_ToggleProductStatus')) AS ParametrosDelProcedimiento,
    CASE WHEN modulo.definition LIKE N'%OUTPUT INSERTED.Activo%'
         THEN N'Devuelve el estado resultante'
         ELSE N'No devuelve estado' END AS ComportamientoDeRetorno
FROM dbo.SchemaMigrationHistory ledger
CROSS JOIN sys.sql_modules modulo
WHERE ledger.MigrationId = N'0023_toggle_product_status_returns_state'
  AND modulo.object_id = OBJECT_ID(N'dbo.sp_Admin_ToggleProductStatus');
