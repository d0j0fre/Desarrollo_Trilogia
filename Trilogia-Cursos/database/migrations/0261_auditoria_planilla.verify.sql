SET NOCOUNT ON;
IF OBJECT_ID(N'dbo.tr_PlanillaAuditoria_AuditoriaSistema',N'TR') IS NULL THROW 56623,N'Falta el registro de auditoría general de planilla.',1;
PRINT N'Verificación 0261 completada.';
