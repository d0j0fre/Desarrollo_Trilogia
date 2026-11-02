SET NOCOUNT ON;
IF OBJECT_ID(N'dbo.EmpleadoSalarioVigencias',N'U') IS NULL THROW 56643,N'Falta el historial de vigencia salarial.',1;
IF EXISTS(SELECT 1 FROM dbo.Empleados e WHERE NOT EXISTS(SELECT 1 FROM dbo.EmpleadoSalarioVigencias v WHERE v.EmpleadoId=e.EmpleadoId)) THROW 56644,N'Hay empleados sin salario inicial.',1;
PRINT N'Verificación 0263 completada.';
