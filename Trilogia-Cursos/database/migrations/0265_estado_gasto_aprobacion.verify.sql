SET NOCOUNT ON;
IF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.GastosOperativos') AND name=N'CK_GastosOperativos_Estado' AND definition LIKE N'%Requiere Aprobación%') THROW 56663,N'Falta el estado de aprobación.',1;
IF EXISTS(SELECT 1 FROM dbo.GastosOperativos g LEFT JOIN dbo.vw_OperatingExpenseImpact v ON v.GastoId=g.GastoId WHERE g.Estado=N'Requiere Aprobación' AND v.GastoId IS NULL) THROW 56664,N'Hay gastos pendientes sin impacto presupuestario.',1;
PRINT N'Verificación 0265 completada.';
