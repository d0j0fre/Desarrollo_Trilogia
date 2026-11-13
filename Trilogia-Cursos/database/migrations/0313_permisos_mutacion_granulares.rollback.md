# Reversión de 0313

Reversión compensatoria; la migración solo **inserta** filas (permisos faltantes y asignaciones):

- Quitar las asignaciones que registró: `DELETE FROM dbo.PerfilPermisos WHERE UsuarioAsignacionNombre = N'Migración 0313';` (devuelve a cada perfil exactamente los permisos que tenía antes; las lecturas `*_VER` que esta migración agregó también se retiran).
- Los permisos del catálogo (`INVENTARIO_ELIMINAR` y los que no existían en otras bases) pueden conservarse; si se retiran, borrar antes las filas de `dbo.PerfilPermisos` que los referencian.
- Volver a desplegar el MVC anterior restablece el control solo por módulo en las acciones de mutación (vuelve a permitir que quien solo puede ver cree, edite, inactive o elimine).
- Marcar la fila `0313_permisos_mutacion_granulares` como `RolledBack` en `dbo.SchemaMigrationHistory`.

Antes de desplegar el MVC nuevo, revisar en `Roles` y `Permisos` que los perfiles personalizados que deban seguir mutando tengan sus permisos granulares: la migración no se los otorga (solo conserva lo que hoy hacen «Crédito y Cobro» y «Supervisor»).
