SET NOCOUNT ON;
IF OBJECT_ID(N'dbo.tr_Perfiles_ImpedirInactivarConUsuarios',N'TR') IS NULL THROW 56610,N'Falta la protección de roles.',1;
IF EXISTS (SELECT 1 FROM dbo.Perfiles p CROSS JOIN dbo.Permisos pe
 WHERE p.Nombre=N'Administrador' AND NOT EXISTS (SELECT 1 FROM dbo.PerfilPermisos pp WHERE pp.PerfilId=p.PerfilId AND pp.PermisoId=pe.PermisoId))
 THROW 56611,N'El administrador no tiene todos los permisos.',1;
IF EXISTS (SELECT 1 FROM dbo.Perfiles p CROSS JOIN dbo.Permisos pe
 WHERE ((p.Nombre=N'Gerente' AND pe.Codigo IN(N'FINANZAS_INGRESOS_EGRESOS_VER',N'PRESUPUESTOS_VER',N'PRESUPUESTOS_APROBAR',N'PRESUPUESTOS_COMPARAR',N'GASTOS_VER',N'COMODATOS_CONSULTAR')) OR (p.Nombre IN(N'Auditor',N'Auditor Interno') AND pe.Codigo=N'COMODATOS_CONSULTAR'))
 AND NOT EXISTS (SELECT 1 FROM dbo.PerfilPermisos pp WHERE pp.PerfilId=p.PerfilId AND pp.PermisoId=pe.PermisoId))
 THROW 56612,N'Faltan permisos de consulta.',1;
PRINT N'Verificación 0260 completada.';
