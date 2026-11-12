# Pedidos {#app-pedidos}

**Pedidos** permite buscar cualquier pedido, ver su detalle (cliente, productos, factura, ruta) y, según el puesto, cambiar su estado.

**Quién la ve:** Empleado y Administrador (consulta y cambio de estado); Gerente y Facturador (solo consulta). Sección **Gestión**. En el sitio web, vea [Pedidos (administración)](#mod-pedidos).

## Buscar y consultar un pedido {#app-pedidos-consultar}

### Para qué sirve

Responder rápido a un cliente: en qué va su pedido, quién lo vendió, si ya se facturó o en qué ruta va.

### Quién puede usarlo

Empleado, Gerente, Facturador y Administrador.

### Pasos

1. En la pantalla de inicio, toque **Pedidos**.

   ![Lista de pedidos](capturas/app/app-pedidos-01.png "ancho=6")

   1. **Cliente, vendedor o número de pedido**: escriba para buscar.
   2. Filtros por estado: **Todos**, **Pendiente**, **Aprobado**, **En proceso**, **Retenido**, **Entregado**, **Cancelado**.
   3. Toque un pedido para ver su detalle.

### Resultado esperado

El detalle muestra el cliente, el estado, la fecha, el tipo de entrega, el vendedor, la factura, la ruta, quién lo preparó, la dirección, los productos y el total. Con **Llamar a …** se marca al cliente.

## Cambiar el estado de un pedido {#app-pedidos-estado}

### Para qué sirve

Avanzar el pedido en su proceso (aprobado, en proceso, entregado) o cancelarlo.

### Quién puede usarlo

Empleado y Administrador.

### Pasos

1. Abra el pedido y baje hasta **Cambiar estado**:

   ![Botones para cambiar el estado](capturas/app/app-pedidos-02.png "ancho=6")

   1. Toque el estado siguiente, por ejemplo **Marcar como entregado**.
   2. O toque **Cancelar pedido**. Si el pedido ya había descontado inventario, se devuelve.

2. Confirme en el mensaje con **Confirmar**.

### Resultado esperado

Aparece «Pedido #…: (nuevo estado).». Solo se ofrecen los pasos permitidos:

| Estado actual | Puede pasar a |
|---|---|
| Pendiente | Aprobado o Cancelado |
| Aprobado | En proceso o Cancelado |
| En proceso | Entregado o Cancelado |
| Liberado | En proceso o Cancelado |
| Facturado | Solo Entregado |

Los pedidos **Retenidos** se resuelven en [Pedidos retenidos](#app-retenidos).

### Si algo sale mal

| Mensaje | Qué hacer |
|---|---|
| Un pedido facturado solo puede marcarse como entregado. | Use **Marcar como entregado**. |
| Un pedido en estado … no puede pasar a …. | Ese cambio no está permitido. Revise la tabla. |
| No se puede cambiar el estado de un pedido cancelado o rechazado. | El pedido está cerrado. |
