# Pedidos por preparar {#app-preparar}

En **Pedidos por preparar** el bodeguero ve los pedidos que debe alistar, marca cada producto a medida que lo saca del estante y deja el pedido como preparado para que salga en la ruta.

**Quién la ve:** Bodeguero y Administrador (sección **Bodega**).

![Pantalla de inicio del bodeguero](capturas/app/app-inicio-bodega-01.png "ancho=6")

## Preparar un pedido {#app-preparar-preparar}

### Para qué sirve

Alistar la mercadería de cada pedido sin olvidar productos, y avisar que el pedido está listo.

### Quién puede usarlo

Bodeguero y Administrador.

### Pasos

1. En la pantalla de inicio, toque **Pedidos por preparar**.

   ![Lista de pedidos por preparar](capturas/app/app-preparar-01.png "ancho=6")

   1. **Por preparar**: los pedidos que esperan ser alistados, del más antiguo al más nuevo.
   2. **Preparados hoy**: los que ya marcó hoy.
   3. Toque un pedido para abrirlo. La tarjeta muestra el cliente, cuántos productos y unidades lleva, el tipo de entrega y la ruta, si ya tiene.

2. En **Productos a alistar**, toque cada producto cuando lo tenga listo. Se marca con un visto y el contador sube (por ejemplo, **2/2**).

   ![Productos marcados y botón Marcar como preparado](capturas/app/app-preparar-02.png "ancho=6")

   1. y 2. Toque cada producto. La línea muestra la cantidad a alistar (**×3**) y el **Stock actual**.
   3. Toque **Marcar como preparado**. El botón se activa solo cuando todos los productos están marcados.

### Resultado esperado

Aparece «Pedido #… marcado como preparado.». El pedido pasa a **Preparados hoy** y en el detalle se lee «Preparado por … el …».

> [!NOTA] Funciona sin señal: el pedido se marca en el teléfono y se envía al recuperar la conexión. Marcar como preparado no cambia el estado del pedido; solo deja constancia de quién lo alistó.

### Si algo sale mal

| Mensaje | Qué hacer |
|---|---|
| Todo preparado — No hay pedidos esperando preparación. | No hay pedidos pendientes. Deslice hacia abajo para actualizar. |
| Este pedido ya no se prepara: fue cancelado, rechazado, está retenido o ya se entregó. | El pedido cambió mientras lo alistaba. Devuelva la mercadería al estante y consulte a su jefatura. |
| El pedido no existe. | Actualice la lista. |
