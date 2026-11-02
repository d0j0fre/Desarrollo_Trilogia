SET NOCOUNT ON;
IF OBJECT_ID(N'dbo.vw_OperatingExpenseImpact',N'V') IS NULL THROW 56653,N'Falta la vista de gastos.',1;
IF EXISTS(SELECT 1 FROM dbo.vw_OperatingExpenseImpact WHERE MontoPresupuesto=0 AND GastoReal+GastoComprometido>0 AND NivelConsumo<>N'Excedido') THROW 56654,N'Hay gastos sin presupuesto clasificados como normales.',1;
PRINT N'Verificación 0264 completada.';
