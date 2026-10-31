# Rollback de 0230 — estado de la deuda y pedido a crédito (BUG-06-02)

## Alcance del cambio

- Agrega `dbo.ClienteCreditos.PlazoCreditoDias INT NOT NULL` (con
  `DF_ClienteCreditos_PlazoCreditoDias` y `CK_ClienteCreditos_PlazoCreditoDias`),
  `dbo.ClienteCreditoMovimientos.PedidoId INT NULL` y el índice filtrado
  `IX_ClienteCreditoMovimientos_PedidoId`.
- Crea `dbo.fn_Cliente_EstadoDeuda`, `dbo.sp_Cliente_EstadoDeuda`,
  `dbo.sp_Credito_CargarPedido` y el disparador `dbo.TR_Pedidos_AnularCargoCredito`.
- Redefine `sp_Admin_UpdateClientCreditSettings`, `sp_Admin_GetClientCreditDetail`
  y `sp_Seller_CreateOrder`.

Dependen de 0230: 0231 (inactivación con saldo), 0232 (estado de cuenta) y
cualquier migración de comodatos que use `fn_Cliente_EstadoDeuda` (BUG-16-02).
Revertirlas primero.

## Procedimiento de reversión

1. Revertir primero el código de aplicación: la versión con BUG-06-02 envía
   `@MetodoPago` a `sp_Seller_CreateOrder`, `@PlazoCreditoDias` a
   `sp_Admin_UpdateClientCreditSettings` y lee las columnas nuevas del detalle
   de crédito.
2. Ejecutar en una ventana controlada:

```sql
SET XACT_ABORT ON;
BEGIN TRANSACTION;

DROP TRIGGER IF EXISTS dbo.TR_Pedidos_AnularCargoCredito;
DROP PROCEDURE IF EXISTS dbo.sp_Credito_CargarPedido;
DROP PROCEDURE IF EXISTS dbo.sp_Cliente_EstadoDeuda;

-- Volver a ejecutar aquí:
--   · sp_Seller_CreateOrder de database/cu098_retencion_autorizacion_pedidos.sql;
--   · sp_Admin_UpdateClientCreditSettings y sp_Admin_GetClientCreditDetail de
--     database/cu062_creditos_patch.sql.
-- Después de eso ya nada usa la función:
DROP FUNCTION IF EXISTS dbo.fn_Cliente_EstadoDeuda;

DROP INDEX IF EXISTS IX_ClienteCreditoMovimientos_PedidoId ON dbo.ClienteCreditoMovimientos;
ALTER TABLE dbo.ClienteCreditoMovimientos DROP COLUMN PedidoId;
ALTER TABLE dbo.ClienteCreditos DROP CONSTRAINT CK_ClienteCreditos_PlazoCreditoDias;
ALTER TABLE dbo.ClienteCreditos DROP CONSTRAINT DF_ClienteCreditos_PlazoCreditoDias;
ALTER TABLE dbo.ClienteCreditos DROP COLUMN PlazoCreditoDias;

UPDATE dbo.SchemaMigrationHistory
SET Status = N'RolledBack',
    Notes = CONCAT(Notes, N' | Revertida el ', CONVERT(NVARCHAR(30), SYSUTCDATETIME(), 126), N' UTC.')
WHERE MigrationId = N'0230_cliente_estado_deuda';

COMMIT TRANSACTION;
```

Los cargos y reversos de pedidos a crédito ya registrados se conservan como
movimientos normales (referencia `Pedido #N`): la deuda del cliente no cambia
al revertir. Los pedidos quedan con `MetodoPago = N'Crédito'`.

## Restauración de BACPAC

No procede.
