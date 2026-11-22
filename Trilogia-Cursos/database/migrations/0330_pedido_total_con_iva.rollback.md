# Reversión de 0330

## Qué introduce

- `dbo.Pedidos.Subtotal` e `Impuesto` (columnas nuevas, NULL permitido) y el respaldo `dbo.PedidosTotalAntesIva`.
- **Cambia datos:** el total de cada pedido existente pasa a ser el total con IVA (si hay factura, igual a la
  factura; si no, total anterior + 13 %). El total anterior de cada pedido queda en `PedidosTotalAntesIva.TotalAnterior`.
- `sp_Store_CreateOrderWithPromotions`, `sp_Store_GetCheckoutResult` y `sp_Admin_GetOrderHeader` (definiciones
  nuevas con `CREATE OR ALTER`).

## Revertir (en este orden; si 0332 está aplicada, revertir 0332 primero)

1. Devolver los totales anteriores (exacto, desde el respaldo):

```sql
BEGIN TRANSACTION;
UPDATE p
SET p.Total = b.TotalAnterior
FROM dbo.Pedidos p
INNER JOIN dbo.PedidosTotalAntesIva b ON b.PedidoId = p.PedidoId;
-- Pedidos creados DESPUÉS de 0330 (sin respaldo): su total sin IVA es el subtotal.
UPDATE p SET p.Total = p.Subtotal
FROM dbo.Pedidos p
WHERE p.Subtotal IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM dbo.PedidosTotalAntesIva b WHERE b.PedidoId = p.PedidoId);
COMMIT TRANSACTION;
```

2. Volver a crear `dbo.sp_Store_CreateOrderWithPromotions` (0012), `dbo.sp_Store_GetCheckoutResult` (0012) y
   `dbo.sp_Admin_GetOrderHeader` (0200) desde esos archivos (`CREATE OR ALTER`). Si no se hace, siguen
   guardando/leyendo Subtotal e Impuesto, que existen, pero el total volvería a ser sin IVA y el pedido
   quedaría inconsistente con sus columnas.
3. Opcional, una vez que ningún procedimiento use las columnas: `ALTER TABLE dbo.Pedidos DROP COLUMN Subtotal, Impuesto;`
   y `DROP TABLE dbo.PedidosTotalAntesIva;`. Conviene conservarlas: no estorban.
4. Marcar la fila en el ledger:

```sql
UPDATE dbo.SchemaMigrationHistory SET Status = N'RolledBack' WHERE MigrationId = N'0330_pedido_total_con_iva';
```

La factura, el cargo a crédito, el estado de cuenta y los reportes de ventas no se tocaron, así que no hay nada
que revertir en ellos. Restaurar un BACPAC es el último recurso.
