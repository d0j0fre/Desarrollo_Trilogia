# Mis rutas {#app-mis-rutas}

**Mis rutas** es la herramienta del chofer en la calle: muestra sus rutas, el mapa con el orden de las paradas y, en cada parada, los botones para navegar, llamar al cliente y marcar si el pedido se entregó.

**Quién la ve:** Chofer (sección **Operación en ruta**).

## Ver mis rutas y el mapa {#app-mis-rutas-ver}

### Para qué sirve

Saber qué rutas tiene asignadas, cuánto le falta y en qué orden visitar a los clientes.

### Quién puede usarlo

Chofer.

### Pasos

1. En la pantalla de inicio, toque **Mis rutas**.
2. Toque la ruta del día:

   ![Lista de rutas del chofer](capturas/app/app-mis-rutas-01.png "ancho=6")

   Cada tarjeta muestra el código, la zona, el estado (**Despachada**, **Planificada** o **Completada**), la placa, cuántas entregas lleva y el porcentaje de avance.

3. Revise la ruta:

   ![Detalle de la ruta con el mapa](capturas/app/app-mis-rutas-02.png "ancho=6")

   1. **Avance**: entregas cerradas del total y paradas pendientes.
   2. **Mapa**: cada parada con su número, en el orden de visita, unidas por una línea. Puede moverlo y acercarlo con los dedos.
   3. **Ver mapa completo**: abre el mapa en toda la pantalla.
   4. **Actualizar**: vuelve a traer la ruta, por ejemplo si operaciones cambió una parada.

### Resultado esperado

Debajo del mapa aparece la sección **Paradas**, una tarjeta por cliente en el orden de la ruta.

![Mapa completo de la ruta](capturas/app/app-mis-rutas-07.png "ancho=6")

> [!NOTA] La ruta queda guardada en el teléfono: si pierde la señal, sigue viendo la última versión descargada. El dibujo del mapa sí necesita Internet; sin señal, guíese por el orden de las tarjetas.

### Si algo sale mal

| Mensaje | Qué hacer |
|---|---|
| No tiene rutas asignadas | Operaciones aún no le asignó una ruta. Deslice hacia abajo para revisar de nuevo. Cuando se la asignen, le llega una notificación. |
| Ninguna parada tiene ubicación registrada. Siga la secuencia de la lista. | Las paradas no tienen ubicación en el mapa. Use la dirección de cada tarjeta. |
| Secuencia de visitas: … sin ubicación (ver lista). | Algunas paradas no aparecen en el mapa. Revise la lista completa. |
| No se pudieron traer las rutas y no hay nada guardado en el teléfono. | Busque señal y deslice hacia abajo para actualizar. |
| Esta ruta ya no está disponible para usted. Puede que operaciones la haya reasignado. | Vuelva a la lista y actualice. Consulte a operaciones. |

## Marcar una entrega {#app-mis-rutas-entregar}

### Para qué sirve

Registrar en el momento si el cliente recibió el pedido. El cliente recibe un aviso y operaciones ve el avance de la ruta.

### Quién puede usarlo

Chofer, en una ruta **Despachada**.

### Pasos

1. En **Paradas**, busque la tarjeta del cliente:

   ![Tarjeta de una parada](capturas/app/app-mis-rutas-03.png "ancho=6")

   1. **Navegar**: abre Waze, Google Maps u otra aplicación de mapas hasta el cliente.
   2. **Llamar**: marca el teléfono del cliente.
   3. **Entregado**: el cliente recibió el pedido.
   4. **No entregado**: no se pudo entregar (vea [Registrar una entrega no realizada](#app-mis-rutas-no-entregado)).

   La etiqueta de la tarjeta muestra el estado de la parada. Una ruta despachada deja todas sus paradas en **En ruta**, por eso la aplicación no tiene un botón aparte para marcar esa salida: las acciones son **Entregado** y **No entregado**.

2. Toque **Entregado**. Se abre **Confirmar entrega**, que dice «Va a marcar el pedido #… de … como entregado.».

   ![Confirmar la entrega](capturas/app/app-mis-rutas-04.png "ancho=6")

3. Si quiere, agregue la **Evidencia (opcional)** de la entrega:
   - **Tomar foto**: abre la cámara. Puede agregar hasta 3 fotos.
   - **Elegir de la galería**: usa una foto que ya tiene en el teléfono.
   - **Firma del cliente**: abre la pantalla **Firma del cliente**. Pida al cliente que firme con el dedo dentro del recuadro y toque **Usar firma** (o **Borrar** para repetirla). Después el botón dice **Repetir firma**.
   - Cada foto o firma aparece como miniatura con una **X** roja para quitarla.
4. Toque **Sí, fue entregado**. **Cancelar** cierra la hoja y descarta lo que haya tomado.

### Resultado esperado

Aparece «Pedido #… marcado como entregado.» (o «…, con N archivo(s) de evidencia.» si adjuntó fotos o firma) y la parada cambia a **Entregado**, sin botones. Cuando todas las paradas tienen resultado, la ruta pasa a **Completada**.

![Parada marcada como entregada](capturas/app/app-mis-rutas-05.png "ancho=6")

> [!ADVERTENCIA] Un resultado marcado no se puede cambiar desde la aplicación. Si se equivocó, avise de inmediato a operaciones.

> [!NOTA] La foto y la firma tienen el mismo tope que en el sitio web: 5 MB por archivo y formatos JPG, PNG o WEBP. Con señal se envían al marcar la entrega. **Sin señal**, quedan en [Por enviar](#app-por-enviar) como **Foto del pedido #…** o **Firma del pedido #…** y salen solas, después del estado, cuando vuelve la señal. La empresa las ve en el sitio web, en **Rutas → Evidencias** (vea [Rutas y entregas](#mod-rutas)).

> [!NOTA] Si la aplicación no puede abrir la cámara, dice «No se pudo abrir la cámara. Revise el permiso de la aplicación.». Si la foto pesa más de 5 MB, dice «La foto pesa más de 5 MB. Tome otra o elija una más liviana.».

## Registrar una entrega no realizada {#app-mis-rutas-no-entregado}

### Para qué sirve

Dejar constancia de por qué no se entregó un pedido, para que operaciones lo reprograme.

### Quién puede usarlo

Chofer, en una ruta **Despachada**.

### Pasos

1. En la tarjeta del cliente, toque **No entregado**.
2. Elija el motivo y confirme:

   ![Motivo de la entrega no realizada](capturas/app/app-mis-rutas-06.png "ancho=6")

   1. Toque el motivo: **Cliente ausente**, **Local cerrado**, **Dirección incorrecta**, **Cliente rechazó el pedido**, **No hubo pago** u **Otro**. Con **Otro**, escriba el motivo en **Escriba el motivo** (hasta 300 caracteres).
   2. Toque **Marcar como no entregado**.

### Resultado esperado

Aparece «Pedido #… marcado como no entregado.». La parada queda **No entregado** con el motivo debajo. Operaciones y el cliente ven el motivo.

### Si algo sale mal

Si marcó una entrega sin señal y el servidor la rechaza al enviarla (por ejemplo, porque la ruta fue reasignada), la acción aparece en **Por enviar** como **Necesita atención** con el mensaje «El recurso no existe o no está disponible para usted.». Hable con operaciones antes de decidir (vea [Por enviar](#app-por-enviar)).
