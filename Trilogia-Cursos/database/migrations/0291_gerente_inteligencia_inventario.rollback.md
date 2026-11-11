# Rollback de 0291

## Qué introduce

- Asigna el permiso `INVENTARIO_INTELIGENCIA_VER` al perfil `Gerente`
  (solo inserta la asignación si no existía). No crea permisos ni cambia otros
  perfiles ni datos.

## Revertir

Quitar la asignación y la fila del ledger (la asignación se identifica porque la
migración la marca con `UsuarioAsignacionNombre = 'Migración 0291'`):

```sql
DELETE pp
FROM dbo.PerfilPermisos pp
INNER JOIN dbo.Perfiles p ON p.PerfilId = pp.PerfilId
INNER JOIN dbo.Permisos pe ON pe.PermisoId = pp.PermisoId
WHERE p.Nombre = N'Gerente' AND pe.Codigo = N'INVENTARIO_INTELIGENCIA_VER'
  AND pp.UsuarioAsignacionNombre = N'Migración 0291';

DELETE FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0291_gerente_inteligencia_inventario';
```

Si el Gerente ya tenía el permiso antes de aplicar la migración, la asignación
original no lleva esa marca y no se borra.
