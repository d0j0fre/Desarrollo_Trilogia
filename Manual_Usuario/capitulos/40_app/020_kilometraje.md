# Kilometraje {#app-kilometraje}

En **Kilometraje** el chofer anota el kilometraje del tablero al salir y al volver. Funciona sin señal: la jornada se guarda en el teléfono y se envía sola.

**Quién la ve:** Chofer (sección **Operación en ruta**). En el sitio web, el mismo registro está en [Kilometraje (web)](#mod-kilometraje).

## Iniciar la jornada {#app-kilometraje-iniciar}

### Para qué sirve

Registrar con qué kilometraje sale el vehículo.

### Quién puede usarlo

Chofer, para el vehículo de una de sus rutas **Planificada** o **Despachada**.

### Pasos

1. En la pantalla de inicio, toque **Kilometraje**.
2. Complete **Iniciar jornada**:

   ![Iniciar la jornada del vehículo](capturas/app/app-kilometraje-01.png "ancho=6")

   1. **Vehículo**: toque y elija la placa. Cada opción muestra el último kilometraje registrado.
   2. **Kilometraje inicial**: escriba el número que marca el tablero.
   3. Toque **Iniciar jornada**.

### Resultado esperado

Aparece «Jornada iniciada.» y la pantalla cambia a **Jornada en curso**, con la etiqueta **Abierta**.

### Si algo sale mal

| Mensaje | Qué hacer |
|---|---|
| Elija el vehículo. | Toque **Vehículo** y elija la placa. |
| Escriba el kilometraje. / No puede ser negativo. | Escriba el número del tablero. |
| El vehículo marcaba … km. Revise el número. | Escribió menos de lo último registrado. Revise el tablero. |
| No tiene vehículo asignado | No tiene una ruta activa. Pida a operaciones que le asignen la ruta con su vehículo. |
| La placa aparece como «(jornada sin cerrar)» | Ese vehículo tiene una jornada abierta de otro día o de otra persona. Avise a operaciones. |

## Cerrar la jornada {#app-kilometraje-cerrar}

### Para qué sirve

Registrar con qué kilometraje vuelve el vehículo. El sistema calcula el recorrido y actualiza el odómetro.

### Quién puede usarlo

Chofer, con una jornada abierta.

### Pasos

1. Al volver, abra **Kilometraje**. Verá **Jornada en curso**:

   ![Jornada en curso lista para cerrar](capturas/app/app-kilometraje-02.png "ancho=6")

   1. **Kilometraje final**: escriba el número del tablero. Debajo aparece el **Recorrido** calculado.
   2. Toque **Cerrar jornada**.

2. Revise el resumen y toque **Cerrar jornada** otra vez para confirmar.

   ![Confirmar el cierre de la jornada](capturas/app/app-kilometraje-03.png "ancho=6")

### Resultado esperado

Aparece «Jornada cerrada.» y la pantalla vuelve a **Iniciar jornada** para el día siguiente.

![Jornada cerrada](capturas/app/app-kilometraje-04.png "ancho=6")

> [!NOTA] Sin señal, la tarjeta dice «Guardada en el teléfono, pendiente de enviar» y «Se va a enviar sola en cuanto haya señal; no hace falta registrarla de nuevo.». No la registre dos veces.

### Si algo sale mal

| Mensaje | Qué hacer |
|---|---|
| No puede ser menor a … km. | El kilometraje final no puede ser menor que el inicial. Revise el tablero. |
| No se pudo consultar su jornada. Revise la señal e intente de nuevo. | Busque señal y vuelva a abrir la pantalla. |
| En **Por enviar**: «La jornada ya fue cerrada.» / «El vehículo tiene una jornada sin cerrar.» | La jornada se registró también desde otro lado. Consulte a operaciones y luego descarte la acción. |
