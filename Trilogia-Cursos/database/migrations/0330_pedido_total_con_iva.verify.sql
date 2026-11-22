SET NOCOUNT ON;

/* Verificacion de 0330 (el pedido guarda el total con IVA). Solo lectura. */
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0330_pedido_total_con_iva' AND Status = N'Applied')
    THROW 59380, N'0330 no figura aplicada en el ledger.', 1;
IF COL_LENGTH(N'dbo.Pedidos', N'Subtotal') IS NULL OR COL_LENGTH(N'dbo.Pedidos', N'Impuesto') IS NULL
    THROW 59381, N'Faltan Pedidos.Subtotal o Pedidos.Impuesto.', 1;
IF OBJECT_ID(N'dbo.PedidosTotalAntesIva', N'U') IS NULL
    THROW 59382, N'Falta el respaldo dbo.PedidosTotalAntesIva.', 1;

EXEC sys.sp_executesql N'
IF EXISTS (SELECT 1 FROM dbo.PedidosTotalAntesIva b INNER JOIN dbo.Pedidos p ON p.PedidoId = b.PedidoId WHERE p.Subtotal IS NULL)
    THROW 59383, N''Quedan pedidos migrados sin Subtotal.'', 1;
/* Los pedidos migrados cumplen Total = Subtotal + Impuesto (o, con factura, son iguales a ella). */
IF EXISTS (SELECT 1 FROM dbo.PedidosTotalAntesIva b INNER JOIN dbo.Pedidos p ON p.PedidoId = b.PedidoId
           WHERE b.Origen = N''Total x 1,13'' AND p.Total <> p.Subtotal + p.Impuesto)
    THROW 59384, N''Un pedido migrado sin factura no cumple Total = Subtotal + Impuesto.'', 1;
IF EXISTS (SELECT 1 FROM dbo.PedidosTotalAntesIva b INNER JOIN dbo.Pedidos p ON p.PedidoId = b.PedidoId
           INNER JOIN dbo.Facturas f ON f.PedidoId = p.PedidoId
           WHERE b.Origen = N''Factura'' AND (p.Subtotal <> f.Subtotal OR p.Impuesto <> f.Impuesto OR p.Total <> f.Total)
             AND f.FacturaId = (SELECT MIN(f2.FacturaId) FROM dbo.Facturas f2 WHERE f2.PedidoId = p.PedidoId))
    THROW 59385, N''Un pedido migrado con factura no coincide con su factura.'', 1;';

IF CHARINDEX(N'orders.Subtotal', OBJECT_DEFINITION(OBJECT_ID(N'dbo.sp_Store_CreateOrderWithPromotions'))) = 0
    THROW 59386, N'sp_Store_CreateOrderWithPromotions no guarda el Subtotal.', 1;
IF CHARINDEX(N'Impuesto', OBJECT_DEFINITION(OBJECT_ID(N'dbo.sp_Store_GetCheckoutResult'))) = 0
   OR CHARINDEX(N'Impuesto', OBJECT_DEFINITION(OBJECT_ID(N'dbo.sp_Admin_GetOrderHeader'))) = 0
    THROW 59387, N'Los procedimientos de lectura no devuelven el Impuesto.', 1;
PRINT N'Verificacion 0330 completada.';
