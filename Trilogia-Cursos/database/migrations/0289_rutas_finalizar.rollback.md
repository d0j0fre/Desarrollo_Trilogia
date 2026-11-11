# Rollback de 0289

## Qué introduce

- `sp_Rutas_Finalizar`: pasa una ruta `Despachada` a `Completada`, deja las paradas sin
  entregar (`Pendiente` o `EnRuta`) en `Fallido` con motivo y fecha, y sincroniza los pedidos
  con `sp_Pedidos_SincronizarEstadoEntrega` (0220). No toca inventario ni `Rutas.Liquidada`.
- No modifica tablas ni procedimientos existentes. `sp_Rutas_Liquidar` sigue en la base,
  sin uso desde la aplicación.

## Revertir

```sql
DROP PROCEDURE IF EXISTS dbo.sp_Rutas_Finalizar;
DELETE FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0289_rutas_finalizar';
```

Las rutas ya finalizadas conservan su estado (`Completada`, paradas `Fallido`). Sin el
procedimiento, el botón «Finalizar ruta» del detalle de la ruta falla con el mensaje genérico:
revertir también el código o reaplicar la 0289.
