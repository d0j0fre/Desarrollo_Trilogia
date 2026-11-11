SET NOCOUNT ON;

/* Verificacion de 0291 (solo lectura). */
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0291_gerente_inteligencia_inventario' AND Status = N'Applied')
    THROW 56930, N'0291 no figura aplicada en el ledger.', 1;
IF NOT EXISTS (SELECT 1 FROM dbo.Perfiles p
               INNER JOIN dbo.PerfilPermisos pp ON pp.PerfilId = p.PerfilId
               INNER JOIN dbo.Permisos pe ON pe.PermisoId = pp.PermisoId
               WHERE p.Nombre = N'Gerente' AND pe.Codigo = N'INVENTARIO_INTELIGENCIA_VER')
    THROW 56931, N'El perfil Gerente no tiene INVENTARIO_INTELIGENCIA_VER.', 1;
IF NOT EXISTS (SELECT 1 FROM dbo.Perfiles p
               INNER JOIN dbo.PerfilPermisos pp ON pp.PerfilId = p.PerfilId
               INNER JOIN dbo.Permisos pe ON pe.PermisoId = pp.PermisoId
               WHERE p.Nombre = N'Administrador' AND pe.Codigo = N'INVENTARIO_INTELIGENCIA_VER')
    THROW 56932, N'El Administrador perdio INVENTARIO_INTELIGENCIA_VER.', 1;
IF EXISTS (SELECT 1 FROM dbo.PerfilPermisos GROUP BY PerfilId, PermisoId HAVING COUNT(*) > 1)
    THROW 56933, N'Hay asignaciones de permiso duplicadas.', 1;
PRINT N'Verificación 0291 completada.';
