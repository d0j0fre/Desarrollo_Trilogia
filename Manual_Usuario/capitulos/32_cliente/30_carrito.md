# Carrito y pago {#mod-carrito}

En el **Carrito** usted revisa los productos que eligió, ajusta las cantidades, ve las promociones que se aplican y finaliza la compra con los datos de entrega y la forma de pago.

## Revisar y ajustar el carrito {#mod-carrito-revisar}

### Para qué sirve

Confirmar qué lleva, cuántas unidades y cuánto va a pagar antes de hacer el pedido.

### Quién puede usarlo

Cualquier visitante puede armar el carrito. Para finalizar la compra debe iniciar sesión con su cuenta de cliente.

### Pasos

1. En el menú superior, haga clic en **Carrito**.
2. Revise cada producto:

   ![Carrito con dos productos](capturas/cliente/cli-carrito-01.png)

   1. **Cantidad**: use **−** y **+**, o escriba el número y haga clic en **Actualizar**. No puede pasar de las unidades disponibles.
   2. **Quitar**: saca el producto del carrito. Confirme con **Quitar** en el mensaje que aparece.
   3. **Finalizar compra**: pasa al pago (vea [Finalizar la compra](#mod-carrito-pagar)). **Seguir comprando** vuelve a la tienda.

3. Debajo del carrito, en **También podría interesarte**, el sistema le sugiere productos que otros clientes compran junto con los suyos. Para agregar uno, haga clic en **Agregar al carrito**.

   ![Sugerencias de productos](capturas/cliente/cli-carrito-02.png)

### Resultado esperado

El **Resumen** de la derecha muestra el total de productos, las líneas del pedido y el **Total**. Las sugerencias nunca se agregan solas.

> [!NOTA] El carrito se guarda mientras su sesión está abierta, aunque inicie sesión después de armarlo. Si cierra sesión o pasan 45 minutos sin usar el sitio, el carrito se vacía.

### Si algo sale mal

| Mensaje | Qué hacer |
|---|---|
| Su carrito está vacío | No hay productos. Haga clic en **Ir a la tienda**. |
| Un producto del carrito ya no está disponible. | El producto se dejó de vender y se quitó del carrito. Elija otro. |
| No hay stock suficiente para completar el pedido: … El carrito se ajustó a las unidades disponibles; revíselo antes de finalizar la compra. | Otro cliente compró unidades mientras tanto. Revise las cantidades nuevas y finalice la compra otra vez. |

## Promociones en el carrito: minorista y mayorista {#mod-carrito-promociones}

### Para qué sirve

Las promociones vigentes se aplican solas cuando el pedido cumple la condición (por ejemplo, una cantidad mínima). Algunas son solo para clientes mayoristas.

### Quién puede usarlo

Todos los clientes. Las promociones **para mayoristas** solo se aplican a las cuentas de tipo mayorista (vea [Clientes minoristas y mayoristas](#puesto-cliente-tipos)).

### Pasos

1. Agregue los productos de la promoción con la cantidad necesaria.
2. Revise el carrito: el sistema aplica la promoción sin que usted haga nada más.

### Resultado esperado

Con el mismo carrito (12 six packs Imperial y 6 six packs Pilsen), el cliente minorista y el mayorista ven esto:

**Cliente minorista:** recibe la regalía para todos los clientes (una bolsa de hielo gratis, en una línea con la etiqueta **Regalía** y el subtotal **GRATIS**), pero no el descuento de mayoreo.

![Carrito de un cliente minorista](capturas/cliente/cli-carrito-03.png)

**Cliente mayorista:** recibe la misma regalía y, además, el descuento de mayoreo en la cerveza Imperial. El subtotal anterior aparece tachado, el nuevo en verde con el nombre de la promoción, y el **Resumen** muestra **Descuento por promociones**.

![Carrito de un cliente mayorista con el descuento aplicado](capturas/cliente/cli-carrito-04.png)

> [!NOTA] Si baja la cantidad por debajo del mínimo de la promoción, el descuento o la regalía desaparecen del carrito.

## Finalizar la compra {#mod-carrito-pagar}

### Para qué sirve

Convertir el carrito en un pedido, con la dirección de entrega y la forma de pago.

### Quién puede usarlo

Clientes con sesión iniciada.

### Pasos

1. En el carrito, haga clic en **Finalizar compra**.
2. **Paso 1 — Datos del cliente.** El correo ya viene lleno con el de su cuenta.

   ![Paso 1: datos del cliente](capturas/cliente/cli-carrito-05.png)

   1. **Tipo de cliente**: **Cliente Físico** (persona) o **Cliente Jurídico** (empresa).
   2. **Identificación**: su cédula física (9 dígitos) o jurídica (10 dígitos), DIMEX o NITE, sin guiones.
   3. **Factura electrónica**: márquela si necesita factura electrónica.

3. **Paso 2 — Contacto.** Escriba un **Teléfono** donde lo pueda llamar el repartidor. **Teléfono 2** es opcional.
4. **Paso 3 — Entrega.** Las compras en línea se entregan a domicilio en Costa Rica.

   ![Paso 3: dirección de entrega](capturas/cliente/cli-carrito-06.png)

   1. **Provincia**.
   2. **Cantón**: se llena según la provincia.
   3. **Distrito**: se llena según el cantón.
   4. **Dirección escrita o señas**: las señas exactas, por ejemplo «150 m sur del Mercado Central, casa portón negro».

5. **Paso 4 — Pago simulado.** Elija cómo va a pagar:

   ![Paso 4: forma de pago](capturas/cliente/cli-carrito-07.png)

   1. **Método de pago**: **Efectivo contra entrega**, **SINPE Móvil simulado**, **Tarjeta demo** o **Transferencia simulada**.
   2. **Referencia de pago**: si pagó por SINPE o transferencia, escriba el número de comprobante. Es opcional.

   El tipo de cliente, la factura electrónica y los teléfonos quedan guardados con el pedido para su facturación.

6. **Paso 5 — Notas del pedido.** En **Observaciones**, escriba una indicación para la entrega, si la tiene (hasta 300 caracteres).
7. Revise el **Resumen** de la derecha (productos, regalías, descuentos y total) y haga clic en **Finalizar compra**.

   ![Resumen del pedido antes de finalizar](capturas/cliente/cli-carrito-08.png "ancho=8")

> [!ADVERTENCIA] El pago en línea es una **simulación**: no se cobra nada y no hay campos de tarjeta. **Nunca** escriba números de tarjeta, CVV ni claves bancarias en ningún campo.

### Resultado esperado

Aparece «Pedido #… creado correctamente.» y la pantalla **Pedido creado correctamente** con el número de pedido, la dirección, los productos y el total. Recibe además un correo de confirmación.

![Pedido creado](capturas/cliente/cli-carrito-09.png)

Haga clic en **Mis pedidos** para seguirlo (vea [Mis pedidos](#mod-mis-pedidos)). El pedido queda **Pendiente** hasta que la empresa lo aprueba y lo prepara.

> [!NOTA] El total del pedido no incluye el IVA. El 13 % de IVA se suma cuando la empresa genera la factura, por eso el total de la factura es mayor.

### Si algo sale mal

Los mensajes aparecen en rojo debajo del campo con el problema o arriba del formulario:

![Mensaje cuando falta la identificación](capturas/cliente/cli-carrito-10.png)

| Mensaje | Qué hacer |
|---|---|
| Debe iniciar sesión para finalizar la compra. | Inicie sesión: el sistema lo devuelve solo a **Finalizar compra**, con su carrito. Si no tiene cuenta, créela, inicie sesión y abra **Carrito → Finalizar compra**. |
| La identificación es obligatoria. | Escriba su número de cédula. |
| La identificación debe ser una cédula costarricense, jurídica, DIMEX o NITE válida. | Revise los dígitos: física 9 (sin empezar en 0), jurídica 10 (empieza con 3), DIMEX 11 o 12, NITE 10 (empieza con 4). |
| La provincia es obligatoria. / El cantón es obligatorio. / El distrito es obligatorio. / La dirección es obligatoria. | Complete todos los datos de entrega. |
| La referencia no puede superar 80 caracteres. | Escriba solo el número de comprobante. |
| No hay stock suficiente para completar el pedido. Revise el carrito e intente nuevamente. | Alguien compró las últimas unidades. Vuelva al carrito, ajuste las cantidades y finalice de nuevo. |
| El intento de compra expiró. Recargue el checkout e intente nuevamente. | Pasó mucho tiempo en la pantalla de pago. Recargue la página y finalice de nuevo. |
| El pedido fue confirmado, pero no fue posible enviar el comprobante por correo. | El pedido sí quedó registrado. Consúltelo en **Mis pedidos**. |
