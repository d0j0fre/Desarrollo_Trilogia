# Mis jornadas (web) {#mod-mis-jornadas}

En esta pantalla usted registra las horas que trabajó cada día y sigue su aprobación. Su supervisor revisa cada jornada y la aprueba o la rechaza; las jornadas aprobadas se usan para calcular la planilla.

## Registrar una jornada {#mod-mis-jornadas-registrar}

### Para qué sirve

Informar a su supervisor cuántas horas trabajó en un día: horas ordinarias, horas extra y horas de ausencia.

### Quién puede usarlo

Todo el personal que tiene expediente de empleado y permiso para registrar jornadas: Empleado, Vendedor, Gerente, Supervisor, Auditor, Cajero, Bodeguero, Facturador, Crédito y Cobro, Compras, Soporte y Chofer. El botón **Mis jornadas** aparece en su menú superior. También puede registrar su jornada desde la aplicación móvil (vea [Mis jornadas en la aplicación](#app-mis-jornadas)).

### Pasos

1. En el menú, haga clic en **Mis jornadas**.

   ![Botón Mis jornadas en el menú](capturas/empleado/emp-jornadas-01.png)

2. Complete el formulario de la parte superior:

   ![Formulario para registrar una jornada](capturas/empleado/emp-jornadas-02.png)

   1. **Fecha**: el día que trabajó. Haga clic en el calendario o escríbala.
   2. **Ordinarias**: horas de su horario normal.
   3. **Extra**: horas trabajadas fuera de su horario. Si no hubo, deje **0**.
   4. **Ausencia**: horas que no trabajó dentro de su horario (por ejemplo, una cita médica). Si no hubo, deje **0**.
   5. **Observaciones**: una nota para su supervisor, por ejemplo el motivo de las horas extra. Es opcional.
   6. Haga clic en **Enviar**.

### Resultado esperado

Aparece el mensaje «Jornada enviada para aprobación.» y la jornada se agrega a la tabla de abajo con el estado **Enviada**.

![La jornada enviada aparece en la tabla](capturas/empleado/emp-jornadas-03.png)

La columna **Estado** le indica en qué va cada jornada:

| Estado | Qué significa |
|---|---|
| Borrador | La guardó sin enviar. Su supervisor todavía no la ve. |
| Enviada | Está esperando la revisión de su supervisor. |
| Aprobada | Su supervisor la aprobó. Se tomará en cuenta para la planilla. |
| Rechazada | Su supervisor la rechazó. El motivo aparece en la columna **Respuesta**. |

> [!NOTA] Si la guarda con **Guardar borrador**, su supervisor todavía no la ve. Para enviarla después, use **Completar** (vea [Completar un borrador o una jornada rechazada](#mod-mis-jornadas-completar)).

> [!CONSEJO] Registre su jornada al terminar cada turno. Así no se le olvidan las horas extra y su supervisor puede aprobarlas a tiempo para la planilla.

### Si algo sale mal

![Mensaje cuando no se pudo guardar la jornada](capturas/empleado/emp-jornadas-04.png)

| Mensaje | Causa más común | Qué hacer |
|---|---|---|
| Ya registró la jornada del … y está enviada: no se puede modificar. | Ya existe una jornada **Enviada** o **Aprobada** para esa fecha. | Revise la tabla: no la registre de nuevo. Si las horas están mal, pídale a su supervisor que la rechace; después podrá completarla. |
| No fue posible guardar la jornada. | La suma de horas es 0 o pasa de 24. | Revise las horas: entre ordinarias, extra y ausencia deben sumar más de 0 y como máximo 24. |
| The value '…' is not valid for… | Escribió letras o un formato de número que el sistema no reconoce. | Escriba solo números enteros, sin letras ni símbolos. |

## Completar un borrador o una jornada rechazada {#mod-mis-jornadas-completar}

### Para qué sirve

Terminar una jornada que guardó como borrador, o corregir y volver a enviar una jornada que su supervisor rechazó.

### Quién puede usarlo

Los mismos puestos que registran una jornada.

### Pasos

1. Abra **Mis jornadas**. En la tabla, las jornadas en **Borrador** o **Rechazada** tienen el botón **Completar**. Si fue rechazada, lea primero el motivo en la columna **Respuesta**.

   ![Jornada rechazada con el botón Completar](capturas/empleado/emp-jornadas-05.png)

2. Haga clic en **Completar**. El formulario de arriba se llena con los datos de esa jornada y dice «Completando la jornada del …».

   ![Formulario con la jornada a completar](capturas/empleado/emp-jornadas-06.png)

   1. Corrija lo que haga falta (en el ejemplo, las horas **Extra**). La fecha no se cambia.
   2. Haga clic en **Enviar** (o en **Guardar borrador** si todavía no la quiere enviar).

   Si se arrepiente, haga clic en **Registrar otra fecha** para volver al formulario vacío.

### Resultado esperado

Aparece el mensaje «Jornada enviada para aprobación.» (o «Borrador de jornada guardado.») y la jornada vuelve a la tabla con el estado **Enviada** (o **Borrador**). Su supervisor la vuelve a revisar.

> [!CONSEJO] Si escribe en el formulario vacío la fecha de una jornada en **Borrador** o **Rechazada**, el sistema también la completa en lugar de crear otra.

### Si algo sale mal

| Mensaje | Qué hacer |
|---|---|
| No fue posible guardar la jornada. | Recargue la página y vuelva a intentar: puede que la jornada haya cambiado mientras la completaba. Revise también que las horas sumen más de 0 y como máximo 24. |
