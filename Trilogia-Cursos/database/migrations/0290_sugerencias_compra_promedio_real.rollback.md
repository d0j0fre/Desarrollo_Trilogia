# Rollback de 0290

## Qué introduce

- Reemplaza `dbo.sp_Admin_GetPurchaseSuggestions` (solo lectura) para agregar el
  parámetro `@IncluirTodos BIT = 0`. Con el valor por defecto devuelve lo mismo
  que la definición de la 0012; con `1` devuelve todos los productos activos con
  el mismo cálculo de demanda.
- No cambia datos.

## Revertir

1. Antes de revertir, quitar de la aplicación el uso de `@IncluirTodos` (la
   pantalla Sugerencias de compra y el formulario de órdenes lo envían); si no,
   fallarían con «parámetro no válido».
2. Reaplicar la definición de `sp_Admin_GetPurchaseSuggestions` de
   `0012_inventory_combos_transformations_intelligence.sql` (`CREATE OR ALTER`).
3. Borrar la fila del ledger:

```sql
DELETE FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0290_sugerencias_compra_promedio_real';
```
