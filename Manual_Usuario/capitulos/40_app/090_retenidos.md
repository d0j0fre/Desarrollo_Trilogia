# Pedidos retenidos {#app-retenidos}

Los pedidos de los vendedores que superan ₡50 000 antes de impuestos quedan **Retenidos** hasta que un gerente los aprueba o los rechaza. En la aplicación, el gerente lo resuelve desde donde esté.

**Quién la ve:** Gerente y Administrador (sección **Gestión**). En el sitio web, vea [Pedidos retenidos](#mod-pedidos-retenidos).

## Aprobar un pedido retenido {#app-retenidos-aprobar}

### Para qué sirve

Liberar un pedido grande para que se facture y se prepare.

### Quién puede usarlo

Gerente y Administrador.

### Pasos

1. En la pantalla de inicio, toque **Pedidos retenidos**.

   ![Pedidos que esperan aprobación](capturas/app/app-retenidos-01.png "ancho=6")

   1. Cada tarjeta muestra el cliente, el número de pedido, cuántos productos lleva, el vendedor, la fecha y el monto. Toque la parte superior para ver el pedido completo.
   2. **Aprobar**.
   3. **Rechazar** (vea [Rechazar un pedido retenido](#app-retenidos-rechazar)).

2. Toque **Aprobar**, lea el resumen y toque **Aprobar y facturar**.

   ![Confirmar la aprobación](capturas/app/app-retenidos-02.png "ancho=6")

### Resultado esperado

Aparece «Pedido aprobado. Factura FAC-….». El pedido pasa a **Liberado**, se descuenta el inventario y se genera la factura. El vendedor ve el cambio en sus pedidos.

### Si algo sale mal

| Mensaje | Qué hacer |
|---|---|
| Nada por aprobar | No hay pedidos retenidos. |
| Stock insuficiente para uno o más productos al momento de aprobar. | Ya no hay stock para el pedido. Hable con el vendedor: rechace el pedido o espere la reposición. |
| Solo se pueden aprobar pedidos en estado Retenido. | Otra persona ya lo resolvió. Actualice la lista. |
| El pedido ya tiene una factura asociada. | Ya se facturó. Actualice la lista. |

## Rechazar un pedido retenido {#app-retenidos-rechazar}

### Para qué sirve

Detener un pedido que no conviene procesar, explicando el motivo al vendedor.

### Quién puede usarlo

Gerente y Administrador.

### Pasos

1. En la tarjeta del pedido, toque **Rechazar**.
2. Escriba el motivo y confirme:

   ![Motivo del rechazo](capturas/app/app-retenidos-03.png "ancho=6")

   1. **Motivo del rechazo**: entre 5 y 200 caracteres. El vendedor lo verá.
   2. Toque **Rechazar pedido**.

### Resultado esperado

Aparece «Pedido #… rechazado.». El pedido pasa a **Rechazado** y el vendedor ve el motivo en **Mis pedidos** de Venta móvil.

### Si algo sale mal

| Mensaje | Qué hacer |
|---|---|
| Explique el motivo del rechazo (al menos 5 caracteres). | Escriba un motivo más claro. |
