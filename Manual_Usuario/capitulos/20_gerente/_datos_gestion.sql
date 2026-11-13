/* Datos de ejemplo de la Parte III (Gerente): logística, flota, metas y reportes.
   Base (localdb)\Trilogia_manualgest, después de fix_perfiles_oficina.sql y datos_demo_manual.sql.
   Se puede volver a correr: borra y vuelve a crear solo sus propios registros
   (pedidos con la observación «Reposición semanal», rutas de las zonas de abajo,
   órdenes de mantenimiento, documentos de vehículos y metas). No toca la ruta de datos_demo_manual.sql. */
SET NOCOUNT ON; SET XACT_ABORT ON; SET QUOTED_IDENTIFIER ON;
BEGIN TRAN;

DECLARE @Ger INT = (SELECT UsuarioId FROM Usuarios WHERE Correo='gerente@labodega.cr');
DECLARE @Cho INT = (SELECT UsuarioId FROM Usuarios WHERE Correo='chofer@labodega.cr');
DECLARE @Ran INT = (SELECT UsuarioId FROM Usuarios WHERE Correo='rquiros@labodega.cr');
DECLARE @Sol INT = (SELECT UsuarioId FROM Usuarios WHERE Correo='jsolis@labodega.cr');
DECLARE @Ari INT = (SELECT UsuarioId FROM Usuarios WHERE Correo='marias@labodega.cr');
DECLARE @Ven INT = (SELECT UsuarioId FROM Usuarios WHERE Correo='vendedor@labodega.cr');

/* ── Limpieza de lo creado antes por este script ── */
DECLARE @Zonas TABLE (Zona NVARCHAR(120));
INSERT @Zonas VALUES (N'Heredia Norte'), (N'Cartago y Tres Ríos'), (N'Alajuela y San Carlos'), (N'Escazú y Santa Ana');
DELETE rp FROM RutaPedidos rp JOIN Rutas r ON r.RutaId = rp.RutaId WHERE r.Zona IN (SELECT Zona FROM @Zonas);
DELETE FROM Rutas WHERE Zona IN (SELECT Zona FROM @Zonas);
DELETE rp FROM RutaPedidos rp JOIN Pedidos p ON p.PedidoId = rp.PedidoId WHERE p.Observaciones = N'Reposición semanal.';
DELETE d FROM PedidoDetalle d JOIN Pedidos p ON p.PedidoId = d.PedidoId WHERE p.Observaciones = N'Reposición semanal.';
DELETE FROM Pedidos WHERE Observaciones = N'Reposición semanal.';
DELETE FROM OrdenesMantenimiento;
DELETE FROM VehiculoDocumentos;
DELETE FROM MetasVendedor WHERE Anio = 2026 AND Mes IN (10, 11);
DELETE FROM Vehiculos WHERE Placa IN (N'CRJ-004');

/* ── Coordenadas de clientes (para el mapa de las rutas) ── */
UPDATE Usuarios SET Latitud=10.016224, Longitud=-84.121541 WHERE UsuarioId=6;   -- Pulpería El Tucán (Barva)
UPDATE Usuarios SET Latitud=9.933612,  Longitud=-84.100317 WHERE UsuarioId=7;   -- Minisúper La Sabana
UPDATE Usuarios SET Latitud=9.933905,  Longitud=-84.050712 WHERE UsuarioId=8;   -- Licorera Pura Vida
UPDATE Usuarios SET Latitud=10.016352, Longitud=-84.214470 WHERE UsuarioId=9;   -- Super Don Beto (Alajuela)
UPDATE Usuarios SET Latitud=9.918712,  Longitud=-84.140533 WHERE UsuarioId=10;  -- El Mirador (Escazú)
UPDATE Usuarios SET Latitud=9.998612,  Longitud=-84.116094 WHERE UsuarioId=11;  -- Abastecedor Los Lagos
UPDATE Usuarios SET Latitud=9.864402,  Longitud=-83.919385 WHERE UsuarioId=12;  -- Soda y Bar La Carreta
UPDATE Usuarios SET Latitud=10.470845, Longitud=-84.644637 WHERE UsuarioId=13;  -- Hotel Camino Verde
UPDATE Usuarios SET Latitud=10.023911, Longitud=-84.071820 WHERE UsuarioId=17;  -- Comercial Santa Lucía

/* Dirección del cliente demo con tildes dañadas en la base demo. */
UPDATE Usuarios SET Direccion = N'San Jos' + NCHAR(233) + N' Centro' WHERE Direccion = N'San Jos' + NCHAR(195) + NCHAR(169) + N' Centro';
UPDATE Pedidos  SET DireccionEntrega = N'San Jos' + NCHAR(233) + N' Centro' WHERE DireccionEntrega = N'San Jos' + NCHAR(195) + NCHAR(169) + N' Centro';

/* ── Pedidos para las rutas ── */
DECLARE @p TABLE (n INT, Cliente INT, Fecha DATETIME2, Estado NVARCHAR(20), Prod INT, Cant INT);
INSERT @p VALUES
 (1, 6, '2026-11-02T07:30:00', N'Preparando', 13, 6), (1, 6, '2026-11-02T07:30:00', N'Preparando', 23, 10),
 (2,11, '2026-11-02T07:45:00', N'Preparando', 11, 6), (2,11, '2026-11-02T07:45:00', N'Preparando', 2, 12),
 (3,17, '2026-11-02T08:05:00', N'Preparando', 12, 8), (3,17, '2026-11-02T08:05:00', N'Preparando', 21, 24),
 (4, 7, '2026-11-02T08:20:00', N'Preparando', 14, 5), (4, 7, '2026-11-02T08:20:00', N'Preparando', 6, 24),
 (5,16, '2026-11-02T08:35:00', N'Pendiente',  1, 6),  (5,16, '2026-11-02T08:35:00', N'Pendiente', 22, 6),
 (6,12, '2026-11-01T15:10:00', N'Preparando', 3, 6),  (6,12, '2026-11-01T15:10:00', N'Preparando', 24, 20),
 (7, 8, '2026-11-01T15:30:00', N'Preparando', 7, 4),  (7, 8, '2026-11-01T15:30:00', N'Preparando', 16, 3),
 (8, 9, '2026-10-30T09:00:00', N'Entregado', 12, 10), (8, 9, '2026-10-30T09:00:00', N'Entregado', 23, 15),
 (9,13, '2026-10-30T09:15:00', N'Preparando', 15, 6), (9,13, '2026-10-30T09:15:00', N'Preparando', 19, 6);
DECLARE @Ped TABLE (n INT, PedidoId INT);
DECLARE @i INT = 1, @Id INT;
WHILE @i <= 9
BEGIN
  INSERT Pedidos (UsuarioId, FechaPedido, Estado, TipoEntrega, DireccionEntrega, Total, Observaciones, VendedorUsuarioId, VendedorNombre,
                  CanalPedido, FechaActualizacion, MetodoPago, EstadoPago, InventarioDescontado)
  SELECT TOP 1 x.Cliente, x.Fecha, x.Estado, N'Entrega a domicilio', u.Direccion,
         (SELECT SUM(y.Cant * pr.Precio) FROM @p y JOIN Productos pr ON pr.ProductoId = y.Prod WHERE y.n = @i),
         N'Reposición semanal.', IIF(@i % 2 = 0, @Sol, @Ari), IIF(@i % 2 = 0, N'José Andrés Solís Castro', N'Marco Vinicio Arias Soto'),
         N'Venta móvil', x.Fecha, N'Crédito', N'Pendiente', 1
  FROM @p x JOIN Usuarios u ON u.UsuarioId = x.Cliente WHERE x.n = @i;
  SET @Id = SCOPE_IDENTITY();
  INSERT @Ped VALUES (@i, @Id);
  INSERT PedidoDetalle (PedidoId, ProductoId, Cantidad, PrecioUnitario, ProductoNombreSnapshot, EsRegalo)
  SELECT @Id, pr.ProductoId, x.Cant, pr.Precio, pr.Nombre, 0 FROM @p x JOIN Productos pr ON pr.ProductoId = x.Prod WHERE x.n = @i;
  SET @i += 1;
END;
-- Los pedidos 4 y 5 quedan sin ruta: son los que se ven al crear una ruta nueva.

/* ── Rutas ── */
DECLARE @R INT;
-- Planificada (para secuenciar, agregar/quitar y despachar)
INSERT Rutas (Codigo, Zona, ChoferUsuarioId, VehiculoId, Estado, Observaciones, CreadaPorUsuarioId, CreadaPorNombre, FechaCreacion, FechaActualizacion, Liquidada)
VALUES (N'RUT-20261102080000', N'Heredia Norte', @Ran, 3, N'Planificada', N'Salida a las 9:00 a. m. desde la bodega.', @Ger, N'Gerente Demo', '2026-11-02T08:00:00', '2026-11-02T08:00:00', 0);
SET @R = SCOPE_IDENTITY();
INSERT RutaPedidos (RutaId, PedidoId, Secuencia, EstadoEntrega, Latitud, Longitud, FechaActualizacion)
SELECT @R, pd.PedidoId, pd.n, N'Pendiente', u.Latitud, u.Longitud, SYSDATETIME()
FROM @Ped pd JOIN Pedidos p ON p.PedidoId = pd.PedidoId JOIN Usuarios u ON u.UsuarioId = p.UsuarioId WHERE pd.n IN (1, 2, 3);

-- Despachada con avance (para recalcular y para el tablero)
INSERT Rutas (Codigo, Zona, ChoferUsuarioId, VehiculoId, Estado, Observaciones, CreadaPorUsuarioId, CreadaPorNombre, FechaCreacion, FechaDespacho, FechaActualizacion, Liquidada)
VALUES (N'RUT-20261102063000', N'Cartago y Tres Ríos', @Cho, 2, N'Despachada', NULL, @Ger, N'Gerente Demo', '2026-11-02T06:30:00', '2026-11-02T07:10:00', SYSDATETIME(), 0);
SET @R = SCOPE_IDENTITY();
INSERT RutaPedidos (RutaId, PedidoId, Secuencia, EstadoEntrega, FechaEntrega, Latitud, Longitud, FechaActualizacion)
SELECT @R, pd.PedidoId, pd.n - 5, IIF(pd.n = 7, N'Entregado', N'EnRuta'), IIF(pd.n = 7, '2026-11-02T08:40:00', NULL), u.Latitud, u.Longitud, SYSDATETIME()
FROM @Ped pd JOIN Pedidos p ON p.PedidoId = pd.PedidoId JOIN Usuarios u ON u.UsuarioId = p.UsuarioId WHERE pd.n IN (6, 7);

-- Completada y liquidada (historial)
INSERT Rutas (Codigo, Zona, ChoferUsuarioId, VehiculoId, Estado, Observaciones, CreadaPorUsuarioId, CreadaPorNombre, FechaCreacion, FechaDespacho, FechaCierre,
              FechaActualizacion, Liquidada, FechaLiquidacion, LiquidadaPorUsuarioId, LiquidadaPorNombre)
VALUES (N'RUT-20261030070000', N'Alajuela y San Carlos', @Ran, 1, N'Completada', NULL, @Ger, N'Gerente Demo', '2026-10-30T07:00:00', '2026-10-30T07:40:00', '2026-10-30T17:30:00',
        '2026-10-30T17:30:00', 1, '2026-10-30T17:30:00', @Ger, N'Gerente Demo');
SET @R = SCOPE_IDENTITY();
INSERT RutaPedidos (RutaId, PedidoId, Secuencia, EstadoEntrega, MotivoFallo, FechaEntrega, Latitud, Longitud, FechaActualizacion)
SELECT @R, pd.PedidoId, pd.n - 7, IIF(pd.n = 8, N'Entregado', N'Fallido'), IIF(pd.n = 9, N'Local cerrado por fumigación', NULL),
       IIF(pd.n = 8, '2026-10-30T10:15:00', NULL), u.Latitud, u.Longitud, '2026-10-30T17:30:00'
FROM @Ped pd JOIN Pedidos p ON p.PedidoId = pd.PedidoId JOIN Usuarios u ON u.UsuarioId = p.UsuarioId WHERE pd.n IN (8, 9);

/* ── Flota: odómetros, mantenimiento y documentos ── */
UPDATE Vehiculos SET KilometrajeActual = 48250, Marca = ISNULL(Marca, N'Toyota') WHERE VehiculoId = 1;
UPDATE Vehiculos SET KilometrajeActual = 91400, Marca = ISNULL(Marca, N'Isuzu')  WHERE VehiculoId = 2;
UPDATE Vehiculos SET KilometrajeActual = 30120, Marca = ISNULL(Marca, N'Nissan') WHERE VehiculoId = 3;

INSERT OrdenesMantenimiento (VehiculoId, Tipo, Descripcion, Taller, Costo, Estado, FechaProgramada, FechaRealizada, KilometrajeProximo, RegistradoPorUsuarioId, RegistradoPorNombre, FechaRegistro) VALUES
 (2, N'Correctivo', N'Cambio de pastillas de freno delanteras', N'Frenos y Clutch Alajuela', 85000, N'Completada', NULL, '2026-10-20', NULL, @Ger, N'Gerente Demo', '2026-10-19T10:00:00'),
 (3, N'Preventivo', N'Revisión de los 30 000 km', N'Agencia Nissan Zapote', 120000, N'EnProceso', '2026-10-31', NULL, NULL, @Ger, N'Gerente Demo', '2026-10-28T09:00:00'),
 (2, N'Preventivo', N'Rotación y alineamiento de llantas', N'Llantas del Oeste', 35000, N'Programada', NULL, NULL, 90000, @Ger, N'Gerente Demo', '2026-10-05T08:00:00'),
 (1, N'Preventivo', N'Cambio de aceite y filtros', N'Servicentro La Uruca', 45000, N'Programada', '2026-11-09', NULL, 50000, @Ger, N'Gerente Demo', '2026-10-29T15:00:00');

INSERT VehiculoDocumentos (VehiculoId, Tipo, FechaVencimiento, Observaciones, FechaRegistro) VALUES
 (3, N'Seguro', '2026-10-28', N'Póliza de responsabilidad civil INS', '2026-01-10'),
 (1, N'RTV', '2026-11-12', N'Cita en Riteve de La Uruca', '2025-11-12'),
 (2, N'Marchamo', '2026-12-31', NULL, '2026-01-05');

/* ── Vendedores: pedidos repartidos entre el equipo y metas de octubre y noviembre ── */
UPDATE Pedidos SET VendedorUsuarioId = CASE PedidoId % 3 WHEN 0 THEN @Sol WHEN 1 THEN @Ari ELSE @Ven END,
                   VendedorNombre    = CASE PedidoId % 3 WHEN 0 THEN N'José Andrés Solís Castro' WHEN 1 THEN N'Marco Vinicio Arias Soto' ELSE N'Vendedor Demo' END
WHERE VendedorNombre = N'Administrador General';

INSERT MetasVendedor (VendedorUsuarioId, Anio, Mes, MontoMeta, Observaciones, RegistradoPorUsuarioId, RegistradoPorNombre) VALUES
 (@Sol, 2026, 10, 250000, N'Meta de octubre.', @Ger, N'Gerente Demo'),
 (@Ari, 2026, 10, 300000, N'Meta de octubre.', @Ger, N'Gerente Demo'),
 (@Ven, 2026, 10, 420000, N'Meta de octubre.', @Ger, N'Gerente Demo'),
 (@Sol, 2026, 11, 320000, N'Temporada de fin de año.', @Ger, N'Gerente Demo'),
 (@Ari, 2026, 11, 350000, N'Temporada de fin de año.', @Ger, N'Gerente Demo');

COMMIT;
SELECT r.RutaId, r.Codigo, r.Zona, r.Estado FROM Rutas r ORDER BY r.RutaId;
