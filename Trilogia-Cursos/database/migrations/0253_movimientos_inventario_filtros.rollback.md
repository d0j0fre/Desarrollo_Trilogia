# Rollback de 0253

## Qué introduce

- Reemplaza `dbo.sp_Admin_GetInventoryMovements` (solo lectura) con cuatro
  parámetros opcionales: `@Buscar` (texto del producto), `@TipoMovimiento`,
  `@Desde` y `@Hasta`. Sin parámetros devuelve lo mismo que antes: los últimos
  200 movimientos, en el mismo orden y con las mismas columnas.
- No cambia tablas ni datos.

## Revertir

Volver a la definición sin parámetros (`database/sp_admin_modulo.sql`, que solo
hace `CREATE OR ALTER` de procedimientos de lectura y escritura del módulo de
administración; reaplicar solo el bloque de `sp_Admin_GetInventoryMovements`) y
borrar la fila del ledger:

```sql
DELETE FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0253_movimientos_inventario_filtros';
```

Con el procedimiento anterior, los filtros de Inventario → Movimientos dejan de
filtrar (la aplicación envía los parámetros y el procedimiento viejo los
rechaza): revertir también el código de la pantalla o reaplicar la 0253.
