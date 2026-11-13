# Rollback de 0293

## Qué introduce

- Reemplaza `dbo.sp_Admin_GetSeasonalSalesTrend` (solo lectura) para agregar el
  parámetro `@Categoria NVARCHAR(100) = NULL`. Sin categoría devuelve lo mismo que
  la 0292; con categoría suma solo las líneas de factura de esa categoría.
- No cambia datos.

## Revertir

1. Quitar de la aplicación el uso de `@Categoria` (la pantalla Inteligencia de
   inventario lo envía); si no, fallaría con «parámetro no válido».
2. Reaplicar la definición de `sp_Admin_GetSeasonalSalesTrend` de
   `0292_tendencia_estacional_facturas.sql` (`CREATE OR ALTER`).
3. Borrar la fila del ledger:

```sql
DELETE FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0293_tendencia_estacional_categoria';
```
