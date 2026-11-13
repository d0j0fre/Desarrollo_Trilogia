/* Datos de ejemplo del grupo 3 (Vendedor y Chofer). Base (localdb)\Trilogia_manualoper.
   Se aplica una sola vez sobre la base restaurada + fix_perfiles_oficina.sql + datos_demo_manual.sql. */
SET NOCOUNT ON; SET XACT_ABORT ON;
BEGIN TRAN;
DECLARE @Ven INT = (SELECT UsuarioId FROM Usuarios WHERE Correo='vendedor@labodega.cr');
DECLARE @Cho INT = (SELECT UsuarioId FROM Usuarios WHERE Correo='chofer@labodega.cr');
DECLARE @Ger INT = (SELECT UsuarioId FROM Usuarios WHERE Correo='gerente@labodega.cr');

/* ── Vendedor: meta de noviembre y ventas facturadas del mes ── */
IF NOT EXISTS (SELECT 1 FROM MetasVendedor WHERE VendedorUsuarioId=@Ven AND Anio=2026 AND Mes=11)
  INSERT MetasVendedor(VendedorUsuarioId,Anio,Mes,MontoMeta,Observaciones,RegistradoPorUsuarioId,RegistradoPorNombre)
  VALUES(@Ven,2026,11,1900000,N'Meta de noviembre: temporada de fin de año.',@Ger,N'Gerente Demo');

DECLARE @v TABLE(n INT, Cliente INT, Fecha DATETIME2, Prod INT, Cant INT);
INSERT @v VALUES (1,10,'2026-11-01T09:20:00',18,10),(1,10,'2026-11-01T09:20:00',15,12),
                 (2,13,'2026-11-01T14:05:00',4,6),(2,13,'2026-11-01T14:05:00',19,15),
                 (3,6,'2026-11-02T08:40:00',12,20),(3,6,'2026-11-02T08:40:00',23,30),
                 (4,14,'2026-11-02T10:15:00',9,10),(4,14,'2026-11-02T10:15:00',16,12);
DECLARE @i INT = 1, @Ped INT, @Fac INT, @Sub DECIMAL(18,2), @Cli INT, @F DATETIME2;
WHILE @i <= 4
BEGIN
  SELECT TOP 1 @Cli=Cliente, @F=Fecha FROM @v WHERE n=@i;
  SELECT @Sub = SUM(v.Cant*p.Precio) FROM @v v JOIN Productos p ON p.ProductoId=v.Prod WHERE v.n=@i;
  INSERT Pedidos(UsuarioId,FechaPedido,Estado,TipoEntrega,DireccionEntrega,Total,Observaciones,VendedorUsuarioId,VendedorNombre,CanalPedido,FechaActualizacion,MetodoPago,EstadoPago,InventarioDescontado)
  SELECT @Cli,@F,N'Entregado',N'Entrega por vendedor',u.Direccion,@Sub,N'Pedido tomado en visita de campo.',@Ven,N'Vendedor Demo',N'Venta móvil',@F,N'Crédito vendedor',N'Pendiente',1
  FROM Usuarios u WHERE u.UsuarioId=@Cli;
  SET @Ped = SCOPE_IDENTITY();
  INSERT PedidoDetalle(PedidoId,ProductoId,Cantidad,PrecioUnitario,ProductoNombreSnapshot,EsRegalo)
  SELECT @Ped,p.ProductoId,v.Cant,p.Precio,p.Nombre,0 FROM @v v JOIN Productos p ON p.ProductoId=v.Prod WHERE v.n=@i;
  INSERT Facturas(PedidoId,NumeroFactura,UsuarioId,ClienteNombre,ClienteCorreo,FechaFactura,Subtotal,Impuesto,Total,Estado)
  SELECT @Ped,CONCAT(N'FAC-',FORMAT(@F,'yyyyMMddHHmmss'),N'-',RIGHT(CONCAT('000',@Ped),4)),@Cli,u.NombreCompleto,u.Correo,DATEADD(MINUTE,5,@F),@Sub,ROUND(@Sub*0.13,2),ROUND(@Sub*1.13,2),N'Generada'
  FROM Usuarios u WHERE u.UsuarioId=@Cli;
  SET @Fac = SCOPE_IDENTITY();
  INSERT FacturaDetalle(FacturaId,ProductoId,ProductoNombre,Cantidad,PrecioUnitario)
  SELECT @Fac,p.ProductoId,p.Nombre,v.Cant,p.Precio FROM @v v JOIN Productos p ON p.ProductoId=v.Prod WHERE v.n=@i;
  SET @i += 1;
END;

/* ── Chofer: coordenadas de clientes, ruta despachada de 3 paradas, ruta planificada, odómetros ── */
UPDATE Usuarios SET Latitud=9.933612, Longitud=-84.100317 WHERE UsuarioId=7;   -- Minisúper La Sabana
UPDATE Usuarios SET Latitud=9.933905, Longitud=-84.050712 WHERE UsuarioId=8;   -- Licorera Pura Vida
UPDATE Usuarios SET Latitud=9.864402, Longitud=-83.919385 WHERE UsuarioId=12;  -- Soda y Bar La Carreta
UPDATE Usuarios SET Latitud=10.002015, Longitud=-84.117308 WHERE UsuarioId=9;  -- Super Don Beto
UPDATE Usuarios SET Latitud=9.998612, Longitud=-84.116094 WHERE UsuarioId=11;  -- Abastecedor Los Lagos (aprox.)
UPDATE Usuarios SET Latitud=9.912845, Longitud=-84.066411 WHERE UsuarioId=14;  -- Eventos Ticos CR
UPDATE Usuarios SET Latitud=9.918712, Longitud=-84.140533 WHERE UsuarioId=10;  -- Bar y Restaurante El Mirador (Escazú)

IF NOT EXISTS (SELECT 1 FROM RutaPedidos WHERE PedidoId=7)
BEGIN
  INSERT RutaPedidos(RutaId,PedidoId,Secuencia,EstadoEntrega,FechaActualizacion) VALUES(1,7,1,N'EnRuta',SYSDATETIME());
  UPDATE Pedidos SET Estado=N'EnRuta' WHERE PedidoId=7;
END;
UPDATE RutaPedidos SET Secuencia=2 WHERE RutaId=1 AND PedidoId=5;
UPDATE RutaPedidos SET Secuencia=3 WHERE RutaId=1 AND PedidoId=9;
UPDATE rp SET Latitud=u.Latitud, Longitud=u.Longitud FROM RutaPedidos rp JOIN Pedidos p ON p.PedidoId=rp.PedidoId JOIN Usuarios u ON u.UsuarioId=p.UsuarioId WHERE rp.RutaId=1;
UPDATE Rutas SET Zona=N'San José y Cartago' WHERE RutaId=1;

IF NOT EXISTS (SELECT 1 FROM Rutas WHERE Codigo=N'RUT-20261103070000')
BEGIN
  INSERT Rutas(Codigo,Zona,ChoferUsuarioId,VehiculoId,Estado,Observaciones,CreadaPorUsuarioId,CreadaPorNombre,FechaCreacion)
  VALUES(N'RUT-20261103070000',N'Alajuela y Heredia',@Cho,2,N'Planificada',N'Salida 7:00 a. m. del martes.',@Ger,N'Gerente Demo','2026-11-02T15:00:00');
  DECLARE @R2 INT = SCOPE_IDENTITY();
  INSERT RutaPedidos(RutaId,PedidoId,Secuencia,EstadoEntrega,FechaActualizacion,Latitud,Longitud)
  SELECT @R2,p.PedidoId,ROW_NUMBER() OVER(ORDER BY p.PedidoId),N'Pendiente',SYSDATETIME(),u.Latitud,u.Longitud
  FROM Pedidos p JOIN Usuarios u ON u.UsuarioId=p.UsuarioId WHERE p.PedidoId IN (6,11);
END;

UPDATE Vehiculos SET KilometrajeActual=48215, Marca=N'Toyota' WHERE VehiculoId=1;
UPDATE Vehiculos SET KilometrajeActual=92640, Marca=N'Isuzu'  WHERE VehiculoId=2;
UPDATE Vehiculos SET KilometrajeActual=31078, Marca=N'Nissan' WHERE VehiculoId=3;
IF NOT EXISTS (SELECT 1 FROM VehiculoKilometraje WHERE ChoferUsuarioId=@Cho)
  INSERT VehiculoKilometraje(VehiculoId,ChoferUsuarioId,ChoferNombre,Fecha,KmInicial,KmFinal,Observaciones,FechaRegistro,FechaCierre)
  VALUES(1,@Cho,N'Chofer Demo','2026-10-30',47930,48071,N'Ruta San José Centro',  '2026-10-30T06:55:00','2026-10-30T15:40:00'),
        (1,@Cho,N'Chofer Demo','2026-10-31',48071,48215,N'Ruta Cartago',        '2026-10-31T06:50:00','2026-10-31T16:05:00');
COMMIT;
