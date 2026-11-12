# Comodatos {#mod-comodatos}

Un comodato es el préstamo gratuito de un equipo de la empresa (una nevera, un exhibidor) a un cliente, a cambio de que el cliente venda los productos de la Licorera. En este módulo se registra el préstamo con su contrato, se registra la devolución y se revisa si cada préstamo es rentable. Los equipos se dan de alta antes en [Activos](#mod-activos).

Se abre en **Administración → Logística y Flota → Comodatos**.

La pantalla principal, **Comodatos de equipos**, muestra cada préstamo: el equipo, el cliente, dónde está, desde cuándo y cuántos días lleva.

![Lista de comodatos con los botones principales](capturas/gerente/ger-comodatos-01.png)

- **Rentabilidad** (flecha izquierda) abre el reporte de rentabilidad.
- **Asignar comodato** (flecha derecha) registra un préstamo nuevo.
- En cada fila, **Contrato** descarga el contrato en PDF y **Devolver** registra que el equipo volvió.

## Asignar un equipo en comodato {#mod-comodatos-asignar}

### Para qué sirve

Registrar que un equipo sale de la bodega hacia el local de un cliente, con la condición en que se entrega, y emitir el contrato que el cliente debe firmar.

### Quién puede usarlo

Gerente y Administrador.

### Pasos

1. En **Comodatos**, haga clic en **Asignar comodato**.
2. Complete el formulario:

   ![Formulario Asignar comodato](capturas/gerente/ger-comodatos-02.png "ancho=11")

   1. **Activo**: elija el equipo. La lista muestra solo los equipos **Disponibles**.
   2. **Cliente**: elija el cliente. La lista muestra solo los clientes registrados y activos. Al elegirlo, el sistema completa **Identificación** y **Ubicación del equipo** con los datos del cliente, si los tiene.
   3. **Ubicación del equipo**: el local o la dirección exacta donde quedará el equipo. Revísela o corríjala.
   4. **Fecha de asignación**: el día de la entrega. El sistema propone el día de hoy.
   5. **Condición de entrega**: cómo sale el equipo (por ejemplo, «Nuevo, con rótulo luminoso»). Si quiere, agregue **Observaciones**.
   6. Haga clic en **Registrar comodato**.

3. Haga clic en **Descargar contrato (PDF)**, imprímalo y hágalo firmar por el cliente al entregar el equipo.

   ![Mensaje del contrato listo para descargar](capturas/gerente/ger-comodatos-03.png)

### Resultado esperado

- Aparece «Comodato registrado correctamente. Se emitió el contrato de comodato.» y el aviso «Contrato de comodato CMD-00007 listo para imprimir y firmar.» (con el número que corresponda).
- El préstamo aparece en la lista con el estado **Activo**. En [Activos](#mod-activos), el equipo pasa a **Prestado** y la columna **Prestado a** muestra el cliente.
- El contrato se puede volver a descargar cuando quiera con el botón **Contrato** de la fila.

### Si algo sale mal

Si el cliente tiene facturas a crédito vencidas, el sistema lo avisa al elegirlo y **no** permite el préstamo:

![Aviso al elegir un cliente con deuda vencida](capturas/gerente/ger-comodatos-08.png "ancho=8")

![Alerta de riesgo al intentar registrar el comodato](capturas/gerente/ger-comodatos-07.png "ancho=12")

| Mensaje | Causa | Qué hacer |
|---|---|---|
| Alerta de riesgo: cliente con deuda vencida … La asignación del comodato queda bloqueada hasta que el cliente salde su cuenta. | El cliente tiene saldo a crédito con más de 30 días. | No entregue el equipo. Coordine el pago con Crédito y Cobro (vea [Créditos](#mod-creditos)). Cuando la deuda esté al día, vuelva a intentarlo. |
| Seleccione el activo a prestar. | No eligió un equipo. | Elija el equipo en **Activo**. |
| Seleccione el cliente que recibe el equipo. | No eligió un cliente. | Elija el cliente en **Cliente**. |
| La identificación debe ser una cédula costarricense, jurídica, DIMEX o NITE válida. | La identificación tiene un formato que no es válido. | Corríjala o déjela vacía. |
| No hay activos en estado "Disponible". Registre o libere uno en el módulo de Activos. | Todos los equipos están prestados o en reparación. El botón **Registrar comodato** queda desactivado. | Registre un equipo nuevo en [Activos](#mod-activos-registrar) o espere a que se devuelva uno. |
| El activo no está disponible para comodato. | Otra persona prestó ese mismo equipo hace un momento, o lo enviaron a mantenimiento. | Vuelva a abrir **Asignar comodato** y elija otro equipo. |
| El activo no existe o está inactivo. | El equipo se dio de baja mientras usted llenaba el formulario. | Vuelva a abrir **Asignar comodato** y elija otro equipo. |
| Debe seleccionar un cliente registrado y activo. | El cliente se inactivó. | Elija otro cliente o pida al Administrador que lo reactive (vea [Clientes](#mod-clientes)). |

## Registrar la devolución de un equipo {#mod-comodatos-devolver}

### Para qué sirve

Cerrar el préstamo cuando el equipo vuelve a la bodega, dejar anotado en qué condición regresó y decidir si se puede volver a prestar o si va a reparación.

### Quién puede usarlo

Gerente y Administrador.

### Pasos

1. En **Comodatos**, busque el préstamo (puede escribir el código, el equipo o el cliente y hacer clic en **Buscar**).
2. Registre la devolución:

   ![Formulario para registrar la devolución de un equipo](capturas/gerente/ger-comodatos-04.png)

   1. En la fila del préstamo, haga clic en **Devolver**. Debajo de la fila se abre un formulario.
   2. **Destino**: elija **Reingresar a inventario** si el equipo está bien, o **Enviar a mantenimiento** si llegó con daños.
   3. Escriba la condición del equipo (por ejemplo, «Buen estado, rótulo funcionando» o «Puerta no cierra»). Es opcional, pero ayuda mucho al revisar el historial.
   4. Haga clic en **Registrar devolución**.

3. El sistema le pide confirmar: haga clic en **Sí, registrar**.

### Resultado esperado

- Si eligió **Reingresar a inventario**: «Comodato cerrado. Equipo reingresado al inventario.». El equipo vuelve a **Disponible**.
- Si eligió **Enviar a mantenimiento**: «Comodato cerrado. Equipo enviado a mantenimiento (en reparación). Se envió una alerta a taller y cobros.». El equipo pasa a **Mantenimiento** y el taller y Crédito y Cobro reciben un aviso en la campana (vea [Notificaciones](#gen-notificaciones)).

El préstamo queda en la lista con el estado **Devuelto**.

![Mensaje y préstamo devuelto](capturas/gerente/ger-comodatos-05.png)

> [!NOTA] Un equipo enviado a **Mantenimiento** no se puede prestar hasta que esté reparado. Cuando el taller lo entregue, márquelo como disponible en su ficha (vea [Marcar como disponible un equipo reparado](#mod-activos-disponible)).

### Si algo sale mal

| Mensaje | Causa | Qué hacer |
|---|---|---|
| El comodato ya fue cerrado. | Otra persona ya registró la devolución. | Vuelva a cargar la página y revise el estado del préstamo. |
| No se encontró el comodato. | La página quedó abierta mucho tiempo. | Vuelva a cargar la página. |
| No fue posible registrar la devolución. | Falla de conexión con el sistema. | Vuelva a intentar. Si continúa, avise a Soporte. |

## Revisar la rentabilidad de los equipos prestados {#mod-comodatos-rentabilidad}

### Para qué sirve

Saber si cada cliente con un equipo prestado sigue comprando. Un equipo en un local que no compra es un costo para la empresa: conviene retirarlo y prestarlo a otro cliente.

### Quién puede usarlo

Gerente y Administrador. El Auditor Interno también lo consulta, para revisar los equipos prestados.

### Pasos

1. Abra **Administración → Logística y Flota → Rentabilidad de comodatos**, o en **Comodatos** haga clic en **Rentabilidad**.
2. Revise la tabla **Rentabilidad de equipos prestados**. Los préstamos menos rentables aparecen primero.

   ![Reporte de rentabilidad con un equipo para retirar](capturas/gerente/ger-comodatos-06.png)

### Resultado esperado

Por cada préstamo activo se ve: el equipo, el cliente, la ubicación, la **Fecha de entrega**, los **Días prestado**, cuántos **Pedidos** hizo el cliente desde la entrega, el **Total comprado**, la **Última compra** y los **Días sin compra**.

Si un cliente lleva **30 días o más** sin comprar, la fila sale en rojo con la marca **Sugerir retiro**, y arriba aparece la **Alerta de baja rentabilidad**. El mismo aviso llega a la campana de notificaciones de quienes consultan el reporte.

> [!CONSEJO] Antes de retirar el equipo, llame al cliente: a veces solo se atrasó un pedido. Si decide retirarlo, registre la devolución como se explica en [Registrar la devolución de un equipo](#mod-comodatos-devolver).

### Si algo sale mal

| Situación | Qué hacer |
|---|---|
| La tabla dice «No hay comodatos activos.» | No hay equipos prestados en este momento. |
