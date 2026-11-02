SET NOCOUNT ON;

/* Verificacion de 0255. Solo lectura. */
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0255_venta_movil_pago_contra_entrega' AND Status = N'Applied')
    THROW 56600, N'0255 no figura aplicada en el ledger.', 1;
DECLARE @Definicion NVARCHAR(MAX) = OBJECT_DEFINITION(OBJECT_ID(N'dbo.sp_Seller_CreateOrder'));
IF @Definicion NOT LIKE N'%ISNULL(@MetodoPago, N''Efectivo contra entrega'')%'
    THROW 56601, N'sp_Seller_CreateOrder no guarda «Efectivo contra entrega» por defecto.', 1;
IF @Definicion LIKE N'%ISNULL(@MetodoPago, N''Crédito vendedor'')%'
    THROW 56602, N'sp_Seller_CreateOrder sigue guardando «Crédito vendedor».', 1;
IF @Definicion NOT LIKE N'%IF @MetodoPago = N''Crédito''%'
    THROW 56603, N'sp_Seller_CreateOrder perdió el cargo al crédito de la 0230.', 1;

SELECT MetodoPago, COUNT(*) AS Pedidos FROM dbo.Pedidos WHERE CanalPedido LIKE N'Venta m%' GROUP BY MetodoPago;
PRINT N'Verificación 0255 completada.';
