SET NOCOUNT ON;
DECLARE @BatchCode NVARCHAR(30) = N'DEMO-2026-08';

IF DB_NAME() <> N'DistribuidoraJJ_DB_DEV'
    THROW 56010, N'La verificación solo puede ejecutarse en DistribuidoraJJ_DB_DEV.', 1;

IF NOT EXISTS (SELECT 1 FROM dbo.DemoSeedBatches WHERE BatchCode = @BatchCode)
    THROW 56011, N'No se encontró el lote DEMO-2026-08.', 1;

SELECT BatchCode, AppliedAtUtc, Notes
FROM dbo.DemoSeedBatches
WHERE BatchCode = @BatchCode;

SELECT
    PresupuestosDemo = COUNT(*),
    DetallesDemo = COALESCE(SUM(detailCount.Total), 0),
    MontoDemo = COALESCE(SUM(budget.MontoAnual), 0)
FROM dbo.PresupuestosAnuales budget
OUTER APPLY (SELECT COUNT(*) AS Total FROM dbo.PresupuestoDetalles detailLine WHERE detailLine.PresupuestoId = budget.PresupuestoId) detailCount
WHERE budget.Notas = N'DEMO-2026-08: presupuesto QA aditivo.';

IF EXISTS
(
    SELECT 1
    FROM dbo.PresupuestosAnuales budget
    OUTER APPLY (SELECT SUM(MontoAsignado) AS Allocated FROM dbo.PresupuestoDetalles WHERE PresupuestoId = budget.PresupuestoId) detailLine
    WHERE budget.Notas = N'DEMO-2026-08: presupuesto QA aditivo.'
      AND detailLine.Allocated <> budget.MontoAnual
)
    THROW 56012, N'El detalle presupuestario demo no coincide con su encabezado.', 1;
