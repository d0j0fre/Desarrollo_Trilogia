# Rutas y entregas {#mod-rutas}

En Rutas y entregas usted arma los recorridos de reparto del día. Cada ruta agrupa los pedidos de una zona y tiene un chofer y un vehículo. Desde aquí la ordena, la despacha, la sigue mientras está en la calle y la cierra al final del día.

Se abre en **Administración → Logística y Flota → Rutas y Entregas**.

Una ruta pasa por estos estados:

| Estado | Qué significa | Qué puede hacer |
|---|---|---|
| **Planificada** | La ruta está armada, pero el camión todavía no sale. | Agregar o quitar pedidos, ordenar las paradas, despacharla o cancelarla. |
| **Despachada** | El chofer ya salió a entregar. Los pedidos están **En ruta**. | Recalcular el orden de las paradas, finalizarla o cancelarla. |
| **Completada** | La ruta se finalizó cuando el camión regresó. Falta liquidar sus cobros y su mercadería en Finanzas. | Solo consultarla (la liquidación la hace el [Cajero](#mod-liquidacion)). |
| **Cancelada** | La ruta se anuló. Sus pedidos quedan libres para otra ruta. | Solo consultarla. |

> [!CONSEJO] En el teléfono puede seguir las rutas desde la aplicación móvil, en **Rutas y choferes** (vea [Rutas y choferes](#app-rutas)).

## Consultar las rutas {#mod-rutas-consultar}

### Para qué sirve

Ver de un vistazo qué rutas hay, quién las maneja y cuántas entregas lleva cada una.

### Quién puede usarlo

Gerente y Administrador.

### Pasos

1. Abra **Rutas y entregas**. La lista muestra primero las rutas despachadas, después las planificadas y al final las completadas y canceladas.
2. Use los filtros si busca una ruta en particular:

   ![Filtros y lista de rutas](capturas/gerente/ger-rutas-01.png)

   1. **Estado**: elija un estado o deje **Todos**.
   2. **Buscar**: escriba el código, la zona, el nombre del chofer o la placa.
   3. Haga clic en **Filtrar**. Para ver todas de nuevo, haga clic en **Limpiar**.
   4. Haga clic en **Ver** para abrir el detalle de una ruta.

### Resultado esperado

La lista muestra las rutas que cumplen el filtro y, arriba a la derecha, el **Total registros**. La columna **Progreso** cuenta las entregas así: entregadas (✔) / fallidas (✖) / pendientes (reloj).

### Si algo sale mal

| Mensaje | Qué hacer |
|---|---|
| No hay rutas registradas. | No hay rutas con ese filtro. Haga clic en **Limpiar**. |
| No se encontró la ruta solicitada. | La ruta ya no existe. Vuelva a la lista y búsquela de nuevo. |

## Crear una ruta {#mod-rutas-crear}

### Para qué sirve

Agrupar los pedidos listos para entregar en un recorrido, con el chofer y el vehículo que lo harán.

### Quién puede usarlo

Gerente y Administrador.

### Pasos

1. En **Rutas y entregas**, haga clic en **Nueva ruta**.

   ![Botón Nueva ruta](capturas/gerente/ger-rutas-02.png)

2. Complete los datos y elija los pedidos:

   ![Formulario para crear una ruta](capturas/gerente/ger-rutas-03.png)

   1. **Zona**: el nombre del recorrido, por ejemplo «Escazú y Santa Ana».
   2. **Chofer**: elija el chofer. Entre paréntesis aparece cuántas rutas abiertas tiene.
   3. **Vehículo**: elija el vehículo. Solo aparecen los vehículos activos.
   4. **Observaciones**: una indicación para el chofer. Es opcional.
   5. Marque la casilla de cada pedido que va en esta ruta.
   6. En la columna **Prioridad** de cada pedido marcado, elija **Urgente**, **Alta** o **Normal** (por defecto, **Normal**). La prioridad decide el orden de visita al secuenciar la ruta.
   7. Haga clic en **Crear ruta**.

> [!NOTA] La tabla **Pedidos por asignar** muestra los pedidos que todavía no están en otra ruta y que no están cancelados, rechazados, entregados ni retenidos. Un pedido retenido aparece aquí después de que usted lo aprueba (vea [Pedidos retenidos](#mod-pedidos-retenidos)).

### Resultado esperado

Aparece el mensaje «Ruta RUT-… creada correctamente.» y se abre el detalle de la ruta nueva, con el estado **Planificada** y sus pedidos en estado de entrega **Pendiente**.

![Ruta recién creada](capturas/gerente/ger-rutas-04.png)

### Si algo sale mal

![Mensaje cuando no se eligió ningún pedido](capturas/gerente/ger-rutas-05.png)

| Mensaje | Qué hacer |
|---|---|
| Debe seleccionar al menos un pedido para la ruta. | Marque al menos un pedido en la tabla de la derecha. |
| La zona es obligatoria. | Escriba el nombre de la zona. |
| Debe seleccionar un chofer disponible. / Debe seleccionar un vehículo disponible. | Elija un chofer y un vehículo en las listas. |
| No hay chofer o vehículo disponible para asignar la ruta. | No hay choferes activos o vehículos activos. Active un vehículo en [Vehículos](#mod-vehiculos) o pida al Administrador que active al chofer. |
| Uno o más pedidos ya están asignados a otra ruta activa. / Uno o más pedidos ya no se pueden asignar (estado no válido). | Otra persona asignó uno de esos pedidos a otra ruta, o cambió su estado, mientras usted armaba la ruta. Vuelva a abrir **Nueva ruta** y elija los pedidos de nuevo. |
| Uno o más pedidos son de un cliente inactivo: no se pueden asignar a una ruta. | Quite ese pedido de la selección o pida al Administrador que reactive al cliente. |
| No hay chofer disponible o el chofer seleccionado no es válido. / No hay vehículo disponible o el vehículo seleccionado no es válido. | El chofer o el vehículo se inactivó mientras armaba la ruta. Elíjalos de nuevo. |

## Ordenar las paradas de la ruta {#mod-rutas-ordenar}

### Para qué sirve

Definir en qué orden visitará el chofer a los clientes, para que haga el recorrido más corto. El chofer ve este orden en su teléfono.

### Quién puede usarlo

Gerente y Administrador, mientras la ruta está **Planificada**.

### Pasos

1. Abra la ruta (botón **Ver** en la lista).
2. Haga clic en **Secuenciar automáticamente** y confirme con **Sí, secuenciar**. El sistema ordena las paradas por prioridad: primero las **Urgentes**, luego las **Altas** y al final las **Normales**; dentro de cada nivel, de la más cercana a la más lejana según la ubicación de cada cliente.

   ![Botón Secuenciar automáticamente](capturas/gerente/ger-rutas-06.png)

   Aparece el mensaje «Ruta secuenciada automáticamente (3 punto(s)). Tiempo total estimado: … Puede reordenar manualmente antes de despachar.» y la columna **#** muestra el orden nuevo. El detalle de la ruta también muestra el **Tiempo total estimado** (con los kilómetros) y, en cada parada, la **Llegada estimada** desde la salida de la bodega. Son cálculos aproximados.

   ![Orden de entregas después de secuenciar](capturas/gerente/ger-rutas-07.png)

3. Si quiere cambiar el orden a mano o corregir la ubicación de un cliente, baje a **Orden manual y ubicación en mapa**:

   ![Orden manual y ubicación de las paradas en el mapa](capturas/gerente/ger-rutas-08.png)

   1. **Orden**: escriba el número de visita de cada parada (1 es la primera). En **Prioridad** puede cambiar **Urgente**, **Alta** o **Normal** de cada parada.
   2. Para corregir la ubicación de una parada, haga clic en su botón con el marcador.
   3. Haga clic en el punto exacto del mapa. También puede arrastrar el número del mapa. La **Latitud** y la **Longitud** se llenan solas.
   4. Haga clic en **Guardar orden y ubicaciones**.

### Resultado esperado

- Al guardar a mano aparece «Orden de entregas actualizado.».
- La ubicación que fija en el mapa se guarda para ese cliente y se usa en sus próximas rutas.
- El botón **Ver recorrido en Maps** abre el recorrido en Google Maps para revisarlo.
- El botón **Exportar a Excel** (sobre la tabla **Pedidos de la ruta**) descarga la hoja de ruta en un archivo `.xlsx`, con los datos de la ruta, el tiempo estimado y una fila por pedido.

> [!CONSEJO] Guarde el orden **antes** de despachar. Después del despacho solo se puede recalcular (vea [Recalcular una ruta en curso](#mod-rutas-recalcular)).

### Si algo sale mal

| Mensaje | Qué hacer |
|---|---|
| … punto(s) de la ruta no tienen coordenada. Ubíquelos en el mapa y guarde las ubicaciones antes de secuenciar la ruta. | Algunos clientes no tienen ubicación. La ruta no se secuencia y las paradas sin ubicación se marcan en amarillo. Ubíquelas en el mapa (paso 3), guarde y vuelva a secuenciar. |
| Solo se puede secuenciar la ruta antes de despacharla (estado Planificada). | La ruta ya salió. Use **Recalcular ruta** (vea [Recalcular una ruta en curso](#mod-rutas-recalcular)). |
| No hay pedidos pendientes para secuenciar en esta ruta. | La ruta no tiene pedidos. Agregue pedidos primero. |
| No fue posible guardar el orden de la ruta. | Vuelva a intentar. Si continúa, avise a Soporte. |

## Agregar o quitar pedidos {#mod-rutas-pedidos}

### Para qué sirve

Ajustar la carga de una ruta antes de que salga: sumar un pedido que llegó tarde o sacar uno que no se va a entregar hoy.

### Quién puede usarlo

Gerente y Administrador, mientras la ruta está **Planificada**.

### Pasos

1. Abra la ruta.
2. Agregue o quite pedidos:

   ![Agregar o quitar pedidos de la ruta](capturas/gerente/ger-rutas-09.png)

   1. En **Agregar pedido a la ruta**, elija el pedido en la lista.
   2. Haga clic en **Agregar**.
   3. Para quitar un pedido, haga clic en la **X** de su fila y confirme con **Sí, quitar**.

### Resultado esperado

Aparece «Pedido agregado a la ruta.» o «Pedido quitado de la ruta.». El pedido agregado queda al final del orden; el pedido quitado vuelve a la lista de pedidos por asignar.

### Si algo sale mal

| Mensaje | Qué hacer |
|---|---|
| No hay más pedidos por asignar. | Todos los pedidos listos ya están en una ruta. |
| Solo se pueden modificar rutas en estado Planificada (antes del despacho). | La ruta ya se despachó. Ya no se pueden agregar ni quitar pedidos. |
| El pedido no existe o no se puede asignar por su estado. / El pedido ya está asignado a una ruta activa. | El pedido cambió de estado o ya está en otra ruta. Actualice la página y revise. |
| El cliente de este pedido está inactivo: no se puede asignar a una ruta. | Pida al Administrador que reactive al cliente. |
| El pedido no está en la ruta o ya no se puede quitar. | Actualice la página: otra persona ya lo quitó. |

## Despachar una ruta {#mod-rutas-despachar}

### Para qué sirve

Marcar que el camión salió. Los pedidos pasan a **En ruta**, el chofer los ve en su lista de entregas y el sistema avisa por correo a cada cliente.

### Quién puede usarlo

Gerente y Administrador.

### Pasos

1. Abra la ruta **Planificada** y revise los pedidos y el orden.
2. Haga clic en **Despachar ruta**.
3. En la ventana de confirmación («¿Despachar la ruta? Los pedidos pasarán a En ruta y se notificará a los clientes.»), haga clic en **Sí, despachar**.

   ![Confirmación para despachar la ruta](capturas/gerente/ger-rutas-10.png "ancho=9")

### Resultado esperado

Aparece el mensaje «Ruta despachada. Los pedidos están En ruta.». El estado de la ruta cambia a **Despachada**, aparece la fecha **Despachada** y la columna **Entrega** de cada pedido dice **En ruta**.

![Ruta despachada](capturas/gerente/ger-rutas-11.png)

A partir de aquí, el chofer registra cada entrega en su teléfono (vea [Mis rutas](#app-mis-rutas)) y usted sigue el avance en el [Tablero de entregas](#mod-tablero-entregas).

### Si algo sale mal

| Mensaje | Causa | Qué hacer |
|---|---|---|
| La ruta no tiene pedidos para despachar. | La ruta quedó vacía. | Agregue pedidos antes de despachar. |
| Solo se pueden despachar rutas en estado Planificada. | La ruta ya se despachó, se canceló o se completó. | Actualice la página. |
| No fue posible despachar la ruta. | Falla de conexión con el sistema. | Vuelva a intentar. Si continúa, avise a Soporte. |

## Recalcular una ruta en curso {#mod-rutas-recalcular}

### Para qué sirve

Reordenar las paradas que faltan cuando hay un imprevisto en la calle, por ejemplo un cliente cerrado. El chofer ve el orden nuevo en su teléfono.

### Quién puede usarlo

Gerente y Administrador, cuando la ruta está **Despachada**.

### Pasos

1. Abra la ruta despachada.
2. Indique el imprevisto:

   ![Recalcular una ruta despachada](capturas/gerente/ger-rutas-12.png "ancho=8")

   1. Haga clic en **Recalcular ruta**. Se abre un formulario debajo de los botones.
   2. **Punto afectado (opcional)**: elija el pedido que no se puede entregar. Si solo quiere reordenar, deje **— Solo reordenar los pendientes —**.
   3. Escriba el motivo, por ejemplo «Cliente cerrado».
   4. Haga clic en **Recalcular** y confirme con **Sí, recalcular**.

### Resultado esperado

Aparece «Ruta recalculada. … punto(s) reordenados. Tiempo total estimado: … El chofer verá la ruta actualizada.». El pedido que eligió como afectado queda como **Fallido**, con el motivo debajo.

### Si algo sale mal

| Mensaje | Qué hacer |
|---|---|
| No es posible recalcular: no quedan puntos por recalcular. Contacte al chofer manualmente. | Ya no quedan entregas pendientes. Llame al chofer. |
| Solo se puede recalcular una ruta despachada (en curso). | La ruta ya no está despachada. Actualice la página. |

## Finalizar una ruta al regresar el camión {#mod-rutas-finalizar}

### Para qué sirve

Cerrar la ruta cuando el camión regresa. Las paradas que no se entregaron quedan como **No entregado** y la ruta pasa a **Completada**. Después, en Finanzas, se liquidan los cobros y la mercadería (vea [Liquidación de cobros](#mod-liquidacion)).

### Quién puede usarlo

Gerente y Administrador.

### Pasos

1. Abra la ruta despachada y revise que el chofer haya registrado todas sus entregas.
2. Haga clic en **Finalizar ruta**.
3. Lea la ventana de confirmación («¿Finalizar la ruta? Las paradas sin entregar quedarán como No entregado. La mercadería y los cobros se liquidan después en Finanzas.») y haga clic en **Sí, finalizar**.

   ![Confirmación para finalizar la ruta](capturas/gerente/ger-rutas-13.png "ancho=9")

### Resultado esperado

Aparece «Ruta finalizada. Todos los pedidos se entregaron; liquide los cobros en Finanzas.» o, si hubo pedidos sin entregar, «Ruta finalizada. … pedido(s) quedaron como No entregado. Liquide la mercadería para devolverla al inventario y los cobros en Finanzas.». La ruta pasa a **Completada**.

El dinero que cobró el chofer se cuadra en [Liquidación de cobros](#mod-liquidacion-registrar) y la mercadería que regresó se devuelve al inventario en [Liquidar la mercadería de una ruta](#mod-liquidacion-mercaderia). Solo se pueden liquidar las rutas completadas.

> [!ADVERTENCIA] Finalizar no se puede deshacer. Si el chofer todavía tiene entregas sin registrar, esos pedidos quedarán como **No entregado** aunque se hayan entregado. Revise la tabla **Entrega** antes de confirmar.

### Si algo sale mal

| Mensaje | Qué hacer |
|---|---|
| Solo se puede finalizar una ruta despachada. La ruta … está … | La ruta ya se finalizó o se canceló. Actualice la página. |
| No fue posible finalizar la ruta. | Vuelva a intentar. Si continúa, avise a Soporte. |

## Cancelar una ruta {#mod-rutas-cancelar}

### Para qué sirve

Anular una ruta que no va a salir o que se suspendió. Sus pedidos quedan libres para asignarlos a otra ruta.

### Quién puede usarlo

Gerente y Administrador, cuando la ruta está **Planificada** o **Despachada**.

### Pasos

1. Abra la ruta.
2. Cancele la ruta:

   ![Cancelar una ruta](capturas/gerente/ger-rutas-14.png "ancho=8")

   1. Haga clic en **Cancelar ruta**.
   2. **Motivo**: escriba por qué se cancela.
   3. Haga clic en **Confirmar cancelación** y confirme con **Sí, cancelar**.

### Resultado esperado

Aparece «Ruta cancelada correctamente.» y la ruta pasa a **Cancelada**.

### Si algo sale mal

| Mensaje | Qué hacer |
|---|---|
| No se puede cancelar una ruta ya completada. / La ruta ya está cancelada. | La ruta ya terminó o ya se canceló. No hace falta cancelarla. |
| No fue posible cancelar la ruta. | Vuelva a intentar. Si continúa, avise a Soporte. |

## Consultar las evidencias de entrega {#mod-rutas-evidencias}

### Para qué sirve

Revisar las fotos y firmas que el chofer tomó al entregar, por ejemplo cuando un cliente dice que no recibió su pedido.

### Quién puede usarlo

Gerente y Administrador.

### Pasos

1. Abra la ruta.
2. Haga clic en **Evidencias**, arriba a la derecha.

### Resultado esperado

La pantalla **Evidencias de la ruta** muestra el **Resumen por pedido** (estado de la entrega y cuántas evidencias tiene) y, abajo, los **Archivos de evidencia**.

![Evidencias de una ruta](capturas/gerente/ger-rutas-15.png)

> [!NOTA] Si un pedido dice **Sin evidencia**, el chofer no tomó foto ni firma. Pídale que lo haga en las próximas entregas (vea [Mis rutas](#app-mis-rutas)).
