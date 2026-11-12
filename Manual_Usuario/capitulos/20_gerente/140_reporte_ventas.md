# Reporte de ventas {#mod-reporte-ventas}

El Reporte de ventas muestra lo facturado en un rango de fechas, agrupado por día, por mes o por categoría de producto. Se puede imprimir o descargar para trabajarlo en Excel.

Se abre con el botón **Ventas** del menú superior, o en **Administración → Reportes y Gerencia → Reporte de ventas**.

## Consultar las ventas {#mod-reporte-ventas-consultar}

### Para qué sirve

Comparar las ventas entre días, meses o categorías de producto, por ejemplo para preparar el cierre de mes o decidir qué promocionar.

### Quién puede usarlo

Gerente y Administrador (solo consulta).

### Pasos

1. Abra **Ventas**. Al entrar muestra el mes actual, agrupado por día.
2. Elija los filtros:

   ![Filtros del reporte de ventas](capturas/gerente/ger-ventas-01.png)

   1. **Desde**: la fecha inicial.
   2. **Hasta**: la fecha final. El rango puede ser de hasta dos años.
   3. **Agrupación**: **Diaria** (una fila por día), **Mensual** (una fila por mes) o **Categoría** (una fila por categoría de producto).
   4. **Categoría de factura**: deje **Todas** o elija una categoría para ver solo las facturas que incluyen productos de esa categoría.
   5. Haga clic en **Consultar**.

### Resultado esperado

Arriba aparecen cuatro totales del rango: **Ventas facturadas**, **Facturas**, **Pedidos** y **Ticket promedio** (venta promedio por factura). Abajo, la tabla con una fila por grupo.

![Ventas de enero a octubre agrupadas por mes](capturas/gerente/ger-ventas-02.png)

| Columna | Qué muestra |
|---|---|
| **Grupo** | El día, el mes o la categoría. |
| **Ventas** | Total facturado del grupo, impuestos incluidos. |
| **Facturas** / **Pedidos** | Cuántas facturas y pedidos tuvo el grupo. |
| **Ticket promedio** | Venta promedio por factura del grupo. |
| **Unidades** | Unidades de producto vendidas. |

> [!NOTA] El reporte suma todas las facturas que no están anuladas. Esa es la definición oficial de «venta» y la misma que usa el [Tablero gerencial](#mod-tablero-gerencial). En la agrupación **Mensual**, los meses aparecen en español (por ejemplo, **Enero 2026**).

### Si algo sale mal

![Mensaje cuando la fecha inicial es posterior a la final](capturas/gerente/ger-ventas-05.png)

| Mensaje | Qué hacer |
|---|---|
| La fecha inicial no puede superar la final. | Revise **Desde** y **Hasta**: la primera debe ser anterior. |
| El reporte admite un rango máximo de dos años. | Acorte el rango. Si necesita más, haga dos consultas. |
| No hay ventas para los filtros seleccionados. | No hubo facturas en esas fechas o en esa categoría. Amplíe el rango o elija **Todas**. |

## Imprimir o exportar el reporte {#mod-reporte-ventas-exportar}

### Para qué sirve

Llevar el reporte a una reunión en papel o en PDF, o abrirlo en Excel para hacer sus propios cálculos.

### Quién puede usarlo

Gerente y Administrador.

### Pasos

1. Consulte el reporte con los filtros que necesita.
2. Elija el formato:

   ![Botones Imprimir y CSV](capturas/gerente/ger-ventas-03.png)

   1. **Imprimir**: abre el reporte en una pestaña nueva, sin los filtros.
   2. **CSV**: descarga un archivo que se abre con Excel.

3. Si eligió **Imprimir**, en la pestaña nueva haga clic en **Imprimir**. En la ventana del navegador elija la impresora, o **Guardar como PDF**.

   ![Botón Imprimir de la versión para imprimir](capturas/gerente/ger-ventas-04.png)

### Resultado esperado

- **Imprimir**: sale el reporte con sus totales y su tabla.
- **CSV**: se descarga un archivo con nombre como `ventas-20260101-20261031.csv`, con las columnas **Grupo**, **Categoria**, **Ventas**, **Facturas**, **Pedidos**, **TicketPromedio** y **Unidades**.

### Si algo sale mal

| Situación | Qué hacer |
|---|---|
| Se abre una página en blanco o de error al imprimir o al descargar. | Los filtros tienen un error (por ejemplo, la fecha inicial es posterior a la final). Corrija los filtros, haga clic en **Consultar** y vuelva a intentarlo. |
| En Excel, todo el archivo CSV queda en una sola columna. | En Excel, use **Datos → Desde el texto/CSV**, elija el archivo y, en **Delimitador**, elija **Coma**. |
