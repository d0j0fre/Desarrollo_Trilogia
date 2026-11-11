# Rollback de 0292

## Qué introduce

- Reemplaza `dbo.sp_Admin_GetSeasonalSalesTrend` (solo lectura): `TotalVendido` y
  `UnidadesVendidas` salen de las facturas con `Estado <> 'Anulada'` (importe con
  IVA, mes por `FechaFactura`), igual que el Reporte de ventas. Antes sumaba las
  líneas de pedidos sin IVA.
- Mismos parámetros, columnas y validación (error 54542). No cambia datos.

## Revertir

1. Reaplicar la definición de `sp_Admin_GetSeasonalSalesTrend` de
   `0012_inventory_combos_transformations_intelligence.sql` (`CREATE OR ALTER`).
   Si la 0293 ya está aplicada, revertir primero la 0293 (agrega `@Categoria`).
2. Borrar la fila del ledger:

```sql
DELETE FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0292_tendencia_estacional_facturas';
```

La tendencia vuelve a mostrar importes sin IVA y a diferir del Reporte de ventas.
