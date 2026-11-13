# Facturación {#mod-facturacion}

En Facturación se consultan todas las facturas generadas, el resumen de ventas, los productos más vendidos y las ventas de cada mes. Desde el detalle de una factura se imprime el comprobante o se le vuelve a enviar al cliente.

Se abre en **Administración → Ventas y Pedidos → Facturación**.

> [!NOTA] Las facturas se generan desde el pedido, con el botón **Generar factura** del detalle del pedido (vea [Pedidos](#mod-pedidos)). Esta pantalla es para consultarlas y reenviarlas.

## Consultar las facturas {#mod-facturacion-consultar}

### Para qué sirve

Revisar cuánto se ha facturado, buscar la factura de un pedido y ver su estado.

### Quién puede usarlo

Administrador. El Empleado y el Facturador las consultan en **Administración → Ventas y Pedidos → Facturación** o en la aplicación móvil (vea [Facturas](#app-facturas)).

### Pasos

1. Abra **Administración → Ventas y Pedidos → Facturación**.

   ![Resumen de ventas y lista de facturas](capturas/administrador/adm-facturacion-01.png)

   1. **Tarjetas**: **Ventas totales**, **Ventas mes actual**, **Total facturas** y **Pedidos entregados**.
   2. **Estado** de cada factura: **Generada** (comprobante emitido) o **Pendiente** (comprobante todavía sin completar).
   3. Haga clic en **Ver** para abrir el detalle de la factura.

2. Para ver más información, baje en la página: **Productos más vendidos** (con la cantidad y el monto) y **Ventas por mes**.

### Resultado esperado

La tabla **Facturas generadas** muestra las facturas de la más reciente a la más antigua, con su número, el pedido, el cliente, la fecha, el subtotal, el impuesto y el total.

> [!CONSEJO] Para encontrar la factura de un cliente, use la tecla **Ctrl + F** del navegador y escriba su nombre o el número de pedido. También puede abrirla desde el detalle del cliente (vea [Ver el detalle de un cliente](#mod-clientes-detalle)).

## Imprimir o reenviar un comprobante {#mod-facturacion-reenviar}

### Para qué sirve

Entregar al cliente una copia de su factura: impresa, por correo electrónico o por WhatsApp.

### Quién puede usarlo

Administrador.

### Pasos

1. En la lista de facturas, haga clic en **Ver**.
2. En el recuadro **Acciones**, elija cómo entregar el comprobante:

   ![Detalle de factura con sus acciones](capturas/administrador/adm-facturacion-02.png)

   1. **Imprimir factura**: abre la ventana de impresión del navegador. Elija la impresora (o **Guardar como PDF**) y haga clic en **Imprimir**.
   2. **Reenviar comprobante por correo**: envía el comprobante al **Correo** del cliente que aparece en la factura.
   3. **Enviar comprobante por WhatsApp**: abre WhatsApp con un mensaje ya escrito para el teléfono del cliente, con el número de factura, la fecha y el total. Revise el mensaje y envíelo desde WhatsApp.

### Resultado esperado

- Al reenviar por correo aparece el mensaje «Comprobante reenviado correctamente al correo del cliente.» y el envío queda registrado en [Auditoría](#mod-auditoria).
- Al imprimir, la página sale sin el menú del sitio, solo con los datos de la factura y el detalle de líneas.

> [!ADVERTENCIA] Antes de reenviar, revise que el **Correo** y el **Teléfono** del cliente estén bien. Si no, corríjalos en [Clientes](#mod-clientes-editar) y vuelva a abrir la factura.

### Si algo sale mal

![Mensaje cuando no se pudo reenviar el comprobante](capturas/administrador/adm-facturacion-03.png)

| Mensaje | Causa más común | Qué hacer |
|---|---|---|
| No fue posible reenviar el comprobante. Intente nuevamente. | El cliente no tiene correo, o el servicio de correo del sistema no está disponible. | Revise el correo del cliente. Si está bien, intente más tarde o envíe el comprobante por WhatsApp o impreso. Si continúa, avise a Soporte. |
| No se encontro la factura solicitada. | La factura no existe (por ejemplo, un enlace viejo). | Vuelva a la lista de facturas y ábrala desde **Ver**. |
| El botón de WhatsApp no aparece. | El cliente no tiene teléfono registrado. | Agregue el teléfono en [Clientes](#mod-clientes-editar). |
