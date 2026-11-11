SET NOCOUNT ON;

/* Verificacion de 0305. Deja la base como estaba: la prueba corre en una transaccion que se revierte. */
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0305_evidencia_entrega_movil' AND Status = N'Applied')
    THROW 57060, N'0305 no figura aplicada en el ledger.', 1;
IF COL_LENGTH(N'dbo.EntregaEvidencias', N'SyncGuid') IS NULL THROW 57061, N'Falta EntregaEvidencias.SyncGuid.', 1;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.EntregaEvidencias') AND name = N'UX_EntregaEvidencias_SyncGuid' AND is_unique = 1)
    THROW 57062, N'Falta el indice unico UX_EntregaEvidencias_SyncGuid.', 1;
IF OBJECT_ID(N'dbo.sp_Chofer_PrepararEvidencia', N'P') IS NULL THROW 57063, N'Falta dbo.sp_Chofer_PrepararEvidencia.', 1;
IF NOT EXISTS (SELECT 1 FROM sys.parameters WHERE object_id = OBJECT_ID(N'dbo.sp_Entrega_RegisterEvidence') AND name = N'@SyncGuid')
    THROW 57064, N'sp_Entrega_RegisterEvidence no recibe @SyncGuid.', 1;

DECLARE @Chofer INT = (SELECT TOP (1) UsuarioId FROM dbo.Usuarios WHERE Activo = 1 ORDER BY UsuarioId);
DECLARE @Otro INT = (SELECT TOP (1) UsuarioId FROM dbo.Usuarios WHERE Activo = 1 AND UsuarioId <> @Chofer ORDER BY UsuarioId);
DECLARE @Vehiculo INT = (SELECT TOP (1) VehiculoId FROM dbo.Vehiculos ORDER BY VehiculoId);
DECLARE @P1 INT = (SELECT TOP (1) p.PedidoId FROM dbo.Pedidos p WHERE p.Estado NOT IN (N'Cancelado', N'Rechazado', N'Entregado')
                   AND NOT EXISTS (SELECT 1 FROM dbo.RutaPedidos rp WHERE rp.PedidoId = p.PedidoId) ORDER BY p.PedidoId);
DECLARE @P2 INT = (SELECT TOP (1) p.PedidoId FROM dbo.Pedidos p WHERE p.Estado NOT IN (N'Cancelado', N'Rechazado', N'Entregado')
                   AND p.PedidoId <> @P1 AND NOT EXISTS (SELECT 1 FROM dbo.RutaPedidos rp WHERE rp.PedidoId = p.PedidoId) ORDER BY p.PedidoId);
IF @Chofer IS NULL OR @Otro IS NULL OR @Vehiculo IS NULL OR @P1 IS NULL OR @P2 IS NULL
    THROW 57065, N'La prueba necesita dos usuarios, un vehiculo y dos pedidos abiertos sin ruta.', 1;

BEGIN TRANSACTION;
INSERT dbo.Rutas (Codigo, Zona, ChoferUsuarioId, VehiculoId, Estado) VALUES (N'VERIFY-0305-D', N'Verificacion', @Chofer, @Vehiculo, N'Despachada');
DECLARE @Desp INT = SCOPE_IDENTITY();
INSERT dbo.Rutas (Codigo, Zona, ChoferUsuarioId, VehiculoId, Estado) VALUES (N'VERIFY-0305-P', N'Verificacion', @Chofer, @Vehiculo, N'Planificada');
DECLARE @Plan INT = SCOPE_IDENTITY();
INSERT dbo.RutaPedidos (RutaId, PedidoId, Secuencia, EstadoEntrega) VALUES (@Desp, @P1, 1, N'EnRuta');
DECLARE @RpDesp INT = SCOPE_IDENTITY();
INSERT dbo.RutaPedidos (RutaId, PedidoId, Secuencia, EstadoEntrega) VALUES (@Plan, @P2, 1, N'Pendiente');
DECLARE @RpPlan INT = SCOPE_IDENTITY();

DECLARE @Resp TABLE (RutaId INT, PedidoId INT, EvidenciaIdExistente INT);
DECLARE @Guid UNIQUEIDENTIFIER = NEWID();

-- Parada propia de una ruta despachada: devuelve ruta y pedido, sin evidencia previa.
INSERT @Resp EXEC dbo.sp_Chofer_PrepararEvidencia @RutaPedidoId = @RpDesp, @ChoferUsuarioId = @Chofer, @SyncGuid = @Guid;
IF NOT EXISTS (SELECT 1 FROM @Resp WHERE RutaId = @Desp AND PedidoId = @P1 AND EvidenciaIdExistente IS NULL)
BEGIN ROLLBACK TRANSACTION; THROW 57066, N'La parada propia no devolvio ruta y pedido.', 1; END;

-- Parada de otro chofer: se rechaza igual que si no existiera.
BEGIN TRY
    EXEC dbo.sp_Chofer_PrepararEvidencia @RutaPedidoId = @RpDesp, @ChoferUsuarioId = @Otro, @SyncGuid = @Guid;
    THROW 57067, N'Se acepto una parada ajena.', 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() = 57067 BEGIN IF XACT_STATE() <> 0 ROLLBACK TRANSACTION; THROW; END;
    IF ERROR_NUMBER() <> 52086 BEGIN IF XACT_STATE() <> 0 ROLLBACK TRANSACTION; THROW; END;
END CATCH;

-- Ruta no despachada (Planificada): se rechaza.
BEGIN TRY
    EXEC dbo.sp_Chofer_PrepararEvidencia @RutaPedidoId = @RpPlan, @ChoferUsuarioId = @Chofer;
    THROW 57068, N'Se acepto evidencia en una ruta Planificada.', 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() = 57068 BEGIN IF XACT_STATE() <> 0 ROLLBACK TRANSACTION; THROW; END;
    IF ERROR_NUMBER() <> 52087 BEGIN IF XACT_STATE() <> 0 ROLLBACK TRANSACTION; THROW; END;
END CATCH;

-- Registro con SyncGuid: queda Pending; confirmada, el reenvio se reconoce como duplicado.
DECLARE @Id INT;
DECLARE @Ins TABLE (Id INT);
INSERT @Ins EXEC dbo.sp_Entrega_RegisterEvidence
    @PedidoId = @P1, @RutaId = @Desp, @TipoEvidencia = N'Foto',
    @StorageKey = N'0123456789abcdef0123456789abcdef.jpg', @MimeType = N'image/jpeg',
    @Observaciones = NULL, @RegistradoPorUsuarioId = @Chofer, @RegistradoPorNombre = N'Verificacion', @SyncGuid = @Guid;
SELECT @Id = Id FROM @Ins;
IF @Id IS NULL OR NOT EXISTS (SELECT 1 FROM dbo.EntregaEvidencias WHERE EvidenciaId = @Id AND SyncGuid = @Guid AND StorageStatus = N'Pending')
BEGIN ROLLBACK TRANSACTION; THROW 57069, N'La evidencia no quedo Pending con su SyncGuid.', 1; END;

-- Pending huerfana: el reintento la descarta y no hay duplicado.
DELETE @Resp;
INSERT @Resp EXEC dbo.sp_Chofer_PrepararEvidencia @RutaPedidoId = @RpDesp, @ChoferUsuarioId = @Chofer, @SyncGuid = @Guid;
IF EXISTS (SELECT 1 FROM dbo.EntregaEvidencias WHERE EvidenciaId = @Id) OR NOT EXISTS (SELECT 1 FROM @Resp WHERE EvidenciaIdExistente IS NULL)
BEGIN ROLLBACK TRANSACTION; THROW 57070, N'Una evidencia Pending huerfana no se descarto en el reintento.', 1; END;

-- Ready: el reenvio devuelve la existente.
DELETE @Ins;
INSERT @Ins EXEC dbo.sp_Entrega_RegisterEvidence
    @PedidoId = @P1, @RutaId = @Desp, @TipoEvidencia = N'Firma',
    @StorageKey = N'fedcba9876543210fedcba9876543210.png', @MimeType = N'image/png',
    @Observaciones = NULL, @RegistradoPorUsuarioId = @Chofer, @RegistradoPorNombre = N'Verificacion', @SyncGuid = @Guid;
SELECT @Id = Id FROM @Ins;
EXEC dbo.sp_Entrega_MarkEvidenceReady @EvidenciaId = @Id, @RegistradoPorUsuarioId = @Chofer;
DELETE @Resp;
INSERT @Resp EXEC dbo.sp_Chofer_PrepararEvidencia @RutaPedidoId = @RpDesp, @ChoferUsuarioId = @Chofer, @SyncGuid = @Guid;
IF NOT EXISTS (SELECT 1 FROM @Resp WHERE EvidenciaIdExistente = @Id)
BEGIN ROLLBACK TRANSACTION; THROW 57071, N'El reenvio de una evidencia lista no se reconocio como duplicado.', 1; END;

-- El mismo SyncGuid de otro chofer se rechaza.
BEGIN TRY
    INSERT dbo.Rutas (Codigo, Zona, ChoferUsuarioId, VehiculoId, Estado) VALUES (N'VERIFY-0305-O', N'Verificacion', @Otro, @Vehiculo, N'Despachada');
    DECLARE @RutaOtro INT = SCOPE_IDENTITY();
    INSERT dbo.RutaPedidos (RutaId, PedidoId, Secuencia, EstadoEntrega) VALUES (@RutaOtro, @P2, 1, N'EnRuta');
    DECLARE @RpOtro INT = SCOPE_IDENTITY();
    EXEC dbo.sp_Chofer_PrepararEvidencia @RutaPedidoId = @RpOtro, @ChoferUsuarioId = @Otro, @SyncGuid = @Guid;
    THROW 57072, N'Se acepto el SyncGuid de otro chofer.', 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() = 57072 BEGIN IF XACT_STATE() <> 0 ROLLBACK TRANSACTION; THROW; END;
    IF ERROR_NUMBER() <> 52088 BEGIN IF XACT_STATE() <> 0 ROLLBACK TRANSACTION; THROW; END;
END CATCH;

-- Ruta completada: se acepta dentro de las 48 horas y se rechaza despues.
UPDATE dbo.Rutas SET Estado = N'Completada', FechaCierre = SYSDATETIME() WHERE RutaId = @Desp;
DELETE @Resp;
INSERT @Resp EXEC dbo.sp_Chofer_PrepararEvidencia @RutaPedidoId = @RpDesp, @ChoferUsuarioId = @Chofer;
IF NOT EXISTS (SELECT 1 FROM @Resp WHERE RutaId = @Desp)
BEGIN ROLLBACK TRANSACTION; THROW 57073, N'Una ruta recien completada deberia admitir evidencia.', 1; END;

UPDATE dbo.Rutas SET FechaCierre = DATEADD(HOUR, -49, SYSDATETIME()) WHERE RutaId = @Desp;
BEGIN TRY
    EXEC dbo.sp_Chofer_PrepararEvidencia @RutaPedidoId = @RpDesp, @ChoferUsuarioId = @Chofer;
    THROW 57074, N'Se acepto evidencia en una ruta cerrada hace mas de 48 horas.', 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() = 57074 BEGIN IF XACT_STATE() <> 0 ROLLBACK TRANSACTION; THROW; END;
    IF ERROR_NUMBER() <> 52087 BEGIN IF XACT_STATE() <> 0 ROLLBACK TRANSACTION; THROW; END;
END CATCH;

ROLLBACK TRANSACTION;
PRINT N'Verificación 0305 completada.';
