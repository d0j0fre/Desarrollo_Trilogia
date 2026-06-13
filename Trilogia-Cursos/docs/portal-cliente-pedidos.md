# Portal del cliente e historial de pedidos

## Implementado

- Nuevo portal protegido para usuarios con rol `Cliente`.
- Vista de resumen con informacion basica del cliente.
- Bloque de credito disponible cuando existe informacion de credito.
- Historial de pedidos del cliente autenticado.
- Detalle de pedido con estado, entrega, direccion, total y lineas de productos.
- Enlace `Mis pedidos` en la navegacion para clientes logueados.
- Enlace a `Mis pedidos` desde la confirmacion de compra.

## Limitaciones

- La cancelacion de pedidos no se implemento en esta fase.
- Motivo: aunque se puede validar que el pedido pertenece al usuario y esta en estado `Pendiente`, el servicio actual no expone una validacion segura de factura asociada por `PedidoId`.
- Recomendacion: agregar un procedimiento dedicado de cancelacion de cliente que valide pertenencia, estado pendiente, ausencia de factura y transicion unica a `Cancelado`.

## Pruebas sugeridas

1. Iniciar sesion con un usuario cliente y abrir `ClientPortal/Index`.
2. Confirmar que solo aparecen pedidos del usuario autenticado.
3. Abrir el detalle de un pedido propio desde `Mis pedidos`.
4. Intentar acceder por URL directa a un pedido de otro usuario y confirmar que redirige a `Mis pedidos`.
5. Crear un pedido desde carrito y usar el enlace `Mis pedidos` desde la confirmacion.
6. Iniciar sesion como administrador o empleado e intentar abrir `ClientPortal/Index`; debe redirigir a una vista segura.
