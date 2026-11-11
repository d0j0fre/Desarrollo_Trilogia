SET NOCOUNT ON;

/* Verificacion de 0287. Deja la base como estaba: la prueba corre en una transaccion que se revierte. */
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0287_mantenimiento_flota_correcciones' AND Status = N'Applied')
    THROW 56920, N'0287 no figura aplicada en el ledger.', 1;
IF COL_LENGTH(N'dbo.Vehiculos', N'InactivoPorMantenimiento') IS NULL
   OR OBJECT_ID(N'dbo.sp_Mantenimiento_Iniciar', N'P') IS NULL OR OBJECT_ID(N'dbo.sp_Vehiculos_SincronizarMantenimiento', N'P') IS NULL
    THROW 56921, N'Faltan la columna InactivoPorMantenimiento o los procedimientos de la 0287.', 1;

BEGIN TRANSACTION;
DECLARE @V INT;
INSERT dbo.Vehiculos (Placa, Descripcion, Capacidad, Activo) VALUES (N'VERIFY-0287', N'Verificacion 0287', 10, 1);
SET @V = SCOPE_IDENTITY();
DECLARE @O TABLE (Id INT, Fuera BIT);
DECLARE @Id INT, @Id2 INT;

-- BUG-15-01: una orden correctiva En proceso saca el vehiculo de circulacion.
INSERT @O EXEC dbo.sp_Mantenimiento_Create @VehiculoId = @V, @Tipo = N'Correctivo', @Descripcion = N'Verificacion', @Estado = N'EnProceso', @Costo = 20000;
SET @Id = (SELECT Id FROM @O);
IF (SELECT Activo FROM dbo.Vehiculos WHERE VehiculoId = @V) <> 0 OR (SELECT Fuera FROM @O) <> 1
BEGIN ROLLBACK TRANSACTION; THROW 56922, N'La orden correctiva En proceso no saco el vehiculo de circulacion.', 1; END;

-- Un vehiculo en reparacion no se reactiva a mano.
BEGIN TRY
    EXEC dbo.sp_Vehiculos_ToggleStatus @VehiculoId = @V;
    THROW 56923, N'Se reactivo a mano un vehiculo en reparacion.', 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() = 56923 BEGIN IF XACT_STATE() <> 0 ROLLBACK TRANSACTION; THROW; END;
    IF ERROR_NUMBER() <> 52094 BEGIN IF XACT_STATE() <> 0 ROLLBACK TRANSACTION; THROW; END;
END CATCH;

-- Al completar (con costo final y fecha) el vehiculo vuelve a circulacion.
EXEC dbo.sp_Mantenimiento_MarcarCompletada @OrdenMantenimientoId = @Id, @Costo = 25000;
IF (SELECT Activo FROM dbo.Vehiculos WHERE VehiculoId = @V) <> 1
BEGIN ROLLBACK TRANSACTION; THROW 56924, N'El vehiculo no volvio a circulacion al completar la orden.', 1; END;
IF NOT EXISTS (SELECT 1 FROM dbo.OrdenesMantenimiento WHERE OrdenMantenimientoId = @Id AND Estado = N'Completada' AND Costo = 25000 AND FechaRealizada IS NOT NULL)
BEGIN ROLLBACK TRANSACTION; THROW 56925, N'La orden completada no guardo el costo final y la fecha.', 1; END;

-- Una orden cerrada no se completa otra vez.
BEGIN TRY
    EXEC dbo.sp_Mantenimiento_MarcarCompletada @OrdenMantenimientoId = @Id;
    THROW 56926, N'Se completo dos veces la misma orden.', 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() = 56926 BEGIN IF XACT_STATE() <> 0 ROLLBACK TRANSACTION; THROW; END;
    IF ERROR_NUMBER() <> 53058 BEGIN IF XACT_STATE() <> 0 ROLLBACK TRANSACTION; THROW; END;
END CATCH;

-- G2-18: una orden Completada sin fecha usa la del dia.
DELETE @O;
INSERT @O EXEC dbo.sp_Mantenimiento_Create @VehiculoId = @V, @Tipo = N'Preventivo', @Descripcion = N'Verificacion preventiva', @Estado = N'Completada';
SET @Id2 = (SELECT Id FROM @O);
IF NOT EXISTS (SELECT 1 FROM dbo.OrdenesMantenimiento WHERE OrdenMantenimientoId = @Id2 AND FechaRealizada = CAST(SYSDATETIME() AS DATE))
BEGIN ROLLBACK TRANSACTION; THROW 56927, N'La orden Completada no guardo la fecha de realizacion.', 1; END;

-- Una correctiva Programada se inicia: ahi el vehiculo sale de circulacion.
DELETE @O;
INSERT @O EXEC dbo.sp_Mantenimiento_Create @VehiculoId = @V, @Tipo = N'Correctivo', @Descripcion = N'Verificacion iniciar', @Estado = N'Programada';
SET @Id2 = (SELECT Id FROM @O);
IF (SELECT Activo FROM dbo.Vehiculos WHERE VehiculoId = @V) <> 1
BEGIN ROLLBACK TRANSACTION; THROW 56928, N'Una orden Programada saco el vehiculo de circulacion.', 1; END;
EXEC dbo.sp_Mantenimiento_Iniciar @OrdenMantenimientoId = @Id2;
IF (SELECT Activo FROM dbo.Vehiculos WHERE VehiculoId = @V) <> 0
BEGIN ROLLBACK TRANSACTION; THROW 56929, N'Iniciar la orden correctiva no saco el vehiculo de circulacion.', 1; END;

-- Con rutas abiertas no se envia a reparacion.
EXEC dbo.sp_Mantenimiento_MarcarCompletada @OrdenMantenimientoId = @Id2;
DECLARE @Chofer INT = (SELECT TOP (1) UsuarioId FROM dbo.Usuarios ORDER BY UsuarioId);
INSERT dbo.Rutas (Codigo, Zona, ChoferUsuarioId, VehiculoId, Estado) VALUES (N'VERIFY-0287', N'Verificacion', @Chofer, @V, N'Planificada');
BEGIN TRY
    EXEC dbo.sp_Mantenimiento_Create @VehiculoId = @V, @Tipo = N'Correctivo', @Descripcion = N'Con ruta abierta', @Estado = N'EnProceso';
    THROW 56930, N'Se envio a reparacion un vehiculo con rutas abiertas.', 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() = 56930 BEGIN IF XACT_STATE() <> 0 ROLLBACK TRANSACTION; THROW; END;
    IF ERROR_NUMBER() <> 53057 BEGIN IF XACT_STATE() <> 0 ROLLBACK TRANSACTION; THROW; END;
END CATCH;

ROLLBACK TRANSACTION;
PRINT N'Verificación 0287 completada.';
