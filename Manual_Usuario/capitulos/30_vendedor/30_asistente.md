# Asistente virtual {#mod-asistente}

El **Asistente** responde preguntas escritas sobre el negocio (pedidos, inventario, clientes, entregas…) con los datos del sistema en ese momento, y explica paso a paso cómo usar los módulos. Cada puesto ve solo la información que le corresponde.

## Hacer una pregunta {#mod-asistente-preguntar}

### Para qué sirve

Obtener un dato al instante, sin abrir reportes: cuántos pedidos hay pendientes, qué productos están en stock bajo, cuántas entregas faltan.

### Quién puede usarlo

Vendedor, Empleado, Chofer, Bodeguero, Gerente y Administrador. La información disponible depende del puesto:

| Puesto | Temas que puede consultar |
|---|---|
| Administrador y Gerente | Finanzas, Inventario, Pedidos, Logística, Clientes y Personal |
| Vendedor | Pedidos, Inventario y Clientes |
| Empleado | Inventario y Pedidos |
| Bodeguero | Inventario, Pedidos y Logística |
| Chofer | Logística (rutas, entregas, vehículos y kilometraje) |

Todos pueden pedir ayuda sobre cómo usar un módulo.

### Pasos

1. En el menú superior, haga clic en **Asistente**.
2. Haga su pregunta:

   ![Pantalla del asistente](capturas/vendedor/ven-asis-01.png)

   1. **Preguntas sugeridas**: haga clic en una para preguntarla de inmediato.
   2. **Cuadro de pregunta**: o escriba su propia pregunta, con sus palabras (hasta 500 caracteres).
   3. **Enviar**: envía la pregunta escrita. También puede presionar **Enter**.
   4. **Exportar**: descarga la conversación (vea [Exportar la conversación](#mod-asistente-exportar)).

### Resultado esperado

Su pregunta aparece a la derecha y la respuesta del asistente debajo, con un título y nuevas preguntas sugeridas para seguir.

![Respuesta del asistente a «Dame un resumen»](capturas/vendedor/ven-asis-02.png)

Preguntas que funcionan bien:

| Para saber… | Escriba |
|---|---|
| Qué puede preguntar según su puesto | ¿Qué puedo preguntar? |
| El resumen de sus temas | Dame un resumen |
| Pedidos pendientes, en ruta y retenidos | ¿Cuántos pedidos pendientes hay? |
| Productos en stock bajo o agotados | ¿Cómo está el inventario? |
| Clientes activos y consultas abiertas | ¿Cuántos clientes tenemos? |
| Entregas pendientes (Chofer) | ¿Cuántas entregas pendientes hay? |
| Más datos del último tema | ¿Desea más detalle? (o «más detalle») |

### Si algo sale mal

![Respuesta cuando el tema no está permitido para su puesto](capturas/vendedor/ven-asis-03.png)

| Mensaje | Qué significa | Qué hacer |
|---|---|---|
| Información restringida | Preguntó por un tema que su puesto no puede ver (por ejemplo, las ventas del mes, que son de Finanzas). | Pregunte por los temas de su puesto. Si necesita ese dato para su trabajo, pídaselo a su jefatura. |
| No entendí la solicitud | El asistente no reconoció la pregunta. | Use palabras más simples o una pregunta sugerida, como «dame un resumen». |
| No fue posible responder — Escriba una pregunta. | Envió el cuadro vacío. | Escriba la pregunta antes de enviar. |
| La pregunta no puede superar 500 caracteres. | La pregunta es muy larga. | Acórtela. |
| Sin conexión — No fue posible comunicarse con el asistente. | Se perdió Internet. | Revise la conexión y envíe de nuevo. |
| No fue posible procesar la consulta en este momento. | Hizo muchas preguntas seguidas (más de 20 por minuto) o hubo una falla momentánea. | Espere un minuto y vuelva a preguntar. |

> [!NOTA] Las preguntas sugeridas dependen del puesto: **¿Cuánto vendimos este mes?** solo aparece al Gerente y al Administrador. Si otro puesto la escribe, el asistente responde «Información restringida».

## Pedir ayuda sobre un módulo {#mod-asistente-ayuda}

### Para qué sirve

Recordar los pasos de una tarea sin buscar en el manual.

### Quién puede usarlo

Los mismos puestos que [Hacer una pregunta](#mod-asistente-preguntar).

### Pasos

1. En el cuadro de pregunta, escriba «cómo» y la tarea. Por ejemplo: «¿Cómo uso el módulo de rutas?», «¿Cómo facturo un pedido?», «¿Cómo registro una devolución?».
2. Haga clic en **Enviar**.

### Resultado esperado

El asistente responde con el nombre del módulo, dónde está en el menú y los pasos principales. Hay ayuda para Inventario, Pedidos, Facturación, Clientes, Créditos, Cuentas por cobrar, Rutas, Vehículos, Kilometraje, Mantenimiento, Alertas de flota, Activos, Devoluciones, Cuarentena, Empleados, Roles y permisos, Consultas y el propio Asistente.

## Exportar la conversación {#mod-asistente-exportar}

### Para qué sirve

Guardar las preguntas y respuestas en una hoja de Excel, por ejemplo para adjuntarlas a un informe.

### Quién puede usarlo

Los mismos puestos que [Hacer una pregunta](#mod-asistente-preguntar).

### Pasos

1. Haga al menos una pregunta.
2. Haga clic en **Exportar** (a la derecha de **Enviar**).

### Resultado esperado

Se descarga el archivo `asistente-conversacion-AAAAMMDD-HHMM.xlsx`. Tiene su nombre, su puesto, la fecha y una fila por pregunta con la fecha y hora, la pregunta, el tema y la respuesta.

### Si algo sale mal

| Mensaje | Qué hacer |
|---|---|
| Nada que exportar — Aún no hay respuestas en la conversación. Haga una pregunta y luego exporte. | Haga una pregunta antes de exportar. |

> [!ADVERTENCIA] La conversación se borra al salir de la pantalla o recargarla. Exporte antes si la necesita.
