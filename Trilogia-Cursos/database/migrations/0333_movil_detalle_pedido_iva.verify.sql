SET NOCOUNT ON;

/* Verificacion de 0333. Solo lectura. */
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0333_movil_detalle_pedido_iva' AND Status = N'Applied')
    THROW 59340, N'0333 no figura aplicada en el ledger.', 1;
IF CHARINDEX(N'Impuesto', OBJECT_DEFINITION(OBJECT_ID(N'dbo.sp_Movil_Pedido_Detalle'))) = 0
    THROW 59341, N'sp_Movil_Pedido_Detalle no devuelve el Impuesto.', 1;
PRINT N'Verificacion 0333 completada.';
