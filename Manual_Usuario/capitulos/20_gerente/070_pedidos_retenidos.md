# Pedidos retenidos {#mod-pedidos-retenidos}

Cuando un vendedor registra en [Venta móvil](#mod-venta-movil) un pedido de más de **₡50 000**, el sistema no lo despacha de inmediato: lo deja **Retenido** hasta que el Gerente lo autorice. En esta pantalla el Gerente revisa cada pedido retenido y lo aprueba o lo rechaza.

Se abre con el botón **Pedidos retenidos** del menú superior.

> [!CONSEJO] También puede aprobar o rechazar estos pedidos desde el teléfono, en la aplicación móvil (vea [Pedidos retenidos en la aplicación](#app-retenidos)).

## Revisar un pedido retenido {#mod-pedidos-retenidos-revisar}

### Para qué sirve

Ver quién pidió, cuánto, qué productos y si hay inventario suficiente, antes de decidir si se autoriza la venta.

### Quién puede usarlo

Gerente y Administrador.

### Pasos

1. Abra **Pedidos retenidos**. Arriba a la derecha se ve cuántos pedidos están pendientes (recuadro).

   ![Lista de pedidos retenidos](capturas/gerente/ger-retenidos-01.png)

   1. Si busca uno en particular, escriba el cliente, el vendedor o el número de pedido y haga clic en la **lupa**.
   2. En la fila del pedido, haga clic en **Revisar**.

2. Revise la información del pedido:

   ![Página para revisar un pedido retenido](capturas/gerente/ger-retenidos-02.png)

   - **Información del pedido**: cliente, fecha, tipo de entrega, dirección y observaciones del vendedor.
   - **Productos del pedido**: cantidades, precios y total. La columna **Stock actual** (recuadro) muestra cuántas unidades hay hoy en la bodega: en verde si alcanzan; en rojo si no alcanzan.
   - A la derecha, las dos decisiones posibles: **Aprobar pedido** (tarjeta verde) y **Rechazar pedido** (tarjeta roja).

3. Decida y siga el procedimiento [Aprobar un pedido retenido](#mod-pedidos-retenidos-aprobar) o [Rechazar un pedido retenido](#mod-pedidos-retenidos-rechazar).

### Resultado esperado

Se abre **Revisar pedido #…** con el estado **Retenido**. Si algún producto no tiene inventario suficiente, debajo de la tabla aparece en rojo: «Uno o más productos no tienen stock suficiente. Si aprueba, el sistema re-validará el stock en tiempo real.».

### Si algo sale mal

| Mensaje | Qué hacer |
|---|---|
| Sin pedidos pendientes de revisión. No hay pedidos retenidos en este momento. | No tiene pedidos por revisar. |
| La búsqueda no devolvió resultados. | Revise lo escrito o haga clic en la **X** para limpiar la búsqueda. |
| Solo se pueden revisar pedidos en estado Retenido. | Otra persona ya aprobó o rechazó ese pedido. Vuelva a la lista. |
| No se encontró el pedido solicitado. | Vuelva a la lista y búsquelo de nuevo. |

## Aprobar un pedido retenido {#mod-pedidos-retenidos-aprobar}

### Para qué sirve

Autorizar la venta: el sistema descuenta el inventario y genera la factura en un solo paso.

### Quién puede usarlo

Gerente y Administrador.

### Pasos

1. En **Revisar pedido #…**, haga clic en **Aprobar y facturar** (tarjeta verde).
2. El sistema le pide confirmar. Haga clic en **Sí, aprobar**.

   ![Confirmación antes de aprobar y facturar](capturas/gerente/ger-retenidos-03.png "ancho=8")

### Resultado esperado

Se abre la página **Pedido aprobado y facturado** con el número de pedido, el estado **Liberado** y el número de la factura generada (recuadro).

![Pedido aprobado y factura generada](capturas/gerente/ger-retenidos-04.png)

- **Ver más retenidos** vuelve a la lista.
- **Ver detalle del pedido** (flecha) abre el pedido en [Pedidos](#mod-pedidos). La factura se consulta en [Facturación](#mod-facturacion).

El pedido sale de la lista de retenidos y sigue su camino normal: bodega lo prepara y logística lo asigna a una ruta.

> [!ADVERTENCIA] La aprobación es definitiva: descuenta el inventario y emite la factura. Revise bien el pedido antes de confirmar.

### Si algo sale mal

| Mensaje | Causa más común | Qué hacer |
|---|---|---|
| Stock insuficiente para uno o más productos al momento de aprobar. | Mientras usted revisaba, se vendió o se movió inventario de algún producto. | Revise la columna **Stock actual**. Si falta inventario, rechace el pedido con ese motivo para que el vendedor lo ajuste. |
| Solo se pueden aprobar pedidos en estado Retenido. | Otra persona ya aprobó o rechazó el pedido. | Vuelva a la lista: el pedido ya no debería aparecer. |
| El pedido ya tiene una factura asociada. | El pedido ya se facturó. | Revíselo en [Pedidos](#mod-pedidos) y en [Facturación](#mod-facturacion). |
| Error de concurrencia de stock. Intente nuevamente. | Otra venta tocó el mismo producto al mismo tiempo. | Vuelva a hacer clic en **Aprobar y facturar**. |
| No fue posible aprobar el pedido en este momento. | Falla de conexión con el sistema. | Vuelva a intentar. Si continúa, avise a Soporte. |

## Rechazar un pedido retenido {#mod-pedidos-retenidos-rechazar}

### Para qué sirve

No autorizar la venta (por ejemplo, por crédito insuficiente, producto agotado o pedido duplicado) y avisarle al vendedor por qué.

### Quién puede usarlo

Gerente y Administrador.

### Pasos

1. En **Revisar pedido #…**, use la tarjeta roja **Rechazar pedido**:

   ![Motivo del rechazo](capturas/gerente/ger-retenidos-05.png "ancho=8")

   1. En **Motivo de rechazo**, explique por qué no se aprueba. El vendedor leerá este texto.
   2. Haga clic en **Rechazar pedido**.

2. El sistema le pide confirmar con la ventana **Rechazar pedido #…**, que le recuerda que el vendedor verá el motivo y que el inventario no cambia. Haga clic en **Sí, rechazar** (o en **Cancelar** para no hacer nada).

   ![Confirmación antes de rechazar](capturas/gerente/ger-retenidos-06.png "ancho=8")

### Resultado esperado

Vuelve a la lista de retenidos con el mensaje «Pedido #60 rechazado correctamente.» (con el número que corresponda). El pedido queda **Rechazado**, el inventario no cambia y el vendedor ve el motivo en su lista de pedidos de [Venta móvil](#mod-venta-movil).

![Mensaje de pedido rechazado](capturas/gerente/ger-retenidos-08.png)

### Si algo sale mal

Si hace clic en **Rechazar pedido** sin escribir el motivo, no se abre ninguna ventana: el campo se marca en rojo con el mensaje «El motivo de rechazo es obligatorio.».

![Mensaje cuando falta el motivo](capturas/gerente/ger-retenidos-07.png "ancho=8")

| Mensaje | Qué hacer |
|---|---|
| El motivo de rechazo es obligatorio. | Escriba el motivo y vuelva a hacer clic en **Rechazar pedido**. |
| El motivo no puede superar 500 caracteres. | Resuma el motivo. |
| Solo se pueden rechazar pedidos en estado Retenido. | Otra persona ya resolvió el pedido. Vuelva a la lista. |
| No fue posible rechazar el pedido en este momento. | Falla de conexión con el sistema. Vuelva a intentar. Si continúa, avise a Soporte. |
