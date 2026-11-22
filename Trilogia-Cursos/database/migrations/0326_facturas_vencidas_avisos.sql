SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

/* POST-qa2-03 · CU-093-04: una factura a crédito que pasa su fecha de vencimiento no se marca como vencida
   ni genera ninguna alerta.

   Decisión del gerente: NO se toca dbo.Facturas.Estado. La condición de «vencida» se calcula con el mismo
   FIFO de 0230 (los abonos pagan primero los cargos más antiguos y cada cargo vence PlazoCreditoDias después):

     - dbo.fn_Cliente_FacturasVencidas(@UsuarioId): una fila por factura cuyo cargo a crédito aún tiene saldo
       y ya venció (monto del cargo, pendiente, vencimiento y días de atraso). La factura se une con el cargo
       por PedidoId o, en los cargos antiguos sin pedido, por la referencia = número de factura.
     - dbo.sp_Facturas_Vencidas(@UsuarioId = NULL): las facturas vencidas de un cliente o de todos.
     - dbo.sp_FacturasVencidas_GenerarAvisos: deja un aviso FacturaVencida en la bandeja
       (dbo.NotificacionesUsuario, 0221) de los usuarios activos Administrador, Gerente y Crédito y Cobro,
       UNA VEZ por cliente y día (idempotente). Lo llama un servicio diario del sitio, que también corre al
       iniciar. Devuelve cuántos avisos nuevos creó.

   Solo agrega objetos; no cambia tablas ni datos existentes. Rollback en 0326_facturas_vencidas_avisos.rollback.md. */
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL THROW 58490,N'Falta el ledger 0001.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0326_facturas_vencidas_avisos' AND Status=N'Applied')
    THROW 58491,N'0326 ya figura aplicada.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0230_cliente_estado_deuda' AND Status=N'Applied')
    THROW 58492,N'0326 usa fn_Cliente_EstadoDeuda y el plazo de crédito de la 0230. Aplicar 0230 primero.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0221_notificaciones_usuario' AND Status=N'Applied')
    THROW 58493,N'0326 deja los avisos en dbo.NotificacionesUsuario, que crea la 0221. Aplicar 0221 primero.',1;

BEGIN TRANSACTION;
GO

CREATE OR ALTER FUNCTION dbo.fn_Cliente_FacturasVencidas (@UsuarioId INT)
RETURNS TABLE
AS
RETURN
WITH Anulados AS
(
    -- Pedidos a crédito ya reversados: su cargo y su reverso no entran al FIFO (igual que fn_Cliente_EstadoDeuda).
    SELECT DISTINCT m.PedidoId
    FROM dbo.ClienteCreditoMovimientos m
    WHERE m.UsuarioId = @UsuarioId
      AND m.PedidoId IS NOT NULL
      AND m.TipoMovimiento = N'AjusteNegativo'
),
Movimientos AS
(
    SELECT m.CreditoMovimientoId, m.TipoMovimiento, m.Monto, m.FechaMovimiento, m.PedidoId, m.Referencia
    FROM dbo.ClienteCreditoMovimientos m
    WHERE m.UsuarioId = @UsuarioId
      AND (m.PedidoId IS NULL OR m.PedidoId NOT IN (SELECT a.PedidoId FROM Anulados a))
),
Cargos AS
(
    SELECT c.CreditoMovimientoId, c.Monto, c.FechaMovimiento, c.PedidoId, c.Referencia,
           SUM(c.Monto) OVER (ORDER BY c.FechaMovimiento, c.CreditoMovimientoId
                              ROWS UNBOUNDED PRECEDING) AS Acumulado
    FROM Movimientos c
    WHERE c.TipoMovimiento IN (N'Cargo', N'AjustePositivo')
),
Pagos AS
(
    SELECT ISNULL(SUM(p.Monto), 0) AS Total
    FROM Movimientos p
    WHERE p.TipoMovimiento IN (N'Abono', N'AjusteNegativo')
),
Configuracion AS
(
    SELECT ISNULL(cc.PlazoCreditoDias, 30) AS PlazoCreditoDias
    FROM dbo.Usuarios u
    LEFT JOIN dbo.ClienteCreditos cc ON cc.UsuarioId = u.UsuarioId
    WHERE u.UsuarioId = @UsuarioId
),
Pendientes AS
(
    SELECT c.PedidoId, c.Referencia, c.Monto,
           DATEADD(DAY, cfg.PlazoCreditoDias, c.FechaMovimiento) AS Vencimiento,
           CASE WHEN p.Total >= c.Acumulado THEN 0
                WHEN p.Total <= c.Acumulado - c.Monto THEN c.Monto
                ELSE c.Acumulado - p.Total END AS Pendiente
    FROM Cargos c
    CROSS JOIN Pagos p
    CROSS JOIN Configuracion cfg
)
SELECT f.FacturaId,
       f.NumeroFactura,
       f.PedidoId,
       @UsuarioId AS UsuarioId,
       CAST(p.Monto AS DECIMAL(18,2)) AS MontoCargo,
       CAST(p.Pendiente AS DECIMAL(18,2)) AS Pendiente,
       p.Vencimiento,
       DATEDIFF(DAY, p.Vencimiento, SYSDATETIME()) AS DiasAtraso
FROM Pendientes p
INNER JOIN dbo.Facturas f
        ON f.UsuarioId = @UsuarioId
       AND f.Estado <> N'Anulada'
       AND ((p.PedidoId IS NOT NULL AND f.PedidoId = p.PedidoId)
         OR (p.PedidoId IS NULL AND p.Referencia IS NOT NULL AND p.Referencia = f.NumeroFactura))
WHERE p.Pendiente > 0
  AND p.Vencimiento <= SYSDATETIME();
GO

CREATE OR ALTER PROCEDURE dbo.sp_Facturas_Vencidas
    @UsuarioId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT v.FacturaId, v.NumeroFactura, v.PedidoId, v.UsuarioId, v.MontoCargo, v.Pendiente, v.Vencimiento, v.DiasAtraso
    FROM dbo.Usuarios u
    CROSS APPLY dbo.fn_Cliente_FacturasVencidas(u.UsuarioId) v
    WHERE (@UsuarioId IS NULL OR u.UsuarioId = @UsuarioId)
      AND EXISTS (SELECT 1 FROM dbo.ClienteCreditoMovimientos m WHERE m.UsuarioId = u.UsuarioId)
    ORDER BY v.Vencimiento, v.FacturaId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_FacturasVencidas_GenerarAvisos
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DECLARE @Hoy DATE = CAST(SYSDATETIME() AS DATE);

    INSERT dbo.NotificacionesUsuario (UsuarioId, Tipo, Titulo, Mensaje, EntidadTipo, EntidadId)
    SELECT r.UsuarioId,
           N'FacturaVencida',
           N'Factura a crédito vencida',
           LEFT(CONCAT(N'El cliente ', c.NombreCompleto, N' tiene ',
                       CASE WHEN c.Facturas > 0 THEN CONCAT(c.Facturas, N' factura(s) a crédito vencida(s)') ELSE N'una deuda a crédito vencida' END,
                       N' por ₡', FORMAT(c.DeudaVencida, N'N2', N'en-US'),
                       N' (', c.DiasAtraso, N' día(s) de atraso',
                       CASE WHEN c.PrimeraFactura IS NOT NULL THEN CONCAT(N'; la más antigua: ', c.PrimeraFactura) ELSE N'' END,
                       N'). Cobros: dar seguimiento. Ver Cuentas por cobrar.'), 400),
           N'Cliente',
           c.UsuarioId
    FROM
    (
        SELECT u.UsuarioId, u.NombreCompleto, d.DeudaVencida, d.DiasAtraso,
               (SELECT COUNT(*) FROM dbo.fn_Cliente_FacturasVencidas(u.UsuarioId)) AS Facturas,
               (SELECT TOP (1) v.NumeroFactura FROM dbo.fn_Cliente_FacturasVencidas(u.UsuarioId) v ORDER BY v.Vencimiento, v.FacturaId) AS PrimeraFactura
        FROM dbo.Usuarios u
        CROSS APPLY dbo.fn_Cliente_EstadoDeuda(u.UsuarioId) d
        WHERE d.TieneDeudaVencida = 1
          AND EXISTS (SELECT 1 FROM dbo.ClienteCreditoMovimientos m WHERE m.UsuarioId = u.UsuarioId)
    ) c
    CROSS JOIN
    (
        SELECT u.UsuarioId
        FROM dbo.Usuarios u
        INNER JOIN dbo.Perfiles p ON p.PerfilId = u.PerfilId
        WHERE ISNULL(u.Activo, 1) = 1
          AND p.Nombre IN (N'Administrador', N'Gerente', N'Crédito y Cobro')
    ) r
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM dbo.NotificacionesUsuario n
        WHERE n.Tipo = N'FacturaVencida'
          AND n.EntidadTipo = N'Cliente'
          AND n.EntidadId = c.UsuarioId
          AND n.UsuarioId = r.UsuarioId
          AND CAST(n.FechaCreacion AS DATE) = @Hoy
    );

    SELECT @@ROWCOUNT AS Nuevos;
END;
GO

IF XACT_STATE()<>1 THROW 58494,N'La transacción 0326 no está disponible.',1;
DECLARE @MigrationSha256 NVARCHAR(128)=N'$(MigrationSha256)';
IF LEN(@MigrationSha256)<>64 OR @MigrationSha256 LIKE N'%[^0-9A-Fa-f]%' THROW 58495,N'SHA-256 inválido para 0326.',1;
INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes)
VALUES(N'0326_facturas_vencidas_avisos',N'0326_facturas_vencidas_avisos.sql',UPPER(@MigrationSha256),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),
       N'POST-qa2-03 (CU-093-04): fn_Cliente_FacturasVencidas, sp_Facturas_Vencidas y sp_FacturasVencidas_GenerarAvisos (aviso FacturaVencida una vez por cliente y día). No cambia Facturas.Estado.');
COMMIT TRANSACTION;
GO
