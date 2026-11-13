# Mis pedidos {#mod-mis-pedidos}

**Mis pedidos** es su portal de cliente: sus datos, su crédito, el historial de pedidos y sus facturas. Desde aquí puede ver el detalle de cada pedido, cancelar uno que todavía no se procesa y descargar sus facturas.

## Consultar mis pedidos y facturas {#mod-mis-pedidos-consultar}

### Para qué sirve

Saber en qué va cada compra y tener a mano sus comprobantes.

### Quién puede usarlo

Clientes con sesión iniciada. Cada cliente ve solo lo suyo.

### Pasos

1. En el menú superior, haga clic en **Mi cuenta** y elija una opción:

   ![Menú Mi cuenta](capturas/cliente/cli-pedidos-01.png "ancho=8")

   1. **Mis pedidos**: este portal.
   2. **Mis garantías**: sus solicitudes de garantía (vea [Mis garantías](#mod-mis-garantias)).
   3. **Estado de cuenta**: su saldo y sus movimientos (vea [Estado de cuenta](#mod-estado-cuenta)).

2. En **Mis pedidos**, revise su información:

   ![Portal del cliente (ejemplo de un cliente mayorista con crédito)](capturas/cliente/cli-pedidos-02.png)

   1. **Crédito**: el crédito disponible, el límite y la deuda. **Ver estado de cuenta** abre el detalle.
   2. **Pedidos recientes**: **Ver detalle** abre un pedido.
   3. **Mis facturas**: **Ver factura** abre el comprobante.

### Resultado esperado

La columna **Estado** indica en qué va cada pedido:

| Estado | Qué significa |
|---|---|
| Pendiente | Recibido. La empresa todavía no lo procesa. Puede cancelarlo. |
| Aprobado / En proceso | La empresa lo aprobó y lo está preparando. |
| En ruta | Va en camino con el repartidor. |
| Entregado | Se entregó. Puede pedir garantía de sus productos. |
| Cancelado | Se canceló (por usted o por la empresa). |
| Retenido / Liberado / Rechazado | Pedidos grandes tomados por un vendedor que necesitan aprobación del gerente. |

> [!NOTA] Si no tiene crédito, la tarjeta **Crédito** dice «No hay crédito asignado actualmente.».

### Si algo sale mal

| Mensaje | Qué hacer |
|---|---|
| No fue posible cargar la información del cliente. | Falla momentánea. Recargue la página. |
| No fue posible cargar el pedido solicitado. | El pedido no es de su cuenta o ya no existe. Vuelva a **Mis pedidos**. |

## Cancelar un pedido pendiente {#mod-mis-pedidos-cancelar}

### Para qué sirve

Anular un pedido que ya no quiere, antes de que la empresa lo procese.

### Quién puede usarlo

Clientes, solo para pedidos en estado **Pendiente** y sin factura.

### Pasos

1. En **Mis pedidos**, haga clic en **Ver detalle** del pedido.
2. Haga clic en **Cancelar pedido**.

   ![Detalle de un pedido pendiente con el botón Cancelar pedido](capturas/cliente/cli-pedidos-03.png)

3. Confirme con **Sí, cancelar**. **Volver** cierra el mensaje sin cancelar.

   ![Confirmación para cancelar el pedido](capturas/cliente/cli-pedidos-04.png "ancho=9")

### Resultado esperado

Aparece «Pedido cancelado correctamente.» y el pedido pasa a **Cancelado**.

![Pedido cancelado](capturas/cliente/cli-pedidos-05.png)

### Si algo sale mal

| Mensaje | Qué hacer |
|---|---|
| Este pedido no permite cancelación desde el portal. | El pedido ya se procesó o ya tiene factura. Comuníquese con la empresa (vea los datos de contacto al pie de la página). |
| No fue posible cancelar el pedido solicitado. | El pedido cambió de estado mientras tanto. Recargue la página. |

## Ver y descargar una factura {#mod-mis-pedidos-factura}

### Para qué sirve

Tener el comprobante de su compra, en pantalla, impreso o en PDF.

### Quién puede usarlo

Clientes, para los pedidos que ya tienen factura.

### Pasos

1. En **Mis pedidos**, en la tabla **Mis facturas**, haga clic en **Ver factura**. También puede usar **Ver comprobante** en el detalle de un pedido.
2. En el **Comprobante de pedido**:

   ![Comprobante de un pedido](capturas/cliente/cli-pedidos-06.png)

   1. **Descargar factura (PDF)**: guarda el archivo `factura-FAC-….pdf` en su equipo.
   2. **Imprimir**: imprime el comprobante.

### Resultado esperado

Se descarga el PDF con el número de factura, la fecha, los productos, el subtotal, el impuesto y el total.

> [!NOTA] El comprobante del portal no sustituye una factura electrónica oficial.

### Si algo sale mal

| Mensaje | Qué hacer |
|---|---|
| No fue posible cargar el comprobante solicitado. | El pedido todavía no tiene factura. |
| No fue posible descargar la factura en este momento. Intente nuevamente. | Falla momentánea. Intente de nuevo. |
