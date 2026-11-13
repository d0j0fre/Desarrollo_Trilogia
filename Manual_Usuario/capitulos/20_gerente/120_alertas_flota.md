# Alertas de flota {#mod-alertas-flota}

Alertas de flota avisa qué documentos legales de los vehículos (marchamo, revisión técnica, seguro) están por vencer o ya vencieron, y qué mantenimientos (preventivos o correctivos) ya tocan o están en curso. Sirve para evitar multas y averías mayores.

Se abre en **Administración → Logística y Flota → Alertas de flota**.

## Consultar las alertas {#mod-alertas-flota-consultar}

### Para qué sirve

Saber, cada semana, qué trámites o servicios de la flota hay que atender pronto.

### Quién puede usarlo

Gerente y Administrador.

### Pasos

1. Abra **Alertas de flota**. Se muestran las alertas de los próximos 15 días.
2. Si quiere ver con más anticipación:

   ![Lista de alertas y días de anticipación](capturas/gerente/ger-alertas-01.png "ancho=12")

   1. En **Avisar con … días de anticipación**, escriba el número de días (de 1 a 180).
   2. Haga clic en **Actualizar**.

### Resultado esperado

La tabla **Alertas activas** muestra primero lo vencido y después lo que está por vencer. La columna **Estado** dice:

| Estado | Qué significa | Qué hacer |
|---|---|---|
| **Vencido** (rojo) | El documento ya venció, la fecha del servicio ya pasó o el vehículo ya llegó al kilometraje del servicio. | Atiéndalo de inmediato. No envíe el vehículo a ruta con un documento vencido. |
| **En … día(s)** (amarillo) | Faltan esos días para el vencimiento o el servicio. | Programe el trámite o la cita en el taller. |
| **Por vencer** (amarillo) | El vehículo está a 500 km o menos del kilometraje de un servicio preventivo. | Programe la cita en el taller. |
| **En curso** | Una reparación correctiva está **En proceso** y el vehículo está en el taller. | Dé seguimiento hasta completar la orden. |
| **Pendiente** | Hay una reparación correctiva **Programada** sin fecha. | Defina la fecha en [Mantenimiento de flota](#mod-mantenimiento). |

Las alertas de **Mantenimiento** salen de las órdenes **Programadas** o **En proceso** (preventivas y correctivas) de [Mantenimiento de flota](#mod-mantenimiento). Al marcar la orden como completada, la alerta desaparece.

De los documentos legales, solo el **vigente** (el de vencimiento más reciente de cada vehículo y tipo) genera alertas. Cuando registra una renovación, el documento anterior deja de avisar y queda como historial.

### Si algo sale mal

| Situación | Qué hacer |
|---|---|
| La tabla dice «Sin alertas pendientes.». | No hay nada por vencer en ese plazo. Aumente los días de anticipación si quiere ver más adelante. |
| No aparece un documento que registró. | Vence después del plazo elegido. Aumente los días de anticipación. |

## Registrar un documento legal {#mod-alertas-flota-documento}

### Para qué sirve

Guardar la fecha de vencimiento del marchamo, la revisión técnica (RTV), el seguro u otro permiso de un vehículo, para que el sistema avise a tiempo.

### Quién puede usarlo

Gerente y Administrador.

### Pasos

1. En **Alertas de flota**, use el formulario **Registrar documento legal**, a la derecha:

   ![Formulario Registrar documento legal](capturas/gerente/ger-alertas-02.png "ancho=8")

   1. **Vehículo**: elija la placa.
   2. **Tipo**: **Marchamo**, **RTV (Riteve)**, **Seguro** u **Otro**.
   3. **Fecha de vencimiento**: el día en que vence el documento.
   4. **Observaciones**: un detalle útil, por ejemplo el número de póliza. Es opcional.
   5. Haga clic en **Registrar documento**.

### Resultado esperado

Aparece «Documento registrado. Se vigilará su vencimiento y reemplaza como vigente a los anteriores del mismo tipo.». Si vence dentro del plazo de aviso, aparece de una vez en **Alertas activas**, y el documento queda en la tabla **Documentos legales**.

![Documento registrado entre las alertas](capturas/gerente/ger-alertas-03.png)

> [!CONSEJO] Si renueva un documento, solo regístrelo de nuevo con la fecha nueva: el anterior deja de avisar solo. No hace falta borrarlo.

### Si algo sale mal

| Mensaje | Qué hacer |
|---|---|
| Revise los datos del documento. | Elija el vehículo y escriba la fecha de vencimiento. |
| Ese vehículo ya tiene un documento de ese tipo con esa fecha de vencimiento. | Ya lo registró. Búsquelo en **Documentos legales**. |
| El vehículo indicado no existe. | Actualice la página y elija otro. |
| No fue posible registrar el documento. | Vuelva a intentar. Si continúa, avise a Soporte. |

## Corregir o dar de baja un documento legal {#mod-alertas-flota-corregir}

### Para qué sirve

Corregir un dato mal escrito (por ejemplo, una fecha) o retirar un documento que se registró por error.

### Quién puede usarlo

Gerente y Administrador.

### Pasos

1. En **Alertas de flota**, baje a la tabla **Documentos legales**. Si quiere, elija un **Vehículo** para ver solo sus documentos. La columna **Estado** marca **Vigente**, **En … día(s)** o **Vencido**.

   ![Tabla de documentos legales con sus acciones](capturas/gerente/ger-alertas-04.png "ancho=10")

2. Para corregir, haga clic en el botón del lápiz de la fila. Se abre **Editar documento**: cambie el **Tipo**, la **Fecha de vencimiento** o las **Observaciones** y haga clic en **Guardar cambios**.
3. Para retirarlo, haga clic en el botón del basurero y confirme en la ventana («¿Dar de baja el documento …? Dejará de contarse en las alertas.»).

### Resultado esperado

Aparece «Documento actualizado.» o «Documento dado de baja.». Un documento dado de baja deja de contarse en las alertas; el sistema conserva su registro.

### Si algo sale mal

| Mensaje | Qué hacer |
|---|---|
| No se encontró el documento solicitado. / No se encontró el documento indicado. | El documento ya no existe. Vuelva a **Alertas de flota** y revise la tabla. |
| Ese vehículo ya tiene un documento de ese tipo con esa fecha de vencimiento. | La corrección dejaría dos documentos iguales. Cambie la fecha o dé de baja el otro. |
| No fue posible dar de baja el documento. | Vuelva a intentar. Si continúa, avise a Soporte. |
