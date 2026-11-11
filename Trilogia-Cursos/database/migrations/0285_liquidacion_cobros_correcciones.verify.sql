SET NOCOUNT ON;

/* Verificacion de 0285. Deja la base como estaba: la prueba corre en una transaccion que se revierte. */
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0285_liquidacion_cobros_correcciones' AND Status = N'Applied')
    THROW 56890, N'0285 no figura aplicada en el ledger.', 1;
IF OBJECT_ID(N'dbo.ChoferFaltantes', N'U') IS NULL OR COL_LENGTH(N'dbo.RutaLiquidacionCobros', N'Anulada') IS NULL
    THROW 56891, N'Faltan la tabla ChoferFaltantes o la columna Anulada.', 1;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.RutaLiquidacionCobros') AND name = N'UX_RutaLiquidacionCobros_RutaVigente' AND is_unique = 1 AND has_filter = 1)
    THROW 56892, N'Falta el indice unico filtrado de liquidaciones vigentes.', 1;
IF OBJECT_ID(N'dbo.sp_LiquidacionCobros_Anular', N'P') IS NULL OR OBJECT_ID(N'dbo.sp_LiquidacionCobros_GetRecibo', N'P') IS NULL OR OBJECT_ID(N'dbo.sp_ChoferFaltantes_List', N'P') IS NULL
    THROW 56893, N'Faltan procedimientos de la 0285.', 1;

DECLARE @Chofer INT = (SELECT TOP (1) UsuarioId FROM dbo.Usuarios ORDER BY UsuarioId);
DECLARE @Vehiculo INT = (SELECT TOP (1) VehiculoId FROM dbo.Vehiculos ORDER BY VehiculoId);
DECLARE @Pedido INT = (SELECT TOP (1) p.PedidoId FROM dbo.Pedidos p WHERE NOT EXISTS (SELECT 1 FROM dbo.RutaPedidos rp WHERE rp.PedidoId = p.PedidoId) ORDER BY p.PedidoId);
IF @Chofer IS NULL OR @Vehiculo IS NULL OR @Pedido IS NULL THROW 56894, N'La prueba necesita un usuario, un vehiculo y un pedido sin ruta.', 1;

BEGIN TRANSACTION;
UPDATE dbo.Pedidos SET MetodoPago = N'Efectivo', Total = 10000 WHERE PedidoId = @Pedido;
INSERT dbo.Rutas (Codigo, Zona, ChoferUsuarioId, VehiculoId, Estado) VALUES (N'VERIFY-0285-D', N'Verificacion', @Chofer, @Vehiculo, N'Despachada');
DECLARE @Desp INT = SCOPE_IDENTITY();
INSERT dbo.Rutas (Codigo, Zona, ChoferUsuarioId, VehiculoId, Estado) VALUES (N'VERIFY-0285-C', N'Verificacion', @Chofer, @Vehiculo, N'Completada');
DECLARE @Comp INT = SCOPE_IDENTITY();
INSERT dbo.RutaPedidos (RutaId, PedidoId, Secuencia, EstadoEntrega) VALUES (@Comp, @Pedido, 1, N'Entregado');

-- BUG-10-04: una ruta Despachada no se liquida.
BEGIN TRY
    EXEC dbo.sp_LiquidacionCobros_Registrar @RutaId = @Desp, @MontoEfectivoRecibido = 0, @MontoComprobantes = 0;
    THROW 56895, N'Se liquido una ruta Despachada.', 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() = 56895 BEGIN IF XACT_STATE() <> 0 ROLLBACK TRANSACTION; THROW; END;
    IF ERROR_NUMBER() <> 53400 BEGIN IF XACT_STATE() <> 0 ROLLBACK TRANSACTION; THROW; END;
END CATCH;

-- G2-38: el detalle debe sumar el total de comprobantes.
BEGIN TRY
    EXEC dbo.sp_LiquidacionCobros_Registrar @RutaId = @Comp, @MontoEfectivoRecibido = 0, @MontoComprobantes = 500,
         @ComprobantesJson = N'[{"Tipo":"SINPE","Referencia":"X","Monto":100}]';
    THROW 56896, N'Se acepto un detalle que no suma el total.', 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() = 56896 BEGIN IF XACT_STATE() <> 0 ROLLBACK TRANSACTION; THROW; END;
    IF ERROR_NUMBER() <> 53401 BEGIN IF XACT_STATE() <> 0 ROLLBACK TRANSACTION; THROW; END;
END CATCH;

-- BUG-10-05: faltante a cargo del chofer.
DECLARE @R TABLE (Id INT, Estado NVARCHAR(20), Dif DECIMAL(18,2), Chofer NVARCHAR(150), Faltante DECIMAL(18,2));
INSERT @R EXEC dbo.sp_LiquidacionCobros_Registrar @RutaId = @Comp, @MontoEfectivoRecibido = 7000, @MontoComprobantes = 0;
DECLARE @Liq INT = (SELECT Id FROM @R);
IF NOT EXISTS (SELECT 1 FROM dbo.ChoferFaltantes WHERE LiquidacionCobrosId = @Liq AND ChoferUsuarioId = @Chofer AND Monto = 3000 AND Estado = N'Pendiente')
BEGIN ROLLBACK TRANSACTION; THROW 56897, N'El faltante no quedo asociado al chofer.', 1; END;

-- No se liquida dos veces sin anular.
BEGIN TRY
    EXEC dbo.sp_LiquidacionCobros_Registrar @RutaId = @Comp, @MontoEfectivoRecibido = 10000, @MontoComprobantes = 0;
    THROW 56898, N'Se registro una segunda liquidacion vigente.', 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() = 56898 BEGIN IF XACT_STATE() <> 0 ROLLBACK TRANSACTION; THROW; END;
    IF ERROR_NUMBER() <> 53071 BEGIN IF XACT_STATE() <> 0 ROLLBACK TRANSACTION; THROW; END;
END CATCH;

-- G2-37: anular exige motivo, anula el faltante y permite liquidar de nuevo.
BEGIN TRY
    EXEC dbo.sp_LiquidacionCobros_Anular @LiquidacionCobrosId = @Liq, @Motivo = N'   ';
    THROW 56899, N'Se anulo sin motivo.', 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() = 56899 BEGIN IF XACT_STATE() <> 0 ROLLBACK TRANSACTION; THROW; END;
    IF ERROR_NUMBER() <> 53405 BEGIN IF XACT_STATE() <> 0 ROLLBACK TRANSACTION; THROW; END;
END CATCH;
EXEC dbo.sp_LiquidacionCobros_Anular @LiquidacionCobrosId = @Liq, @Motivo = N'Verificacion 0285';
IF EXISTS (SELECT 1 FROM dbo.ChoferFaltantes WHERE LiquidacionCobrosId = @Liq AND Estado <> N'Anulado')
BEGIN ROLLBACK TRANSACTION; THROW 56900, N'El faltante no se anulo con la liquidacion.', 1; END;
DELETE @R;
INSERT @R EXEC dbo.sp_LiquidacionCobros_Registrar @RutaId = @Comp, @MontoEfectivoRecibido = 10000, @MontoComprobantes = 0;
IF (SELECT Estado FROM @R) <> N'Cuadrada'
BEGIN ROLLBACK TRANSACTION; THROW 56901, N'La liquidacion corregida no quedo Cuadrada.', 1; END;

ROLLBACK TRANSACTION;
PRINT N'Verificación 0285 completada.';
