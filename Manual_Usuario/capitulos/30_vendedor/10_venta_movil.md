# Venta móvil (web) {#mod-venta-movil}

En **Venta móvil** usted registra los pedidos que le hacen los clientes durante la visita. El sistema descuenta el inventario, genera la factura y le permite enviar el comprobante al cliente por WhatsApp. Si se queda sin Internet, el pedido se guarda en el navegador y se envía cuando vuelve la señal.

## Registrar un pedido {#mod-venta-movil-registrar}

### Para qué sirve

Dejar en firme el pedido de un cliente: qué productos lleva, cómo se le entrega y cómo paga. Si el pedido es menor de ₡50 000, la factura se genera sola.

### Quién puede usarlo

Vendedor, Empleado y Administrador.

### Pasos

1. En el menú superior, haga clic en **Venta móvil**.

   ![Botón Venta móvil en el menú](capturas/vendedor/ven-venta-01.png)

2. En el cuadro **Cliente y entrega**, complete los datos del pedido:

   ![Datos del cliente y de la entrega](capturas/vendedor/ven-venta-02.png "ancho=8")

   1. **Cliente**: elija el cliente de la lista. La lista muestra el nombre y el correo de cada cliente activo.
   2. **Identificación del cliente**: escriba la cédula física o jurídica, sin guiones. Es opcional, pero si la escribe debe ser válida.
   3. **Tipo de entrega**: **Entrega por vendedor**, **Retiro en local** o **Envío a domicilio**.
   4. **Forma de pago**: **Pago contra entrega** o **Crédito del cliente** (vea [Registrar un pedido a crédito](#mod-venta-movil-credito)).
   5. **Dirección de entrega**: se llena sola con la dirección del cliente. Corríjala si la entrega es en otro lugar.
   6. **Observaciones**: una nota para bodega o facturación. Es opcional.

3. En **Productos disponibles**, escriba la cantidad de cada producto:

   ![Búsqueda de productos y cantidad](capturas/vendedor/ven-venta-03.png)

   1. **Buscar producto o categoría**: escriba parte del nombre o de la categoría (por ejemplo, «cerveza») para ver solo esos productos.
   2. **Cantidad**: escriba cuántas unidades lleva el cliente. No puede pasar del **Stock** que muestra la tarjeta. Los productos con cantidad 0 no se guardan.

4. Puede aparecer una sugerencia de otro producto. Agréguelo o descártelo (vea [Usar la sugerencia de producto complementario](#mod-venta-movil-sugerencia)).
5. Revise la barra inferior y haga clic en **Registrar pedido**.

   ![Total del pedido y botón Registrar pedido](capturas/vendedor/ven-venta-05.png)

   La barra muestra cuántos productos y unidades eligió y el **Total estimado** (sin impuesto).

### Resultado esperado

Aparece la pantalla **¡Pedido registrado con éxito!** con el número de pedido y el número de la factura generada. El pedido queda en estado **Pendiente** para que bodega lo prepare.

![Pedido registrado con su factura](capturas/vendedor/ven-venta-06.png)

Desde esta pantalla puede:

- Enviar el comprobante por WhatsApp (vea [Enviar el comprobante por WhatsApp](#mod-venta-movil-whatsapp)).
- **Nuevo pedido**: vuelve al formulario vacío.
- **Mis pedidos**: abre la lista de sus pedidos.
- **Ver detalle completo**: abre el detalle del pedido (vea [Consultar mis pedidos](#mod-venta-movil-mis-pedidos)).

> [!NOTA] El sistema también envía la factura al correo del cliente. Si el envío falla, aparece arriba el aviso «El pedido se registró y facturó, pero no fue posible enviar el comprobante por correo.». El pedido y la factura quedaron bien: envíe el comprobante por WhatsApp.

> [!ADVERTENCIA] Si el pedido supera ₡50 000 antes de impuestos, no se factura de inmediato: pasa a revisión del gerente (vea [Pedidos que pasan a revisión del gerente](#mod-venta-movil-retenido)).

### Si algo sale mal

| Mensaje | Causa más común | Qué hacer |
|---|---|---|
| Debe seleccionar al menos un producto para registrar el pedido. | Todas las cantidades están en 0. | Escriba la cantidad de al menos un producto. |
| Debe seleccionar un cliente. | No eligió el cliente. | Elija el cliente en la lista **Cliente**. |
| La dirección de entrega es obligatoria. | El cliente no tiene dirección registrada y el campo quedó vacío. | Escriba la dirección o las señas de entrega. |
| La identificación debe ser una cédula costarricense, jurídica, DIMEX o NITE válida. | La identificación tiene un formato que no existe. | Escriba la cédula completa: física de 9 dígitos, jurídica de 10 (empieza con 3), DIMEX de 11 o 12. O deje el campo vacío. |
| No fue posible registrar el pedido en este momento. Intente nuevamente. | Otro vendedor se llevó el stock de un producto, o un producto dejó de estar disponible. | Recargue la página para ver el stock actualizado y registre el pedido de nuevo. |

## Usar la sugerencia de producto complementario {#mod-venta-movil-sugerencia}

### Para qué sirve

Ofrecerle al cliente un producto que suele comprarse junto con lo que ya pidió, para aumentar la venta.

### Quién puede usarlo

Vendedor, Empleado y Administrador.

### Pasos

1. Elija el cliente y escriba la cantidad de un producto, como en [Registrar un pedido](#mod-venta-movil-registrar).
2. Espere un segundo. En la esquina de la pantalla aparece el cuadro **Sugerencia para este pedido**:

   ![Sugerencia de producto complementario](capturas/vendedor/ven-venta-04.png "ancho=9")

   1. El motivo de la sugerencia, por ejemplo «Se compró junto con productos de este pedido en 4 pedidos.».
   2. **Agregar 1**: agrega una unidad del producto sugerido al pedido. Luego puede cambiar la cantidad en su tarjeta.
   3. **No, gracias**: cierra la sugerencia. No vuelve a aparecer en este pedido.

### Resultado esperado

Si eligió **Agregar 1**, el producto sugerido aparece con cantidad 1 y el total de la barra inferior se actualiza.

> [!NOTA] La sugerencia solo aparece con Internet. Siempre es de un producto con stock y de una categoría que el pedido todavía no tiene. Nada se agrega sin que usted lo elija.

## Registrar un pedido a crédito {#mod-venta-movil-credito}

### Para qué sirve

Venderle a un cliente que paga después, dentro del límite de crédito que le aprobó el departamento de Crédito y Cobro.

### Quién puede usarlo

Vendedor, Empleado y Administrador. El cliente debe tener crédito activo, sin pagos vencidos y con disponible suficiente.

### Pasos

1. Llene el pedido como en [Registrar un pedido](#mod-venta-movil-registrar).
2. En **Forma de pago**, elija **Crédito del cliente**.

   ![Forma de pago Crédito del cliente](capturas/vendedor/ven-venta-07.png "ancho=10")

3. Haga clic en **Registrar pedido**.

### Resultado esperado

El pedido se registra igual que uno de contado y el monto de la factura se carga al crédito del cliente. El cliente lo ve en su **Estado de cuenta** (vea [Estado de cuenta](#mod-estado-cuenta)).

> [!ADVERTENCIA] El crédito se revisa en el momento de registrar: necesita Internet. Un pedido a crédito no se puede guardar sin conexión.

### Si algo sale mal

El mensaje aparece arriba del formulario, en rojo:

![Mensaje cuando el cliente no tiene crédito](capturas/vendedor/ven-venta-08.png)

| Mensaje | Qué hacer |
|---|---|
| El cliente no tiene crédito activo. Registre el pedido con pago contra entrega. | Cambie la forma de pago a **Pago contra entrega**. Si el cliente necesita crédito, pídale que lo solicite a Crédito y Cobro. |
| El crédito del cliente está bloqueado. No se puede registrar el pedido a crédito. | Registre el pedido contra entrega y avise a Crédito y Cobro. |
| El cliente tiene pagos vencidos pendientes. Debe ponerse al día antes de comprar a crédito. | El cliente debe pagar lo vencido. Mientras tanto, registre el pedido contra entrega. |
| El pedido supera el crédito disponible del cliente (se superó el límite de crédito). | Baje las cantidades o registre el pedido contra entrega. |
| El pago a crédito necesita conexión para validar el crédito del cliente. | Lo intentó sin Internet. Espere a tener señal o use **Pago contra entrega**. |

## Enviar el comprobante por WhatsApp {#mod-venta-movil-whatsapp}

### Para qué sirve

Que el cliente reciba en su teléfono el número de factura y el total de su compra, sin imprimir nada.

### Quién puede usarlo

Vendedor, Empleado y Administrador.

### Pasos

1. Registre el pedido. En la pantalla **¡Pedido registrado con éxito!**, haga clic en **Enviar comprobante por WhatsApp** (vea la imagen de [Registrar un pedido](#mod-venta-movil-registrar)).
2. Se abre WhatsApp (en el teléfono o en WhatsApp Web) con el chat del cliente y este mensaje ya escrito: «Licorera La Bodega: su comprobante FAC-… del dd/mm/aaaa por un total de ₡…. Gracias por su compra.».
3. Toque o haga clic en **Enviar** dentro de WhatsApp.

### Resultado esperado

El cliente recibe el mensaje con su comprobante.

### Si algo sale mal

| Mensaje | Qué hacer |
|---|---|
| El cliente no tiene un número de teléfono registrado para enviar el comprobante por WhatsApp. | El cliente no tiene teléfono en el sistema. Pídale el número y solicite a administración que lo agregue a su ficha. La factura igual se envió por correo. |
| No aparece el botón de WhatsApp | El pedido pasó a revisión del gerente y todavía no tiene factura. El botón aparece solo cuando hay factura. |

## Pedidos que pasan a revisión del gerente {#mod-venta-movil-retenido}

### Para qué sirve

Los pedidos grandes (más de ₡50 000 antes de impuestos) necesitan la aprobación de un gerente antes de prepararse. Así se controlan las ventas de alto monto.

### Quién puede usarlo

Vendedor, Empleado y Administrador registran el pedido; el Gerente lo aprueba o lo rechaza (vea [Pedidos retenidos](#mod-pedidos-retenidos)).

### Pasos

1. Registre el pedido normalmente. Si supera el monto, en lugar de la factura aparece la pantalla **Pedido enviado a revisión**:

   ![Pedido enviado a revisión del gerente](capturas/vendedor/ven-venta-09.png)

2. Avise al cliente que su pedido está en revisión.
3. Siga el estado en **Mis pedidos** (vea [Consultar mis pedidos](#mod-venta-movil-mis-pedidos)).

### Resultado esperado

El pedido queda en estado **Retenido**. El inventario todavía no se descuenta. Cuando el gerente lo aprueba pasa a **Liberado** y sigue su proceso; si lo rechaza, pasa a **Rechazado** y en **Mis pedidos** aparece el motivo.

## Trabajar sin conexión {#mod-venta-movil-sin-conexion}

### Para qué sirve

No perder un pedido cuando el teléfono o la tableta se quedan sin Internet en la visita.

### Quién puede usarlo

Vendedor, Empleado y Administrador.

### Pasos

1. Abra **Venta móvil** mientras tiene Internet. Así el navegador guarda la lista de clientes y productos.
2. Si se pierde la señal, el indicador de arriba cambia de **En línea** a **Sin conexión**. Llene el pedido como siempre, con **Pago contra entrega**.
3. Haga clic en **Guardar sin conexión** (o en **Registrar pedido**: sin señal, también lo guarda en el navegador).

   ![Pedido guardado sin conexión](capturas/vendedor/ven-venta-10.png)

   1. **Ver pendientes**: muestra los pedidos guardados en este navegador.
   2. **Sincronizar ahora**: los envía al sistema cuando hay Internet.

4. Al recuperar la señal, el sistema envía los pedidos solo. Si no se enviaron, haga clic en **Sincronizar ahora**.

### Resultado esperado

Al guardar sin conexión aparece «Pedido guardado sin conexión. Quedó pendiente para sincronizar.» y el contador **Pendientes** sube.

Al sincronizar aparece «Todos los pedidos offline fueron sincronizados correctamente.» y cada pedido cambia a **Sincronizado**, con el botón **Ver pedido**:

![Pedidos sincronizados](capturas/vendedor/ven-venta-11.png)

Cada pedido guardado tiene un estado:

| Estado | Qué significa |
|---|---|
| Pendiente | Está guardado en el navegador y todavía no llega al sistema. |
| Sincronizando | Se está enviando. |
| Sincronizado | Ya está en el sistema. Puede quitarlo de la lista con **Limpiar sincronizados**. |
| Error | No se pudo enviar. El motivo aparece en rojo. Corrija lo necesario y sincronice de nuevo, o use **Descartar borrador**. |

> [!ADVERTENCIA] Los pedidos sin conexión se guardan **solo en ese navegador y ese equipo**, a nombre de su usuario, y se conservan 24 horas. **Cerrar sesión** los borra. Sincronice los pedidos y espere a que digan **Sincronizado** antes de cerrar sesión, borrar el historial o cambiar de equipo.

> [!NOTA] Al sincronizar, el sistema revisa el stock del momento. Si un producto se agotó mientras tanto, el pedido queda en **Error**.

### Si algo sale mal

| Mensaje | Qué hacer |
|---|---|
| No hay conexión. Los pedidos seguirán guardados localmente. | Hizo clic en **Sincronizar ahora** sin señal. Espere a tener Internet. |
| Algunos pedidos no se pudieron sincronizar. Revise la lista de pendientes. | Abra **Ver pendientes** y lea el error de cada pedido. |
| La sesión no está activa. Inicie sesión nuevamente para sincronizar pedidos offline. | Su sesión venció. Inicie sesión de nuevo **en el mismo navegador** y sincronice. |
| Error de conexión al sincronizar. Intente nuevamente. | La señal se cortó durante el envío. Vuelva a sincronizar. |
| Los pedidos pendientes ya no aparecen | Pasaron más de 24 horas o cerró sesión: en esos casos se borran. Si entró con otro usuario, cada uno ve solo los suyos. Registre el pedido de nuevo. |
| No fue posible sincronizar el pedido offline en este momento. Intente nuevamente. | Probablemente falta stock. Revise las cantidades con el cliente; si no se puede, descarte el borrador y registre un pedido nuevo. |
| Debe seleccionar un cliente. / Debe indicar la dirección de entrega. / Debe seleccionar al menos un producto. | Falta un dato del pedido. Complételo antes de guardarlo. |
| La cantidad de … supera el stock disponible. | Baje la cantidad de ese producto. |

## Consultar mis pedidos {#mod-venta-movil-mis-pedidos}

### Para qué sirve

Ver en qué va cada pedido que usted registró: si ya se facturó, si está en revisión o si el gerente lo rechazó.

### Quién puede usarlo

Vendedor, Empleado y Administrador. Cada persona ve solo sus pedidos (los 20 más recientes).

### Pasos

1. En **Venta móvil**, haga clic en **Ver pedidos** (arriba a la derecha).
2. Revise la tabla:

   ![Lista de mis pedidos](capturas/vendedor/ven-venta-12.png)

   - La columna **Estado** muestra en qué va cada pedido. En la imagen se marca un pedido **Retenido**, que espera la revisión del gerente.
   - El botón del ojo, a la derecha de cada fila, abre el detalle del pedido.

3. En el detalle, revise los datos del pedido:

   ![Detalle de un pedido del vendedor](capturas/vendedor/ven-venta-13.png)

   1. **Información general**: el cliente, la fecha, el estado, el tipo de entrega, el total, el método y el estado de pago, la dirección y las observaciones.
   2. **Facturación**: el número de la factura, si ya se generó.
   3. **Detalle de productos**: cada producto con su cantidad, precio y subtotal.
   4. **Volver a mis pedidos**: regresa a la lista.


### Resultado esperado

El detalle es de solo consulta: el vendedor no cambia el estado ni factura desde ahí. El Empleado y el Administrador abren el detalle de la administración de pedidos, con esas opciones (vea [Pedidos (administración)](#mod-pedidos)).

| Estado | Qué significa |
|---|---|
| Pendiente | Registrado y facturado; bodega lo va a preparar. |
| Retenido | Supera el monto de autorización. Espera la revisión del gerente. |
| Liberado | El gerente lo aprobó. |
| Rechazado | El gerente lo rechazó. Debajo aparece **Motivo de rechazo**. |
| En proceso | La empresa lo está preparando. |
| En ruta | Va en camino con el chofer. |
| Entregado | Ya se entregó. |
| No entregado | El chofer no pudo entregarlo. Logística decide qué hacer. |
