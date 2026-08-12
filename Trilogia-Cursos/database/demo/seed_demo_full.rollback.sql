/* Compensating rollback: only removes records bearing the DEMO-2026-08 marker. */
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @BatchCode NVARCHAR(30) = N'DEMO-2026-08';
IF DB_NAME() <> N'DistribuidoraJJ_DB_DEV'
    THROW 56020, N'El rollback solo puede ejecutarse en DistribuidoraJJ_DB_DEV.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    DELETE auditLog
    FROM dbo.PresupuestoAuditoria auditLog
    INNER JOIN dbo.PresupuestosAnuales budget ON budget.PresupuestoId = auditLog.PresupuestoId
    WHERE budget.Notas = N'DEMO-2026-08: presupuesto QA aditivo.';

    DELETE detailLine
    FROM dbo.PresupuestoDetalles detailLine
    INNER JOIN dbo.PresupuestosAnuales budget ON budget.PresupuestoId = detailLine.PresupuestoId
    WHERE budget.Notas = N'DEMO-2026-08: presupuesto QA aditivo.';

    DELETE FROM dbo.PresupuestosAnuales WHERE Notas = N'DEMO-2026-08: presupuesto QA aditivo.';
    DELETE FROM dbo.CategoriasGasto WHERE Codigo IN (N'DEMO-2026-08-OPER', N'DEMO-2026-08-COMER', N'DEMO-2026-08-LOG');
    DELETE FROM dbo.DemoSeedBatches WHERE BatchCode = @BatchCode;

    COMMIT;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK;
    THROW;
END CATCH;
