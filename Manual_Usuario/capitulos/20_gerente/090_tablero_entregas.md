# Tablero de entregas {#mod-tablero-entregas}

El Tablero de entregas muestra en una sola pantalla cómo van las rutas del día: cuántas entregas faltan, cuántas se hicieron y cuántas fallaron. Se actualiza solo, sin que usted tenga que recargar la página.

Se abre en **Administración → Logística y Flota → Tablero de entregas**.

## Consultar el avance de las entregas {#mod-tablero-entregas-consultar}

### Para qué sirve

Seguir el reparto en tiempo real y detectar a tiempo una ruta atrasada o con entregas fallidas.

### Quién puede usarlo

Gerente y Administrador (solo consulta).

### Pasos

1. Abra **Tablero de entregas**.
2. Lea los indicadores y la tabla:

   ![Tablero de entregas](capturas/gerente/ger-tablero-01.png)

   - Arriba a la derecha, **Actualizado** indica la hora de la última actualización. El tablero se refresca cada 15 segundos.
   - Las cinco tarjetas resumen el reparto:

     | Tarjeta | Qué cuenta |
     |---|---|
     | **Rutas activas** | Rutas **Planificadas** o **Despachadas** (todavía abiertas). |
     | **En ruta** | Pedidos que ya salieron con el chofer y esperan la entrega. |
     | **Pendientes** | Pedidos a domicilio que todavía no salen: están en una ruta planificada o sin ruta. |
     | **Entregadas** | Entregas que el chofer marcó como entregadas. |
     | **Fallidas** | Entregas que no se pudieron hacer. |

   - **Pedidos en ruta**: la lista de los pedidos que van con el chofer, con su cliente, dirección, ruta, chofer, número de parada y total.
   - **Pendientes de entrega**: la lista de los pedidos a domicilio que todavía no salen, con su ruta (o **Sin ruta**), prioridad, fecha y total. El número de la tarjeta **Pendientes** es el de esta lista.
   - **Rutas**: cada ruta con su zona, chofer, placa, estado y progreso. Primero aparecen las rutas abiertas.
3. Para ver los pedidos de una ruta, haga clic en **Ver ruta**. Se abre su detalle en [Rutas y entregas](#mod-rutas-consultar).

### Resultado esperado

Los números cambian solos a medida que los choferes registran sus entregas desde el teléfono. Usted puede dejar el tablero abierto en una pantalla durante el día.

> [!NOTA] Los pedidos **Sin ruta** necesitan una ruta: arme una en [Rutas y entregas](#mod-rutas-crear). Las tarjetas **Entregadas** y **Fallidas** suman también las rutas completadas de la lista; para saber cuánto falta **hoy**, mire la columna **Progreso** de las rutas **Despachadas**.

### Si algo sale mal

| Situación | Qué hacer |
|---|---|
| La hora de **Actualizado** no cambia. | Se perdió la conexión o la pestaña estuvo oculta. Recargue la página (tecla F5). |
| Una ruta lleva mucho tiempo con entregas **pendientes**. | Abra la ruta con **Ver ruta** y, si hay un imprevisto, recalcúlela (vea [Recalcular una ruta en curso](#mod-rutas-recalcular)). |
