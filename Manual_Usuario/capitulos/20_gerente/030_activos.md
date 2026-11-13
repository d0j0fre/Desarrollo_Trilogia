# Activos (equipos) {#mod-activos}

Los activos son los equipos de la empresa que se prestan a los clientes: neveras, exhibidores y otros. Cada uno tiene un código de activo (su placa). Aquí se registran y se consulta su historial; el préstamo a un cliente se hace en [Comodatos](#mod-comodatos).

Se abre en **Administración → Inventario → Activos**.

## Registrar un activo {#mod-activos-registrar}

### Para qué sirve

Dar de alta un equipo nuevo para poder prestarlo a un cliente y seguir quién lo tiene.

### Quién puede usarlo

Gerente y Administrador.

### Pasos

1. Abra **Activos** y haga clic en **Nuevo activo**.

   ![Lista de activos con el botón Nuevo activo](capturas/gerente/ger-activos-01.png)

2. Complete el formulario **Nuevo activo**:

   ![Formulario Nuevo activo](capturas/gerente/ger-activos-02.png "ancho=9")

   1. **Código de activo (placa)**: el código que lleva pegado el equipo, por ejemplo **ACT-0107**. No puede repetirse.
   2. **Nombre**: una descripción corta, por ejemplo «Nevera exhibidora 3 puertas».
   3. **Tipo**: **Nevera**, **Exhibidor** u **Otro**.
   4. **Descripción**: la marca, la capacidad u otro dato útil. Es opcional.
   5. Haga clic en **Guardar**. Para salir sin guardar, haga clic en **Cancelar**.

### Resultado esperado

Aparece «Activo registrado correctamente.» y el equipo se agrega a **Inventario de activos** con el estado **Disponible**.

![El activo nuevo aparece en la lista como Disponible](capturas/gerente/ger-activos-03.png)

El estado de cada equipo lo cambia el sistema, según los préstamos:

| Estado | Qué significa |
|---|---|
| Disponible | Está en la bodega y se puede prestar. |
| Prestado | Está en el local de un cliente. La columna **Prestado a** dice cuál. |
| Mantenimiento | Volvió de un préstamo con daños y está en reparación. Cuando esté reparado, se marca como disponible (vea [Marcar como disponible un equipo reparado](#mod-activos-disponible)). |
| Baja | Se inactivó. Ya no se usa, pero su historial se conserva. |

> [!NOTA] El estado y el cliente no se cambian en este formulario. Cambian solos al registrar un préstamo o una devolución en [Comodatos](#mod-comodatos), y con el botón **Marcar como disponible** del detalle del activo.

### Si algo sale mal

![Mensaje cuando no se pudo registrar el activo](capturas/gerente/ger-activos-05.png "ancho=8")

| Mensaje | Causa | Qué hacer |
|---|---|---|
| El código de activo es obligatorio. | Dejó vacío el código. | Escriba el código de la placa. |
| El nombre es obligatorio. | Dejó vacío el nombre. | Escriba un nombre para el equipo. |
| Ya existe un activo con ese código. | Otro equipo ya tiene esa placa. | Busque el código en la lista; si el equipo ya está registrado, no lo registre de nuevo. Si es otro equipo, revise la placa. |
| No fue posible registrar el activo. | Falla de conexión con el sistema. | Vuelva a intentar. Si continúa, avise a Soporte. |

## Consultar, editar o dar de baja un activo {#mod-activos-gestionar}

### Para qué sirve

Ver a quién se ha prestado un equipo y en qué condición volvió, corregir sus datos o sacarlo de uso cuando ya no sirve.

### Quién puede usarlo

Gerente y Administrador.

### Pasos

1. Abra **Activos**. Para encontrar un equipo, escriba su código, su nombre o el cliente, elija un estado si quiere y haga clic en **Buscar**.
2. En la fila del equipo, use los botones de **Acciones**:

   ![Botones de acciones de un activo](capturas/gerente/ger-activos-06.png)

   1. El **ojo** (**Ver detalle**) abre la ficha del equipo y su **Historial de comodatos**.
   2. El **lápiz** (**Editar**) abre **Editar activo**: corrija el código, el nombre, el tipo o la descripción y haga clic en **Guardar cambios**.
   3. El **círculo tachado** (**Inactivar**) da de baja el equipo. El sistema le pide confirmar: haga clic en **Sí, inactivar**.

   ![Ficha de un activo con su historial de préstamos](capturas/gerente/ger-activos-04.png)

   En la ficha, el **Historial de comodatos** muestra cada préstamo: cliente, fechas de entrega y devolución, la condición en que se entregó y en que volvió, y el destino. El botón rojo de la columna **Contrato** descarga el contrato de ese préstamo en PDF.

### Resultado esperado

- Al editar: «Activo actualizado correctamente.».
- Al inactivar: «Activo eliminado (dado de baja) correctamente.». El equipo queda con el estado **Baja**: ya no aparece para préstamos, pero su historial se conserva.

### Si algo sale mal

| Mensaje | Causa | Qué hacer |
|---|---|---|
| No se puede eliminar un activo que está prestado. Registre primero su devolución. | Quiso inactivar un equipo que está **Prestado**. | Registre primero su devolución en [Comodatos](#mod-comodatos-devolver) y después inactívelo. |
| Ya existe un activo con ese código. | Al editar, escribió un código que ya tiene otro equipo. | Use otro código. |
| No se encontró el activo. | Otra persona lo borró mientras usted trabajaba. | Vuelva a la lista y búsquelo de nuevo. |
| No se encontró el activo solicitado. | El equipo se borró o el enlace es viejo. | Vuelva a la lista y búsquelo de nuevo. |
| No fue posible eliminar el activo. | Falla de conexión con el sistema. | Vuelva a intentar. Si continúa, avise a Soporte. |

## Marcar como disponible un equipo reparado {#mod-activos-disponible}

### Para qué sirve

Cuando un equipo que volvió de un préstamo con daños (estado **Mantenimiento**) ya está reparado, marcarlo como disponible para volver a prestarlo a un cliente.

### Quién puede usarlo

Gerente y Administrador.

### Pasos

1. Abra **Activos**, elija **Mantenimiento** en la lista de estados y haga clic en **Buscar**.
2. En la fila del equipo, haga clic en el **ojo** (**Ver detalle**).
3. Arriba a la derecha de la ficha, haga clic en **Marcar como disponible**. El botón solo aparece en los equipos en **Mantenimiento**.

   ![Botón Marcar como disponible en la ficha de un equipo en mantenimiento](capturas/gerente/ger-activos-07.png)

4. El sistema pregunta si el equipo ya está reparado. Haga clic en **Sí, marcar disponible** (o en **Cancelar** para no hacer nada).

   ![Confirmación antes de marcar el equipo como disponible](capturas/gerente/ger-activos-08.png "ancho=8")

### Resultado esperado

Aparece «El activo quedó disponible para asignarlo en comodato.» y el estado del equipo cambia a **Disponible**. Ya aparece en la lista de equipos de **Asignar comodato** (vea [Asignar un equipo en comodato](#mod-comodatos-asignar)).

![El equipo quedó disponible](capturas/gerente/ger-activos-09.png)

> [!CONSEJO] Marque el equipo como disponible solo cuando el taller confirme que funciona. Así no se le entrega a un cliente un equipo dañado.

### Si algo sale mal

| Mensaje | Causa | Qué hacer |
|---|---|---|
| Solo un activo en mantenimiento se puede marcar como disponible. | Otra persona ya lo marcó, o el equipo cambió de estado. | Vuelva a cargar la ficha y revise el estado. |
| No se encontró el activo. | El enlace es viejo o el equipo se borró. | Vuelva a la lista y búsquelo de nuevo. |
| No fue posible marcar el activo como disponible. | Falla de conexión con el sistema. | Vuelva a intentar. Si continúa, avise a Soporte. |
