SET NOCOUNT ON;
IF OBJECT_ID(N'dbo.sp_Payroll_ListarPeriodosAbiertos',N'P') IS NULL OR OBJECT_ID(N'dbo.sp_Payroll_ListarEmpleadosActivos',N'P') IS NULL OR OBJECT_ID(N'dbo.sp_Payroll_ListarReglas',N'P') IS NULL THROW 58675,N'Faltan los procedimientos de lista de planilla.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0267_planilla_aprobacion_gerente_y_listas' AND Status=N'Applied') THROW 58676,N'La migracion 0267 no esta registrada.',1;
IF (SELECT COUNT(*) FROM dbo.PerfilPermisos pp JOIN dbo.Perfiles pf ON pf.PerfilId=pp.PerfilId JOIN dbo.Permisos pm ON pm.PermisoId=pp.PermisoId
    WHERE pf.Nombre=N'Gerente' AND pm.Codigo IN(N'PLANILLA_VER',N'PLANILLA_APROBAR'))<>2 THROW 58677,N'El Gerente no tiene PLANILLA_VER y PLANILLA_APROBAR.',1;
-- El Gerente aprueba pero no calcula ni paga (segregacion de funciones).
IF EXISTS(SELECT 1 FROM dbo.PerfilPermisos pp JOIN dbo.Perfiles pf ON pf.PerfilId=pp.PerfilId JOIN dbo.Permisos pm ON pm.PermisoId=pp.PermisoId
    WHERE pf.Nombre=N'Gerente' AND pm.Codigo IN(N'PLANILLA_CALCULAR',N'PLANILLA_PAGAR',N'PLANILLA_CONFIGURAR',N'PLANILLA_GESTIONAR')) THROW 58678,N'El Gerente acumula permisos de calcular o pagar.',1;
EXEC dbo.sp_Payroll_ListarPeriodosAbiertos;
EXEC dbo.sp_Payroll_ListarEmpleadosActivos;
EXEC dbo.sp_Payroll_ListarReglas;
PRINT N'Verificacion 0267 completada.';
