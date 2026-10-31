SET NOCOUNT ON;

/*  Verificación de 0201. Sólo lectura: no modifica datos ni definiciones. */

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrationHistory
               WHERE MigrationId = N'0201_invoice_client_phone'
                 AND Status = N'Applied'
                 AND LEN(FileSha256) = 64)
    THROW 55730, N'0201 no figura aplicada correctamente.', 1;

IF OBJECT_ID(N'dbo.sp_Admin_GetInvoiceClientPhone', N'P') IS NULL
    THROW 55731, N'Falta dbo.sp_Admin_GetInvoiceClientPhone.', 1;

IF NOT EXISTS (SELECT 1 FROM sys.dm_exec_describe_first_result_set_for_object(OBJECT_ID(N'dbo.sp_Admin_GetInvoiceClientPhone'), 0)
               WHERE column_ordinal = 1 AND name = N'Telefono')
   OR (SELECT COUNT(*) FROM sys.dm_exec_describe_first_result_set_for_object(OBJECT_ID(N'dbo.sp_Admin_GetInvoiceClientPhone'), 0)) <> 1
    THROW 55732, N'sp_Admin_GetInvoiceClientPhone no devuelve únicamente Telefono.', 1;

SELECT ledger.MigrationId, ledger.Status, ledger.AppliedAtUtc, ledger.EnvironmentName
FROM dbo.SchemaMigrationHistory ledger
WHERE ledger.MigrationId = N'0201_invoice_client_phone';
