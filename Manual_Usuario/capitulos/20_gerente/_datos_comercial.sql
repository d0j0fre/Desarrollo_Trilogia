-- Datos de ejemplo de la Parte III (Gerente): Devoluciones, Cuarentena, Activos, Comodatos,
-- Promociones, Reclamos y Pedidos retenidos. Se puede correr las veces que haga falta:
-- primero borra solo lo que este mismo script (o las capturas de gerente_comercial.json) creó.
--   sqlcmd -S <pipe> -d DistribuidoraJJ_DB -E -I -b -i capitulos\20_gerente\_datos_comercial.sql
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;

DECLARE @Ger INT = (SELECT UsuarioId FROM dbo.Usuarios WHERE Correo = N'gerente@labodega.cr');
DECLARE @GerNom NVARCHAR(150) = (SELECT NombreCompleto FROM dbo.Usuarios WHERE UsuarioId = @Ger);
DECLARE @Ven INT = (SELECT UsuarioId FROM dbo.Usuarios WHERE Correo = N'vendedor@labodega.cr');
DECLARE @VenNom NVARCHAR(150) = (SELECT NombreCompleto FROM dbo.Usuarios WHERE UsuarioId = @Ven);
DECLARE @Hoy DATE = CAST(SYSDATETIME() AS DATE);

-- ───────────── Limpieza ─────────────
-- Devoluciones registradas por el Gerente: se devuelve el stock de las reintegradas.
UPDATE p SET p.Stock = p.Stock - d.Cantidad
FROM dbo.Productos p JOIN dbo.Devoluciones d ON d.ProductoId = p.ProductoId
WHERE d.RegistradoPorUsuarioId = @Ger AND d.Estado = N'Reintegrada';
DELETE m FROM dbo.MovimientosInventario m
WHERE m.Motivo LIKE N'Reintegro desde cuarentena (devolución #%'
  AND EXISTS (SELECT 1 FROM dbo.Devoluciones d WHERE d.RegistradoPorUsuarioId = @Ger
              AND m.Motivo = CONCAT(N'Reintegro desde cuarentena (devolución #', d.DevolucionId, N')'));
DELETE FROM dbo.Devoluciones WHERE RegistradoPorUsuarioId = @Ger;

-- Activos ACT-01xx y sus comodatos.
DELETE c FROM dbo.Comodatos c JOIN dbo.Activos a ON a.ActivoId = c.ActivoId WHERE a.CodigoActivo LIKE N'ACT-01%';
DELETE FROM dbo.Activos WHERE CodigoActivo LIKE N'ACT-01%';

-- Promociones creadas por el Gerente.
DELETE FROM dbo.PromocionAplicaciones WHERE PromocionId IN (SELECT PromocionId FROM dbo.Promociones WHERE RegistradoPorUsuarioId = @Ger);
DELETE FROM dbo.Promociones WHERE RegistradoPorUsuarioId = @Ger;

-- Reclamos registrados por el Gerente.
DELETE FROM dbo.Reclamos WHERE RegistradoPorUsuarioId = @Ger;

-- Pedidos retenidos de ejemplo (identificados por su GUID fijo).
DECLARE @Guids TABLE (G UNIQUEIDENTIFIER);
INSERT @Guids VALUES ('6A1F0C11-0000-4000-8000-000000000301'), ('6A1F0C11-0000-4000-8000-000000000302'),
                     ('6A1F0C11-0000-4000-8000-000000000303');
DECLARE @Ped TABLE (PedidoId INT);
INSERT @Ped SELECT PedidoId FROM dbo.Pedidos WHERE PedidoOfflineGuid IN (SELECT G FROM @Guids);
UPDATE pr SET pr.Stock = pr.Stock + d.Cantidad
FROM dbo.Productos pr JOIN dbo.PedidoDetalle d ON d.ProductoId = pr.ProductoId
JOIN dbo.Pedidos p ON p.PedidoId = d.PedidoId
WHERE p.PedidoId IN (SELECT PedidoId FROM @Ped) AND p.InventarioDescontado = 1;
DELETE m FROM dbo.MovimientosInventario m JOIN @Ped x ON m.Motivo LIKE CONCAT(N'Pedido #', x.PedidoId, N' liberado por %');
DELETE FROM dbo.ClienteCreditoMovimientos WHERE PedidoId IN (SELECT PedidoId FROM @Ped);
DELETE FROM dbo.Reclamos WHERE PedidoId IN (SELECT PedidoId FROM @Ped);
DELETE fd FROM dbo.FacturaDetalle fd JOIN dbo.Facturas f ON f.FacturaId = fd.FacturaId WHERE f.PedidoId IN (SELECT PedidoId FROM @Ped);
DELETE FROM dbo.Facturas WHERE PedidoId IN (SELECT PedidoId FROM @Ped);
DELETE FROM dbo.PedidoDetalle WHERE PedidoId IN (SELECT PedidoId FROM @Ped);
DELETE FROM dbo.Pedidos WHERE PedidoId IN (SELECT PedidoId FROM @Ped);

-- ───────────── Devoluciones ─────────────
DECLARE @Dev TABLE (DevolucionId INT);
DECLARE @Id INT;

INSERT @Dev EXEC dbo.sp_Devoluciones_Create @PedidoId = 4, @ProductoId = 5, @Cantidad = 1,
    @Motivo = N'Botella con el corcho dañado', @ClienteInfo = N'Minisúper La Sabana', @UsuarioId = @Ger, @Nombre = @GerNom;
SELECT @Id = MAX(DevolucionId) FROM @Dev;
UPDATE dbo.Devoluciones SET FechaRegistro = DATEADD(HOUR, 10, CAST(DATEADD(DAY, -12, @Hoy) AS DATETIME2)) WHERE DevolucionId = @Id;
EXEC dbo.sp_Cuarentena_Descartar @DevolucionId = @Id, @UsuarioId = @Ger, @Nombre = @GerNom, @Observacion = N'No apta para la venta';

INSERT @Dev EXEC dbo.sp_Devoluciones_Create @PedidoId = 6, @ProductoId = 2, @Cantidad = 6,
    @Motivo = N'Producto vencido', @ClienteInfo = N'Super Don Beto', @UsuarioId = @Ger, @Nombre = @GerNom;
SELECT @Id = MAX(DevolucionId) FROM @Dev;
UPDATE dbo.Devoluciones SET FechaRegistro = DATEADD(HOUR, 9, CAST(DATEADD(DAY, -9, @Hoy) AS DATETIME2)) WHERE DevolucionId = @Id;
EXEC dbo.sp_Cuarentena_Descartar @DevolucionId = @Id, @UsuarioId = @Ger, @Nombre = @GerNom, @Observacion = N'Se destruye con acta';

INSERT @Dev EXEC dbo.sp_Devoluciones_Create @PedidoId = 7, @ProductoId = 7, @Cantidad = 1,
    @Motivo = N'Error de despacho: el cliente pidió Flor de Caña', @ClienteInfo = N'Bar y Restaurante El Mirador', @UsuarioId = @Ger, @Nombre = @GerNom;
SELECT @Id = MAX(DevolucionId) FROM @Dev;
UPDATE dbo.Devoluciones SET FechaRegistro = DATEADD(HOUR, 15, CAST(DATEADD(DAY, -6, @Hoy) AS DATETIME2)) WHERE DevolucionId = @Id;
EXEC dbo.sp_Cuarentena_Liberar @DevolucionId = @Id, @UsuarioId = @Ger, @Nombre = @GerNom, @Observacion = N'Sellada y en buen estado';

INSERT @Dev EXEC dbo.sp_Devoluciones_Create @PedidoId = 5, @ProductoId = 3, @Cantidad = 2,
    @Motivo = N'Empaque roto en la entrega', @ClienteInfo = N'Licorera Pura Vida', @UsuarioId = @Ger, @Nombre = @GerNom;
SELECT @Id = MAX(DevolucionId) FROM @Dev;
UPDATE dbo.Devoluciones SET FechaRegistro = DATEADD(HOUR, 11, CAST(DATEADD(DAY, -3, @Hoy) AS DATETIME2)) WHERE DevolucionId = @Id;

INSERT @Dev EXEC dbo.sp_Devoluciones_Create @PedidoId = NULL, @ProductoId = 12, @Cantidad = 3,
    @Motivo = N'Cambio de producto', @ClienteInfo = N'Abastecedor Los Lagos', @UsuarioId = @Ger, @Nombre = @GerNom;
SELECT @Id = MAX(DevolucionId) FROM @Dev;
UPDATE dbo.Devoluciones SET FechaRegistro = DATEADD(HOUR, 14, CAST(DATEADD(DAY, -2, @Hoy) AS DATETIME2)) WHERE DevolucionId = @Id;

INSERT @Dev EXEC dbo.sp_Devoluciones_Create @PedidoId = 9, @ProductoId = 21, @Cantidad = 12,
    @Motivo = N'Latas abolladas', @ClienteInfo = N'Soda y Bar La Carreta', @UsuarioId = @Ger, @Nombre = @GerNom;
SELECT @Id = MAX(DevolucionId) FROM @Dev;
UPDATE dbo.Devoluciones SET FechaRegistro = DATEADD(HOUR, 8, CAST(DATEADD(DAY, -1, @Hoy) AS DATETIME2)) WHERE DevolucionId = @Id;

-- ───────────── Activos y comodatos ─────────────
DECLARE @Act TABLE (ActivoId INT);
INSERT @Act EXEC dbo.sp_Activos_Create @CodigoActivo = N'ACT-0101', @Nombre = N'Nevera exhibidora 2 puertas', @Tipo = N'Nevera',
    @Descripcion = N'Marca Imperial, 900 litros', @Estado = N'Disponible', @ClientePrestamo = NULL;
INSERT @Act EXEC dbo.sp_Activos_Create @CodigoActivo = N'ACT-0102', @Nombre = N'Nevera vertical 1 puerta', @Tipo = N'Nevera',
    @Descripcion = N'Marca Coca-Cola, 450 litros', @Estado = N'Disponible', @ClientePrestamo = NULL;
INSERT @Act EXEC dbo.sp_Activos_Create @CodigoActivo = N'ACT-0103', @Nombre = N'Exhibidor de licores 4 niveles', @Tipo = N'Exhibidor',
    @Descripcion = N'Metal negro con logo de la Licorera', @Estado = N'Disponible', @ClientePrestamo = NULL;
INSERT @Act EXEC dbo.sp_Activos_Create @CodigoActivo = N'ACT-0104', @Nombre = N'Congelador horizontal para hielo', @Tipo = N'Nevera',
    @Descripcion = N'300 litros', @Estado = N'Disponible', @ClientePrestamo = NULL;
INSERT @Act EXEC dbo.sp_Activos_Create @CodigoActivo = N'ACT-0105', @Nombre = N'Exhibidor de piso Red Bull', @Tipo = N'Exhibidor',
    @Descripcion = NULL, @Estado = N'Disponible', @ClientePrestamo = NULL;
INSERT @Act EXEC dbo.sp_Activos_Create @CodigoActivo = N'ACT-0106', @Nombre = N'Nevera exhibidora 1 puerta', @Tipo = N'Nevera',
    @Descripcion = N'Compresor en revisión', @Estado = N'Mantenimiento', @ClientePrestamo = NULL;

DECLARE @Com TABLE (ComodatoId INT);
DECLARE @A INT;
SELECT @A = ActivoId FROM dbo.Activos WHERE CodigoActivo = N'ACT-0101';
INSERT @Com EXEC dbo.sp_Comodato_Asignar @ActivoId = @A, @ClienteUsuarioId = 6, @ClienteNombre = N'Pulpería El Tucán',
    @ClienteIdentificacion = NULL, @Ubicacion = N'Barrio Escalante, San José', @FechaAsignacion = '2026-09-15',
    @CondicionEntrega = N'Nueva, sin rayones', @Observaciones = NULL, @UsuarioId = @Ger, @Nombre = @GerNom;
SELECT @A = ActivoId FROM dbo.Activos WHERE CodigoActivo = N'ACT-0102';
INSERT @Com EXEC dbo.sp_Comodato_Asignar @ActivoId = @A, @ClienteUsuarioId = 13, @ClienteNombre = N'Hotel Camino Verde',
    @ClienteIdentificacion = NULL, @Ubicacion = N'Bar de la piscina, La Fortuna', @FechaAsignacion = '2026-08-01',
    @CondicionEntrega = N'Buen estado', @Observaciones = NULL, @UsuarioId = @Ger, @Nombre = @GerNom;
SELECT @A = ActivoId FROM dbo.Activos WHERE CodigoActivo = N'ACT-0103';
INSERT @Com EXEC dbo.sp_Comodato_Asignar @ActivoId = @A, @ClienteUsuarioId = 16, @ClienteNombre = N'La Esquina del Sabor',
    @ClienteIdentificacion = NULL, @Ubicacion = N'Frente al parque, Cartago centro', @FechaAsignacion = '2026-09-10',
    @CondicionEntrega = N'Buen estado', @Observaciones = NULL, @UsuarioId = @Ger, @Nombre = @GerNom;
SELECT @A = ActivoId FROM dbo.Activos WHERE CodigoActivo = N'ACT-0104';
DELETE @Com;
INSERT @Com EXEC dbo.sp_Comodato_Asignar @ActivoId = @A, @ClienteUsuarioId = 15, @ClienteNombre = N'Marisquería Puerto Azul',
    @ClienteIdentificacion = NULL, @Ubicacion = N'Cocina, Puntarenas centro', @FechaAsignacion = '2026-06-01',
    @CondicionEntrega = N'Buen estado', @Observaciones = N'Préstamo por temporada alta', @UsuarioId = @Ger, @Nombre = @GerNom;
SELECT @Id = MAX(ComodatoId) FROM @Com;
EXEC dbo.sp_Comodato_Devolver @ComodatoId = @Id, @Destino = N'Inventario', @CondicionDevolucion = N'Buen estado, limpio',
    @FechaDevolucion = '2026-10-20', @UsuarioId = @Ger, @Nombre = @GerNom;

-- ───────────── Promociones ─────────────
DECLARE @Pro TABLE (PromocionId INT);
INSERT @Pro EXEC dbo.sp_Promociones_Create @Nombre = N'Pilsen de temporada', @Descripcion = N'10 % en Pilsen desde 4 six-packs',
    @Tipo = N'DescuentoPorcentual', @ProductoId = 13, @CantidadMinima = 4, @PorcentajeDescuento = 10,
    @ProductoRegaloId = NULL, @CantidadRegalo = NULL, @SegmentoCliente = N'Todos',
    @FechaInicio = '2026-11-01', @FechaFin = '2026-11-30', @Prioridad = 1, @UsuarioId = @Ger, @NombreUsr = @GerNom, @LimiteUnidades = NULL;
INSERT @Pro EXEC dbo.sp_Promociones_Create @Nombre = N'Hielo gratis para mayoristas', @Descripcion = N'1 bolsa de hielo por cada 10 Imperial Silver',
    @Tipo = N'RegaliaPorVolumen', @ProductoId = 12, @CantidadMinima = 10, @PorcentajeDescuento = NULL,
    @ProductoRegaloId = 23, @CantidadRegalo = 1, @SegmentoCliente = N'Mayorista',
    @FechaInicio = '2026-11-01', @FechaFin = '2026-12-31', @Prioridad = 2, @UsuarioId = @Ger, @NombreUsr = @GerNom, @LimiteUnidades = 200;
INSERT @Pro EXEC dbo.sp_Promociones_Create @Nombre = N'Whisky de octubre', @Descripcion = N'15 % en Black Label',
    @Tipo = N'DescuentoPorcentual', @ProductoId = 4, @CantidadMinima = 1, @PorcentajeDescuento = 15,
    @ProductoRegaloId = NULL, @CantidadRegalo = NULL, @SegmentoCliente = N'Todos',
    @FechaInicio = '2026-10-01', @FechaFin = '2026-10-31', @Prioridad = 0, @UsuarioId = @Ger, @NombreUsr = @GerNom, @LimiteUnidades = NULL;
INSERT @Pro EXEC dbo.sp_Promociones_Create @Nombre = N'Cacique para el turno', @Descripcion = N'8 % en Cacique desde 6 botellas',
    @Tipo = N'DescuentoPorcentual', @ProductoId = 1, @CantidadMinima = 6, @PorcentajeDescuento = 8,
    @ProductoRegaloId = NULL, @CantidadRegalo = NULL, @SegmentoCliente = N'Minorista',
    @FechaInicio = '2026-11-01', @FechaFin = '2026-11-15', @Prioridad = 0, @UsuarioId = @Ger, @NombreUsr = @GerNom, @LimiteUnidades = NULL;
SELECT @Id = PromocionId FROM dbo.Promociones WHERE Nombre = N'Cacique para el turno' AND RegistradoPorUsuarioId = @Ger;
EXEC dbo.sp_Promociones_Inactivar @PromocionId = @Id, @Motivo = N'Se agotó el Cacique 750 ml', @UsuarioId = @Ger, @Nombre = @GerNom;

-- ───────────── Reclamos ─────────────
DECLARE @Rec TABLE (ReclamoId INT);
INSERT @Rec EXEC dbo.sp_Reclamos_Create @UsuarioId = 8, @FacturaId = 4, @PedidoId = NULL,
    @Asunto = N'Botellas con el empaque roto', @Descripcion = N'El cliente recibió dos six-packs de Pilsen con el cartón roto y una botella quebrada.',
    @Categoria = N'Producto', @Prioridad = N'Alta', @AgenteId = @Ger, @AgenteNombre = @GerNom;
INSERT @Rec EXEC dbo.sp_Reclamos_Create @UsuarioId = 10, @FacturaId = 6, @PedidoId = NULL,
    @Asunto = N'La entrega llegó dos horas tarde', @Descripcion = N'El pedido debía llegar antes de las 10 a. m. para el almuerzo y llegó a las 12 m.',
    @Categoria = N'Entrega', @Prioridad = N'Media', @AgenteId = @Ger, @AgenteNombre = @GerNom;
SELECT @Id = MAX(ReclamoId) FROM @Rec;
EXEC dbo.sp_Reclamos_CambiarEstado @ReclamoId = @Id, @Estado = N'EnProceso', @Resolucion = NULL, @UsuarioId = @Ger, @Nombre = @GerNom;
INSERT @Rec EXEC dbo.sp_Reclamos_Create @UsuarioId = 9, @FacturaId = 5, @PedidoId = NULL,
    @Asunto = N'Cobro duplicado en la factura', @Descripcion = N'La factura cobra dos veces la línea de Coca-Cola 2,5 L.',
    @Categoria = N'Facturación', @Prioridad = N'Alta', @AgenteId = @Ger, @AgenteNombre = @GerNom;
SELECT @Id = MAX(ReclamoId) FROM @Rec;
EXEC dbo.sp_Reclamos_CambiarEstado @ReclamoId = @Id, @Estado = N'Cerrado',
    @Resolucion = N'Se emitió una nota de crédito por ₡9 250 y se llamó al cliente para confirmar.', @UsuarioId = @Ger, @Nombre = @GerNom;
UPDATE dbo.Reclamos SET FechaRegistro = DATEADD(DAY, -4, FechaRegistro) WHERE ReclamoId IN (SELECT ReclamoId FROM @Rec);

-- ───────────── Pedidos retenidos (Venta móvil del vendedor, total > ₡50 000) ─────────────
DECLARE @Res TABLE (PedidoId INT, Estado NVARCHAR(30), FacturaId INT, NumeroFactura NVARCHAR(30));
INSERT @Res EXEC dbo.sp_Seller_CreateOrder @ClienteUsuarioId = 14, @VendedorUsuarioId = @Ven, @VendedorNombre = @VenNom,
    @TipoEntrega = N'Entrega a domicilio', @DireccionEntrega = N'Salón de eventos Los Robles, Santa Ana',
    @Observaciones = N'Evento del sábado. Confirmar antes de despachar.',
    @ItemsJson = N'[{"productoId":4,"cantidad":2},{"productoId":18,"cantidad":2}]',
    @PedidoOfflineGuid = '6A1F0C11-0000-4000-8000-000000000301', @CanalPedido = N'Venta móvil';
INSERT @Res EXEC dbo.sp_Seller_CreateOrder @ClienteUsuarioId = 13, @VendedorUsuarioId = @Ven, @VendedorNombre = @VenNom,
    @TipoEntrega = N'Entrega a domicilio', @DireccionEntrega = N'Hotel Camino Verde, La Fortuna',
    @Observaciones = N'Pedido para la semana de ocupación alta.',
    @ItemsJson = N'[{"productoId":12,"cantidad":10}]',
    @PedidoOfflineGuid = '6A1F0C11-0000-4000-8000-000000000302', @CanalPedido = N'Venta móvil';
INSERT @Res EXEC dbo.sp_Seller_CreateOrder @ClienteUsuarioId = 15, @VendedorUsuarioId = @Ven, @VendedorNombre = @VenNom,
    @TipoEntrega = N'Entrega a domicilio', @DireccionEntrega = N'Paseo de los Turistas, Puntarenas',
    @Observaciones = NULL,
    @ItemsJson = N'[{"productoId":11,"cantidad":8}]',
    @PedidoOfflineGuid = '6A1F0C11-0000-4000-8000-000000000303', @CanalPedido = N'Venta móvil';

SELECT * FROM @Res;
