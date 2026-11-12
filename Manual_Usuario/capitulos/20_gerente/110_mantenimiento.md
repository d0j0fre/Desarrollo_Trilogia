# Mantenimiento de flota {#mod-mantenimiento}

En Mantenimiento de flota se registran las visitas al taller de cada vehículo: las preventivas (cambio de aceite, llantas, revisiones) y las correctivas (reparaciones). Así se lleva el control de lo que se gasta en cada camión y se reciben avisos antes de que toque el siguiente servicio.

Se abre en **Administración → Logística y Flota → Mantenimiento**.

## Registrar una orden de mantenimiento {#mod-mantenimiento-crear}

### Para qué sirve

Dejar constancia de un servicio programado o de una reparación, con su taller y su costo.

### Quién puede usarlo

Gerente y Administrador.

### Pasos

1. Abra **Mantenimiento**. A la izquierda está el formulario **Nueva orden**.
2. Complete los datos:

   ![Formulario Nueva orden de mantenimiento](capturas/gerente/ger-mant-01.png "ancho=8")

   1. **Vehículo**: elija la placa. Solo aparecen los vehículos activos.
   2. **Tipo**: **Preventivo** (servicio planeado) o **Correctivo** (reparación de una falla).
   3. **Descripción**: qué se le hace al vehículo. **Taller**: dónde se hace (opcional).
   4. **Costo (₡)**: el monto del servicio, sin símbolos. **Estado**: **Programada**, **En proceso** o **Completada**.
   5. **Fecha programada**: el día en que debe ir al taller. **Próximo (km)**: el kilometraje en que toca el servicio. Llene uno de los dos, o ambos, si quiere recibir el aviso en [Alertas de flota](#mod-alertas-flota).
   6. **Fecha de realización**: solo para una orden **Completada**. Si la deja vacía, el sistema usa la fecha de hoy. No puede ser futura.
   7. Haga clic en **Registrar orden**.

### Resultado esperado

Aparece el mensaje «Orden de mantenimiento registrada.» y la orden se agrega al **Historial de mantenimiento**. Si la orden es **Correctiva** y queda **En proceso**, el mensaje agrega que el vehículo quedó fuera de circulación hasta que se complete la reparación.

![Orden registrada en el historial](capturas/gerente/ger-mant-02.png)

> [!NOTA] Una orden **Programada** o **En proceso**, preventiva o correctiva, genera avisos en [Alertas de flota](#mod-alertas-flota) cuando su fecha programada se acerca o ya pasó. Mientras una orden **Correctiva** está **En proceso**, el vehículo queda fuera de circulación y no se puede elegir para una ruta.

### Si algo sale mal

| Mensaje | Qué hacer |
|---|---|
| Debe seleccionar un vehículo. | Elija la placa en **Vehículo**. |
| La descripción es obligatoria. | Escriba qué se le hace al vehículo. |
| El costo no es válido. | Escriba solo números, sin el símbolo ₡ ni espacios. |
| La fecha de realización no puede ser futura. | Escriba la fecha de hoy o una anterior. |
| No fue posible registrar la orden de mantenimiento. | Vuelva a intentar. Si continúa, avise a Soporte. |

## Iniciar, filtrar y completar órdenes {#mod-mantenimiento-completar}

### Para qué sirve

Revisar qué servicios están pendientes, pasar a **En proceso** los que el taller ya empezó y cerrar los que terminó.

### Quién puede usarlo

Gerente y Administrador.

### Pasos

1. Use el filtro y elija la orden:

   ![Historial de mantenimiento con el filtro y el botón para completar](capturas/gerente/ger-mant-03.png "ancho=11")

   1. Elija un estado (**Programada**, **En proceso** o **Completada**) o deje **Todos los estados**.
   2. Haga clic en **Filtrar**. Para ver todas de nuevo, haga clic en **Limpiar**.

2. Cuando el taller empiece un servicio **Programado**, haga clic en el botón de iniciar (▶) de su fila y confirme con **Sí, iniciar**. La orden pasa a **En proceso**. Si es una reparación correctiva, el vehículo queda fuera de circulación.
3. Cuando el taller termine un servicio, haga clic en el botón verde (✔) de su fila (la flecha lo señala). Debajo de la fila se abre un formulario.
4. Escriba el **Costo final (₡)** (el monto real que cobró el taller) y la **Fecha de realización** (si la deja vacía, se usa la de hoy) y haga clic en **Completar orden**.
5. En la ventana de confirmación, haga clic en **Sí, completar**.

### Resultado esperado

Aparece «Orden marcada como completada. Si era una reparación, el vehículo volvió a circulación.». La orden pasa a **Completada** y muestra la fecha de realización y el costo final. Deja de aparecer en Alertas de flota. Al iniciar, aparece «Orden en proceso. Si es correctiva, el vehículo quedó fuera de circulación.».

### Si algo sale mal

| Mensaje | Qué hacer |
|---|---|
| La fecha de realización no puede ser futura. | Escriba la fecha de hoy o una anterior. |
| No fue posible iniciar la orden. / No fue posible completar la orden. | La orden ya cambió de estado o ya no existe. Actualice la página y vuelva a intentar. Si continúa, avise a Soporte. |
