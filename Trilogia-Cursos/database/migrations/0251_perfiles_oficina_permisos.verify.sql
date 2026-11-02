SET NOCOUNT ON;

/* Verificacion de 0251. Solo lectura. */
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0251_perfiles_oficina_permisos' AND Status = N'Applied')
    THROW 56560, N'0251 no figura aplicada en el ledger.', 1;

DECLARE @Matriz TABLE (Perfil NVARCHAR(100) NOT NULL, Codigo NVARCHAR(100) NOT NULL);
INSERT @Matriz (Perfil, Codigo) VALUES
    (N'Supervisor', N'MOVIL_ACCESO'), (N'Supervisor', N'RRHH_JORNADAS_REGISTRAR'), (N'Supervisor', N'RRHH_JORNADAS_VER_PROPIAS'),
    (N'Supervisor', N'RRHH_JORNADAS_APROBAR'),
    (N'Cajero', N'MOVIL_ACCESO'), (N'Cajero', N'RRHH_JORNADAS_REGISTRAR'), (N'Cajero', N'RRHH_JORNADAS_VER_PROPIAS'),
    (N'Cajero', N'LIQUIDACION_FINANCIERA'),
    (N'Bodeguero', N'MOVIL_ACCESO'), (N'Bodeguero', N'RRHH_JORNADAS_REGISTRAR'), (N'Bodeguero', N'RRHH_JORNADAS_VER_PROPIAS'),
    (N'Bodeguero', N'INVENTARIO_VER'), (N'Bodeguero', N'INVENTARIO_MOVIMIENTOS'), (N'Bodeguero', N'COMPRAS_ORDENES_VER'),
    (N'Bodeguero', N'COMPRAS_ORDENES_RECIBIR'), (N'Bodeguero', N'PEDIDOS_VER'), (N'Bodeguero', N'PEDIDOS_PREPARAR'),
    (N'Facturador', N'MOVIL_ACCESO'), (N'Facturador', N'RRHH_JORNADAS_REGISTRAR'), (N'Facturador', N'RRHH_JORNADAS_VER_PROPIAS'),
    (N'Facturador', N'FACTURACION_VER'), (N'Facturador', N'PEDIDOS_VER'),
    (N'Cr_dito y Cobro', N'MOVIL_ACCESO'), (N'Cr_dito y Cobro', N'RRHH_JORNADAS_REGISTRAR'), (N'Cr_dito y Cobro', N'RRHH_JORNADAS_VER_PROPIAS'),
    (N'Cr_dito y Cobro', N'CREDITOS_VER'), (N'Cr_dito y Cobro', N'CLIENTES_VER'),
    (N'Compras', N'MOVIL_ACCESO'), (N'Compras', N'RRHH_JORNADAS_REGISTRAR'), (N'Compras', N'RRHH_JORNADAS_VER_PROPIAS'),
    (N'Compras', N'COMPRAS_ORDENES_VER'), (N'Compras', N'COMPRAS_SUGERENCIAS_VER'), (N'Compras', N'INVENTARIO_VER'),
    (N'Compras', N'PROVEEDORES_VER'),
    (N'Soporte', N'MOVIL_ACCESO'), (N'Soporte', N'RRHH_JORNADAS_REGISTRAR'), (N'Soporte', N'RRHH_JORNADAS_VER_PROPIAS'),
    (N'Soporte', N'CONSULTAS_VER'), (N'Soporte', N'CONSULTAS_ATENDER'),
    (N'Auditor Interno', N'MOVIL_ACCESO'), (N'Auditor Interno', N'RRHH_JORNADAS_REGISTRAR'), (N'Auditor Interno', N'RRHH_JORNADAS_VER_PROPIAS'),
    (N'Auditor Interno', N'AUDITORIA_VER');

/* Cada perfil de oficina que exista tiene toda su matriz. */
IF EXISTS (
    SELECT 1 FROM @Matriz m
    INNER JOIN dbo.Perfiles pf ON pf.Nombre LIKE m.Perfil
    INNER JOIN dbo.Permisos p ON p.Codigo = m.Codigo
    WHERE NOT EXISTS (SELECT 1 FROM dbo.PerfilPermisos pp WHERE pp.PerfilId = pf.PerfilId AND pp.PermisoId = p.PermisoId))
    THROW 56561, N'Un perfil de oficina no tiene todos los permisos de la matriz 0028.', 1;

/* Con la 0260 aplicada (en cualquier orden), todo perfil de oficina con un permiso
   activo de Finanzas tiene tambien FINANZAS_INGRESOS_EGRESOS_VER (regla de la 0260). */
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0260_roles_finanzas_consulta' AND Status=N'Applied')
   AND EXISTS (
    SELECT 1 FROM @Matriz m
    INNER JOIN dbo.Perfiles pf ON pf.Nombre LIKE m.Perfil
    INNER JOIN dbo.Permisos p ON p.Codigo = m.Codigo
    WHERE p.Modulo = N'Finanzas' AND p.Activo = 1 AND p.Codigo <> N'FINANZAS_INGRESOS_EGRESOS_VER'
      AND NOT EXISTS (SELECT 1 FROM dbo.PerfilPermisos pp INNER JOIN dbo.Permisos ie ON ie.PermisoId = pp.PermisoId
                      WHERE pp.PerfilId = pf.PerfilId AND ie.Codigo = N'FINANZAS_INGRESOS_EGRESOS_VER'))
    THROW 56564, N'Un perfil de oficina con permisos de Finanzas no tiene FINANZAS_INGRESOS_EGRESOS_VER (regla de la 0260).', 1;

/* Lo que verifica la 0028: todo perfil del personal entra a la aplicacion; Cliente no. */
IF EXISTS (
    SELECT 1 FROM dbo.Perfiles pf
    WHERE pf.Nombre <> N'Cliente'
      AND NOT EXISTS (SELECT 1 FROM dbo.PerfilPermisos pp INNER JOIN dbo.Permisos p ON p.PermisoId = pp.PermisoId
                      WHERE pp.PerfilId = pf.PerfilId AND p.Codigo = N'MOVIL_ACCESO'))
    THROW 56562, N'Hay un perfil del personal sin MOVIL_ACCESO.', 1;
IF EXISTS (
    SELECT 1 FROM dbo.PerfilPermisos pp
    INNER JOIN dbo.Perfiles pf ON pf.PerfilId = pp.PerfilId
    INNER JOIN dbo.Permisos p ON p.PermisoId = pp.PermisoId
    WHERE pf.Nombre = N'Cliente')
    THROW 56563, N'El perfil Cliente no debe tener permisos administrativos.', 1;

SELECT pf.Nombre AS Perfil, COUNT(pp.PermisoId) AS Permisos
FROM dbo.Perfiles pf
LEFT JOIN dbo.PerfilPermisos pp ON pp.PerfilId = pf.PerfilId
GROUP BY pf.PerfilId, pf.Nombre
ORDER BY pf.PerfilId;
PRINT N'Verificación 0251 completada.';
