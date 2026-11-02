# Rollback de 0255

## Qué introduce

- Reemplaza `dbo.sp_Seller_CreateOrder` con la definición de la 0230 y dos
  cambios: sin `@MetodoPago` el pedido se guarda como «Efectivo contra entrega»
  (antes «Crédito vendedor») y `@MetodoPago` acepta ese valor además de
  «Crédito». Así Liquidación de cobros cuenta esos pedidos como efectivo
  esperado.
- No modifica tablas ni pedidos ya registrados.

## Revertir

Reaplicar la definición de `sp_Seller_CreateOrder` de
`0230_cliente_estado_deuda.sql` (bloque «5. Venta móvil con pago a crédito»,
`CREATE OR ALTER`) y borrar la fila del ledger:

```sql
DELETE FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0255_venta_movil_pago_contra_entrega';
```

Los pedidos creados mientras la 0255 estuvo aplicada conservan «Efectivo contra
entrega». Si hiciera falta devolverlos al valor anterior (no recomendado:
Liquidación dejaría de contarlos como efectivo), identificarlos por
`CanalPedido` de venta móvil y la fecha de aplicación de la 0255 en el ledger.
