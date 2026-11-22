# Reversión de 0320

Aditiva y sin tablas ni datos nuevos.

1. Quitar el aviso: `DROP TRIGGER dbo.TR_Pedidos_NotificarRechazoVendedor;`
2. Devolver `dbo.sp_Seller_GetMyOrders` a la versión de `database/cu102_sp_seller_get_my_orders.sql` (ejecutar su `CREATE OR ALTER`). Con eso Mis pedidos vuelve a mostrar el motivo de rechazo vacío.
3. Marcar la fila `0320_pedido_rechazado_aviso_vendedor` como `RolledBack` en `dbo.SchemaMigrationHistory`.

Los avisos ya creados quedan en `dbo.NotificacionesUsuario` (tipo `PedidoRechazado`); no estorban. Restaurar un BACPAC es el último recurso.
