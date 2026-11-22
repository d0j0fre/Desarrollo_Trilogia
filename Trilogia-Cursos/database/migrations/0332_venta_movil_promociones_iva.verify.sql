SET NOCOUNT ON;

/* Verificacion de 0332 (Venta movil con promociones y total con IVA). Todo corre en una transaccion que se
   revierte al final; los casos que fallan a proposito estan en 0325.verify.sql. */
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0332_venta_movil_promociones_iva' AND Status = N'Applied')
    THROW 59400, N'0332 no figura aplicada en el ledger.', 1;
IF OBJECT_ID(N'dbo.sp_Seller_CotizarPedido', N'P') IS NULL THROW 59401, N'Falta dbo.sp_Seller_CotizarPedido.', 1;

DECLARE @Cliente INT, @Vendedor INT, @A INT, @B INT, @Hoy DATE = CAST(SYSDATETIME() AS DATE);
DECLARE @Items NVARCHAR(MAX), @Ped INT, @Sub DECIMAL(18,2), @Precio DECIMAL(18,2);
DECLARE @StockA INT, @StockB INT;
SELECT TOP 1 @Cliente = u.UsuarioId FROM dbo.Usuarios u INNER JOIN dbo.Perfiles p ON p.PerfilId = u.PerfilId WHERE p.Nombre = N'Cliente' AND u.Activo = 1 ORDER BY u.UsuarioId;
SELECT TOP 1 @Vendedor = UsuarioId FROM dbo.Usuarios WHERE Activo = 1 ORDER BY UsuarioId;
SELECT TOP 1 @A = ProductoId FROM dbo.Productos WHERE Activo = 1 AND Stock >= 30 AND Precio > 0 ORDER BY ProductoId;
SELECT TOP 1 @B = ProductoId FROM dbo.Productos WHERE Activo = 1 AND Stock >= 30 AND Precio > 0 AND ProductoId <> @A ORDER BY ProductoId;
IF @Cliente IS NULL OR @Vendedor IS NULL OR @A IS NULL OR @B IS NULL THROW 59402, N'La verificacion necesita un cliente, un usuario y dos productos con stock.', 1;
SELECT @Precio = Precio FROM dbo.Productos WHERE ProductoId = @A;

BEGIN TRANSACTION;
UPDATE dbo.Promociones SET Estado = N'Inactiva' WHERE ProductoId IN (@A, @B) AND Estado = N'Activa';
INSERT dbo.Promociones (Nombre, Tipo, ProductoId, CantidadMinima, PorcentajeDescuento, SegmentoCliente, FechaInicio, FechaFin, Estado, Prioridad)
VALUES (N'VERIFY 0332 descuento', N'DescuentoPorcentual', @A, 1, 10.00, N'Todos', DATEADD(DAY, -1, @Hoy), DATEADD(DAY, 1, @Hoy), N'Activa', 1);

/* A. Con descuento: el pedido guarda subtotal con el 10 %, IVA 13 % y total con IVA; igual que la factura y que la cotizacion. */
SET @Items = CONCAT(N'[{"productoId":', @A, N',"cantidad":3}]');
DECLARE @Out TABLE (PedidoId INT, Estado NVARCHAR(30), FacturaId INT, NumeroFactura NVARCHAR(30));
INSERT @Out EXEC dbo.sp_Seller_CreateOrder @ClienteUsuarioId = @Cliente, @VendedorUsuarioId = @Vendedor, @VendedorNombre = N'Verify 0332',
    @TipoEntrega = N'Retiro en local', @DireccionEntrega = N'Prueba', @IdentificacionCliente = N'1-0234-0567', @ItemsJson = @Items;
SELECT @Ped = PedidoId FROM @Out;
SET @Sub = 3 * @Precio - ROUND(3 * @Precio * 0.10, 2);   -- (precio x 3) menos el 10 %
IF NOT EXISTS (SELECT 1 FROM dbo.Pedidos WHERE PedidoId = @Ped AND Estado = N'Pendiente' AND Subtotal = @Sub AND Impuesto = ROUND(@Sub * 0.13, 2) AND Total = @Sub + ROUND(@Sub * 0.13, 2))
    THROW 59403, N'El pedido de Venta movil no guardo subtotal con descuento, IVA y total con IVA.', 1;
IF NOT EXISTS (SELECT 1 FROM dbo.Facturas f INNER JOIN dbo.Pedidos p ON p.PedidoId = f.PedidoId WHERE f.PedidoId = @Ped AND f.Subtotal = p.Subtotal AND f.Impuesto = p.Impuesto AND f.Total = p.Total)
    THROW 59404, N'La factura automatica no es igual al pedido.', 1;
IF NOT EXISTS (SELECT 1 FROM dbo.PromocionAplicaciones WHERE PedidoId = @Ped AND TipoBeneficio = N'Descuento' AND MontoDescontado = ROUND(3 * @Precio * 0.10, 2))
    THROW 59405, N'No se registro la promocion aplicada.', 1;
CREATE TABLE #Cot (Orden INT, ProductoId INT, ProductoNombre NVARCHAR(150), Cantidad INT, PrecioLista DECIMAL(18,2), MontoDescuento DECIMAL(18,2), PrecioUnitario DECIMAL(18,2), SubtotalLinea DECIMAL(18,2), EsRegalo BIT, PromocionNombre NVARCHAR(150));
/* La cotizacion tiene dos conjuntos de resultados; INSERT ... EXEC solo captura uno, asi que se reproducen con la misma funcion. */
INSERT #Cot (Orden, ProductoId, ProductoNombre, Cantidad, PrecioLista, MontoDescuento, PrecioUnitario, SubtotalLinea, EsRegalo, PromocionNombre)
SELECT Orden, ProductoId, ProductoNombre, Cantidad, PrecioLista, MontoDescuento, PrecioUnitario, Cantidad * PrecioUnitario, EsRegalo, PromocionNombre
FROM dbo.fn_Venta_Lineas(@Cliente, @Items);
IF (SELECT SUM(SubtotalLinea) FROM #Cot) <> @Sub OR NOT EXISTS (SELECT 1 FROM #Cot WHERE MontoDescuento > 0 AND PromocionNombre = N'VERIFY 0332 descuento')
    THROW 59406, N'La cotizacion no coincide con el pedido registrado.', 1;
DROP TABLE #Cot;
EXEC dbo.sp_Seller_CotizarPedido @ClienteUsuarioId = @Cliente, @ItemsJson = @Items;   -- debe ejecutar sin error

/* B. Regalia: comprar 2 de A regala 1 de B; el inventario baja por lo comprado MAS lo regalado y la factura lleva la linea a precio 0. */
UPDATE dbo.Promociones SET Estado = N'Inactiva' WHERE ProductoId = @A AND Estado = N'Activa';
INSERT dbo.Promociones (Nombre, Tipo, ProductoId, CantidadMinima, ProductoRegaloId, CantidadRegalo, SegmentoCliente, FechaInicio, FechaFin, Estado, Prioridad)
VALUES (N'VERIFY 0332 regalia', N'RegaliaPorVolumen', @A, 2, @B, 1, N'Todos', DATEADD(DAY, -1, @Hoy), DATEADD(DAY, 1, @Hoy), N'Activa', 1);
SELECT @StockA = Stock FROM dbo.Productos WHERE ProductoId = @A;
SELECT @StockB = Stock FROM dbo.Productos WHERE ProductoId = @B;
SET @Items = CONCAT(N'[{"productoId":', @A, N',"cantidad":4}]');
DELETE @Out;
INSERT @Out EXEC dbo.sp_Seller_CreateOrder @ClienteUsuarioId = @Cliente, @VendedorUsuarioId = @Vendedor, @VendedorNombre = N'Verify 0332',
    @TipoEntrega = N'Retiro en local', @DireccionEntrega = N'Prueba', @IdentificacionCliente = N'1-0234-0567', @ItemsJson = @Items;
SELECT @Ped = PedidoId FROM @Out;
IF (SELECT Stock FROM dbo.Productos WHERE ProductoId = @A) <> @StockA - 4 OR (SELECT Stock FROM dbo.Productos WHERE ProductoId = @B) <> @StockB - 2
    THROW 59407, N'El inventario no bajo por lo comprado mas lo regalado.', 1;
IF NOT EXISTS (SELECT 1 FROM dbo.PedidoDetalle WHERE PedidoId = @Ped AND ProductoId = @B AND EsRegalo = 1 AND Cantidad = 2 AND PrecioUnitario = 0)
    THROW 59408, N'Falta la linea de regalia a precio 0.', 1;
IF NOT EXISTS (SELECT 1 FROM dbo.Pedidos p INNER JOIN dbo.Facturas f ON f.PedidoId = p.PedidoId WHERE p.PedidoId = @Ped AND p.Total = f.Total AND p.Subtotal = 4 * @Precio)
    THROW 59409, N'El pedido con regalia no cobra solo lo comprado o no iguala a la factura.', 1;
IF NOT EXISTS (SELECT 1 FROM dbo.FacturaDetalle fd INNER JOIN dbo.Facturas f ON f.FacturaId = fd.FacturaId WHERE f.PedidoId = @Ped AND fd.ProductoId = @B AND fd.PrecioUnitario = 0 AND fd.Cantidad = 2)
    THROW 59410, N'La factura no trae la regalia a precio 0.', 1;

/* C. Retenido (total con IVA sobre el umbral) con regalia: no descuenta inventario; al aprobar, descuenta lo comprado y lo regalado y factura igual que el pedido. */
SELECT @StockA = Stock FROM dbo.Productos WHERE ProductoId = @A;
SELECT @StockB = Stock FROM dbo.Productos WHERE ProductoId = @B;
DELETE @Out;
INSERT @Out EXEC dbo.sp_Seller_CreateOrder @ClienteUsuarioId = @Cliente, @VendedorUsuarioId = @Vendedor, @VendedorNombre = N'Verify 0332',
    @TipoEntrega = N'Retiro en local', @DireccionEntrega = N'Prueba', @IdentificacionCliente = N'1-0234-0567', @ItemsJson = @Items, @UmbralRetencion = 1;
SELECT @Ped = PedidoId FROM @Out;
IF NOT EXISTS (SELECT 1 FROM dbo.Pedidos WHERE PedidoId = @Ped AND Estado = N'Retenido' AND InventarioDescontado = 0) THROW 59411, N'El pedido sobre el umbral no quedo retenido.', 1;
IF (SELECT Stock FROM dbo.Productos WHERE ProductoId = @A) <> @StockA THROW 59412, N'Un pedido retenido descuento inventario.', 1;
EXEC dbo.sp_Manager_ApproveOrder @PedidoId = @Ped, @UsuarioId = @Vendedor, @UsuarioNombre = N'Verify 0332';
IF (SELECT Stock FROM dbo.Productos WHERE ProductoId = @A) <> @StockA - 4 OR (SELECT Stock FROM dbo.Productos WHERE ProductoId = @B) <> @StockB - 2
    THROW 59413, N'Al aprobar no se descontaron las unidades compradas y regaladas.', 1;
IF NOT EXISTS (SELECT 1 FROM dbo.Pedidos p INNER JOIN dbo.Facturas f ON f.PedidoId = p.PedidoId WHERE p.PedidoId = @Ped AND p.Estado = N'Liberado' AND f.Subtotal = p.Subtotal AND f.Impuesto = p.Impuesto AND f.Total = p.Total)
    THROW 59414, N'La factura de un pedido aprobado no es igual al pedido.', 1;
IF NOT EXISTS (SELECT 1 FROM dbo.FacturaDetalle fd INNER JOIN dbo.Facturas f ON f.FacturaId = fd.FacturaId WHERE f.PedidoId = @Ped AND fd.ProductoId = @B AND fd.PrecioUnitario = 0)
    THROW 59415, N'La factura de un pedido aprobado no trae la regalia a precio 0.', 1;
ROLLBACK TRANSACTION;
PRINT N'Verificacion 0332 completada.';
