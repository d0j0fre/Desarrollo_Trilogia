# Tablero gerencial {#mod-tablero-gerencial}

El Tablero gerencial resume la salud del negocio en una sola pantalla: lo vendido en el período, los productos con poco inventario, los pedidos en la calle y lo que los clientes deben.

Se abre con el botón **Tablero** del menú superior, o en **Administración → Reportes y Gerencia → Tablero gerencial**.

## Consultar los indicadores {#mod-tablero-gerencial-consultar}

### Para qué sirve

Empezar el día sabiendo cuánto se vendió y qué necesita atención: inventario, reparto y cobros.

### Quién puede usarlo

Gerente y Administrador (solo consulta).

### Pasos

1. Abra **Tablero**. Al entrar muestra los datos de **Hoy**.
2. Elija el período:

   ![Período del tablero gerencial](capturas/gerente/ger-tablerog-01.png)

   1. Haga clic en **Hoy**, **Semana** (los últimos 7 días) o **Mes** (desde el día 1 del mes hasta hoy). El botón elegido queda resaltado.
   2. O escriba un **Rango personalizado**: la fecha inicial y la final.
   3. Haga clic en **Aplicar**.

   Arriba a la derecha se ve el período que se está mostrando.

3. Lea los indicadores:

   ![Indicadores y gráfico de ventas de octubre](capturas/gerente/ger-tablerog-02.png)

   1. **Ventas del período**: total facturado en el período, con el número de facturas no anuladas y el ticket promedio (venta promedio por factura).
   2. **Stock bajo**: productos activos con poco inventario. Debajo dice cuántos están agotados.
   3. **Pedidos en ruta**: entregas que los choferes llevan en este momento.
   4. **Cobros pendientes**: lo que los clientes con crédito deben hoy.
   5. **Ventas facturadas por día**: una barra por cada día con ventas. Pase el puntero sobre una barra para ver la fecha y el monto exacto.

4. Para ver el detalle de un indicador, haga clic en su tarjeta (**Ventas del período**, **Stock bajo**, **Pedidos en ruta** o **Cobros pendientes**). Abajo se abre **Detalle** con sus gráficos de barras:

   | Indicador | Gráficos del detalle |
   |---|---|
   | **Ventas del período** | Ventas por categoría, productos más vendidos y ventas por vendedor. |
   | **Stock bajo** | Productos con stock bajo o agotados por categoría y productos más cerca de agotarse. |
   | **Pedidos en ruta** | Pedidos en ruta por ruta y entregas de rutas abiertas por estado. |
   | **Cobros pendientes** | Clientes con mayor saldo por cobrar. |

   ![Detalle del indicador Ventas del período](capturas/gerente/ger-tablerog-04.png)

   Haga clic en **Cerrar detalle** para ocultarlo. El detalle conserva el período que eligió.

### Resultado esperado

Los indicadores se calculan para el período elegido. **Stock bajo**, **Pedidos en ruta** y **Cobros pendientes** siempre muestran la situación **de este momento**, sin importar el período (en **Cobros pendientes**, solo los movimientos de crédito del detalle se filtran por el período).

Una nota al pie del tablero recuerda la regla: «Venta = facturas no anuladas, la misma definición del Reporte de ventas.». Por eso **Ventas del período** coincide con el [Reporte de ventas](#mod-reporte-ventas) para las mismas fechas.

> [!NOTA] Las pantallas por vendedor ([Metas de ventas](#mod-metas), [Reporte de KPIs](#mod-kpis) y [Desempeño de vendedores](#mod-desempeno)) solo cuentan los pedidos que tienen vendedor asignado. Por eso pueden mostrar menos que el tablero.

> [!CONSEJO] Si ve stock bajo, revise qué comprar en [Inventario](#mod-inventario). Si los cobros pendientes suben, coordine con Crédito y Cobro.

### Si algo sale mal

![Aviso cuando el período no tiene movimientos](capturas/gerente/ger-tablerog-03.png)

| Mensaje | Qué hacer |
|---|---|
| Aún no hay transacciones registradas para el período seleccionado. | En esas fechas no hubo pedidos ni facturas. Elija **Semana**, **Mes** u otro rango. |
| No hay ventas facturadas en el período. | Hubo pedidos, pero ninguno se facturó todavía. |
| La fecha inicial (…) no puede ser posterior a la final (…). Se invirtieron las fechas y se muestra el período … | Escribió el rango al revés. El tablero lo corrige solo y se lo avisa en un cuadro amarillo; revise que el período mostrado sea el que quería. |
| No hay datos para mostrar. | En el detalle de un indicador, ese gráfico no tiene información para el período. |
