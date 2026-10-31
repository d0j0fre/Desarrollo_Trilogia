# Rollback de 0210 — procedimientos de productos (BUG-05-01)

## Alcance del cambio

0210 sustituye las definiciones de `dbo.sp_Admin_CreateProduct`,
`dbo.sp_Admin_UpdateProduct`, `dbo.sp_Admin_GetProductById` y
`dbo.sp_Admin_GetProducts`. No crea ni altera tablas, columnas, índices,
permisos ni filas. Se aplicó con `CREATE OR ALTER`, así que `object_id` y
`GRANT EXECUTE` se conservan.

## Procedimiento de reversión

No se recomienda: con las definiciones anteriores la aplicación vuelve a
responder HTTP 405 al crear o editar un producto (el procedimiento rechaza los
parámetros `@StockMinimo`, `@ImagenUrl` y `@EsDestacado`).

Si aun así se requiere, ejecutar en una ventana controlada las definiciones
históricas de `database/sp_admin_modulo.sql` para esos cuatro procedimientos
(bloques `CREATE OR ALTER PROCEDURE dbo.sp_Admin_GetProducts`,
`sp_Admin_GetProductById`, `sp_Admin_CreateProduct` y `sp_Admin_UpdateProduct`)
y marcar el ledger:

```sql
UPDATE dbo.SchemaMigrationHistory
SET Status = N'RolledBack',
    Notes = CONCAT(Notes, N' | Revertida el ', CONVERT(NVARCHAR(30), SYSUTCDATETIME(), 126), N' UTC.')
WHERE MigrationId = N'0210_product_save_procedures';
```

Si ya se aplicaron 0211 o posteriores que vuelven a definir estos
procedimientos, revertirlas primero.

## Restauración de BACPAC

No procede: el cambio sólo afecta definiciones de procedimientos.
