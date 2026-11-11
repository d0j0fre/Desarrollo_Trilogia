SET NOCOUNT ON;

/* Verificacion de 0286. Deja la base como estaba: la prueba corre en una transaccion que se revierte. */
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0286_liquidacion_mercaderia_ruta' AND Status = N'Applied')
    THROW 56910, N'0286 no figura aplicada en el ledger.', 1;
IF OBJECT_ID(N'dbo.RutaLiquidacionMercaderia', N'U') IS NULL OR OBJECT_ID(N'dbo.RutaLiquidacionMercaderiaDetalle', N'U') IS NULL
   OR OBJECT_ID(N'dbo.sp_RutaMercaderia_Preparar', N'P') IS NULL OR OBJECT_ID(N'dbo.sp_RutaMercaderia_Registrar', N'P') IS NULL
   OR OBJECT_ID(N'dbo.sp_RutaMercaderia_List', N'P') IS NULL
    THROW 56911, N'Faltan tablas o procedimientos de la 0286.', 1;

DECLARE @Usuario INT = (SELECT TOP (1) UsuarioId FROM dbo.Usuarios ORDER BY UsuarioId);
DECLARE @Vehiculo INT = (SELECT TOP (1) VehiculoId FROM dbo.Vehiculos ORDER BY VehiculoId);
DECLARE @Pedido INT = (SELECT TOP (1) p.PedidoId FROM dbo.Pedidos p
                       WHERE NOT EXISTS (SELECT 1 FROM dbo.RutaPedidos rp WHERE rp.PedidoId = p.PedidoId)
                         AND p.Estado NOT IN (N'Cancelado', N'Rechazado')
                         AND EXISTS (SELECT 1 FROM dbo.PedidoDetalle d INNER JOIN dbo.Productos pr ON pr.ProductoId = d.ProductoId WHERE d.PedidoId = p.PedidoId)
                       ORDER BY p.PedidoId);
IF @Usuario IS NULL OR @Vehiculo IS NULL OR @Pedido IS NULL THROW 56912, N'La prueba necesita un usuario, un vehiculo y un pedido con productos que no este en una ruta.', 1;

BEGIN TRANSACTION;
INSERT dbo.Rutas (Codigo, Zona, ChoferUsuarioId, VehiculoId, Estado) VALUES (N'VERIFY-0286-D', N'Verificacion', @Usuario, @Vehiculo, N'Despachada');
DECLARE @Desp INT = SCOPE_IDENTITY();
INSERT dbo.Rutas (Codigo, Zona, ChoferUsuarioId, VehiculoId, Estado) VALUES (N'VERIFY-0286-C', N'Verificacion', @Usuario, @Vehiculo, N'Completada');
DECLARE @Comp INT = SCOPE_IDENTITY();
INSERT dbo.RutaPedidos (RutaId, PedidoId, Secuencia, EstadoEntrega) VALUES (@Comp, @Pedido, 1, N'Fallido');

DECLARE @Producto INT = (SELECT TOP (1) d.ProductoId FROM dbo.PedidoDetalle d INNER JOIN dbo.Productos pr ON pr.ProductoId = d.ProductoId WHERE d.PedidoId = @Pedido ORDER BY d.ProductoId);
DECLARE @Esperado INT = (SELECT SUM(d.Cantidad) FROM dbo.PedidoDetalle d WHERE d.PedidoId = @Pedido AND d.ProductoId = @Producto);
DECLARE @StockAntes INT = (SELECT Stock FROM dbo.Productos WHERE ProductoId = @Producto);
DECLARE @Json NVARCHAR(MAX);

-- Una ruta que no esta completada no se liquida.
BEGIN TRY
    EXEC dbo.sp_RutaMercaderia_Registrar @RutaId = @Desp, @DetalleJson = N'[]', @UsuarioId = @Usuario;
    THROW 56913, N'Se liquido la mercaderia de una ruta Despachada.', 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() = 56913 BEGIN IF XACT_STATE() <> 0 ROLLBACK TRANSACTION; THROW; END;
    IF ERROR_NUMBER() <> 53411 BEGIN IF XACT_STATE() <> 0 ROLLBACK TRANSACTION; THROW; END;
END CATCH;

-- Sin contar nada falta toda la mercaderia: no cierra y no toca el stock.
BEGIN TRY
    EXEC dbo.sp_RutaMercaderia_Registrar @RutaId = @Comp, @DetalleJson = N'[]', @UsuarioId = @Usuario;
    THROW 56914, N'Se cerro la ruta con mercaderia faltante.', 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() = 56914 BEGIN IF XACT_STATE() <> 0 ROLLBACK TRANSACTION; THROW; END;
    IF ERROR_NUMBER() <> 53416 BEGIN IF XACT_STATE() <> 0 ROLLBACK TRANSACTION; THROW; END;
END CATCH;
IF (SELECT Stock FROM dbo.Productos WHERE ProductoId = @Producto) <> @StockAntes
BEGIN ROLLBACK TRANSACTION; THROW 56915, N'El bloqueo por faltante modifico el stock.', 1; END;

-- Contar de mas tampoco cierra.
SET @Json = (SELECT @Pedido AS PedidoId, @Producto AS ProductoId, @Esperado + 1 AS Cantidad FOR JSON PATH);
BEGIN TRY
    EXEC dbo.sp_RutaMercaderia_Registrar @RutaId = @Comp, @DetalleJson = @Json, @UsuarioId = @Usuario;
    THROW 56916, N'Se acepto mas mercaderia de la despachada.', 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() = 56916 BEGIN IF XACT_STATE() <> 0 ROLLBACK TRANSACTION; THROW; END;
    IF ERROR_NUMBER() <> 53415 BEGIN IF XACT_STATE() <> 0 ROLLBACK TRANSACTION; THROW; END;
END CATCH;

-- Contado completo: devuelve al stock con un movimiento de entrada y cierra.
SET @Json = (SELECT d.PedidoId, d.ProductoId, SUM(d.Cantidad) AS Cantidad FROM dbo.PedidoDetalle d
             WHERE d.PedidoId = @Pedido AND EXISTS (SELECT 1 FROM dbo.Productos pr WHERE pr.ProductoId = d.ProductoId)
             GROUP BY d.PedidoId, d.ProductoId FOR JSON PATH);
DECLARE @TotalPedido INT = (SELECT SUM(d.Cantidad) FROM dbo.PedidoDetalle d WHERE d.PedidoId = @Pedido AND EXISTS (SELECT 1 FROM dbo.Productos pr WHERE pr.ProductoId = d.ProductoId));
DECLARE @R TABLE (MercaderiaId INT, Lineas INT, Unidades INT);
INSERT @R EXEC dbo.sp_RutaMercaderia_Registrar @RutaId = @Comp, @DetalleJson = @Json, @UsuarioId = @Usuario;
IF (SELECT Unidades FROM @R) <> @TotalPedido
BEGIN ROLLBACK TRANSACTION; THROW 56917, N'Las unidades devueltas no coinciden con el pedido.', 1; END;
IF (SELECT Stock FROM dbo.Productos WHERE ProductoId = @Producto) <> @StockAntes + @Esperado
BEGIN ROLLBACK TRANSACTION; THROW 56918, N'El stock no subio lo despachado.', 1; END;
IF NOT EXISTS (SELECT 1 FROM dbo.MovimientosInventario WHERE ProductoId = @Producto AND TipoMovimiento = N'Entrada' AND Motivo LIKE N'%VERIFY-0286-C%')
BEGIN ROLLBACK TRANSACTION; THROW 56919, N'Falta el movimiento de entrada de inventario.', 1; END;
IF (SELECT Liquidada FROM dbo.Rutas WHERE RutaId = @Comp) <> 1
BEGIN ROLLBACK TRANSACTION; THROW 56920, N'La ruta no quedo liquidada.', 1; END;

-- No se liquida dos veces.
BEGIN TRY
    EXEC dbo.sp_RutaMercaderia_Registrar @RutaId = @Comp, @DetalleJson = @Json, @UsuarioId = @Usuario;
    THROW 56921, N'Se liquido dos veces la misma ruta.', 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() = 56921 BEGIN IF XACT_STATE() <> 0 ROLLBACK TRANSACTION; THROW; END;
    IF ERROR_NUMBER() <> 53412 BEGIN IF XACT_STATE() <> 0 ROLLBACK TRANSACTION; THROW; END;
END CATCH;

ROLLBACK TRANSACTION;
PRINT N'Verificación 0286 completada.';
