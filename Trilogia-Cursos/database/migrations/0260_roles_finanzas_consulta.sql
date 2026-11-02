SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL THROW 56600,N'Falta el registro de migraciones.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0260_roles_finanzas_consulta' AND Status=N'Applied') THROW 56600,N'La migración 0260 ya fue aplicada.',1;
IF (SELECT COUNT(DISTINCT Codigo) FROM dbo.Permisos WHERE Codigo IN(N'PRESUPUESTOS_VER',N'PRESUPUESTOS_APROBAR',N'PRESUPUESTOS_COMPARAR',N'GASTOS_VER') AND Activo=1) <> 4
 THROW 56603,N'Faltan permisos activos de presupuestos o gastos; revisar las migraciones previas.',1;
BEGIN TRANSACTION;
GO
CREATE OR ALTER TRIGGER dbo.tr_Perfiles_ImpedirInactivarConUsuarios ON dbo.Perfiles
AFTER UPDATE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS (SELECT 1 FROM inserted i JOIN deleted d ON d.PerfilId=i.PerfilId
            JOIN dbo.Usuarios u ON u.PerfilId=i.PerfilId
            WHERE d.Activo=1 AND i.Activo=0)
    THROW 56601,N'No se puede inactivar el rol porque tiene usuarios asignados.',1;
END;
GO
IF NOT EXISTS (SELECT 1 FROM dbo.Permisos WHERE Codigo=N'FINANZAS_INGRESOS_EGRESOS_VER')
 INSERT dbo.Permisos(Codigo,Modulo,Nombre,Descripcion,Activo)
 VALUES(N'FINANZAS_INGRESOS_EGRESOS_VER',N'Finanzas',N'Ver ingresos y egresos',N'Consultar ingresos, egresos y balance.',1);
IF NOT EXISTS (SELECT 1 FROM dbo.Permisos WHERE Codigo=N'COMODATOS_CONSULTAR')
 INSERT dbo.Permisos(Codigo,Modulo,Nombre,Descripcion,Activo)
 VALUES(N'COMODATOS_CONSULTAR',N'Activos',N'Consultar comodatos',N'Consultar equipos prestados y rentabilidad.',1);
-- Conservar el acceso previo de roles que ya tenían permisos activos del módulo Finanzas.
INSERT dbo.PerfilPermisos(PerfilId,PermisoId,UsuarioAsignacionId,UsuarioAsignacionNombre)
SELECT DISTINCT pp.PerfilId, nuevo.PermisoId, NULL, N'Migración 0260'
FROM dbo.PerfilPermisos pp JOIN dbo.Permisos previo ON previo.PermisoId=pp.PermisoId
CROSS JOIN dbo.Permisos nuevo
WHERE previo.Modulo=N'Finanzas' AND previo.Activo=1 AND previo.Codigo<>N'FINANZAS_INGRESOS_EGRESOS_VER'
 AND nuevo.Codigo=N'FINANZAS_INGRESOS_EGRESOS_VER'
 AND NOT EXISTS (SELECT 1 FROM dbo.PerfilPermisos x WHERE x.PerfilId=pp.PerfilId AND x.PermisoId=nuevo.PermisoId);
INSERT dbo.PerfilPermisos(PerfilId,PermisoId,UsuarioAsignacionId,UsuarioAsignacionNombre)
SELECT p.PerfilId, pe.PermisoId, NULL, N'Migración 0260'
FROM dbo.Perfiles p CROSS JOIN dbo.Permisos pe
WHERE (p.Nombre=N'Administrador'
 OR (p.Nombre=N'Gerente' AND pe.Codigo IN(N'FINANZAS_INGRESOS_EGRESOS_VER',N'PRESUPUESTOS_VER',N'PRESUPUESTOS_APROBAR',N'PRESUPUESTOS_COMPARAR',N'GASTOS_VER',N'COMODATOS_CONSULTAR'))
 OR (p.Nombre IN(N'Auditor',N'Auditor Interno') AND pe.Codigo=N'COMODATOS_CONSULTAR'))
 AND NOT EXISTS (SELECT 1 FROM dbo.PerfilPermisos pp WHERE pp.PerfilId=p.PerfilId AND pp.PermisoId=pe.PermisoId);
DECLARE @sha NVARCHAR(128)=N'$(MigrationSha256)';
IF LEN(@sha)<>64 OR @sha LIKE N'%[^0-9A-Fa-f]%' THROW 56602,N'SHA-256 inválido.',1;
INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes)
VALUES(N'0260_roles_finanzas_consulta',N'0260_roles_finanzas_consulta.sql',UPPER(@sha),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),N'Bloqueo de roles asignados y permisos de consulta.');
COMMIT TRANSACTION;
GO
