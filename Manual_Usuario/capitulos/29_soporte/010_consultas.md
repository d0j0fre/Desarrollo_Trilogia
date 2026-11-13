# Consultas de clientes {#mod-consultas}

Cuando un cliente o un visitante escribe en el formulario **Contacto** del sitio web, su mensaje llega a esta pantalla como una consulta. Soporte la revisa, atiende al cliente (por teléfono o por correo) y deja anotado qué se hizo, para que cualquier compañero pueda seguir el caso.

Se abre en **Administración → Clientes → Consultas**.

## Buscar una consulta {#mod-consultas-buscar}

### Para qué sirve

Ver qué consultas están sin atender y encontrar las de un cliente o un tema.

### Quién puede usarlo

Soporte, Empleado y Administrador. También desde el teléfono (vea [Consultas de clientes en la aplicación](#app-consultas)).

### Pasos

1. Abra **Consultas**. La tabla **Consultas registradas** muestra todas las consultas, con su fecha, el cliente, su correo, el asunto y el estado.
2. Use los filtros:

   ![Filtros de la lista de consultas](capturas/soporte/sop-consultas-01.png)

   1. **Estado**: elija **Pendiente** para ver lo que falta atender.
   2. **Buscar**: escriba un nombre, un correo, o una palabra del asunto o del mensaje.
   3. Haga clic en **Buscar**.

3. Haga clic en **Ver** en la fila de la consulta.

   ![Abrir una consulta de la lista](capturas/soporte/sop-consultas-02.png)

### Resultado esperado

La tabla muestra solo las consultas que cumplen los filtros y, arriba a la derecha, cuántas son. Al hacer clic en **Ver** se abre **Consulta #…** con el mensaje completo del cliente.

| Estado | Qué significa |
|---|---|
| Pendiente | Nadie la ha atendido. |
| Atendida | Ya se le respondió al cliente. |
| Cerrada | El caso terminó y no requiere más seguimiento. |

> [!CONSEJO] Revise las consultas **Pendientes** al inicio y al final de cada turno. Atienda primero las más antiguas.

### Si algo sale mal

| Mensaje | Qué hacer |
|---|---|
| No se encontró la consulta solicitada. | Vuelva a la lista y búsquela de nuevo. |

## Atender una consulta {#mod-consultas-atender}

### Para qué sirve

Registrar que el cliente ya recibió respuesta y dejar una nota de lo que se hizo.

### Quién puede usarlo

Soporte, Empleado y Administrador.

### Pasos

1. Abra la consulta (vea el procedimiento anterior). A la izquierda está el mensaje del cliente con su nombre y su correo.
2. Responda al cliente por teléfono o por correo, desde su propio correo de trabajo.
3. En **Actualizar seguimiento**, a la derecha:

   ![Actualizar el seguimiento de una consulta](capturas/soporte/sop-consultas-03.png)

   1. **Estado**: elija **Atendida** (o **Cerrada** si ya no hace falta nada más).
   2. **Respuesta u observación interna**: escriba qué se hizo, por ejemplo «Se llamó a la cliente y se le envió la lista de precios». Máximo 1000 caracteres.
   3. Haga clic en **Guardar cambios**.

### Resultado esperado

Aparece el mensaje «Consulta actualizada correctamente. Se notificó la resolución al cliente por correo.». El estado cambia y, debajo del mensaje del cliente, aparece **Última atención** con su nombre, la fecha y la hora.

![Consulta atendida con la última atención](capturas/soporte/sop-consultas-05.png)

> [!NOTA] Al pasar una consulta a **Atendida** o **Cerrada**, el sistema le envía al cliente un correo que dice que su consulta fue resuelta. Ese correo **no** incluye su observación interna: la observación solo la ve el personal. Por eso debe responderle al cliente directamente (paso 2).

### Si algo sale mal

![Mensaje cuando no se pudo enviar el correo al cliente](capturas/soporte/sop-consultas-04.png)

| Mensaje | Qué hacer |
|---|---|
| La consulta se actualizó, pero no fue posible notificar la resolución al cliente por correo. (aviso amarillo) | El cambio sí se guardó. Escríbale usted al cliente para confirmarle la respuesta. Si se repite con todas las consultas, avise al Administrador: el envío de correos no está funcionando. |
| Revise el estado y la observación ingresada. | Elija el estado y revise que la observación no pase de 1000 caracteres. |
| No se pudo actualizar la consulta. | Recargue la página y vuelva a intentar. |
| Ocurrió un error al actualizar la consulta. | Vuelva a intentar. Si continúa, avise al Administrador. |
