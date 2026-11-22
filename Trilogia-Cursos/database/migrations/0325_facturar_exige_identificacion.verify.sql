SET NOCOUNT ON;

/* Verificacion de 0325 (POST-qa2-02). Cada bloque corre en su propia transaccion y se revierte
   (sp_Seller_CreateOrder y sp_Manager_ApproveOrder revierten la transaccion al rechazar). */
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0325_facturar_exige_identificacion' AND Status = N'Applied')
    THROW 58480, N'0325 no figura aplicada en el ledger.', 1;

DECLARE @Cliente INT, @Vendedor INT, @Producto INT, @Ped INT, @Err INT, @Antes INT, @Gerente INT;
SELECT TOP 1 @Cliente = u.UsuarioId FROM dbo.Usuarios u INNER JOIN dbo.Perfiles p ON p.PerfilId = u.PerfilId WHERE p.Nombre = N'Cliente' AND u.Activo = 1 ORDER BY u.UsuarioId;
SELECT TOP 1 @Vendedor = u.UsuarioId FROM dbo.Usuarios u WHERE u.Activo = 1 ORDER BY u.UsuarioId;
SELECT TOP 1 @Producto = ProductoId FROM dbo.Productos WHERE Activo = 1 AND Stock >= 5 AND Precio > 0 ORDER BY ProductoId;
IF @Cliente IS NULL OR @Vendedor IS NULL OR @Producto IS NULL THROW 58481, N'La verificacion necesita un cliente, un usuario y un producto con stock.', 1;
SET @Gerente = @Vendedor;
DECLARE @Items NVARCHAR(MAX) = CONCAT(N'[{"productoId":', @Producto, N',"cantidad":1}]');

/* A. «Generar factura» */
BEGIN TRANSACTION;
INSERT dbo.Pedidos (UsuarioId, Estado, TipoEntrega, DireccionEntrega, Total) VALUES (@Cliente, N'Pendiente', N'Retiro en local', N'Direccion de prueba', 1000);
SET @Ped = SCOPE_IDENTITY();
INSERT dbo.PedidoDetalle (PedidoId, ProductoId, Cantidad, PrecioUnitario) VALUES (@Ped, @Producto, 1, 1000);
SET @Err = 0;
BEGIN TRY EXEC dbo.sp_Admin_GenerateInvoiceFromOrder @PedidoId = @Ped; END TRY BEGIN CATCH SET @Err = ERROR_NUMBER(); END CATCH;
IF @Err <> 50906 BEGIN IF XACT_STATE() <> 0 ROLLBACK TRANSACTION; THROW 58482, N'Se facturo (o fallo distinto) un pedido sin identificacion en «Generar factura».', 1; END;
IF EXISTS (SELECT 1 FROM dbo.Facturas WHERE PedidoId = @Ped) BEGIN ROLLBACK TRANSACTION; THROW 58483, N'Quedo una factura de un pedido sin identificacion.', 1; END;
UPDATE dbo.Pedidos SET IdentificacionCliente = N'1-0234-0567' WHERE PedidoId = @Ped;
EXEC dbo.sp_Admin_GenerateInvoiceFromOrder @PedidoId = @Ped;
IF NOT EXISTS (SELECT 1 FROM dbo.Facturas WHERE PedidoId = @Ped) BEGIN ROLLBACK TRANSACTION; THROW 58484, N'Con identificacion no se genero la factura.', 1; END;
ROLLBACK TRANSACTION;

/* B. Venta movil sin identificacion: se rechaza y no queda pedido */
SELECT @Antes = COUNT(*) FROM dbo.Pedidos;
SET @Err = 0;
BEGIN TRANSACTION;
BEGIN TRY
    EXEC dbo.sp_Seller_CreateOrder @ClienteUsuarioId = @Cliente, @VendedorUsuarioId = @Vendedor, @VendedorNombre = N'Verify 0325',
         @TipoEntrega = N'Retiro en local', @ItemsJson = @Items, @IdentificacionCliente = NULL;
END TRY BEGIN CATCH SET @Err = ERROR_NUMBER(); END CATCH;
IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
IF @Err <> 50718 THROW 58485, N'Venta movil facturo (o fallo distinto) un pedido sin identificacion.', 1;
IF (SELECT COUNT(*) FROM dbo.Pedidos) <> @Antes THROW 58486, N'Venta movil dejo un pedido tras rechazar la falta de identificacion.', 1;

/* C. Venta movil con identificacion: se factura */
BEGIN TRANSACTION;
EXEC dbo.sp_Seller_CreateOrder @ClienteUsuarioId = @Cliente, @VendedorUsuarioId = @Vendedor, @VendedorNombre = N'Verify 0325',
     @TipoEntrega = N'Retiro en local', @ItemsJson = @Items, @IdentificacionCliente = N'1-0234-0567';
IF NOT EXISTS (SELECT 1 FROM dbo.Facturas f INNER JOIN dbo.Pedidos p ON p.PedidoId = f.PedidoId WHERE p.VendedorNombre = N'Verify 0325' AND p.PedidoId > (SELECT MAX(PedidoId) - 5 FROM dbo.Pedidos))
BEGIN ROLLBACK TRANSACTION; THROW 58487, N'Venta movil con identificacion no genero la factura.', 1; END;
ROLLBACK TRANSACTION;

/* D. Pedido retenido sin identificacion: no se aprueba ni se factura */
BEGIN TRANSACTION;
INSERT dbo.Pedidos (UsuarioId, Estado, TipoEntrega, DireccionEntrega, Total) VALUES (@Cliente, N'Retenido', N'Retiro en local', N'Direccion de prueba', 1000);
SET @Ped = SCOPE_IDENTITY();
INSERT dbo.PedidoDetalle (PedidoId, ProductoId, Cantidad, PrecioUnitario) VALUES (@Ped, @Producto, 1, 1000);
SET @Err = 0;
BEGIN TRY EXEC dbo.sp_Manager_ApproveOrder @PedidoId = @Ped, @UsuarioId = @Gerente, @UsuarioNombre = N'Verify 0325'; END TRY BEGIN CATCH SET @Err = ERROR_NUMBER(); END CATCH;
IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
IF @Err <> 50737 THROW 58488, N'«Aprobar y facturar» facturo (o fallo distinto) un pedido retenido sin identificacion.', 1;

/* E. Pedido retenido con identificacion: se aprueba y se factura */
BEGIN TRANSACTION;
INSERT dbo.Pedidos (UsuarioId, Estado, TipoEntrega, DireccionEntrega, Total, IdentificacionCliente) VALUES (@Cliente, N'Retenido', N'Retiro en local', N'Direccion de prueba', 1000, N'1-0234-0567');
SET @Ped = SCOPE_IDENTITY();
INSERT dbo.PedidoDetalle (PedidoId, ProductoId, Cantidad, PrecioUnitario) VALUES (@Ped, @Producto, 1, 1000);
EXEC dbo.sp_Manager_ApproveOrder @PedidoId = @Ped, @UsuarioId = @Gerente, @UsuarioNombre = N'Verify 0325';
IF NOT EXISTS (SELECT 1 FROM dbo.Facturas WHERE PedidoId = @Ped) BEGIN ROLLBACK TRANSACTION; THROW 58489, N'Con identificacion «Aprobar y facturar» no genero la factura.', 1; END;
ROLLBACK TRANSACTION;

PRINT N'Verificacion 0325 completada.';
