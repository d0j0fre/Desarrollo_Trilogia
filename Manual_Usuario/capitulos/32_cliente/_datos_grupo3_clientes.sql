/* Datos de ejemplo del grupo 3 (Cliente). Base (localdb)\Trilogia_manualoper. Se aplica una sola vez. */
SET NOCOUNT ON; SET XACT_ABORT ON;
BEGIN TRAN;
DECLARE @Cli INT = (SELECT UsuarioId FROM Usuarios WHERE Correo='cliente@labodega.cr');
DECLARE @May INT = (SELECT UsuarioId FROM Usuarios WHERE Correo='mayorista@labodega.cr');
DECLARE @Adm INT = (SELECT UsuarioId FROM Usuarios WHERE Correo='admin@labodega.cr');

UPDATE Usuarios SET NombreCompleto=N'Ana Rodríguez Solano', Direccion=N'San José, Merced, 150 m sur del Mercado Central', Telefono=N'8700-0014' WHERE UsuarioId=@Cli;
UPDATE Usuarios SET NombreCompleto=N'Abastecedor La Bodeguita', Direccion=N'Heredia, San Francisco, frente a la plaza', Telefono=N'8700-0015', SegmentoCliente=N'Mayorista' WHERE UsuarioId=@May;

/* Crédito del mayorista */
IF NOT EXISTS (SELECT 1 FROM ClienteCreditos WHERE UsuarioId=@May)
  INSERT ClienteCreditos(UsuarioId,LimiteCredito,CreditoActivo,CreditoBloqueado,FechaCreacion,FechaActualizacion,PlazoCreditoDias)
  VALUES(@May,600000,1,0,'2026-08-01T09:00:00',SYSDATETIME(),30);

/* Promociones vigentes en noviembre */
IF NOT EXISTS (SELECT 1 FROM Promociones WHERE Nombre=N'Mayoreo Imperial: 10 % desde 12 six packs')
  INSERT Promociones(Nombre,Descripcion,Tipo,ProductoId,CantidadMinima,PorcentajeDescuento,SegmentoCliente,FechaInicio,FechaFin,Estado,Prioridad,RegistradoPorUsuarioId,RegistradoPorNombre,FechaRegistro)
  VALUES(N'Mayoreo Imperial: 10 % desde 12 six packs',N'Descuento exclusivo para clientes mayoristas.',N'DescuentoPorcentual',12,12,10,N'Mayorista','2026-11-01','2026-12-31',N'Activa',1,@Adm,N'Admin Demo',SYSDATETIME());
IF NOT EXISTS (SELECT 1 FROM Promociones WHERE Nombre=N'Hielo gratis con 6 Pilsen')
  INSERT Promociones(Nombre,Descripcion,Tipo,ProductoId,CantidadMinima,ProductoRegaloId,CantidadRegalo,SegmentoCliente,FechaInicio,FechaFin,Estado,Prioridad,RegistradoPorUsuarioId,RegistradoPorNombre,FechaRegistro)
  VALUES(N'Hielo gratis con 6 Pilsen',N'Una bolsa de hielo de regalía por cada 6 six packs Pilsen.',N'RegaliaPorVolumen',13,6,23,1,N'Todos','2026-11-01','2026-12-31',N'Activa',2,@Adm,N'Admin Demo',SYSDATETIME());

/* Pedidos históricos con factura */
DECLARE @v TABLE(n INT, Cliente INT, Fecha DATETIME2, Estado NVARCHAR(30), Pago NVARCHAR(40), Canal NVARCHAR(40), Prod INT, Cant INT);
INSERT @v VALUES
 (1,@Cli,'2026-10-20T18:10:00',N'Entregado',N'Tarjeta demo',N'Tienda en línea',19,2),(1,@Cli,'2026-10-20T18:10:00',N'Entregado',N'Tarjeta demo',N'Tienda en línea',2,2),
 (2,@May,'2026-09-20T10:00:00',N'Entregado',N'Crédito',N'Venta móvil',12,24),(2,@May,'2026-09-20T10:00:00',N'Entregado',N'Crédito',N'Venta móvil',7,6),
 (3,@May,'2026-10-25T09:30:00',N'Entregado',N'Crédito',N'Venta móvil',13,12),(3,@May,'2026-10-25T09:30:00',N'Entregado',N'Crédito',N'Venta móvil',16,6),
 (4,@May,'2026-11-01T11:00:00',N'EnProceso',N'Efectivo contra entrega',N'Tienda en línea',14,10),(4,@May,'2026-11-01T11:00:00',N'EnProceso',N'Efectivo contra entrega',N'Tienda en línea',22,12);
DECLARE @i INT=1, @Ped INT, @Fac INT, @Sub DECIMAL(18,2), @C INT, @F DATETIME2, @E NVARCHAR(30), @P NVARCHAR(40), @Ca NVARCHAR(40);
IF NOT EXISTS (SELECT 1 FROM Pedidos WHERE UsuarioId IN (@Cli,@May))
WHILE @i<=4
BEGIN
  SELECT TOP 1 @C=Cliente,@F=Fecha,@E=Estado,@P=Pago,@Ca=Canal FROM @v WHERE n=@i;
  SELECT @Sub=SUM(v.Cant*p.Precio) FROM @v v JOIN Productos p ON p.ProductoId=v.Prod WHERE v.n=@i;
  INSERT Pedidos(UsuarioId,FechaPedido,Estado,TipoEntrega,DireccionEntrega,Total,Observaciones,CanalPedido,FechaActualizacion,MetodoPago,EstadoPago,FechaPago,InventarioDescontado)
  SELECT @C,@F,@E,N'Envío a domicilio',u.Direccion,@Sub,NULL,@Ca,@F,@P,CASE WHEN @P=N'Crédito' THEN N'Pendiente' ELSE N'Confirmado simulado' END,CASE WHEN @P=N'Crédito' THEN NULL ELSE @F END,1 FROM Usuarios u WHERE u.UsuarioId=@C;
  SET @Ped=SCOPE_IDENTITY();
  INSERT PedidoDetalle(PedidoId,ProductoId,Cantidad,PrecioUnitario,ProductoNombreSnapshot,EsRegalo)
  SELECT @Ped,p.ProductoId,v.Cant,p.Precio,p.Nombre,0 FROM @v v JOIN Productos p ON p.ProductoId=v.Prod WHERE v.n=@i;
  IF @E=N'Entregado'
  BEGIN
    INSERT Facturas(PedidoId,NumeroFactura,UsuarioId,ClienteNombre,ClienteCorreo,FechaFactura,Subtotal,Impuesto,Total,Estado)
    SELECT @Ped,CONCAT(N'FAC-',FORMAT(@F,'yyyyMMddHHmmss'),N'-',RIGHT(CONCAT('000',@Ped),4)),@C,u.NombreCompleto,u.Correo,DATEADD(MINUTE,20,@F),@Sub,ROUND(@Sub*0.13,2),ROUND(@Sub*1.13,2),N'Generada' FROM Usuarios u WHERE u.UsuarioId=@C;
    SET @Fac=SCOPE_IDENTITY();
    INSERT FacturaDetalle(FacturaId,ProductoId,ProductoNombre,Cantidad,PrecioUnitario)
    SELECT @Fac,p.ProductoId,p.Nombre,v.Cant,p.Precio FROM @v v JOIN Productos p ON p.ProductoId=v.Prod WHERE v.n=@i;
    IF @P=N'Crédito'
      INSERT ClienteCreditoMovimientos(UsuarioId,TipoMovimiento,Monto,Descripcion,Referencia,RegistradoPorUsuarioId,RegistradoPorNombre,FechaMovimiento,PedidoId)
      VALUES(@C,N'Cargo',ROUND(@Sub*1.13,2),CONCAT(N'Pedido a crédito #',@Ped),CONCAT(N'Pedido #',@Ped),@Adm,N'Admin Demo',DATEADD(MINUTE,20,@F),@Ped);
  END;
  SET @i+=1;
END;

/* Abono parcial del mayorista (deja parte del primer cargo vencida) */
IF NOT EXISTS (SELECT 1 FROM ClienteCreditoMovimientos WHERE UsuarioId=@May AND TipoMovimiento=N'Abono')
  INSERT ClienteCreditoMovimientos(UsuarioId,TipoMovimiento,Monto,Descripcion,Referencia,RegistradoPorUsuarioId,RegistradoPorNombre,FechaMovimiento)
  VALUES(@May,N'Abono',150000,N'Abono por transferencia SINPE.',N'SINPE-BODEGUITA-0915',@Adm,N'Admin Demo','2026-10-15T14:00:00');

/* Garantías del mayorista */
IF NOT EXISTS (SELECT 1 FROM Garantias WHERE UsuarioId=@May)
BEGIN
  INSERT Garantias(PedidoDetalleId,UsuarioId,FechaSolicitud,Motivo,Descripcion,Telefono,Estado,Resolucion,FechaResolucion,ResueltoPorUsuarioId,ResueltoPorNombre)
  SELECT TOP 1 d.PedidoDetalleId,@May,'2026-10-27T15:00:00',N'Producto dañado',N'Dos six packs llegaron con latas golpeadas y una abierta.',N'8700-0015',N'Aprobada',N'Se reponen 2 six packs en el próximo pedido.','2026-10-28T10:00:00',@Adm,N'Admin Demo'
  FROM PedidoDetalle d JOIN Pedidos p ON p.PedidoId=d.PedidoId WHERE p.UsuarioId=@May AND p.FechaPedido='2026-10-25T09:30:00' AND d.ProductoId=13;
  INSERT Garantias(PedidoDetalleId,UsuarioId,FechaSolicitud,Motivo,Descripcion,Telefono,Estado)
  SELECT TOP 1 d.PedidoDetalleId,@May,'2026-11-01T16:30:00',N'Producto defectuoso',N'Una botella de vodka trae el sello de seguridad roto.',N'8700-0015',N'En revisión'
  FROM PedidoDetalle d JOIN Pedidos p ON p.PedidoId=d.PedidoId WHERE p.UsuarioId=@May AND p.FechaPedido='2026-10-25T09:30:00' AND d.ProductoId=16;
END;
COMMIT;
SELECT * FROM dbo.fn_Cliente_EstadoDeuda((SELECT UsuarioId FROM Usuarios WHERE Correo='mayorista@labodega.cr'));
