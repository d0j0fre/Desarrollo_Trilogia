# Pedidos (administración) {#mod-pedidos}

En esta pantalla se consultan todos los pedidos de los clientes (de la tienda en línea, de la venta móvil y de los vendedores) y se avanza su estado: aprobarlo, ponerlo en proceso, marcarlo como entregado o cancelarlo.

Se abre en **Administración → Ventas y Pedidos → Pedidos**.

## Buscar y consultar un pedido {#mod-pedidos-consultar}

### Para qué sirve

Ver qué pidió un cliente, cuánto paga, cómo lo paga y a dónde hay que entregarlo, por ejemplo para responder una llamada del cliente o preparar la entrega.

### Quién puede usarlo

Empleado y Administrador. Gerente, Bodeguero y Facturador solo consultan: en su pantalla, el cuadro **Actualizar estado** dice «Su perfil puede consultar el pedido, pero no cambiar su estado.».

### Pasos

1. Abra **Pedidos**. La lista muestra los pedidos más recientes primero.
2. Filtre y abra el pedido:

   ![Filtro por estado y lista de pedidos](capturas/empleado/emp-pedidos-01.png)

   1. En **Filtrar por estado**, elija el estado que busca, por ejemplo **Pendiente**.
   2. Haga clic en **Filtrar**. Para volver a ver todos, haga clic en **Limpiar filtro**.
   3. Haga clic en **Ver** en la fila del pedido.

### Resultado esperado

Se abre **Detalle del pedido #…** con la información general (cliente, fecha, estado actual, tipo de entrega, total, método y estado de pago, dirección y observaciones del cliente) y, más abajo, la lista de productos.

![Información general del pedido](capturas/empleado/emp-pedidos-02.png)

En **Detalle de productos**, la columna **Stock actual** muestra cuántas unidades hay hoy en bodega. Compárela con la **Cantidad** pedida antes de aprobar el pedido.

![Productos del pedido con el stock actual](capturas/empleado/emp-pedidos-06.png)

La columna **Estado** puede mostrar estos valores:

| Estado | Qué significa |
|---|---|
| Pendiente | El cliente hizo el pedido y nadie lo ha revisado. |
| Aprobado | Se revisó y se puede preparar. |
| En proceso | Se está preparando o va en camino. |
| Preparando | La bodega lo está alistando para una ruta (solo en pedidos antiguos). |
| En ruta | Va en el camión, con el chofer. |
| Entregado | El cliente lo recibió. |
| No entregado | El chofer no pudo entregarlo. |
| Retenido | Supera el monto permitido y espera la aprobación del Gerente (vea [Pedidos retenidos](#mod-pedidos-retenidos)). |
| Liberado / Rechazado | El Gerente aprobó o rechazó un pedido retenido. |
| Facturado | Ya tiene factura (solo en pedidos antiguos). |
| Cancelado | Se anuló. Los productos vuelven al inventario. |

> [!CONSEJO] La lista **Filtrar por estado** tiene todos estos estados. Por ejemplo, elija **No entregado** para ver los pedidos que el chofer no pudo entregar.

### Si algo sale mal

| Mensaje | Qué hacer |
|---|---|
| No se encontró el pedido solicitado. | El número no existe. Vuelva a la lista y búsquelo de nuevo. |
| No se encontraron pedidos registrados. | No hay pedidos con ese estado. Haga clic en **Limpiar filtro**. |

## Cambiar el estado de un pedido {#mod-pedidos-estado}

### Para qué sirve

Llevar el pedido por sus etapas para que el cliente, la bodega y el equipo de reparto sepan en qué va.

### Quién puede usarlo

Empleado y Administrador.

### Pasos

1. Abra el pedido (vea el procedimiento anterior).
2. En el cuadro **Actualizar estado**, a la derecha:

   ![Cuadro Actualizar estado](capturas/empleado/emp-pedidos-03.png "ancho=7")

   1. En **Nuevo estado**, elija el estado siguiente. La lista solo ofrece los cambios permitidos desde el estado actual.
   2. Haga clic en **Guardar estado**.

Un pedido solo puede avanzar un paso a la vez:

| Estado actual | Puede pasar a |
|---|---|
| Pendiente | Aprobado o Cancelado |
| Aprobado | En proceso o Cancelado |
| En proceso | Entregado o Cancelado |
| Entregado | Cancelado |
| Retenido | Liberado, Rechazado o Cancelado (lo normal es que el Gerente lo resuelva en [Pedidos retenidos](#mod-pedidos-retenidos)) |
| Liberado | En proceso o Cancelado |
| Con factura | Solo Entregado |
| En ruta, No entregado | Ninguno: los actualiza [Rutas y entregas](#mod-rutas) |
| Cancelado, Rechazado, Preparando, Facturado | Ninguno |

Si el pedido no tiene cambios posibles, el cuadro **Actualizar estado** no muestra la lista y explica por qué, por ejemplo «El estado «En ruta» lo actualiza Rutas y entregas.».

### Resultado esperado

Aparece el mensaje «Estado del pedido actualizado correctamente.» y el **Estado actual** del pedido cambia.

![Estado del pedido actualizado](capturas/empleado/emp-pedidos-04.png)

> [!ADVERTENCIA] Al cancelar un pedido que ya había descontado inventario, el sistema devuelve los productos a la bodega. Un pedido facturado no se puede cancelar: queda como **Entregado**. Si el cliente devuelve productos de un pedido facturado, use [Devoluciones](#mod-devoluciones).

> [!NOTA] Debajo está el cuadro **Facturación**. El Administrador ve ahí el botón **Generar factura** (vea [Facturación](#mod-facturacion)); para los demás puestos dice «Este pedido todavía no tiene factura. Su perfil no genera facturas.». Si el pedido ya tiene factura, quien tiene acceso a Facturación ve el botón **Ver factura**.

### Si algo sale mal

| Mensaje | Causa más común | Qué hacer |
|---|---|---|
| La transición de estado solicitada no es válida. | Otra persona cambió el pedido mientras usted lo tenía abierto. | Recargue la página: la lista **Nuevo estado** se actualiza con los cambios posibles. |
| El pedido facturado debe permanecer como Entregado. | El pedido ya tiene factura. | Solo puede marcarlo **Entregado**. Si el cliente devuelve productos, use [Devoluciones](#mod-devoluciones). |
| No se puede cambiar el estado de un pedido cancelado o rechazado. | El pedido ya se anuló. | No se puede reactivar. Registre un pedido nuevo si hace falta. |
| La solicitud para actualizar el estado no es válida. | La página quedó abierta mucho tiempo. | Recargue la página y vuelva a intentar. |
| No fue posible actualizar el estado del pedido en este momento. Intente nuevamente. | Falla temporal. | Vuelva a intentar. Si continúa, avise a Soporte. |
