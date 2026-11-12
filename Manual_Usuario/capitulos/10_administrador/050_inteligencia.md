# Inteligencia de inventario {#mod-inteligencia}

Este reporte analiza las ventas para decirle qué productos comprar y cuántos, cuáles no se están vendiendo y en qué meses se vende más. Los cálculos usan solo pedidos válidos (no cuentan los cancelados). El Gerente también abre esta pantalla, porque desde ella puede crear promociones para los productos que no se venden.

Se abre en **Administración → Reportes y Gerencia → Inteligencia de inventario**.

## Consultar las sugerencias de compra {#mod-inteligencia-consultar}

### Para qué sirve

Saber qué productos reponer antes de que se agoten, y en qué cantidad, según lo que se ha vendido en los últimos meses.

### Quién puede usarlo

Administrador y Gerente.

### Pasos

1. Abra **Administración → Reportes y Gerencia → Inteligencia de inventario**.
2. Revise las tarjetas y la tabla **Sugerencias de abastecimiento**:

   ![Tarjetas y sugerencias de abastecimiento](capturas/administrador/adm-inteligencia-02.png)

   - **SUGERENCIAS DE COMPRA**: cuántos productos conviene reponer.
   - **PRODUCTOS ESTANCADOS**: productos con existencias que no se han vendido en el período elegido.
   - **MESES COMPARABLES**: cuántos meses del año tienen datos para comparar.
   - En la tabla, la columna **SUGERENCIA** dice cuántas unidades comprar de cada producto.

### Resultado esperado

Cada fila muestra el **STOCK / MÍNIMO** actual, lo vendido en el período (**VENTA VENTANA**), el **PROMEDIO MENSUAL** y la **COBERTURA** (cuántos meses quiere tener cubiertos). La sugerencia se calcula así:

> promedio mensual × meses de cobertura + stock mínimo − stock actual

Por ejemplo, Yucas Tostadas vende 19 unidades al mes: para 2 meses de cobertura y un mínimo de 10 necesita 48 unidades. Hay 40, así que la sugerencia es **Comprar 8**.

Si un producto dice **Datos insuficientes**, todavía no tiene ventas suficientes para calcular el promedio. En lugar de una cantidad, la columna **SUGERENCIA** dice **Ingresar cantidad manualmente**: decida usted cuántas unidades comprar y escríbalas en la pantalla **Sugerencias de compra** (el enlace **Ingresarla en Sugerencias de compra** lo lleva allí).

> [!CONSEJO] Use estas cantidades al crear una [orden de compra](#mod-ordenes-compra-crear). La pantalla **Sugerencias de compra** de Órdenes de compra usa el mismo cálculo que esta pantalla y trae las cantidades ya escritas.

## Cambiar los parámetros del análisis {#mod-inteligencia-parametros}

### Para qué sirve

Ajustar el análisis a su forma de comprar: cuántos meses de ventas promediar, para cuántos meses comprar y desde cuántos días sin venta un producto se considera estancado.

### Quién puede usarlo

Administrador y Gerente.

### Pasos

1. En el recuadro **Parámetros del análisis**, cambie los valores que necesite:

   ![Parámetros del análisis](capturas/administrador/adm-inteligencia-01.png)

   1. **Año inicial**: primer año que se toma en cuenta para la tendencia estacional.
   2. **Año final**: último año.
   3. **Meses para promedio**: cuántos meses recientes se usan para calcular el promedio mensual de ventas (de 1 a 12).
   4. **Meses de cobertura**: para cuántos meses quiere tener existencias (de 1 a 12).
   5. **Ventana sin rotación**: cuántos días sin ventas hacen que un producto se considere estancado (de 30 a 365; el sistema propone 60).
   6. **Categoría (tendencia estacional)**: elija una categoría para ver la tendencia solo de esos productos, o deje **Todas las categorías**.
   7. Haga clic en **Actualizar**.

### Resultado esperado

La pantalla se vuelve a calcular con los valores nuevos. La línea **Generado …** de la parte de arriba muestra la fecha y hora del cálculo.

## Revisar productos sin rotación y la tendencia estacional {#mod-inteligencia-tendencia}

### Para qué sirve

Detectar mercadería que no se mueve (y ocupa espacio y dinero) y conocer los meses de más venta para planificar las compras de temporada.

### Quién puede usarlo

Administrador y Gerente.

### Pasos

1. Si hay productos estancados, arriba aparece el aviso **Alerta gerencial: N producto(s) estancado(s)**. Haga clic en **Ver productos** o baje hasta **Productos sin rotación**.

   ![Productos sin rotación con el botón para incluirlos en una promoción](capturas/administrador/adm-inteligencia-04.png)

   Un producto está estancado cuando está activo, tiene existencias, fue creado hace al menos los días de la **Ventana sin rotación** y no se vendió en ese tiempo. Si dice **Sin productos estancados**, todos los productos con existencias se vendieron dentro de la ventana elegida. El aviso se quita solo cuando el producto vuelve a venderse.
2. Para darle salida a un producto, haga clic en **Incluir en promoción** en su fila: se abre el formulario de [Promociones](#mod-promociones) con el producto ya elegido.
3. Baje hasta **Tendencia estacional**. El gráfico de barras compara los 12 meses y la tabla muestra el **TOTAL VENDIDO (IVA INCLUIDO)** y las **UNIDADES** de cada mes, sumando los años elegidos. Los totales son los mismos del [Reporte de ventas](#mod-reporte-ventas): facturas no anuladas, con IVA.

   ![Tendencia estacional de ventas por mes](capturas/administrador/adm-inteligencia-03.png)

4. Para llevar la tabla a Excel, haga clic en **Exportar Excel**. Se descarga un archivo con el período, la categoría elegida y los totales por mes.

### Resultado esperado

Los meses con las barras más altas son los de más venta. Prepare las compras de esos meses con anticipación.

### Si algo sale mal

| Mensaje | Qué hacer |
|---|---|
| No fue posible cargar los indicadores en este momento. | Vuelva a cargar la página en unos minutos. Si continúa, avise a Soporte. |
| El año inicial no puede ser mayor que el año final. | Corrija los años. El sistema no recalcula hasta que el rango sea válido. |
| El período estacional no puede superar 10 años. | Elija un rango de 10 años como máximo. |
| El año inicial debe estar entre 2000 y 2100. (o el año final) | Escriba un año entre 2000 y 2100. |
| Los meses para el promedio deben estar entre 1 y 12. (o los meses de cobertura) | Use un número de 1 a 12. |
| La ventana sin rotación debe estar entre 30 y 365 días. | Use un número de 30 a 365. |
