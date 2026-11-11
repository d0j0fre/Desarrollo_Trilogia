SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

/* BUG-24-03 · CU-242: la alerta gerencial de productos estancados y la sugerencia de
   incluirlos en una promocion viven en Inteligencia de inventario, pero esa pantalla solo
   estaba permitida al Administrador. El Gerente (que si gestiona Promociones) no podia
   abrirla, asi que nunca veia la alerta.

   Se asigna el permiso INVENTARIO_INTELIGENCIA_VER al perfil Gerente. No crea permisos
   ni toca otros perfiles; solo inserta la asignacion si falta.
   Rollback en 0291_gerente_inteligencia_inventario.rollback.md. */
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL THROW 56920,N'Falta el ledger 0001.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0291_gerente_inteligencia_inventario' AND Status=N'Applied')
    THROW 56921,N'0291 ya figura aplicada.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.Permisos WHERE Codigo=N'INVENTARIO_INTELIGENCIA_VER' AND Activo=1)
    THROW 56922,N'Falta el permiso activo INVENTARIO_INTELIGENCIA_VER (0012).',1;

BEGIN TRANSACTION;

INSERT dbo.PerfilPermisos(PerfilId,PermisoId,UsuarioAsignacionId,UsuarioAsignacionNombre)
SELECT p.PerfilId, pe.PermisoId, NULL, N'Migración 0291'
FROM dbo.Perfiles p
CROSS JOIN dbo.Permisos pe
WHERE p.Nombre=N'Gerente' AND pe.Codigo=N'INVENTARIO_INTELIGENCIA_VER'
  AND NOT EXISTS (SELECT 1 FROM dbo.PerfilPermisos x WHERE x.PerfilId=p.PerfilId AND x.PermisoId=pe.PermisoId);

DECLARE @MigrationSha256 NVARCHAR(128)=N'$(MigrationSha256)';
IF LEN(@MigrationSha256)<>64 OR @MigrationSha256 LIKE N'%[^0-9A-Fa-f]%' THROW 56923,N'SHA-256 inválido para 0291.',1;
INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes)
VALUES(N'0291_gerente_inteligencia_inventario',N'0291_gerente_inteligencia_inventario.sql',UPPER(@MigrationSha256),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),
       N'BUG-24-03: el perfil Gerente puede abrir Inteligencia de inventario (alerta de productos estancados).');
COMMIT TRANSACTION;
GO
