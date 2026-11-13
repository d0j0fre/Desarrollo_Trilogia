SET NOCOUNT ON;

/* Verificacion de 0289. Deja la base como estaba: la prueba corre en una transaccion que se revierte. */
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0289_rutas_finalizar' AND Status = N'Applied')
    THROW 56950, N'0289 no figura aplicada en el ledger.', 1;
IF OBJECT_ID(N'dbo.sp_Rutas_Finalizar', N'P') IS NULL THROW 56951, N'Falta dbo.sp_Rutas_Finalizar.', 1;

DECLARE @Chofer INT = (SELECT TOP (1) UsuarioId FROM dbo.Usuarios ORDER BY UsuarioId);
DECLARE @Vehiculo INT = (SELECT TOP (1) VehiculoId FROM dbo.Vehiculos ORDER BY VehiculoId);
DECLARE @P1 INT = (SELECT TOP (1) p.PedidoId FROM dbo.Pedidos p WHERE p.Estado NOT IN (N'Cancelado', N'Rechazado', N'Entregado')
                   AND NOT EXISTS (SELECT 1 FROM dbo.RutaPedidos rp WHERE rp.PedidoId = p.PedidoId) ORDER BY p.PedidoId);
DECLARE @P2 INT = (SELECT TOP (1) p.PedidoId FROM dbo.Pedidos p WHERE p.Estado NOT IN (N'Cancelado', N'Rechazado', N'Entregado')
                   AND p.PedidoId <> @P1 AND NOT EXISTS (SELECT 1 FROM dbo.RutaPedidos rp WHERE rp.PedidoId = p.PedidoId) ORDER BY p.PedidoId);
IF @Chofer IS NULL OR @Vehiculo IS NULL OR @P1 IS NULL OR @P2 IS NULL THROW 56952, N'La prueba necesita un usuario, un vehiculo y dos pedidos abiertos sin ruta.', 1;

BEGIN TRANSACTION;
INSERT dbo.Rutas (Codigo, Zona, ChoferUsuarioId, VehiculoId, Estado) VALUES (N'VERIFY-0289-D', N'Verificacion', @Chofer, @Vehiculo, N'Despachada');
DECLARE @Desp INT = SCOPE_IDENTITY();
INSERT dbo.Rutas (Codigo, Zona, ChoferUsuarioId, VehiculoId, Estado) VALUES (N'VERIFY-0289-P', N'Verificacion', @Chofer, @Vehiculo, N'Planificada');
DECLARE @Plan INT = SCOPE_IDENTITY();
INSERT dbo.RutaPedidos (RutaId, PedidoId, Secuencia, EstadoEntrega) VALUES (@Desp, @P1, 1, N'Entregado'), (@Desp, @P2, 2, N'EnRuta');
DECLARE @StockAntes BIGINT = (SELECT SUM(CAST(Stock AS BIGINT)) FROM dbo.Productos);

-- Solo una ruta despachada se finaliza.
BEGIN TRY
    EXEC dbo.sp_Rutas_Finalizar @RutaId = @Plan;
    THROW 56953, N'Se finalizo una ruta Planificada.', 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() = 56953 BEGIN IF XACT_STATE() <> 0 ROLLBACK TRANSACTION; THROW; END;
    IF ERROR_NUMBER() <> 53441 BEGIN IF XACT_STATE() <> 0 ROLLBACK TRANSACTION; THROW; END;
END CATCH;

EXEC dbo.sp_Rutas_Finalizar @RutaId = @Desp, @UsuarioId = @Chofer, @Nombre = N'Verificacion';

IF (SELECT Estado FROM dbo.Rutas WHERE RutaId = @Desp) <> N'Completada' OR (SELECT FechaCierre FROM dbo.Rutas WHERE RutaId = @Desp) IS NULL
BEGIN ROLLBACK TRANSACTION; THROW 56954, N'La ruta no quedo Completada con fecha de cierre.', 1; END;
IF (SELECT Liquidada FROM dbo.Rutas WHERE RutaId = @Desp) <> 0
BEGIN ROLLBACK TRANSACTION; THROW 56955, N'Finalizar no debe marcar la ruta como liquidada.', 1; END;
IF NOT EXISTS (SELECT 1 FROM dbo.RutaPedidos WHERE RutaId = @Desp AND PedidoId = @P2 AND EstadoEntrega = N'Fallido' AND MotivoFallo IS NOT NULL AND FechaEntrega IS NOT NULL)
BEGIN ROLLBACK TRANSACTION; THROW 56956, N'La parada no entregada no quedo Fallido con motivo.', 1; END;
IF (SELECT EstadoEntrega FROM dbo.RutaPedidos WHERE RutaId = @Desp AND PedidoId = @P1) <> N'Entregado'
BEGIN ROLLBACK TRANSACTION; THROW 56957, N'La parada entregada cambio de estado.', 1; END;
IF (SELECT Estado FROM dbo.Pedidos WHERE PedidoId = @P2) <> N'Fallido'
BEGIN ROLLBACK TRANSACTION; THROW 56958, N'El pedido no entregado no quedo Fallido.', 1; END;
IF (SELECT SUM(CAST(Stock AS BIGINT)) FROM dbo.Productos) <> @StockAntes
BEGIN ROLLBACK TRANSACTION; THROW 56959, N'Finalizar la ruta modifico el inventario.', 1; END;

-- Ya completada: no se finaliza otra vez.
BEGIN TRY
    EXEC dbo.sp_Rutas_Finalizar @RutaId = @Desp;
    THROW 56960, N'Se finalizo dos veces la misma ruta.', 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() = 56960 BEGIN IF XACT_STATE() <> 0 ROLLBACK TRANSACTION; THROW; END;
    IF ERROR_NUMBER() <> 53441 BEGIN IF XACT_STATE() <> 0 ROLLBACK TRANSACTION; THROW; END;
END CATCH;

ROLLBACK TRANSACTION;
PRINT N'Verificación 0289 completada.';
