# Reversión de 0332

## Qué introduce

- `dbo.sp_Seller_CreateOrder`: aplica promociones (0331), guarda Subtotal, Impuesto y Total con IVA, retiene según el
  total con IVA y descuenta del inventario lo comprado más lo regalado.
- `dbo.sp_Manager_ApproveOrder`: descuenta lo comprado y regalado y factura las líneas del pedido tal cual.
- `dbo.sp_Seller_CotizarPedido` (nuevo).
- No cambia tablas ni datos.

## Revertir

1. Volver a crear `dbo.sp_Seller_CreateOrder` y `dbo.sp_Manager_ApproveOrder` con su definición de
   `0325_facturar_exige_identificacion.sql` (los dos `CREATE OR ALTER`). Con eso Venta móvil vuelve a no aplicar
   promociones y a guardar `Total = subtotal` (sin IVA): para quedar coherente, revertir también 0330.
2. `DROP PROCEDURE dbo.sp_Seller_CotizarPedido;`
3. Marcar `0332_venta_movil_promociones_iva` como `RolledBack` en `dbo.SchemaMigrationHistory`.

Los pedidos ya registrados con promociones conservan sus líneas, descuentos y regalías (son datos normales:
`PedidoDetalle`, `PromocionAplicaciones`, `MovimientosInventario`); no se revierten. Restaurar un BACPAC es el último recurso.
