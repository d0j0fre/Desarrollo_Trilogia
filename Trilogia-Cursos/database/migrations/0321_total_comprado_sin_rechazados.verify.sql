SET NOCOUNT ON;

/* Verificacion de 0321. Solo lectura. */
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0321_total_comprado_sin_rechazados' AND Status = N'Applied')
    THROW 56380, N'0321 no figura aplicada en el ledger.', 1;
IF OBJECT_ID(N'dbo.sp_Admin_GetClients', N'P') IS NULL OR OBJECT_ID(N'dbo.sp_Admin_GetClientById', N'P') IS NULL
    THROW 56381, N'Faltan sp_Admin_GetClients o sp_Admin_GetClientById.', 1;
IF CHARINDEX(N'Rechazado', OBJECT_DEFINITION(OBJECT_ID(N'dbo.sp_Admin_GetClients'))) = 0
   OR CHARINDEX(N'Rechazado', OBJECT_DEFINITION(OBJECT_ID(N'dbo.sp_Admin_GetClientById'))) = 0
    THROW 56382, N'Los procedimientos de clientes no excluyen los pedidos Rechazados.', 1;
PRINT N'Verificacion 0321 completada.';
