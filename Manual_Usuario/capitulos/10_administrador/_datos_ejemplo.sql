-- Datos de ejemplo de la Parte II (Administrador). Solo para la base local de capturas.
-- Idempotente: borra lo que creó antes y lo vuelve a crear. Marcas invisibles: presupuestos con
-- FechaCreacionUtc = 2026-01-05, gastos con Comprobante = 'datos-ejemplo-manual', garantías por fecha de solicitud.
-- Ejecutar: sqlcmd -S <pipe de Trilogia_manualadmin> -d DistribuidoraJJ_DB -E -I -b -i _datos_ejemplo.sql
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;

DECLARE @admin int = (SELECT UsuarioId FROM dbo.Usuarios WHERE Correo = N'admin@labodega.cr');
DECLARE @gerente int = (SELECT UsuarioId FROM dbo.Usuarios WHERE Correo = N'gerente@labodega.cr');

-- ── Presupuestos 2026 (los módulos Presupuestos y Gastos no abren en Windows en español: se cargan aquí) ──
UPDATE dbo.PresupuestosAnuales SET FechaCreacionUtc = '20260105' WHERE Notas LIKE N'%[[]manual]%';
DELETE d FROM dbo.PresupuestoDetalles d JOIN dbo.PresupuestosAnuales p ON p.PresupuestoId = d.PresupuestoId WHERE p.FechaCreacionUtc = '20260105';
DELETE a FROM dbo.PresupuestoAuditoria a JOIN dbo.PresupuestosAnuales p ON p.PresupuestoId = a.PresupuestoId WHERE p.FechaCreacionUtc = '20260105';
DELETE FROM dbo.PresupuestosAnuales WHERE FechaCreacionUtc = '20260105';

DECLARE @p TABLE (Dep int, Monto decimal(18,2), Estado nvarchar(20), Nota nvarchar(200));
INSERT @p VALUES
 (4, 7200000, N'Aprobado',   N'Flota, bodega y entregas'),
 (1, 3600000, N'Aprobado',   N'Oficina central'),
 (3, 1800000, N'Presentado', N'Suministros de compras');

INSERT dbo.PresupuestosAnuales (Anio, DepartamentoId, Moneda, MontoAnual, Estado, Notas, Activo, CreadoPorUsuarioId, CreadoPorNombre, AprobadoPorUsuarioId, AprobadoPorNombre, FechaAprobacionUtc, FechaCreacionUtc)
SELECT 2026, Dep, 'CRC', Monto, Estado, Nota, 1, @admin, N'Admin Demo',
       CASE WHEN Estado = N'Aprobado' THEN @gerente END, CASE WHEN Estado = N'Aprobado' THEN N'Gerente Demo' END,
       CASE WHEN Estado = N'Aprobado' THEN '20260115' END, '20260105'
FROM @p;

-- Reparto mensual por categoría (porcentaje del monto anual / 12)
DECLARE @reparto TABLE (Dep int, Cat int, Pct decimal(5,4));
INSERT @reparto VALUES (4,3,0.45),(4,4,0.30),(4,5,0.15),(4,6,0.10),
                       (1,1,0.50),(1,2,0.30),(1,5,0.20),
                       (3,5,0.70),(3,6,0.30);
;WITH meses AS (SELECT TOP 12 ROW_NUMBER() OVER (ORDER BY (SELECT 1)) m FROM sys.objects)
INSERT dbo.PresupuestoDetalles (PresupuestoId, CategoriaId, Mes, MontoAsignado)
SELECT pa.PresupuestoId, r.Cat, meses.m, ROUND(pa.MontoAnual * r.Pct / 12, 2)
FROM dbo.PresupuestosAnuales pa JOIN @reparto r ON r.Dep = pa.DepartamentoId CROSS JOIN meses
WHERE pa.FechaCreacionUtc = '20260105' AND pa.Anio = 2026;

-- ── Gastos operativos 2026 ──
DELETE FROM dbo.GastosOperativos WHERE Comprobante = N'datos-ejemplo-manual' OR Descripcion LIKE N'%[[]manual]%';
DECLARE @g TABLE (Fecha date, Dep int, Cat int, Prov nvarchar(120), Doc nvarchar(40), Descr nvarchar(200), Sub decimal(18,2), Estado nvarchar(20), Metodo nvarchar(30));
INSERT @g VALUES
 ('2026-01-20',4,3,N'Estación de Servicio La Uruca',N'FE-1001',N'Combustible camiones de reparto, enero',210000,N'Pagado',N'Transferencia'),
 ('2026-02-18',4,3,N'Estación de Servicio La Uruca',N'FE-1044',N'Combustible camiones de reparto, febrero',225000,N'Pagado',N'Transferencia'),
 ('2026-03-19',4,4,N'Taller Mecánico El Pistón',N'FE-2210',N'Cambio de frenos camión C-02',185000,N'Pagado',N'Transferencia'),
 ('2026-04-21',4,3,N'Estación de Servicio La Uruca',N'FE-1120',N'Combustible camiones de reparto, abril',240000,N'Pagado',N'Transferencia'),
 ('2026-05-05',1,1,N'Inmobiliaria Paseo Colón',N'FE-0505',N'Alquiler de oficina, mayo',150000,N'Pagado',N'Transferencia'),
 ('2026-06-05',1,1,N'Inmobiliaria Paseo Colón',N'FE-0606',N'Alquiler de oficina, junio',150000,N'Pagado',N'Transferencia'),
 ('2026-07-14',1,2,N'Compañía de Fuerza y Luz',N'REC-7781',N'Electricidad de bodega y oficina, julio',96000,N'Pagado',N'Débito automático'),
 ('2026-08-22',4,4,N'Taller Mecánico El Pistón',N'FE-2390',N'Mantenimiento preventivo flota',260000,N'Pagado',N'Transferencia'),
 ('2026-09-17',4,3,N'Estación de Servicio La Uruca',N'FE-1301',N'Combustible camiones de reparto, septiembre',255000,N'Aprobado',N'Transferencia'),
 ('2026-10-06',1,5,N'Librería y Suministros Central',N'FE-8812',N'Papel, tóner y artículos de oficina',58000,N'Aprobado',N'Tarjeta'),
 ('2026-10-20',4,3,N'Estación de Servicio La Uruca',N'FE-1355',N'Combustible camiones de reparto, octubre',248000,N'Aprobado',N'Transferencia'),
 ('2026-10-28',4,5,N'Empaques del Valle',N'FE-3307',N'Cajas y cinta para despacho',72000,N'Registrado',N'Transferencia');

INSERT dbo.GastosOperativos (FechaGasto, Fecha, Monto, Concepto, DepartamentoId, CategoriaId, Proveedor, NumeroDocumento, TipoDocumento, Descripcion,
  Subtotal, Impuesto, Total, MetodoPago, Moneda, Estado, TokenOperacion, RegistradoPorUsuarioId, RegistradoPorNombre, FechaRegistro,
  CreadoPorUsuarioId, CreadoPorNombre, ActualizadoPorUsuarioId, ActualizadoPorNombre, Comprobante)
SELECT Fecha, Fecha, ROUND(Sub * 1.13, 2), Descr, Dep, Cat, Prov, Doc, N'Factura', Descr,
  Sub, ROUND(Sub * 0.13, 2), ROUND(Sub + ROUND(Sub * 0.13, 2), 2), Metodo, 'CRC', Estado, NEWID(), @admin, N'Admin Demo', Fecha,
  @admin, N'Admin Demo', @admin, N'Admin Demo', N'datos-ejemplo-manual'
FROM @g;

-- ── Ventas cobradas (el reporte de Ingresos y egresos cuenta los pedidos con pago confirmado) ──
UPDATE dbo.Pedidos
   SET EstadoPago = N'Confirmado simulado', MetodoPago = N'Tarjeta', FechaPago = FechaPedido,
       ReferenciaPago = CONCAT(N'TRX-', 40000 + PedidoId)
 WHERE PedidoId IN (2, 3, 4, 8, 10, 33, 34, 35, 36);

-- ── Solicitudes de garantía de clientes ──
DELETE FROM dbo.Garantias WHERE Descripcion LIKE N'%[[]manual]%' OR FechaSolicitud IN ('20261028 09:15', '20261021 14:40', '20261018 11:05');
INSERT dbo.Garantias (PedidoDetalleId, UsuarioId, FechaSolicitud, Motivo, Descripcion, Telefono, Estado)
VALUES
 (20, 13, '20261028 09:15', N'Producto dañado', N'Dos botellas llegaron con el corcho filtrando.', N'8530-5522', N'Pendiente'),
 (8,  7,  '20261021 14:40', N'Empaque defectuoso', N'Un six pack venía con el cartón roto y una lata golpeada.', N'8312-4502', N'En revisión'),
 (5,  6,  '20261018 11:05', N'Producto vencido', N'El cliente reporta fecha de vencimiento cercana al recibir.', N'8881-1101', N'Pendiente');

COMMIT;
PRINT 'Datos de ejemplo de la Parte II cargados.';
