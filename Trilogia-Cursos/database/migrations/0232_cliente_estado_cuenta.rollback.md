# Rollback de 0232 — estado de cuenta del cliente (BUG-06-03, BUG-12-01, BUG-12-02)

## Alcance del cambio

Crea `dbo.sp_Cliente_EstadoCuenta` (solo lectura). No toca tablas, datos ni
otros procedimientos.

## Procedimiento de reversión

1. Revertir primero el código de aplicación: el detalle del cliente, el
   portal (Estado de cuenta y Mis facturas) y los PDF llaman a
   `sp_Cliente_EstadoCuenta`. Sin el código, el procedimiento sobra pero no
   estorba.
2. Ejecutar:

```sql
SET XACT_ABORT ON;
BEGIN TRANSACTION;

DROP PROCEDURE IF EXISTS dbo.sp_Cliente_EstadoCuenta;

UPDATE dbo.SchemaMigrationHistory
SET Status = N'RolledBack',
    Notes = CONCAT(Notes, N' | Revertida el ', CONVERT(NVARCHAR(30), SYSUTCDATETIME(), 126), N' UTC.')
WHERE MigrationId = N'0232_cliente_estado_cuenta';

COMMIT TRANSACTION;
```

## Restauración de BACPAC

No procede.
