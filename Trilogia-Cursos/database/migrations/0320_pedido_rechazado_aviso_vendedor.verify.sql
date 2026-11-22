SET NOCOUNT ON;

/* Verificacion de 0320. Solo lectura. */
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0320_pedido_rechazado_aviso_vendedor' AND Status = N'Applied')
    THROW 56370, N'0320 no figura aplicada en el ledger.', 1;
IF OBJECT_ID(N'dbo.TR_Pedidos_NotificarRechazoVendedor', N'TR') IS NULL
    THROW 56371, N'Falta el disparador dbo.TR_Pedidos_NotificarRechazoVendedor.', 1;
IF EXISTS (SELECT 1 FROM sys.triggers WHERE name = N'TR_Pedidos_NotificarRechazoVendedor' AND is_disabled = 1)
    THROW 56372, N'El disparador TR_Pedidos_NotificarRechazoVendedor está deshabilitado.', 1;
IF OBJECT_ID(N'dbo.sp_Seller_GetMyOrders', N'P') IS NULL
    THROW 56373, N'Falta dbo.sp_Seller_GetMyOrders.', 1;
IF CHARINDEX(N'p.MotivoRechazo', OBJECT_DEFINITION(OBJECT_ID(N'dbo.sp_Seller_GetMyOrders'))) = 0
    THROW 56374, N'sp_Seller_GetMyOrders no devuelve Pedidos.MotivoRechazo.', 1;
PRINT N'Verificacion 0320 completada.';
