-- Datos de ejemplo del grupo 2 (Partes IV a XII). Idempotente: borra y vuelve a crear solo sus registros.
-- Antes: node capitulos/10_administrador/_preparar_datos.js planilla calculos
--        node capitulos/21_supervisor/_preparar_datos_g2.js
SET QUOTED_IDENTIFIER ON; SET NOCOUNT ON;

-- Boletas: los cálculos del período 1 quedan pagados (aprobó Administrador General, pagó Admin Demo).
UPDATE dbo.PlanillaCalculos
SET Estado = N'Pagada', AprobadoPorUsuarioId = 1, AprobadoPorNombre = N'Administrador General',
    PagadoPorUsuarioId = 29, PagadoPorNombre = N'Admin Demo',
    FechaAprobacionUtc = COALESCE(FechaAprobacionUtc, '2026-10-31T20:00:00'), FechaPagoUtc = COALESCE(FechaPagoUtc, '2026-10-31T22:00:00')
WHERE PeriodoId = 1 AND Estado IN (N'Borrador', N'Aprobada');

-- Jornadas enviadas para aprobar (semana del 26/10/2026).
DELETE FROM dbo.EmpleadoJornadas WHERE EmpleadoId IN (16, 17, 18, 20) AND Fecha BETWEEN '2026-10-26' AND '2026-10-30';
INSERT dbo.EmpleadoJornadas (EmpleadoId, Fecha, HorasOrdinarias, HorasExtra, HorasAusencia, Observaciones, Estado, IdempotencyKey, FechaEnvioUtc)
VALUES (18, '2026-10-27', 8, 2, 0, N'Recepción del camión de Distribuidora Central', N'Enviada', NEWID(), SYSUTCDATETIME()),
       (18, '2026-10-28', 8, 0, 0, NULL, N'Enviada', NEWID(), SYSUTCDATETIME()),
       (20, '2026-10-28', 8, 1, 0, N'Ruta a Alajuela con retraso por presa', N'Enviada', NEWID(), SYSUTCDATETIME()),
       (17, '2026-10-29', 6, 0, 2, N'Cita médica en la tarde', N'Enviada', NEWID(), SYSUTCDATETIME()),
       (16, '2026-10-30', 8, 3, 0, N'Visitas extra a clientes de Heredia', N'Enviada', NEWID(), SYSUTCDATETIME());

-- Solicitudes de días libres.
DELETE FROM dbo.EmpleadoSolicitudesTiempoLibre WHERE EmpleadoId IN (16, 17, 18) AND FechaInicio >= '2026-11-01';
INSERT dbo.EmpleadoSolicitudesTiempoLibre (EmpleadoId, FechaInicio, FechaFin, CantidadDias, TipoSolicitud, Motivo, Estado, RespuestaAdmin, UsuarioRespuestaNombre, FechaRespuesta)
VALUES (18, '2026-11-16', '2026-11-18', 3, N'Con goce salarial', N'Vacaciones: viaje familiar a Guanacaste.', N'Pendiente', NULL, NULL, NULL),
       (16, '2026-11-09', '2026-11-09', 1, N'Sin goce salarial', N'Trámite personal en la CCSS.', N'Pendiente', NULL, NULL, NULL),
       (17, '2026-11-05', '2026-11-06', 2, N'Con goce salarial', N'Matrícula de mi hijo en el colegio.', N'Aprobada', N'Aprobado. Coordinar el cierre de caja con Gabriela.', N'Supervisor Demo', SYSDATETIME());

-- Tareas del Empleado Demo (Mi portal).
DELETE FROM dbo.EmpleadoTareas WHERE EmpleadoId = 25;
INSERT dbo.EmpleadoTareas (EmpleadoId, Titulo, Descripcion, Prioridad, Estado, FechaLimite, UsuarioAsignacionNombre)
VALUES (25, N'Actualizar precios de cervezas', N'Revisar en el catálogo los precios de la lista nueva del proveedor.', N'Alta', N'Pendiente', '2026-11-06', N'Supervisor Demo'),
       (25, N'Responder consultas pendientes', N'Atender las consultas de clientes de la semana.', N'Media', N'En proceso', '2026-11-04', N'Supervisor Demo');
GO

-- Pedido propio para Pedidos (administración), Parte VI. Número fijo 9201.
SET QUOTED_IDENTIFIER ON; SET NOCOUNT ON;
DELETE FROM dbo.PedidoDetalle WHERE PedidoId = 9201;
DELETE FROM dbo.Pedidos WHERE PedidoId = 9201;
SET IDENTITY_INSERT dbo.Pedidos ON;
INSERT dbo.Pedidos (PedidoId, UsuarioId, FechaPedido, Estado, TipoEntrega, DireccionEntrega, Total, Observaciones, CanalPedido, MetodoPago, EstadoPago)
VALUES (9201, 6, '2026-11-02T08:15:00', N'Pendiente', N'Entrega a domicilio', N'Barrio Escalante, 200 m este del parque', 29150,
        N'Entregar antes de las 10 a. m. Llamar al llegar.', N'Tienda en línea', N'Transferencia', N'Pendiente');
SET IDENTITY_INSERT dbo.Pedidos OFF;
-- Devuelve la numeración automática a la serie normal (los pedidos nuevos no saltan a 9202).
DECLARE @maxPedido INT = (SELECT MAX(PedidoId) FROM dbo.Pedidos WHERE PedidoId < 9000);
DBCC CHECKIDENT ('dbo.Pedidos', RESEED, @maxPedido) WITH NO_INFOMSGS;
INSERT dbo.PedidoDetalle (PedidoId, ProductoId, Cantidad, PrecioUnitario, ProductoNombreSnapshot, EsRegalo)
VALUES (9201, 3, 5, 4200, N'Cerveza Pilsen 6 Pack', 0),
       (9201, 2, 3, 1850, N'Coca-Cola 2.5L', 0),
       (9201, 6, 4, 650, N'Agua 600ml', 0);
GO

-- Chat interno (Parte VI): un departamento con el Empleado, Bodega y Supervisor, y una conversación con el Supervisor.
SET QUOTED_IDENTIFIER ON; SET NOCOUNT ON;
DECLARE @dep INT = (SELECT DepartamentoId FROM dbo.ChatDepartamentos WHERE Nombre = N'Bodega y despacho');
IF @dep IS NULL
BEGIN
    INSERT dbo.ChatDepartamentos (Nombre, Descripcion, CreadoPorUsuarioId) VALUES (N'Bodega y despacho', N'Coordinación de pedidos, bodega y rutas del día.', 29);
    SET @dep = SCOPE_IDENTITY();
END
DELETE FROM dbo.ChatDepartamentoMensajes WHERE DepartamentoId = @dep;
DELETE FROM dbo.ChatDepartamentoMiembros WHERE DepartamentoId = @dep;
INSERT dbo.ChatDepartamentoMiembros (DepartamentoId, UsuarioId, PuedePublicar, AgregadoPorUsuarioId)
VALUES (@dep, 41, 1, 29), (@dep, 34, 1, 29), (@dep, 31, 1, 29), (@dep, 30, 1, 29);
INSERT dbo.ChatDepartamentoMensajes (DepartamentoId, RemitenteId, Contenido, FechaEnvio)
VALUES (@dep, 34, N'Ya llegó el camión de Distribuidora Central. Hay que acomodar 40 cajas de cerveza.', '2026-11-02T14:05:00'),
       (@dep, 31, N'Gracias. Empleado, por favor apoye en bodega después de las 10.', '2026-11-02T14:12:00');
-- Conversación privada Empleado ↔ Supervisor.
DECLARE @conv INT = (SELECT ConversacionId FROM dbo.ChatConversaciones WHERE UsuarioMenorId = 31 AND UsuarioMayorId = 41);
IF @conv IS NULL
BEGIN
    INSERT dbo.ChatConversaciones (UsuarioMenorId, UsuarioMayorId) VALUES (31, 41);
    SET @conv = SCOPE_IDENTITY();
END
DELETE FROM dbo.ChatMensajes WHERE ConversacionId = @conv;
INSERT dbo.ChatMensajes (ConversacionId, RemitenteId, Contenido, FechaEnvio, Leido)
VALUES (@conv, 31, N'Buenos días. ¿Pudo revisar el pedido 9201 de la Pulpería El Tucán?', '2026-11-02T14:20:00', 1),
       (@conv, 41, N'Sí, ya lo aprobé. Sale en la ruta de la tarde.', '2026-11-02T14:25:00', 1);
GO

-- Liquidación de cobros (Parte VII): ruta propia completada RUT-20261031070000 con 3 pedidos propios (9202–9204).
SET QUOTED_IDENTIFIER ON; SET NOCOUNT ON;
DECLARE @ruta INT = (SELECT RutaId FROM dbo.Rutas WHERE Codigo = N'RUT-20261031070000');
IF @ruta IS NOT NULL
BEGIN
    DELETE c FROM dbo.RutaLiquidacionComprobantes c JOIN dbo.RutaLiquidacionCobros l ON l.LiquidacionCobrosId = c.LiquidacionCobrosId WHERE l.RutaId = @ruta;
    DELETE FROM dbo.RutaLiquidacionCobros WHERE RutaId = @ruta;
    DELETE FROM dbo.RutaPedidos WHERE RutaId = @ruta;
    DELETE FROM dbo.Rutas WHERE RutaId = @ruta;
END
DELETE FROM dbo.PedidoDetalle WHERE PedidoId IN (9202, 9203, 9204);
DELETE FROM dbo.Pedidos WHERE PedidoId IN (9202, 9203, 9204);
SET IDENTITY_INSERT dbo.Pedidos ON;
INSERT dbo.Pedidos (PedidoId, UsuarioId, FechaPedido, Estado, TipoEntrega, DireccionEntrega, Total, CanalPedido, MetodoPago, EstadoPago, InventarioDescontado)
VALUES (9202, 7, '2026-10-30T15:00:00', N'Entregado', N'Entrega a domicilio', N'Sabana Sur, frente al estadio', 46000, N'Venta móvil', N'Efectivo', N'Pendiente', 1),
       (9203, 8, '2026-10-30T15:20:00', N'Entregado', N'Entrega a domicilio', N'San Pedro, calle principal', 32500, N'Venta móvil', N'SINPE Móvil', N'Pendiente', 1),
       (9204, 6, '2026-10-30T15:40:00', N'Entregado', N'Entrega a domicilio', N'Barva de Heredia, 200 m norte del parque', 19950, N'Venta móvil', N'Efectivo', N'Pendiente', 1);
SET IDENTITY_INSERT dbo.Pedidos OFF;
DECLARE @maxPedido2 INT = (SELECT MAX(PedidoId) FROM dbo.Pedidos WHERE PedidoId < 9000);
DBCC CHECKIDENT ('dbo.Pedidos', RESEED, @maxPedido2) WITH NO_INFOMSGS;
INSERT dbo.PedidoDetalle (PedidoId, ProductoId, Cantidad, PrecioUnitario, ProductoNombreSnapshot, EsRegalo)
VALUES (9202, 3, 8, 4200, N'Cerveza Pilsen 6 Pack', 0), (9202, 2, 6, 1850, N'Coca-Cola 2.5L', 0), (9202, 6, 2, 650, N'Agua 600ml', 0),
       (9203, 1, 5, 6500, N'Cacique 750ml', 0),
       (9204, 3, 4, 4200, N'Cerveza Pilsen 6 Pack', 0), (9204, 6, 2, 650, N'Agua 600ml', 0), (9204, 2, 1, 1850, N'Coca-Cola 2.5L', 0);
INSERT dbo.Rutas (Codigo, Zona, ChoferUsuarioId, VehiculoId, Estado, CreadaPorUsuarioId, CreadaPorNombre, FechaCreacion, FechaDespacho, FechaCierre)
VALUES (N'RUT-20261031070000', N'Heredia y Barva', 36, 2, N'Completada', 30, N'Gerente Demo', '2026-10-31T07:00:00', '2026-10-31T07:45:00', '2026-10-31T16:50:00');
SET @ruta = SCOPE_IDENTITY();
INSERT dbo.RutaPedidos (RutaId, PedidoId, Secuencia, EstadoEntrega, FechaEntrega, FechaActualizacion)
VALUES (@ruta, 9202, 1, N'Entregado', '2026-10-31T09:10:00', '2026-10-31T09:10:00'),
       (@ruta, 9203, 2, N'Entregado', '2026-10-31T10:05:00', '2026-10-31T10:05:00'),
       (@ruta, 9204, 3, N'Entregado', '2026-10-31T11:30:00', '2026-10-31T11:30:00');
GO
