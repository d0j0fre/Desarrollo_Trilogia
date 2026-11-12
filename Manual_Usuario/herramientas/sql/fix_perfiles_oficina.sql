-- Reaplica la asignación de permisos de 0025 (MOVIL_ACCESO a Bodeguero) y 0028 (matriz por perfil)
-- en bases donde los perfiles 7–14 se crearon después de esas migraciones.
SET QUOTED_IDENTIFIER ON;
DECLARE @Matriz TABLE (Perfil NVARCHAR(100) NOT NULL, Codigo NVARCHAR(100) NOT NULL);

INSERT @Matriz (Perfil, Codigo) VALUES
    -- Acceso a la aplicacion para el personal que aun no lo tenia.
    (N'Supervisor', N'MOVIL_ACCESO'), (N'Cajero', N'MOVIL_ACCESO'), (N'Facturador', N'MOVIL_ACCESO'),
    (N'Cr_dito y Cobro', N'MOVIL_ACCESO'), (N'Compras', N'MOVIL_ACCESO'), (N'Soporte', N'MOVIL_ACCESO'),
    (N'Auditor Interno', N'MOVIL_ACCESO'),

    -- Registro de jornada propia para todo el personal.
    (N'Chofer', N'RRHH_JORNADAS_REGISTRAR'), (N'Chofer', N'RRHH_JORNADAS_VER_PROPIAS'),
    (N'Bodeguero', N'RRHH_JORNADAS_REGISTRAR'), (N'Bodeguero', N'RRHH_JORNADAS_VER_PROPIAS'),
    (N'Gerente', N'RRHH_JORNADAS_REGISTRAR'), (N'Gerente', N'RRHH_JORNADAS_VER_PROPIAS'),
    (N'Supervisor', N'RRHH_JORNADAS_REGISTRAR'), (N'Supervisor', N'RRHH_JORNADAS_VER_PROPIAS'),
    (N'Cajero', N'RRHH_JORNADAS_REGISTRAR'), (N'Cajero', N'RRHH_JORNADAS_VER_PROPIAS'),
    (N'Facturador', N'RRHH_JORNADAS_REGISTRAR'), (N'Facturador', N'RRHH_JORNADAS_VER_PROPIAS'),
    (N'Cr_dito y Cobro', N'RRHH_JORNADAS_REGISTRAR'), (N'Cr_dito y Cobro', N'RRHH_JORNADAS_VER_PROPIAS'),
    (N'Compras', N'RRHH_JORNADAS_REGISTRAR'), (N'Compras', N'RRHH_JORNADAS_VER_PROPIAS'),
    (N'Soporte', N'RRHH_JORNADAS_REGISTRAR'), (N'Soporte', N'RRHH_JORNADAS_VER_PROPIAS'),
    (N'Auditor Interno', N'RRHH_JORNADAS_REGISTRAR'), (N'Auditor Interno', N'RRHH_JORNADAS_VER_PROPIAS'),

    -- Bodega: inventario, recepcion de compras y preparacion de pedidos.
    (N'Bodeguero', N'INVENTARIO_VER'), (N'Bodeguero', N'INVENTARIO_MOVIMIENTOS'),
    (N'Bodeguero', N'COMPRAS_ORDENES_VER'), (N'Bodeguero', N'COMPRAS_ORDENES_RECIBIR'),
    (N'Bodeguero', N'PEDIDOS_VER'), (N'Bodeguero', N'PEDIDOS_PREPARAR'),

    -- Gerencia: consulta de pedidos e inventario (ya gestiona rutas y aprobaciones).
    (N'Gerente', N'PEDIDOS_VER'), (N'Gerente', N'INVENTARIO_VER'),

    -- Oficina: un modulo enfocado por perfil.
    (N'Supervisor', N'RRHH_JORNADAS_APROBAR'),
    (N'Cajero', N'LIQUIDACION_FINANCIERA'),
    (N'Facturador', N'FACTURACION_VER'), (N'Facturador', N'PEDIDOS_VER'),
    (N'Cr_dito y Cobro', N'CREDITOS_VER'), (N'Cr_dito y Cobro', N'CLIENTES_VER'),
    (N'Compras', N'COMPRAS_ORDENES_VER'), (N'Compras', N'COMPRAS_SUGERENCIAS_VER'),
    (N'Compras', N'INVENTARIO_VER'), (N'Compras', N'PROVEEDORES_VER'),
    (N'Soporte', N'CONSULTAS_VER'), (N'Soporte', N'CONSULTAS_ATENDER'),
    (N'Auditor Interno', N'AUDITORIA_VER');

IF EXISTS (SELECT 1 FROM @Matriz m WHERE NOT EXISTS (SELECT 1 FROM dbo.Permisos p WHERE p.Codigo = m.Codigo))
    THROW 55805, N'Uno o más permisos de la matriz no existen en dbo.Permisos.', 1;

INSERT dbo.PerfilPermisos (PerfilId, PermisoId, UsuarioAsignacionId, UsuarioAsignacionNombre)
SELECT DISTINCT profile.PerfilId, permission.PermisoId, NULL, N'Migración 0028 aplicación móvil por rol'
FROM @Matriz m
INNER JOIN dbo.Perfiles profile ON profile.Nombre LIKE m.Perfil
INNER JOIN dbo.Permisos permission ON permission.Codigo = m.Codigo
WHERE NOT EXISTS (SELECT 1 FROM dbo.PerfilPermisos assigned
                  WHERE assigned.PerfilId = profile.PerfilId AND assigned.PermisoId = permission.PermisoId);
GO
GO
-- 0025: acceso a la aplicación para Bodeguero (creado después de esa migración).
INSERT dbo.PerfilPermisos(PerfilId,PermisoId,UsuarioAsignacionId,UsuarioAsignacionNombre)
SELECT profile.PerfilId,permission.PermisoId,NULL,N'Migración 0025 superficie móvil'
FROM dbo.Perfiles profile CROSS JOIN dbo.Permisos permission
WHERE profile.Nombre IN(N'Bodeguero',N'Bodega') AND permission.Codigo=N'MOVIL_ACCESO'
  AND NOT EXISTS(SELECT 1 FROM dbo.PerfilPermisos a WHERE a.PerfilId=profile.PerfilId AND a.PermisoId=permission.PermisoId);
GO
