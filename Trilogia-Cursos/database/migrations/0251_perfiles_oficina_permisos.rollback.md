# Rollback de 0251

## Qué introduce

- Solo inserta filas en `dbo.PerfilPermisos`: la matriz que la 0028 preveía para
  los perfiles de oficina (Supervisor, Cajero, Bodeguero, Facturador, Crédito y
  Cobro, Compras, Soporte, Auditor Interno) y `MOVIL_ACCESO` de la 0025 para
  Bodeguero. Esos perfiles se crean después de las migraciones (datos demo) y
  habían quedado sin esas asignaciones.
- Cada fila nueva queda marcada con `UsuarioAsignacionNombre` que empieza por
  `Migración 0251`. No crea permisos, no cambia procedimientos ni quita
  asignaciones existentes.

## Revertir

Antes de borrar, revisar con el responsable de seguridad que nadie haya
reasignado a mano alguno de esos permisos después de aplicar la migración.
Luego, en una transacción:

```sql
BEGIN TRANSACTION;
DELETE FROM dbo.PerfilPermisos WHERE UsuarioAsignacionNombre LIKE N'Migración 0251%';
DELETE FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0251_perfiles_oficina_permisos';
COMMIT TRANSACTION;
```

Efecto: esos perfiles vuelven a no ver sus módulos en el menú web (el menú
muestra cada opción según el permiso de la pantalla) y pierden el acceso a la
aplicación móvil. El código de la aplicación no depende de la migración.
