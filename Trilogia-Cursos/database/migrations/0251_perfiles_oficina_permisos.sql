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
   y no quita ninguna. El resultado es el mismo en cualquier orden:
   - Orden numerico (0251 y luego 0260): la 0260 aplica su regla de
     conservacion («quien ya tiene un permiso activo del modulo Finanzas recibe
     FINANZAS_INGRESOS_EGRESOS_VER») y se la da a Cajero por
     LIQUIDACION_FINANCIERA.
   - 0260 ya aplicada (bases que la recibieron antes): esta migracion aplica esa
     misma regla, pero solo a los perfiles a los que acaba de dar un permiso del
     modulo Finanzas. Asi Cajero termina igual en los dos ordenes.

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

DECLARE @Nuevas TABLE (PerfilId INT NOT NULL, PermisoId INT NOT NULL);

BEGIN TRANSACTION;

INSERT dbo.PerfilPermisos (PerfilId, PermisoId, UsuarioAsignacionId, UsuarioAsignacionNombre)
OUTPUT inserted.PerfilId, inserted.PermisoId INTO @Nuevas (PerfilId, PermisoId)
SELECT profile.PerfilId, permission.PermisoId, NULL, MIN(m.Origen)
FROM @Matriz m
INNER JOIN dbo.Perfiles profile ON profile.Nombre LIKE m.Perfil
INNER JOIN dbo.Permisos permission ON permission.Codigo = m.Codigo
WHERE NOT EXISTS (SELECT 1 FROM dbo.PerfilPermisos assigned
                  WHERE assigned.PerfilId = profile.PerfilId AND assigned.PermisoId = permission.PermisoId)
GROUP BY profile.PerfilId, permission.PermisoId;

DECLARE @Asignados INT = @@ROWCOUNT;

/* Regla de conservacion de la 0260, si esa migracion ya corrio: solo para los
   perfiles a los que esta migracion acaba de dar un permiso activo de Finanzas. */
DECLARE @ReglaFinanzas INT = 0;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0260_roles_finanzas_consulta' AND Status=N'Applied')
BEGIN
    INSERT dbo.PerfilPermisos (PerfilId, PermisoId, UsuarioAsignacionId, UsuarioAsignacionNombre)
    SELECT DISTINCT nueva.PerfilId, nuevo.PermisoId, NULL, N'Migración 0251 (regla 0260)'
    FROM @Nuevas nueva
    INNER JOIN dbo.Permisos previo ON previo.PermisoId = nueva.PermisoId
    CROSS JOIN dbo.Permisos nuevo
    WHERE previo.Modulo = N'Finanzas' AND previo.Activo = 1 AND previo.Codigo <> N'FINANZAS_INGRESOS_EGRESOS_VER'
      AND nuevo.Codigo = N'FINANZAS_INGRESOS_EGRESOS_VER'
      AND NOT EXISTS (SELECT 1 FROM dbo.PerfilPermisos x WHERE x.PerfilId = nueva.PerfilId AND x.PermisoId = nuevo.PermisoId);
    SET @ReglaFinanzas = @@ROWCOUNT;
END;

DECLARE @MigrationSha256 NVARCHAR(128)=N'$(MigrationSha256)';
IF LEN(@MigrationSha256)<>64 OR @MigrationSha256 LIKE N'%[^0-9A-Fa-f]%' THROW 56514,N'SHA-256 inválido para 0251.',1;
INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes)
VALUES(N'0251_perfiles_oficina_permisos',N'0251_perfiles_oficina_permisos.sql',UPPER(@MigrationSha256),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),
       CONCAT(N'Matriz 0028 (y MOVIL_ACCESO 0025) para los perfiles de oficina creados después de esas migraciones. Asignaciones nuevas: ', @Asignados,
              N'; por la regla de Finanzas de la 0260: ', @ReglaFinanzas, N'. Aditiva.'));
COMMIT TRANSACTION;
GO
