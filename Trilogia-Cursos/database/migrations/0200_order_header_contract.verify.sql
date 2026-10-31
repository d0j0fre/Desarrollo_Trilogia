SET NOCOUNT ON;

/*  Verificación de 0200. Sólo lectura: no modifica datos ni definiciones. */

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrationHistory
               WHERE MigrationId = N'0200_order_header_contract'
                 AND Status = N'Applied'
                 AND LEN(FileSha256) = 64)
    THROW 55710, N'0200 no figura aplicada correctamente.', 1;

IF OBJECT_ID(N'dbo.sp_Admin_GetOrderHeader', N'P') IS NULL
    THROW 55711, N'Falta dbo.sp_Admin_GetOrderHeader.', 1;

IF (SELECT COUNT(*) FROM sys.dm_exec_describe_first_result_set_for_object(OBJECT_ID(N'dbo.sp_Admin_GetOrderHeader'), 0)) <> 15
   OR NOT EXISTS (SELECT 1 FROM sys.dm_exec_describe_first_result_set_for_object(OBJECT_ID(N'dbo.sp_Admin_GetOrderHeader'), 0)
                  WHERE column_ordinal = 9 AND name = N'Observaciones')
   OR NOT EXISTS (SELECT 1 FROM sys.dm_exec_describe_first_result_set_for_object(OBJECT_ID(N'dbo.sp_Admin_GetOrderHeader'), 0)
                  WHERE column_ordinal = 10 AND name = N'Total')
    THROW 55712, N'sp_Admin_GetOrderHeader no tiene el contrato esperado.', 1;

SELECT column_ordinal AS Posicion, name AS Columna, system_type_name AS Tipo
FROM sys.dm_exec_describe_first_result_set_for_object(OBJECT_ID(N'dbo.sp_Admin_GetOrderHeader'), 0)
ORDER BY column_ordinal;
