# Solicitudes de garantía {#mod-garantias}

Cuando un cliente recibe un producto dañado, defectuoso o vencido, pide la garantía desde su cuenta en la tienda (vea [Mis garantías](#mod-mis-garantias)). En esta pantalla el Administrador revisa cada solicitud y la aprueba o la rechaza, dejando escrita la resolución que verá el cliente.

Se abre en **Administración → Ventas y Pedidos → Solicitudes de Garantía**.

## Revisar y resolver una solicitud de garantía {#mod-garantias-resolver}

### Para qué sirve

Dar respuesta al cliente sobre su reclamo de garantía y dejar constancia de qué se decidió y quién lo decidió.

### Quién puede usarlo

Administrador.

### Pasos

1. Abra **Administración → Ventas y Pedidos → Solicitudes de Garantía**. Arriba a la derecha se ve el **Total** de solicitudes.
2. Revise la solicitud y elija qué hacer en la columna **Gestión**:

   ![Solicitudes de garantía con el formulario de gestión](capturas/administrador/adm-garantias-01.png)

   1. **Pedido / producto**: el pedido y el producto reclamado. Con **Ver cliente** abre la ficha del cliente.
   2. **Solicitud**: el motivo, la explicación del cliente y la fecha.
   3. Elija el estado nuevo: **En revisión** (la está analizando), **Aprobada** o **Rechazada**.
   4. Escriba la resolución: qué se le va a dar al cliente o por qué se rechaza. Es obligatoria para aprobar o rechazar.
   5. Haga clic en **Actualizar**.

### Resultado esperado

Aparece el mensaje «La garantía se actualizó correctamente.». La columna **Estado** muestra el estado nuevo y, debajo, la resolución. Las solicitudes **Aprobada** o **Rechazada** quedan cerradas («Solicitud cerrada.») y pasan al final de la lista; el cliente ve la respuesta en su cuenta.

![Solicitud aprobada y cerrada](capturas/administrador/adm-garantias-02.png)

| Estado | Qué significa |
|---|---|
| Pendiente | El cliente la envió y nadie la ha revisado. |
| En revisión | Alguien la está analizando (por ejemplo, esperando el producto). |
| Aprobada | Se acepta la garantía. Ya no se puede cambiar. |
| Rechazada | No procede la garantía. Ya no se puede cambiar. |

> [!ADVERTENCIA] Aprobar o rechazar cierra la solicitud para siempre. Si necesita más información, déjela **En revisión** y llame al cliente al teléfono que aparece en su ficha.

> [!NOTA] Si aprueba la reposición de un producto, registre la devolución en [Devoluciones](#mod-devoluciones) para que el inventario quede correcto.

### Si algo sale mal

![Mensaje cuando falta la resolución](capturas/administrador/adm-garantias-03.png)

| Mensaje | Qué hacer |
|---|---|
| Debe indicar la resolución de la garantía. | Escriba la resolución antes de aprobar o rechazar. |
| No fue posible aplicar el estado solicitado a la garantía. | La solicitud ya estaba cerrada o cambió mientras la revisaba. Vuelva a cargar la página. |
| La actualización de garantía no es válida. / No fue posible actualizar la garantía. | Vuelva a cargar la página e intente de nuevo. Si continúa, avise a Soporte. |
