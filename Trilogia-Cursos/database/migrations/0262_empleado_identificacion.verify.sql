SET NOCOUNT ON;
IF COL_LENGTH(N'dbo.Empleados',N'Identificacion') IS NULL THROW 56633,N'Falta la identificación del empleado.',1;
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.Empleados') AND name=N'UX_Empleados_Identificacion' AND is_unique=1 AND is_disabled=0) THROW 56634,N'Falta la protección de identificación única.',1;
PRINT N'Verificación 0262 completada.';
