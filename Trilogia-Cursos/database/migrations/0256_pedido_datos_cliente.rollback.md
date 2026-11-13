# Rollback de 0256

## Qué introduce

- Tabla `dbo.PedidoDatosCliente` (un registro por pedido de la Tienda: tipo de
  cliente, factura electrónica, correo del comprobante y dos teléfonos).
- `dbo.sp_Pedido_GuardarDatosCliente` (la llama la Tienda después de crear el
  pedido; solo para pedidos del mismo cliente) y `dbo.sp_Pedido_GetDatosCliente`
  (lectura para el detalle del pedido).
- No modifica `dbo.Pedidos` ni `sp_Store_CreateOrderWithPromotions`.

## Revertir

La tabla guarda datos de clientes: exportarla antes si se necesitan para
facturar. Luego:

```sql
BEGIN TRANSACTION;
DROP PROCEDURE IF EXISTS dbo.sp_Pedido_GetDatosCliente;
DROP PROCEDURE IF EXISTS dbo.sp_Pedido_GuardarDatosCliente;
DROP TABLE IF EXISTS dbo.PedidoDatosCliente;
DELETE FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0256_pedido_datos_cliente';
COMMIT TRANSACTION;
```

Sin estos objetos la Tienda sigue creando pedidos: el guardado de los datos del
cliente falla, queda en el log y la compra continúa; el detalle del pedido no
muestra la sección «Datos del cliente».
