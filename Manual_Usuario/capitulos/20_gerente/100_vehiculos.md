# Vehículos {#mod-vehiculos}

En Vehículos se registra la flotilla de reparto: placa, marca, descripción y capacidad de cada camión. Solo los vehículos activos se pueden asignar a una ruta.

Se abre en **Administración → Logística y Flota → Vehículos**.

## Registrar un vehículo {#mod-vehiculos-crear}

### Para qué sirve

Dar de alta un camión nuevo para poder asignarlo a las rutas y llevar su mantenimiento.

### Quién puede usarlo

Gerente y Administrador.

### Pasos

1. Abra **Vehículos** y haga clic en **Nuevo vehículo**.

   ![Botón Nuevo vehículo](capturas/gerente/ger-vehiculos-02.png)

2. Complete el formulario:

   ![Formulario para registrar un vehículo](capturas/gerente/ger-vehiculos-03.png "ancho=9")

   1. **Placa**: la placa del vehículo, por ejemplo «CRJ-004». No puede repetirse.
   2. **Marca**: por ejemplo «Hyundai». Es opcional.
   3. **Descripción**: el modelo y para qué se usa, por ejemplo «Camión refrigerado zona Puntarenas».
   4. **Capacidad (unidades/cajas)**: cuántas cajas o unidades carga.
   5. Deje marcado **Activo** para poder usarlo en las rutas.
   6. Haga clic en **Guardar**.

### Resultado esperado

Aparece el mensaje «Vehículo registrado correctamente.» y el vehículo se agrega a la lista con el estado **Activo**, **0 km** en el **Odómetro** y **0** rutas abiertas.

![Vehículo registrado en la lista](capturas/gerente/ger-vehiculos-04.png)

> [!NOTA] El **Odómetro** se actualiza solo con el kilometraje que registran los choferes al cerrar su jornada (vea [Kilometraje (web)](#mod-kilometraje)). Se usa para avisar los mantenimientos por kilometraje en [Alertas de flota](#mod-alertas-flota).

### Si algo sale mal

![Mensaje cuando no se pudo guardar el vehículo](capturas/gerente/ger-vehiculos-05.png "ancho=9")

| Mensaje | Causa más común | Qué hacer |
|---|---|---|
| Ya existe un vehículo con esa placa. | La placa ya está registrada (puede estar inactivo). | Busque la placa en la lista. Si está inactivo, reactívelo en vez de crearlo de nuevo. |
| La placa y la descripción son obligatorias. / La placa es obligatoria. / La descripción es obligatoria. | Dejó el campo vacío. | Complete el campo. |
| La capacidad debe ser un número válido. | Escribió letras o un número mayor que 100 000. | Escriba solo números. |

## Buscar, editar o inactivar un vehículo {#mod-vehiculos-editar}

### Para qué sirve

Corregir los datos de un vehículo o sacarlo de servicio (por venta, choque o reparación larga) sin perder su historial.

### Quién puede usarlo

Gerente y Administrador.

### Pasos

1. Busque el vehículo y elija la acción:

   ![Lista de vehículos con sus acciones](capturas/gerente/ger-vehiculos-01.png)

   1. Escriba la placa o parte de la descripción.
   2. Haga clic en **Buscar**. Para ver todos de nuevo, haga clic en **Limpiar**.
   3. Para editar, haga clic en el botón amarillo del lápiz. Se abre el mismo formulario del registro: corrija los datos y haga clic en **Guardar**.
   4. Para inactivar, haga clic en el botón rojo y confirme con **Sí, cambiar**. En un vehículo inactivo, el botón es verde y sirve para reactivarlo.

### Resultado esperado

- Al editar: «Vehículo actualizado correctamente.».
- Al inactivar: «Vehículo inactivado.». El vehículo ya no aparece al crear rutas ni mantenimientos.
- Al reactivar: «Vehículo reactivado.».

La columna **Rutas abiertas** indica en cuántas rutas planificadas o despachadas está el vehículo.

> [!NOTA] Un vehículo con una orden **Correctiva** **En proceso** en [Mantenimiento de flota](#mod-mantenimiento) aparece con el estado **En reparación**. No se puede elegir para una ruta y vuelve a circular cuando usted completa la orden.

### Si algo sale mal

![Mensaje al intentar inactivar un vehículo con rutas abiertas](capturas/gerente/ger-vehiculos-06.png)

| Mensaje | Causa más común | Qué hacer |
|---|---|---|
| No se puede inactivar un vehículo con rutas abiertas. | La columna **Rutas abiertas** es mayor que 0. | Finalice o cancele sus rutas en [Rutas y entregas](#mod-rutas). Después vuelva a intentar. |
| Ya existe un vehículo con esa placa. | Al editar, escribió una placa que ya tiene otro vehículo. | Corrija la placa. |
| El vehículo está en reparación (orden correctiva en proceso): complete la orden en Mantenimiento para volver a activarlo. | Intentó reactivar un vehículo con una orden correctiva **En proceso**. | Complete la orden en [Mantenimiento de flota](#mod-mantenimiento). |
| No se encontró el vehículo solicitado. | El vehículo ya no existe. | Vuelva a la lista y búsquelo de nuevo. |
