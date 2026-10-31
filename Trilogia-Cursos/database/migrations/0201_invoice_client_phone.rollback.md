# Rollback de 0201 — teléfono del cliente de una factura

## Alcance del cambio

0201 crea únicamente `dbo.sp_Admin_GetInvoiceClientPhone`. No toca tablas, columnas,
índices, restricciones, permisos ni datos.

## Procedimiento de reversión

La aplicación tolera que el procedimiento no exista: el detalle de factura y la
confirmación de Venta móvil indican que el cliente no tiene número registrado y no
ofrecen el envío por WhatsApp. Para revertir, por el ejecutor designado:

```sql
SET XACT_ABORT ON;
BEGIN TRANSACTION;

DROP PROCEDURE IF EXISTS dbo.sp_Admin_GetInvoiceClientPhone;

UPDATE dbo.SchemaMigrationHistory
SET Status = N'RolledBack',
    Notes = CONCAT(Notes, N' | Revertida el ', CONVERT(NVARCHAR(30), SYSUTCDATETIME(), 126), N' UTC.')
WHERE MigrationId = N'0201_invoice_client_phone';

COMMIT TRANSACTION;
```

## Restauración de BACPAC

No procede.
