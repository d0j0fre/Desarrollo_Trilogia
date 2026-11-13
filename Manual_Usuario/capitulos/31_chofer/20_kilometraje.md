# Kilometraje (web) {#mod-kilometraje}

En **Kilometraje** usted anota el kilometraje del vehículo al salir (inicio de la jornada) y al regresar (cierre). Con esos datos, la empresa controla el desgaste de cada vehículo y programa su mantenimiento.

## Iniciar la jornada del vehículo {#mod-kilometraje-iniciar}

### Para qué sirve

Registrar con qué kilometraje sale el vehículo.

### Quién puede usarlo

Chofer y Administrador. Cada chofer ve y cierra solo sus propias jornadas; el Administrador ve y cierra las de todos.

### Pasos

1. En el menú superior, haga clic en **Kilometraje**.
2. Complete el cuadro **Iniciar jornada**:

   ![Formulario para iniciar la jornada](capturas/chofer/cho-km-01.png "ancho=8")

   1. **Vehículo**: elija la placa. Cada opción muestra el odómetro registrado del vehículo.
   2. **Kilometraje inicial**: se llena con el odómetro registrado. Compárelo con el tablero y corríjalo si es mayor.
   3. **Observaciones (opcional)**: por ejemplo, la ruta del día.
   4. Haga clic en **Iniciar jornada**.

### Resultado esperado

Aparece «Jornada iniciada. Kilometraje inicial registrado.» y la jornada se agrega a la tabla **Jornadas registradas** sin kilometraje final.

### Si algo sale mal

![Mensaje cuando no se pudo iniciar la jornada](capturas/chofer/cho-km-05.png)

| Mensaje | Causa más común | Qué hacer |
|---|---|---|
| El kilometraje inicial (… km) no puede ser menor al odómetro actual del vehículo (… km). | Escribió menos que el odómetro registrado. | Revise el tablero y escriba un kilometraje igual o mayor. |
| El vehículo tiene una jornada de kilometraje sin cerrar. | El vehículo ya tiene una jornada abierta. | Ciérrela primero (o pida a quien lo usó que la cierre). |
| Revise los datos del kilometraje inicial. | No eligió el vehículo o el kilometraje está vacío. | Elija el vehículo y escriba el kilometraje. |
| No fue posible registrar el kilometraje inicial. | Falla momentánea. | Intente de nuevo. |

## Cerrar la jornada del vehículo {#mod-kilometraje-cerrar}

### Para qué sirve

Registrar con qué kilometraje vuelve el vehículo. El sistema calcula el recorrido del día y actualiza el odómetro.

### Quién puede usarlo

Chofer y Administrador.

### Pasos

1. Al regresar, abra **Kilometraje**. Busque la jornada abierta (la que no tiene **Km final**):

   ![Jornada abierta lista para cerrar](capturas/chofer/cho-km-02.png)

   1. **Km final**: escriba el kilometraje que marca el tablero.
   2. Haga clic en el botón cuadrado verde (**Cerrar jornada**).

2. Confirme con **Sí, cerrar**:

   ![Confirmación para cerrar la jornada](capturas/chofer/cho-km-03.png "ancho=9")

### Resultado esperado

Aparece «Jornada cerrada. Kilometraje final registrado.». La jornada muestra el **Km final**, el **Recorrido** y la etiqueta **Cerrada**.

![Jornada cerrada con el recorrido calculado](capturas/chofer/cho-km-04.png)

### Si algo sale mal

| Mensaje | Causa más común | Qué hacer |
|---|---|---|
| El kilometraje final no puede ser menor al inicial. | Escribió un número menor que el de salida. | Revise el número del tablero. |
| La jornada ya fue cerrada. | Ya se había cerrado. | No haga nada más: aparece **Cerrada**. |
| No se encontró la jornada indicada. | La jornada no es suya. | Cada chofer solo cierra sus jornadas. Consulte a logística. |
| No fue posible registrar el kilometraje final. | Falla momentánea. | Intente de nuevo. |

> [!CONSEJO] Desde la aplicación móvil puede iniciar y cerrar la jornada aunque no tenga señal (vea [Kilometraje en la aplicación](#app-kilometraje)).
