# Mis entregas (web) {#mod-mis-entregas}

En **Mis entregas** usted ve las rutas que le asignaron, el mapa con el orden de las paradas y cada pedido que debe entregar. Ahí marca el resultado de cada entrega y registra la foto o la firma de quien recibe.

## Ver mis rutas y el mapa de la ruta {#mod-mis-entregas-ver}

### Para qué sirve

Saber qué rutas tiene, cuántas entregas le faltan y en qué orden visitar a los clientes.

### Quién puede usarlo

Chofer. El Administrador también puede abrir la pantalla, pero solo ve las rutas asignadas a su propio usuario.

### Pasos

1. En el menú superior, haga clic en **Mis entregas**.
2. Busque la ruta del día y haga clic en **Ver entregas**:

   ![Lista de rutas del chofer](capturas/chofer/cho-entregas-01.png)

   Cada tarjeta muestra el código de la ruta, su estado, la zona, la placa del vehículo, la fecha de despacho y cuántas entregas lleva.

3. Revise la ruta:

   ![Encabezado y mapa de la ruta](capturas/chofer/cho-entregas-02.png)

   1. **En línea / Sin conexión**: indica si el teléfono tiene Internet.
   2. **Mapa**: cada parada tiene un número, en el orden de visita. Toque un número para ver el cliente, la dirección y los botones de Waze y Google Maps.
   3. **Navegar ruta completa**: abre Google Maps con todas las paradas en orden.
   4. **Actualizar ruta**: vuelve a cargar la ruta, por ejemplo si logística cambió una parada.

### Resultado esperado

Debajo del mapa aparece una tarjeta por parada, en el orden de la ruta. Las rutas se ordenan así:

| Estado de la ruta | Qué significa |
|---|---|
| Planificada | Logística la está armando. Puede verla, pero todavía no puede marcar entregas. |
| Despachada | Ya salió. Puede marcar cada entrega. |
| Completada | Todas las entregas tienen resultado. La ruta se cierra sola. |

> [!NOTA] Si pierde la señal, la pantalla muestra la última ruta descargada con el aviso «Sin conexión: se muestra la última ruta descargada. Los datos podrían no estar actualizados.». Abra la ruta antes de salir para tenerla guardada.

### Si algo sale mal

| Mensaje | Qué hacer |
|---|---|
| No tiene rutas asignadas por el momento. | Logística todavía no le asignó una ruta. Consulte a su jefatura. |
| Aún no hay coordenadas capturadas para esta ruta. Contacte a logística para secuenciarla. | Las paradas no tienen ubicación en el mapa. Siga el orden de las tarjetas y use la dirección escrita. |
| Sin coordenada: pídele a logística que ubique esta parada. | Esa parada no tiene ubicación. Use la dirección y el teléfono del cliente. |
| Esta ruta está en estado Planificada. Solo puede actualizar entregas de una ruta despachada. | Espere a que logística despache la ruta. |
| No fue posible consultar la ruta solicitada. | La ruta ya no está asignada a usted. Vuelva a **Mis entregas** y abra la ruta correcta. |

## Marcar una entrega como entregada {#mod-mis-entregas-entregado}

### Para qué sirve

Dejar registrado que el cliente recibió su pedido. El pedido pasa a **Entregado** y el cliente recibe un correo de aviso.

### Quién puede usarlo

Chofer, en una ruta **Despachada**.

### Pasos

1. Abra la ruta (vea [Ver mis rutas y el mapa de la ruta](#mod-mis-entregas-ver)) y busque la tarjeta del cliente:

   ![Tarjeta de una entrega con sus botones](capturas/chofer/cho-entregas-03.png "ancho=11")

   1. **Waze** o **Google Maps**: abren la navegación hasta el cliente.
   2. **Entregado**: haga clic cuando el cliente reciba el pedido.
   3. **Fallido**: haga clic si no se pudo entregar (vea [Registrar una entrega fallida](#mod-mis-entregas-fallido)).
   4. **Registrar evidencia**: foto o firma de quien recibe (vea [Registrar la foto o la firma de la entrega](#mod-mis-entregas-evidencia)).

2. Al llegar a la parada, puede hacer clic en **En ruta** para avisar al cliente que su pedido va en camino. Es opcional.
3. Cuando el cliente reciba, haga clic en **Entregado**.

### Resultado esperado

La etiqueta de la tarjeta cambia a **Entregado** y desaparecen los botones de estado. Cuando todas las paradas tienen resultado, la ruta pasa a **Completada**.

![Entrega marcada como Entregado](capturas/chofer/cho-entregas-04.png "ancho=11")

> [!ADVERTENCIA] Un resultado marcado ya no se puede cambiar desde esta pantalla. Si se equivocó, avise de inmediato a logística.

### Si algo sale mal

| Mensaje | Qué hacer |
|---|---|
| La entrega ya tiene un estado final. / Solo se pueden actualizar entregas de una ruta despachada. | La entrega ya tenía un resultado o la ruta ya no está despachada. El sistema muestra el motivo exacto. Haga clic en **Actualizar ruta** para ver el estado real. |
| No fue posible aplicar el estado solicitado. | Falla momentánea. Intente de nuevo. |
| No se pudo actualizar la entrega. / No fue posible actualizar la entrega. | Falla momentánea. Intente de nuevo; si sigue, avise a logística. |

## Registrar una entrega fallida {#mod-mis-entregas-fallido}

### Para qué sirve

Dejar constancia de que un pedido no se pudo entregar y por qué, para que logística lo reprograme.

### Quién puede usarlo

Chofer, en una ruta **Despachada**.

### Pasos

![Motivo de una entrega fallida](capturas/chofer/cho-entregas-05.png "ancho=11")

1. En la tarjeta del cliente, haga clic en **Fallido**.
2. En **Motivo del fallo**, escriba qué pasó (hasta 300 caracteres). Por ejemplo: «Local cerrado, sin encargado para recibir».
3. Haga clic en **Confirmar fallo**.

### Resultado esperado

La etiqueta cambia a **Fallido** y debajo aparece «Motivo:» con lo que escribió. El pedido queda como no entregado para que logística decida qué hacer.

### Si algo sale mal

| Mensaje | Qué hacer |
|---|---|
| Indique el motivo del fallo. | Escriba el motivo antes de confirmar. |

## Registrar la foto o la firma de la entrega {#mod-mis-entregas-evidencia}

### Para qué sirve

Guardar una prueba de la entrega: una foto del pedido en el local o la firma de quien lo recibe.

### Quién puede usarlo

Chofer, en una ruta **Despachada**.

### Pasos

1. En la tarjeta del cliente, haga clic en **Registrar evidencia**. El número entre paréntesis indica cuántas evidencias tiene esa entrega.
2. Para una **foto**:

   ![Registrar una foto de la entrega](capturas/chofer/cho-entregas-06.png "ancho=10")

   1. Deje elegida la pestaña **Foto**.
   2. Toque el selector de archivo. En el celular se abre la cámara: tome la foto. En la computadora, elija la imagen.
   3. **Observaciones**: una nota opcional, por ejemplo «Recibió el bodeguero».
   4. Haga clic en **Guardar foto**.

3. Para una **firma**:

   ![Registrar la firma de quien recibe](capturas/chofer/cho-entregas-07.png "ancho=10")

   1. Toque la pestaña **Firma**.
   2. Pida a la persona que firme con el dedo dentro del recuadro. **Limpiar** borra la firma para repetirla.
   3. **Observaciones**: por ejemplo, el nombre de quien firmó.
   4. Haga clic en **Guardar firma**.

### Resultado esperado

Aparece el mensaje «Evidencia registrada correctamente.» y el número de **Registrar evidencia** aumenta en 1. Puede registrar varias evidencias por entrega.

### Si algo sale mal

| Mensaje | Qué hacer |
|---|---|
| Capture la firma antes de guardar. | La persona no firmó. Pídale que firme dentro del recuadro. |
| Debe adjuntar una foto o firma válida. | No eligió ninguna foto. Tómela de nuevo. |
| La imagen supera el tamaño máximo permitido de 5 MB. | Tome la foto con menor resolución o elija otra. |
| La imagen debe estar en formato JPG, JPEG, PNG o WEBP. | Use una foto normal de la cámara. |
| No fue posible registrar la evidencia. | Falla momentánea o sin señal. Intente de nuevo cuando tenga Internet. |

> [!NOTA] La foto y la firma necesitan Internet en el momento de guardarlas. Sin señal, marque la entrega y registre la evidencia cuando vuelva la conexión. En la aplicación móvil la foto y la firma se piden al marcar **Entregado** y salen solas al volver la señal (vea [Marcar una entrega](#app-mis-rutas-entregar)).

La empresa ve las evidencias en el sitio web, en **Rutas → Evidencias** (vea [Rutas y entregas](#mod-rutas)).

## Trabajar sin señal {#mod-mis-entregas-sin-senal}

### Para qué sirve

Seguir marcando entregas aunque el teléfono se quede sin Internet en la ruta.

### Quién puede usarlo

Chofer.

### Pasos

1. Abra la ruta **antes** de perder la señal, para que quede guardada en el teléfono.
2. Sin señal, el indicador cambia a **Sin conexión**. Marque las entregas como siempre.
3. Cuando el teléfono recupere Internet, vuelva a abrir la ruta: los cambios guardados se envían solos.

### Resultado esperado

Al marcar sin señal aparece «Cambio guardado localmente. Se sincronizará al recuperar señal.» y la etiqueta dice, por ejemplo, **Entregado (pendiente de sincronizar)**:

![Entrega marcada sin señal, pendiente de enviar](capturas/chofer/cho-entregas-08.png "ancho=11")

Al volver la señal, la página se recarga y la etiqueta queda solo como **Entregado**.

> [!ADVERTENCIA] Los cambios sin señal se guardan en ese navegador, a nombre de su usuario. Se conservan 24 horas (la ruta descargada, 12 horas) y **Cerrar sesión** los borra. No cierre sesión ni borre el historial hasta que se hayan enviado. Si usa la aplicación móvil, revise **Por enviar** (vea [Por enviar](#app-por-enviar)).
