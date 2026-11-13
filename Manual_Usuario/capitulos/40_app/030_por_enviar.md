# Por enviar (trabajo sin señal) {#app-por-enviar}

Cuando el teléfono no tiene señal, la aplicación guarda lo que usted marca (entregas, kilometraje, movimientos de bodega, recepciones, ventas) y lo envía solo en cuanto vuelve la conexión. **Por enviar** muestra esa lista.

**Quién la ve:** la tarjeta está en la pantalla de inicio del Chofer (sección **Operación en ruta**). Todo el personal puede abrir la lista desde la franja de sincronización o desde **Mi cuenta → Por enviar**.

## Trabajar sin señal {#app-por-enviar-sin-senal}

### Para qué sirve

No perder ninguna entrega ni registro aunque esté en una zona sin cobertura.

### Quién puede usarlo

Todo el personal que use funciones que trabajan sin señal (vea [Trabajar con poca señal](#puesto-app-senal)).

### Pasos

1. Abra la ruta (o la pantalla que va a usar) **mientras tiene señal**, para que quede guardada en el teléfono.
2. Sin señal, trabaje como siempre. Por ejemplo, marque una entrega como **Entregado**.
3. Arriba aparece una franja con lo que falta por enviar:

   ![Entrega marcada sin señal](capturas/app/app-por-enviar-01.png "ancho=6") ![Franja de sincronización en la pantalla de inicio](capturas/app/app-por-enviar-02.png "ancho=6")

4. Cuando el teléfono recupere la señal, la aplicación envía todo sola (revisa cada 30 segundos). No tiene que hacer nada más.

### Resultado esperado

La franja indica el estado:

| Franja | Qué significa |
|---|---|
| Sin conexión · trabajando con lo guardado | No hay señal, pero no hay nada pendiente. |
| Sin conexión · 1 acción pendiente | No hay señal y hay acciones guardadas en el teléfono. |
| 1 acción por enviar | Hay señal y se están enviando. |
| 1 acción necesita su atención (en rojo) | El servidor rechazó una acción. Revísela en **Por enviar**. |

Cuando todo se envía, la franja desaparece.

## Revisar lo que falta por enviar {#app-por-enviar-revisar}

### Para qué sirve

Confirmar que todo lo que hizo llegó al sistema, y resolver lo que fue rechazado.

### Quién puede usarlo

Todo el personal.

### Pasos

1. Toque la franja de sincronización, o la tarjeta **Por enviar** (Chofer), o **Mi cuenta → Por enviar**.
2. Revise la lista:

   ![Acciones pendientes de enviar](capturas/app/app-por-enviar-03.png "ancho=6")

   Cada acción muestra qué es (por ejemplo, «Pedido #9 entregado»), su estado y cuándo se registró. Con señal, toque **Enviar ahora** (1, arriba a la derecha) para enviarlas sin esperar.

### Resultado esperado

Cuando todo llegó, la pantalla dice **Todo al día** y la hora de la última sincronización.

![Todo enviado](capturas/app/app-por-enviar-04.png "ancho=6")

| Estado de la acción | Qué significa | Qué hacer |
|---|---|---|
| En espera | Está guardada y se enviará con señal. | Nada. Espere a tener señal. |
| Necesita atención | El servidor no la aceptó (por ejemplo, la ruta fue reasignada o la entrega ya tenía resultado). El motivo aparece en la tarjeta. | Confirme con operaciones. Luego toque **Reintentar** o **Descartar**. |

> [!ADVERTENCIA] **Descartar** borra la acción del teléfono para siempre: nunca llegará al sistema. Úselo solo cuando operaciones le confirme que no hace falta.

> [!ADVERTENCIA] No cierre sesión ni desinstale la aplicación mientras haya acciones pendientes: se borrarían del teléfono, junto con las fotos y firmas de entrega que aún no se enviaron. Al cerrar sesión, la aplicación le avisa cuántas quedan.

Una entrega con foto o firma genera varias acciones: primero **Pedido #… entregado** y después **Foto del pedido #…** o **Firma del pedido #…**, que esperan a que salga la primera.

### Si algo sale mal

| Mensaje | Qué hacer |
|---|---|
| No se pudo enviar después de 10 intentos. … | Revise la señal y toque **Reintentar**. Si sigue, avise a operaciones. |
| No se pudo leer la cola del teléfono. | Cierre y vuelva a abrir la aplicación. Si sigue, avise a operaciones antes de desinstalar nada. |
