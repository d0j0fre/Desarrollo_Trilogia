# Presupuesto vs. real {#mod-presupuesto-real}

Este reporte compara lo que cada departamento tenía presupuestado para el año con lo que realmente gastó. También proyecta cuánto se gastará al cierre del año si se sigue al mismo ritmo, y permite bajar hasta el detalle de cada gasto.

Se abre en **Administración → Finanzas → Presupuesto vs. real**.

> [!NOTA] Los datos vienen de [Presupuestos anuales](#mod-presupuestos) (solo los aprobados) y de [Gastos operativos](#mod-gastos). Un gasto cuenta como **real** cuando está **Aprobado** o **Pagado**; si está **Registrado** o **Requiere Aprobación**, aparece como **Pendiente**.

## Consultar el presupuesto contra lo gastado {#mod-presupuesto-real-consultar}

### Para qué sirve

Saber si los departamentos están gastando dentro de lo planeado y detectar a tiempo las categorías que se van a pasar del presupuesto.

### Quién puede usarlo

Administrador y Gerente (consulta, en **Administración → Finanzas → Presupuesto vs. real**).

### Pasos

1. Abra **Administración → Finanzas → Presupuesto vs. real**.
2. Elija qué quiere ver en la barra de filtros:

   ![Filtros del reporte Presupuesto vs. real](capturas/administrador/adm-presreal-01.png)

   1. **Año**: el año del presupuesto.
   2. **Departamento**: uno, o **Todos los departamentos**.
   3. **Mes**: un mes (1 = enero … 12 = diciembre) o **Año completo**.
   4. **Categoría** del gasto: Alquiler, Combustible, Mantenimiento, Servicios públicos, Suministros u Otros.
   5. **Estado** del gasto: **Registrado**, **Requiere Aprobación**, **Aprobado**, **Pagado** o **Rechazado**.
   6. Haga clic en el botón de actualizar (flechas circulares).

3. Revise los totales y las tablas:

   ![Totales y comparación por departamento y por categoría](capturas/administrador/adm-presreal-02.png)

   - **Presupuesto filtrado**: lo presupuestado para los filtros elegidos.
   - **Real (aprobado + pagado)**: lo gastado.
   - **Disponible**: presupuesto menos real. Sale en rojo si ya se pasó.
   - **Ejecución / proyección anual**: el porcentaje gastado y cuánto se gastará al cierre del año a este ritmo.
   - **Por departamento** y **Por categoría**: el mismo cálculo separado. La etiqueta de color de **Ejecución** cambia a amarillo o rojo cuando el gasto se acerca o pasa del presupuesto.

### Resultado esperado

Debajo de las tablas, la **Serie mensual** muestra mes por mes lo real frente a lo presupuestado. Una franja azul resume el gasto **Pendiente**, los **Departamentos sin presupuesto** y las **Categorías excedidas**.

Después de filtrar, las listas de la barra conservan lo que eligió, así que siempre sabe con qué filtros está viendo el reporte.

## Revisar el detalle de los gastos {#mod-presupuesto-real-detalle}

### Para qué sirve

Ver qué gastos componen el total, para explicar una diferencia o revisar un comprobante.

### Quién puede usarlo

Administrador.

### Pasos

1. Baje hasta **Detalle de gastos (drill-down)**.

   ![Resumen de pendientes y detalle de gastos](capturas/administrador/adm-presreal-03.png)

2. Revise cada gasto: fecha, departamento y categoría, número de **Documento**, **Estado** y **Total**. Use **Anterior** y **Siguiente** para pasar de página.
3. Haga clic en el número de documento para abrir el gasto en [Gastos operativos](#mod-gastos).

### Resultado esperado

Se abre el detalle del gasto con su comprobante y su historial.

## Exportar o imprimir el reporte {#mod-presupuesto-real-exportar}

### Para qué sirve

Llevar el reporte a una hoja de cálculo o presentarlo impreso en una reunión.

### Quién puede usarlo

Administrador.

### Pasos

1. Aplique los filtros que quiere exportar.
2. Use los botones de la parte de arriba:

   ![Botones Exportar Excel, CSV e Imprimir](capturas/administrador/adm-presreal-04.png)

   1. **Exportar Excel**: descarga un archivo de Excel (.xlsx) con los filtros, los totales, las tablas por departamento y por categoría, la serie mensual y la página visible del detalle de gastos.
   2. **CSV**: descarga los datos en un archivo de texto separado por comas, que también se abre con Excel.
   3. **Imprimir**: abre una versión del reporte lista para imprimir. Use **Ctrl + P** para imprimirla o guardarla como PDF.

### Resultado esperado

El archivo o la versión impresa contienen los mismos datos que la pantalla, con los filtros aplicados.

### Si algo sale mal

| Mensaje | Qué hacer |
|---|---|
| El reporte sale en ₡0,00 | No hay presupuestos **aprobados** para ese año o departamento. Revise los filtros y el estado de los presupuestos en [Presupuestos anuales](#mod-presupuestos). |
| No podemos completar esta solicitud en este momento. | Vuelva a intentar en unos minutos. Si continúa, avise a Soporte con el **Identificador de seguimiento** que aparece en la página. |
