# Recepción de compras {#app-recepcion}

En **Recepción de compras** el bodeguero registra la mercadería que llega de los proveedores, línea por línea, contra la orden de compra. El inventario sube en el momento.

**Quién la ve:** Bodeguero y Administrador (sección **Bodega**).

## Recibir mercadería de una orden de compra {#app-recepcion-recibir}

### Para qué sirve

Que el inventario refleje lo que realmente entró a la bodega y que compras sepa qué falta por llegar.

### Quién puede usarlo

Bodeguero y Administrador.

### Pasos

1. En la pantalla de inicio, toque **Recepción de compras**. Aparecen las órdenes abiertas:

   ![Órdenes de compra por recibir](capturas/app/app-recepcion-01.png "ancho=6")

   Cada orden muestra el proveedor, su estado (**Pendiente** o **Recibida parcial**), cuántas unidades se han recibido y el monto. Toque la orden que llegó.

2. En **Productos**, busque la línea que va a recibir y toque **Recibir**:

   ![Líneas de la orden de compra](capturas/app/app-recepcion-02.png "ancho=6")

   Cada línea muestra lo **Pedido**, lo **Recibido** y lo **Pendiente**.

3. Indique cuántas unidades llegaron:

   ![Cantidad recibida](capturas/app/app-recepcion-03.png "ancho=6")

   1. **−** y **+**: ajuste la cantidad. Empieza con todo lo pendiente.
   2. **Recibir todo lo pendiente**: vuelve a poner la cantidad completa.
   3. Toque **Recibir … unidades**.

### Resultado esperado

Aparece «Recepción de … registrada. Se envía en cuanto haya señal.». La línea suma lo recibido. Cuando todas las líneas están completas, la orden pasa a **Recibida** y sale de la lista; si falta algo, queda **Recibida parcial**.

> [!CONSEJO] Cuente la mercadería antes de registrarla. Si llegó menos de lo pedido, registre solo lo que llegó: el resto queda pendiente para la siguiente entrega.

### Si algo sale mal

| Mensaje | Qué hacer |
|---|---|
| Nada por recibir | No hay órdenes abiertas. Si el camión llegó, pida a Compras que registre la orden. |
| La recepción excede lo ordenado. | Registró más unidades de las pendientes. Reciba solo lo pedido y avise a Compras del sobrante. |
| La orden no admite recepciones. | La orden se cerró o se canceló. Consulte a Compras. |
| Cantidad inválida. / La cantidad debe ser mayor a cero. | Indique al menos 1 unidad. |
