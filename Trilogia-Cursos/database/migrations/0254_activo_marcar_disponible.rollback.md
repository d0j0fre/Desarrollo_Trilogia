# Rollback de 0254

## Qué introduce

- Crea `dbo.sp_Activos_MarcarDisponible`, que pasa un activo de `Mantenimiento`
  a `Disponible` (error 53064 si el activo está en otro estado). Lo usa la
  acción «Marcar como disponible» del detalle del activo.
- No modifica tablas, datos ni otros procedimientos.

## Revertir

```sql
DROP PROCEDURE IF EXISTS dbo.sp_Activos_MarcarDisponible;
DELETE FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0254_activo_marcar_disponible';
```

Sin el procedimiento, el botón «Marcar como disponible» falla con el mensaje
genérico de la pantalla: revertir también el código o reaplicar la 0254. Los
activos que ya se marcaron como disponibles conservan ese estado.
