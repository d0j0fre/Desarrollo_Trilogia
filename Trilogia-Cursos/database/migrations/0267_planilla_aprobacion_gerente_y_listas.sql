SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL OR OBJECT_ID(N'dbo.PlanillaPeriodos',N'U') IS NULL OR OBJECT_ID(N'dbo.PlanillaReglas',N'U') IS NULL THROW 58670,N'Faltan dependencias de planilla (0019).',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0267_planilla_aprobacion_gerente_y_listas' AND Status=N'Applied') THROW 58671,N'La migracion ya fue aplicada.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.Perfiles WHERE Nombre=N'Gerente') THROW 58672,N'No existe el perfil Gerente.',1;
IF (SELECT COUNT(*) FROM dbo.Permisos WHERE Codigo IN(N'PLANILLA_VER',N'PLANILLA_APROBAR') AND Activo=1)<>2 THROW 58673,N'Faltan los permisos PLANILLA_VER y PLANILLA_APROBAR (0019).',1;
BEGIN TRANSACTION;
GO
-- G1-06: la segregacion de funciones se mantiene (calcula uno, aprueba otro, paga un tercero).
-- El perfil Gerente recibe PLANILLA_VER y PLANILLA_APROBAR; el Administrador conserva calcular, gestionar, configurar y pagar.
INSERT dbo.PerfilPermisos(PerfilId,PermisoId,UsuarioAsignacionId,UsuarioAsignacionNombre)
SELECT g.PerfilId,p.PermisoId,NULL,N'Migracion 0267 planilla gerente'
FROM dbo.Perfiles g
JOIN dbo.Permisos p ON p.Codigo IN(N'PLANILLA_VER',N'PLANILLA_APROBAR') AND p.Activo=1
WHERE g.Nombre=N'Gerente'
  AND NOT EXISTS(SELECT 1 FROM dbo.PerfilPermisos x WHERE x.PerfilId=g.PerfilId AND x.PermisoId=p.PermisoId);
GO
-- G1-05: listas para las pantallas de Planilla (periodos abiertos, empleados activos) y G1-03: reglas con su porcentaje.
CREATE OR ALTER PROCEDURE dbo.sp_Payroll_ListarPeriodosAbiertos
AS
BEGIN
    SET NOCOUNT ON;
    SELECT PeriodoId,Tipo,Desde,Hasta,FactorSalario
    FROM dbo.PlanillaPeriodos
    WHERE Estado=N'Abierto'
    ORDER BY Desde DESC,PeriodoId DESC;
END;
GO
CREATE OR ALTER PROCEDURE dbo.sp_Payroll_ListarEmpleadosActivos
AS
BEGIN
    SET NOCOUNT ON;
    SELECT e.EmpleadoId,u.NombreCompleto,e.Puesto
    FROM dbo.Empleados e
    JOIN dbo.Usuarios u ON u.UsuarioId=e.UsuarioId
    WHERE e.Activo=1 AND u.Activo=1
    ORDER BY u.NombreCompleto,e.EmpleadoId;
END;
GO
CREATE OR ALTER PROCEDURE dbo.sp_Payroll_ListarReglas
AS
BEGIN
    SET NOCOUNT ON;
    SELECT ReglaId,Codigo,Nombre,Tipo,TipoCalculo,Valor,Tope,VigenteDesde,VigenteHasta,Fuente
    FROM dbo.PlanillaReglas
    ORDER BY Codigo,VigenteDesde DESC,ReglaId DESC;
END;
GO
DECLARE @sha NVARCHAR(128)=N'$(MigrationSha256)';
IF LEN(@sha)<>64 OR @sha LIKE N'%[^0-9A-Fa-f]%' THROW 58674,N'SHA-256 invalido.',1;
INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes) VALUES(N'0267_planilla_aprobacion_gerente_y_listas',N'0267_planilla_aprobacion_gerente_y_listas.sql',UPPER(@sha),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),N'G1-06 Gerente aprueba planilla (PLANILLA_VER y PLANILLA_APROBAR); G1-03/G1-05 listas de reglas, periodos abiertos y empleados activos.');
COMMIT TRANSACTION;
GO
