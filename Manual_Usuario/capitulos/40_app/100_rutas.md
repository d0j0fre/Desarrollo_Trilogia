# Rutas y choferes {#app-rutas}

**Rutas y choferes** le permite a la gerencia seguir las rutas en la calle, despachar las planificadas y cambiar el chofer o el vehículo de una ruta.

**Quién la ve:** Gerente y Administrador (sección **Gestión**). Las rutas se crean en el sitio web (vea [Rutas y entregas](#mod-rutas)).

## Seguir las rutas {#app-rutas-seguir}

### Para qué sirve

Ver cuánto avanzó cada ruta y si hay entregas fallidas.

### Quién puede usarlo

Gerente y Administrador.

### Pasos

1. En la pantalla de inicio, toque **Rutas y choferes**.

   ![Rutas en la calle](capturas/app/app-rutas-01.png "ancho=6")

   1. **En calle**: rutas despachadas (se muestra al abrir).
   2. **Planificadas**, **Completadas** o **Todas**: cambian el filtro.
   3. Cada ruta muestra la zona, el estado, el chofer, la placa, las paradas atendidas y las fallidas. Tóquela para ver sus paradas.

### Resultado esperado

En el detalle de la ruta aparece cada parada con su estado. Al tocar una parada se abre el pedido.

## Despachar una ruta {#app-rutas-despachar}

### Para qué sirve

Dar la salida a una ruta planificada: sus pedidos pasan a **En ruta** y los clientes reciben un aviso.

### Quién puede usarlo

Gerente y Administrador, para rutas **Planificadas** con paradas.

### Pasos

1. En **Rutas y choferes**, toque **Planificadas** y abra la ruta.

   ![Ruta planificada](capturas/app/app-rutas-02.png "ancho=6")

   1. **Despachar ruta**: da la salida.
   2. **Reasignar chofer o vehículo** (vea [Reasignar una ruta](#app-rutas-reasignar)).

2. Toque **Despachar ruta**, lea la confirmación y toque **Despachar**.

### Resultado esperado

Aparece «Ruta despachada: … pedidos en ruta.». La ruta pasa a **Despachada** y el chofer la ve lista para trabajar en [Mis rutas](#app-mis-rutas).

### Si algo sale mal

| Mensaje | Qué hacer |
|---|---|
| La ruta no tiene pedidos para despachar. | Agregue pedidos a la ruta en el sitio web. |
| Solo se pueden despachar rutas en estado Planificada. | Ya se despachó. Actualice la lista. |

## Reasignar una ruta {#app-rutas-reasignar}

### Para qué sirve

Cambiar el chofer o el vehículo de una ruta, por ejemplo si el chofer se enfermó o el vehículo se averió.

### Quién puede usarlo

Gerente y Administrador, para rutas **Planificadas** o **Despachadas**.

### Pasos

1. En el detalle de la ruta, toque **Reasignar chofer o vehículo**.
2. Complete los datos:

   ![Reasignar una ruta](capturas/app/app-rutas-03.png "ancho=6")

   1. **Chofer**: elija el nuevo chofer. La lista indica cuántas rutas abiertas tiene cada uno.
   2. **Vehículo**: elija el vehículo.
   3. **Motivo**: por ejemplo, «el chofer se reportó enfermo» (al menos 5 caracteres).
   4. Toque **Reasignar**.

### Resultado esperado

Aparece «Ruta reasignada. El chofer nuevo recibe una notificación y la ve en Mis rutas.». En una ruta ya en la calle, las entregas cerradas conservan su historia y las pendientes pasan al chofer nuevo.

### Si algo sale mal

| Mensaje | Qué hacer |
|---|---|
| Elija otro chofer o vehículo para continuar. | Cambie al menos el chofer o el vehículo. |
| Explique el motivo de la reasignación (al menos 5 caracteres). | Escriba el motivo. |
| Solo se pueden reasignar rutas planificadas o despachadas. | La ruta ya se completó o se canceló. |
| El chofer seleccionado no existe o está inactivo. | Elija otro chofer. |
