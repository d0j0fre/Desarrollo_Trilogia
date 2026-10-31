# Rollback de 0211 — fecha de vencimiento de productos (BUG-05-02)

## Alcance del cambio

- Agrega `dbo.Productos.FechaVencimiento DATE NULL`.
- Redefine `sp_Admin_CreateProduct`, `sp_Admin_UpdateProduct`,
  `sp_Admin_GetProductById` y `sp_Admin_GetProducts` (con `CREATE OR ALTER`,
  conserva `object_id` y permisos).

## Procedimiento de reversión

1. Revertir primero el código de aplicación: la versión con BUG-05-02 envía
   `@FechaVencimiento` y fallaría contra los procedimientos de 0210.
2. Volver a ejecutar los cuatro bloques `CREATE OR ALTER PROCEDURE` de
   `0210_product_save_procedures.sql` (sin la parte de ledger).
3. La columna puede conservarse (es opcional y ningún otro objeto la usa). Si se
   decide eliminarla, se pierden las fechas registradas:

```sql
SET XACT_ABORT ON;
BEGIN TRANSACTION;
ALTER TABLE dbo.Productos DROP COLUMN FechaVencimiento;
UPDATE dbo.SchemaMigrationHistory
SET Status = N'RolledBack',
    Notes = CONCAT(Notes, N' | Revertida el ', CONVERT(NVARCHAR(30), SYSUTCDATETIME(), 126), N' UTC.')
WHERE MigrationId = N'0211_product_expiration_date';
COMMIT TRANSACTION;
```

## Restauración de BACPAC

No procede.
