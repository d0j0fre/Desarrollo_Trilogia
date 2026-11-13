/* Datos de ejemplo del grupo 3 (Aplicación móvil). Base (localdb)\Trilogia_manualoper.
   Se aplica después de capitulos/30_vendedor/_datos_grupo3_operacion.sql. Se aplica una sola vez. */
SET NOCOUNT ON; SET XACT_ABORT ON;
BEGIN TRAN;
DECLARE @Adm INT = (SELECT UsuarioId FROM Usuarios WHERE Correo='admin@labodega.cr');
DECLARE @Ger INT = (SELECT UsuarioId FROM Usuarios WHERE Correo='gerente@labodega.cr');
DECLARE @Caj INT = (SELECT UsuarioId FROM Usuarios WHERE Correo='cajero@labodega.cr');
DECLARE @Cho INT = (SELECT UsuarioId FROM Usuarios WHERE Correo='chofer@labodega.cr');
DECLARE @Sup INT = (SELECT UsuarioId FROM Usuarios WHERE Correo='supervisor@labodega.cr');

/* ── Compras: proveedores y órdenes por recibir ── */
IF NOT EXISTS (SELECT 1 FROM Proveedores)
BEGIN
  INSERT Proveedores(Nombre,Contacto,Telefono,Email,Activo,FechaCreacionUtc) VALUES
   (N'Cervecería Centroamericana',N'Mario Quesada',N'2209-1100',N'pedidos@cervecentro.cr',1,'2026-08-01'),
   (N'Importadora Licores del Valle',N'Sofía Brenes',N'2290-4455',N'ventas@licoresdelvalle.cr',1,'2026-08-01');
  DECLARE @P1 INT = (SELECT ProveedorId FROM Proveedores WHERE Nombre=N'Cervecería Centroamericana');
  DECLARE @P2 INT = (SELECT ProveedorId FROM Proveedores WHERE Nombre=N'Importadora Licores del Valle');
  INSERT OrdenesCompra(ProveedorId,Estado,Notas,UsuarioCreacionId,UsuarioCreacionNombre,FechaCreacionUtc)
   VALUES(@P1,N'Pendiente',N'Reposición semanal de cerveza.',@Adm,N'Admin Demo','2026-11-01T15:00:00');
  DECLARE @O1 INT = SCOPE_IDENTITY();
  INSERT DetalleOrdenCompra(OrdenCompraId,ProductoId,CantidadOrdenada,CantidadRecibida,PrecioUnitario) VALUES
   (@O1,12,48,0,5200),(@O1,13,36,0,4900),(@O1,14,24,0,6000);
  INSERT OrdenesCompra(ProveedorId,Estado,Notas,UsuarioCreacionId,UsuarioCreacionNombre,FechaCreacionUtc,FechaRecepcionUtc)
   VALUES(@P2,N'RecibidaParcial',N'El ron llega en una segunda entrega.',@Adm,N'Admin Demo','2026-10-29T14:00:00','2026-10-31T16:00:00');
  DECLARE @O2 INT = SCOPE_IDENTITY();
  INSERT DetalleOrdenCompra(OrdenCompraId,ProductoId,CantidadOrdenada,CantidadRecibida,PrecioUnitario) VALUES
   (@O2,4,12,12,27500),(@O2,7,24,10,9800);
END;

/* ── Rutas cerradas y liquidadas (para Liquidación de rutas) ── */
IF NOT EXISTS (SELECT 1 FROM Rutas WHERE Codigo=N'RUT-20261030065500')
BEGIN
  INSERT Rutas(Codigo,Zona,ChoferUsuarioId,VehiculoId,Estado,Observaciones,CreadaPorUsuarioId,CreadaPorNombre,FechaCreacion,FechaDespacho,FechaCierre,Liquidada,FechaLiquidacion,LiquidadaPorUsuarioId,LiquidadaPorNombre)
  VALUES(N'RUT-20261030065500',N'San José Centro',@Cho,1,N'Completada',NULL,@Ger,N'Gerente Demo','2026-10-29T16:00:00','2026-10-30T06:55:00','2026-10-30T15:30:00',1,'2026-10-30T16:10:00',@Caj,N'Cajero Demo');
  DECLARE @R3 INT = SCOPE_IDENTITY();
  INSERT RutaPedidos(RutaId,PedidoId,Secuencia,EstadoEntrega,FechaEntrega,FechaActualizacion) VALUES
   (@R3,2,1,N'Entregado','2026-10-30T09:40:00','2026-10-30T09:40:00'),(@R3,3,2,N'Entregado','2026-10-30T11:15:00','2026-10-30T11:15:00');
  INSERT RutaLiquidacionCobros(RutaId,MontoEsperadoEfectivo,MontoEsperadoOtros,MontoEfectivoRecibido,MontoComprobantes,Estado,Observaciones,LiquidadoPorUsuarioId,LiquidadoPorNombre,FechaLiquidacion)
  VALUES(@R3,42000,14690,42000,14690,N'Cuadrada',N'Sin novedades.',@Caj,N'Cajero Demo','2026-10-30T16:10:00');
  INSERT RutaLiquidacionComprobantes(LiquidacionCobrosId,Tipo,Referencia,Monto) VALUES(SCOPE_IDENTITY(),N'SINPE Móvil',N'SINPE-1030-8812',14690);

  INSERT Rutas(Codigo,Zona,ChoferUsuarioId,VehiculoId,Estado,Observaciones,CreadaPorUsuarioId,CreadaPorNombre,FechaCreacion,FechaDespacho,FechaCierre,Liquidada,FechaLiquidacion,LiquidadaPorUsuarioId,LiquidadaPorNombre)
  VALUES(N'RUT-20261031065000',N'Cartago',@Cho,1,N'Completada',NULL,@Ger,N'Gerente Demo','2026-10-30T16:00:00','2026-10-31T06:50:00','2026-10-31T15:50:00',1,'2026-10-31T16:30:00',@Caj,N'Cajero Demo');
  DECLARE @R4 INT = SCOPE_IDENTITY();
  INSERT RutaPedidos(RutaId,PedidoId,Secuencia,EstadoEntrega,FechaEntrega,FechaActualizacion) VALUES
   (@R4,4,1,N'Entregado','2026-10-31T10:05:00','2026-10-31T10:05:00'),(@R4,8,2,N'Entregado','2026-10-31T13:20:00','2026-10-31T13:20:00');
  INSERT RutaLiquidacionCobros(RutaId,MontoEsperadoEfectivo,MontoEsperadoOtros,MontoEfectivoRecibido,MontoComprobantes,Estado,Observaciones,LiquidadoPorUsuarioId,LiquidadoPorNombre,FechaLiquidacion)
  VALUES(@R4,72923,0,70000,0,N'Faltante',N'El cliente pagó con un billete roto; el chofer repone ₡2 923.',@Caj,N'Cajero Demo','2026-10-31T16:30:00');
END;

/* ── Jornadas del personal ── */
IF NOT EXISTS (SELECT 1 FROM EmpleadoJornadas)
BEGIN
  DECLARE @EEmp INT = (SELECT e.EmpleadoId FROM Empleados e JOIN Usuarios u ON u.UsuarioId=e.UsuarioId WHERE u.Correo='empleado@labodega.cr');
  DECLARE @EBod INT = (SELECT e.EmpleadoId FROM Empleados e JOIN Usuarios u ON u.UsuarioId=e.UsuarioId WHERE u.Correo='bodeguero@labodega.cr');
  DECLARE @ECho INT = (SELECT e.EmpleadoId FROM Empleados e JOIN Usuarios u ON u.UsuarioId=e.UsuarioId WHERE u.Correo='chofer@labodega.cr');
  INSERT EmpleadoJornadas(EmpleadoId,Fecha,HorasOrdinarias,HorasExtra,HorasAusencia,Observaciones,Estado,IdempotencyKey,FechaEnvioUtc,FechaRegistroUtc) VALUES
   (@EEmp,'2026-10-31',8,1,0,N'Cierre de inventario mensual',N'Enviada',NEWID(),'2026-10-31T23:30:00','2026-10-31T23:30:00'),
   (@EBod,'2026-10-31',8,2,0,N'Descarga de contenedor de cerveza',N'Enviada',NEWID(),'2026-10-31T23:40:00','2026-10-31T23:40:00'),
   (@ECho,'2026-11-01',8,0,0,N'Ruta Alajuela',N'Enviada',NEWID(),'2026-11-01T23:10:00','2026-11-01T23:10:00');
  INSERT EmpleadoJornadas(EmpleadoId,Fecha,HorasOrdinarias,HorasExtra,HorasAusencia,Observaciones,Estado,IdempotencyKey,SupervisorUsuarioId,SupervisorNombre,RespuestaSupervisor,FechaEnvioUtc,FechaDecisionUtc,FechaRegistroUtc) VALUES
   (@ECho,'2026-10-31',8,1,0,N'Ruta Cartago, tráfico en Taras',N'Aprobada',NEWID(),@Sup,N'Supervisor Demo',N'Aprobada.','2026-10-31T23:00:00','2026-11-01T14:00:00','2026-10-31T23:00:00'),
   (@ECho,'2026-10-30',8,0,0,NULL,N'Aprobada',NEWID(),@Sup,N'Supervisor Demo',NULL,'2026-10-30T23:00:00','2026-10-31T14:00:00','2026-10-30T23:00:00'),
   (@ECho,'2026-10-29',8,3,0,N'Horas extra por ruta larga',N'Rechazada',NEWID(),@Sup,N'Supervisor Demo',N'Las 3 horas extra no fueron autorizadas; registre solo 1.','2026-10-29T23:00:00','2026-10-30T14:00:00','2026-10-29T23:00:00');
END;
COMMIT;
