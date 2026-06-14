# Portal del cliente e historial de pedidos

## Implementado

- Nuevo portal protegido para usuarios con rol `Cliente`.
- Vista de resumen con informacion basica del cliente.
- Bloque de credito disponible cuando existe informacion de credito.
- Historial de pedidos del cliente autenticado.
- Detalle de pedido con estado, entrega, direccion, total y lineas de productos.
- Cancelacion segura de pedidos propios en estado `Pendiente` y sin factura asociada.
- Comprobante imprimible visible para el cliente cuando el pedido tiene factura asociada.
- Enlace `Mis pedidos` en la navegacion para clientes logueados.
- Enlace a `Mis pedidos` desde la confirmacion de compra.

## Reglas de cancelacion

- El pedido debe pertenecer al `UserId` de la sesion.
- El estado debe ser `Pendiente`.
- No debe existir una factura asociada en `Facturas`.
- La transicion permitida es unicamente `Pendiente` a `Cancelado`.
- No se devuelve stock, porque actualmente la creacion del pedido valida disponibilidad pero no descuenta inventario.
- La validacion principal vive en `dbo.sp_Client_CancelPendingOrder`.

## Reglas de comprobante

- El comprobante se consulta desde el portal usando `PedidoId`, no `FacturaId` directo en la URL cliente.
- La lectura valida `PedidoId` contra el `UserId` de la sesion antes de mostrar datos.
- Si no existe factura asociada o no pertenece al cliente autenticado, se muestra un mensaje generico y se redirige a `Mis pedidos`.
- La vista es imprimible desde navegador con `window.print()`.
- No se genera PDF en esta fase.
- El documento se muestra como comprobante del sistema y no sustituye una factura electronica oficial.

## Pruebas sugeridas

1. Iniciar sesion con un usuario cliente y abrir `ClientPortal/Index`.
2. Confirmar que solo aparecen pedidos del usuario autenticado.
3. Abrir el detalle de un pedido propio desde `Mis pedidos`.
4. Intentar acceder por URL directa a un pedido de otro usuario y confirmar que redirige a `Mis pedidos`.
5. Cancelar un pedido propio pendiente y confirmar que cambia a `Cancelado`.
6. Intentar cancelar un pedido propio no pendiente y confirmar que no cambia.
7. Intentar cancelar un pedido facturado y confirmar que no cambia.
8. Abrir el detalle de un pedido facturado propio y confirmar que aparece `Ver comprobante`.
9. Abrir el comprobante y confirmar que muestra numero, fecha, cliente, totales y lineas.
10. Usar el boton `Imprimir` y confirmar que abre la impresion del navegador.
11. Intentar acceder a `ClientPortal/Invoice/{PedidoId}` con un pedido de otro usuario y confirmar que redirige a `Mis pedidos`.
12. Crear un pedido desde carrito y usar el enlace `Mis pedidos` desde la confirmacion.
13. Iniciar sesion como administrador o empleado e intentar abrir `ClientPortal/Index`; debe redirigir a una vista segura.
