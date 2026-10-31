# Rollback de 0231 — no inactivar clientes con saldo (BUG-06-01)

## Alcance del cambio

Redefine `dbo.sp_Admin_UpdateClient` y `dbo.sp_Admin_ToggleClientStatus` para
rechazar con el error 51012 la inactivación de un cliente cuya deuda actual
(`dbo.fn_Cliente_EstadoDeuda`, 0230) es mayor que cero. No toca tablas ni datos.

## Procedimiento de reversión

1. El código de aplicación funciona con o sin 0231 (solo traduce el error
   51012 a un mensaje); no hace falta revertirlo.
2. Volver a ejecutar, en una ventana controlada, los bloques
   `CREATE OR ALTER PROCEDURE dbo.sp_Admin_UpdateClient` y
   `CREATE OR ALTER PROCEDURE dbo.sp_Admin_ToggleClientStatus` de
   `database/cu061_cu063_clientes_patch.sql`.
3. Registrar la reversión:

```sql
UPDATE dbo.SchemaMigrationHistory
SET Status = N'RolledBack',
    Notes = CONCAT(Notes, N' | Revertida el ', CONVERT(NVARCHAR(30), SYSUTCDATETIME(), 126), N' UTC.')
WHERE MigrationId = N'0231_cliente_inactivar_con_saldo';
```

## Restauración de BACPAC

No procede.
