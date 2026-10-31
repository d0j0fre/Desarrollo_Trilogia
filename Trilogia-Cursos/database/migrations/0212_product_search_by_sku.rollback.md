# Rollback de 0212 — búsqueda por SKU (BUG-05-03)

## Alcance del cambio

Sólo redefine `dbo.sp_Admin_GetProducts` (con `CREATE OR ALTER`). No toca tablas
ni filas.

## Procedimiento de reversión

Volver a ejecutar el bloque `CREATE OR ALTER PROCEDURE dbo.sp_Admin_GetProducts`
de `0211_product_expiration_date.sql` (sin la parte de ledger) y marcar el
ledger:

```sql
UPDATE dbo.SchemaMigrationHistory
SET Status = N'RolledBack',
    Notes = CONCAT(Notes, N' | Revertida el ', CONVERT(NVARCHAR(30), SYSUTCDATETIME(), 126), N' UTC.')
WHERE MigrationId = N'0212_product_search_by_sku';
```

La aplicación sigue funcionando; sólo se pierde la búsqueda por código SKU.

## Restauración de BACPAC

No procede.
