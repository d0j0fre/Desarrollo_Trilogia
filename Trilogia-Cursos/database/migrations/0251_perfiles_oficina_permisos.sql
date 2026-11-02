SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

/* Perfiles de oficina sin permisos (hallazgo del manual de usuario).

   Los perfiles Supervisor, Cajero, Bodeguero, Facturador, Credito y Cobro,
   Compras, Soporte y Auditor Interno se crean despues de las migraciones (datos
   demo). Cuando corrieron la 0025 y la 0028, esos perfiles no existian y la
   asignacion de permisos no los encontro: quedaron con 0 permisos (o solo los de
   la 0023), sin modulos en la web ni en la aplicacion.

   Esta migracion asigna a esos perfiles exactamente lo que la 0028 preveia para
   ellos, mas MOVIL_ACCESO de la 0025 para Bodeguero. No crea permisos ni toca
   otros perfiles; no da nada que la 0028 no diera. Los perfiles se buscan por
   nombre (con LIKE, como la 0028, para no depender de la codificacion de la
   tilde); si un perfil no existe, simplemente no se le asigna nada.

   Convive con 0260-0265: solo inserta filas en dbo.PerfilPermisos que falten
   y no quita ninguna. La 0260 no se vuelve a ejecutar: si corrio antes que esta,
   Cajero queda sin FINANZAS_INGRESOS_EGRESOS_VER (no lo tenia la matriz 0028).

   Idempotente en los datos. Rollback en 0251_perfiles_oficina_permisos.rollback.md. */
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL THROW 56510,N'Falta el ledger 0001.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0251_perfiles_oficina_permisos' AND Status=N'Applied')
    THROW 56511,N'0251 ya figura aplicada.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0028_mobile_roles_surface' AND Status=N'Applied')
    THROW 56512,N'0251 depende de 0028 (matriz de permisos por perfil). Aplicar 0028 primero.',1;

DECLARE @Matriz TABLE (Perfil NVARCHAR(100) NOT NULL, Codigo NVARCHAR(100) NOT NULL, Origen NVARCHAR(150) NOT NULL);

INSERT @Matriz (Perfil, Codigo, Origen) VALUES
    -- 0025: acceso a la aplicacion para Bodeguero.
    (N'Bodeguero', N'MOVIL_ACCESO', N'Migración 0251 (matriz 0025)'),
    (N'Bodega', N'MOVIL_ACCESO', N'Migración 0251 (matriz 0025)'),

    -- 0028: acceso a la aplicacion para el personal de oficina.
    (N'Supervisor', N'MOVIL_ACCESO', N'Migración 0251 (matriz 0028)'),
    (N'Cajero', N'MOVIL_ACCESO', N'Migración 0251 (matriz 0028)'),
    (N'Facturador', N'MOVIL_ACCESO', N'Migración 0251 (matriz 0028)'),
    (N'Cr_dito y Cobro', N'MOVIL_ACCESO', N'Migración 0251 (matriz 0028)'),
    (N'Compras', N'MOVIL_ACCESO', N'Migración 0251 (matriz 0028)'),
    (N'Soporte', N'MOVIL_ACCESO', N'Migración 0251 (matriz 0028)'),
    (N'Auditor Interno', N'MOVIL_ACCESO', N'Migración 0251 (matriz 0028)'),

    -- 0028: registro de jornada propia.
    (N'Bodeguero', N'RRHH_JORNADAS_REGISTRAR', N'Migración 0251 (matriz 0028)'),
    (N'Bodeguero', N'RRHH_JORNADAS_VER_PROPIAS', N'Migración 0251 (matriz 0028)'),
    (N'Supervisor', N'RRHH_JORNADAS_REGISTRAR', N'Migración 0251 (matriz 0028)'),
    (N'Supervisor', N'RRHH_JORNADAS_VER_PROPIAS', N'Migración 0251 (matriz 0028)'),
    (N'Cajero', N'RRHH_JORNADAS_REGISTRAR', N'Migración 0251 (matriz 0028)'),
    (N'Cajero', N'RRHH_JORNADAS_VER_PROPIAS', N'Migración 0251 (matriz 0028)'),
    (N'Facturador', N'RRHH_JORNADAS_REGISTRAR', N'Migración 0251 (matriz 0028)'),
    (N'Facturador', N'RRHH_JORNADAS_VER_PROPIAS', N'Migración 0251 (matriz 0028)'),
    (N'Cr_dito y Cobro', N'RRHH_JORNADAS_REGISTRAR', N'Migración 0251 (matriz 0028)'),
    (N'Cr_dito y Cobro', N'RRHH_JORNADAS_VER_PROPIAS', N'Migración 0251 (matriz 0028)'),
    (N'Compras', N'RRHH_JORNADAS_REGISTRAR', N'Migración 0251 (matriz 0028)'),
    (N'Compras', N'RRHH_JORNADAS_VER_PROPIAS', N'Migración 0251 (matriz 0028)'),
    (N'Soporte', N'RRHH_JORNADAS_REGISTRAR', N'Migración 0251 (matriz 0028)'),
    (N'Soporte', N'RRHH_JORNADAS_VER_PROPIAS', N'Migración 0251 (matriz 0028)'),
    (N'Auditor Interno', N'RRHH_JORNADAS_REGISTRAR', N'Migración 0251 (matriz 0028)'),
    (N'Auditor Interno', N'RRHH_JORNADAS_VER_PROPIAS', N'Migración 0251 (matriz 0028)'),

    -- 0028: bodega (inventario, recepcion de compras y preparacion de pedidos).
    (N'Bodeguero', N'INVENTARIO_VER', N'Migración 0251 (matriz 0028)'),
    (N'Bodeguero', N'INVENTARIO_MOVIMIENTOS', N'Migración 0251 (matriz 0028)'),
    (N'Bodeguero', N'COMPRAS_ORDENES_VER', N'Migración 0251 (matriz 0028)'),
    (N'Bodeguero', N'COMPRAS_ORDENES_RECIBIR', N'Migración 0251 (matriz 0028)'),
    (N'Bodeguero', N'PEDIDOS_VER', N'Migración 0251 (matriz 0028)'),
    (N'Bodeguero', N'PEDIDOS_PREPARAR', N'Migración 0251 (matriz 0028)'),

    -- 0028: oficina, un modulo enfocado por perfil.
    (N'Supervisor', N'RRHH_JORNADAS_APROBAR', N'Migración 0251 (matriz 0028)'),
    (N'Cajero', N'LIQUIDACION_FINANCIERA', N'Migración 0251 (matriz 0028)'),
    (N'Facturador', N'FACTURACION_VER', N'Migración 0251 (matriz 0028)'),
    (N'Facturador', N'PEDIDOS_VER', N'Migración 0251 (matriz 0028)'),
    (N'Cr_dito y Cobro', N'CREDITOS_VER', N'Migración 0251 (matriz 0028)'),
    (N'Cr_dito y Cobro', N'CLIENTES_VER', N'Migración 0251 (matriz 0028)'),
    (N'Compras', N'COMPRAS_ORDENES_VER', N'Migración 0251 (matriz 0028)'),
    (N'Compras', N'COMPRAS_SUGERENCIAS_VER', N'Migración 0251 (matriz 0028)'),
    (N'Compras', N'INVENTARIO_VER', N'Migración 0251 (matriz 0028)'),
    (N'Compras', N'PROVEEDORES_VER', N'Migración 0251 (matriz 0028)'),
    (N'Soporte', N'CONSULTAS_VER', N'Migración 0251 (matriz 0028)'),
    (N'Soporte', N'CONSULTAS_ATENDER', N'Migración 0251 (matriz 0028)'),
    (N'Auditor Interno', N'AUDITORIA_VER', N'Migración 0251 (matriz 0028)');

IF EXISTS (SELECT 1 FROM @Matriz m WHERE NOT EXISTS (SELECT 1 FROM dbo.Permisos p WHERE p.Codigo = m.Codigo))
    THROW 56513, N'Uno o más permisos de la matriz no existen en dbo.Permisos.', 1;

BEGIN TRANSACTION;

INSERT dbo.PerfilPermisos (PerfilId, PermisoId, UsuarioAsignacionId, UsuarioAsignacionNombre)
SELECT profile.PerfilId, permission.PermisoId, NULL, MIN(m.Origen)
FROM @Matriz m
INNER JOIN dbo.Perfiles profile ON profile.Nombre LIKE m.Perfil
INNER JOIN dbo.Permisos permission ON permission.Codigo = m.Codigo
WHERE NOT EXISTS (SELECT 1 FROM dbo.PerfilPermisos assigned
                  WHERE assigned.PerfilId = profile.PerfilId AND assigned.PermisoId = permission.PermisoId)
GROUP BY profile.PerfilId, permission.PermisoId;

DECLARE @Asignados INT = @@ROWCOUNT;
DECLARE @MigrationSha256 NVARCHAR(128)=N'$(MigrationSha256)';
IF LEN(@MigrationSha256)<>64 OR @MigrationSha256 LIKE N'%[^0-9A-Fa-f]%' THROW 56514,N'SHA-256 inválido para 0251.',1;
INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes)
VALUES(N'0251_perfiles_oficina_permisos',N'0251_perfiles_oficina_permisos.sql',UPPER(@MigrationSha256),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),
       CONCAT(N'Matriz 0028 (y MOVIL_ACCESO 0025) para los perfiles de oficina creados después de esas migraciones. Asignaciones nuevas: ', @Asignados, N'. Aditiva.'));
COMMIT TRANSACTION;
GO
