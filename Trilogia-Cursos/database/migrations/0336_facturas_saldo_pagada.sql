SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

/* fin-mejoras M-07: una factura a crédito ya pagada seguía mostrando «Generada».

   Decisión del gerente: NO se toca dbo.Facturas.Estado (lo usan los reportes). «Pagada» es solo una etiqueta de
   pantalla que se calcula con el mismo FIFO de 0230/0326 (los abonos pagan primero los cargos más antiguos):

     - dbo.fn_Cliente_FacturasSaldo(@UsuarioId): una fila por factura NO anulada cuyo cargo a crédito existe, con el
       monto del cargo y lo que aún está pendiente (0 = pagada por completo). Es la misma unión cargo-factura de
       fn_Cliente_FacturasVencidas (por PedidoId o, en cargos antiguos sin pedido, por referencia = número de factura),
       pero sin filtrar por vencimiento ni por saldo.
     - dbo.sp_Facturas_Saldo @UsuarioId = NULL, @FacturaId = NULL: las filas anteriores de un cliente, de una factura o de todas.

   Una factura que no se pagó a crédito (contado) no tiene cargo y no aparece: el sitio la sigue mostrando como «Generada».
   Solo agrega objetos; no cambia tablas ni datos. Rollback en 0336_facturas_saldo_pagada.rollback.md. */
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL THROW 58520,N'Falta el ledger 0001.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0336_facturas_saldo_pagada' AND Status=N'Applied')
    THROW 58521,N'0336 ya figura aplicada.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0326_facturas_vencidas_avisos' AND Status=N'Applied')
    THROW 58522,N'0336 sigue la misma regla FIFO de 0326. Aplicar 0326 primero.',1;

BEGIN TRANSACTION;
GO

CREATE OR ALTER FUNCTION dbo.fn_Cliente_FacturasSaldo (@UsuarioId INT)
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
Pendientes AS
(
    SELECT c.PedidoId, c.Referencia, c.Monto,
           CASE WHEN p.Total >= c.Acumulado THEN 0
                WHEN p.Total <= c.Acumulado - c.Monto THEN c.Monto
                ELSE c.Acumulado - p.Total END AS Pendiente
    FROM Cargos c
    CROSS JOIN Pagos p
)
SELECT f.FacturaId,
       f.NumeroFactura,
       f.PedidoId,
       @UsuarioId AS UsuarioId,
       CAST(p.Monto AS DECIMAL(18,2)) AS MontoCargo,
       CAST(p.Pendiente AS DECIMAL(18,2)) AS Pendiente
FROM Pendientes p
INNER JOIN dbo.Facturas f
        ON f.UsuarioId = @UsuarioId
       AND f.Estado <> N'Anulada'
       AND ((p.PedidoId IS NOT NULL AND f.PedidoId = p.PedidoId)
         OR (p.PedidoId IS NULL AND p.Referencia IS NOT NULL AND p.Referencia = f.NumeroFactura));
GO

CREATE OR ALTER PROCEDURE dbo.sp_Facturas_Saldo
    @UsuarioId INT = NULL,
    @FacturaId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    -- Con @FacturaId solo se revisa el cliente de esa factura.
    IF @UsuarioId IS NULL AND @FacturaId IS NOT NULL
        SELECT @UsuarioId = f.UsuarioId FROM dbo.Facturas f WHERE f.FacturaId = @FacturaId;

    SELECT s.FacturaId, s.NumeroFactura, s.PedidoId, s.UsuarioId, s.MontoCargo, s.Pendiente
    FROM dbo.Usuarios u
    CROSS APPLY dbo.fn_Cliente_FacturasSaldo(u.UsuarioId) s
    WHERE (@UsuarioId IS NULL OR u.UsuarioId = @UsuarioId)
      AND (@FacturaId IS NULL OR s.FacturaId = @FacturaId)
      AND EXISTS (SELECT 1 FROM dbo.ClienteCreditoMovimientos m WHERE m.UsuarioId = u.UsuarioId)
    ORDER BY s.FacturaId;
END;
GO

IF XACT_STATE()<>1 THROW 58523,N'La transacción 0336 no está disponible.',1;
DECLARE @MigrationSha256 NVARCHAR(128)=N'$(MigrationSha256)';
IF LEN(@MigrationSha256)<>64 OR @MigrationSha256 LIKE N'%[^0-9A-Fa-f]%' THROW 58524,N'SHA-256 inválido para 0336.',1;
INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes)
VALUES(N'0336_facturas_saldo_pagada',N'0336_facturas_saldo_pagada.sql',UPPER(@MigrationSha256),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),
       N'fin-mejoras M-07: fn_Cliente_FacturasSaldo y sp_Facturas_Saldo (saldo por factura a credito con el FIFO de 0230/0326) para mostrar «Pagada» solo en pantalla; Facturas.Estado no cambia. Aditiva.');
COMMIT TRANSACTION;
GO
