# Reporte de inventario {#mod-reporte-inventario}

El Reporte de inventario muestra cuántas unidades hay de cada producto y cuánto vale la existencia al precio de venta. Se puede filtrar y descargar en Excel para hacer el conteo físico o preparar compras.

Se abre en **Administración → Reportes y Gerencia → Reporte de inventario**, o con el botón **Reporte / Exportar** de la pantalla [Inventario](#mod-inventario).

## Consultar el inventario {#mod-reporte-inventario-consultar}

### Para qué sirve

Ver de un vistazo qué productos están agotados o con poco stock, y cuánto dinero hay en mercadería.

### Quién puede usarlo

Gerente y Administrador (solo consulta).

### Pasos

1. Abra **Reporte de inventario**. Al entrar muestra todos los productos activos.
2. Si busca algo en particular, use los filtros:

   ![Filtros y totales del reporte de inventario](capturas/gerente/ger-invrep-01.png)

   1. **Producto, SKU o categoría**: escriba parte del nombre, del código o de la categoría.
   2. **Estado de existencias**: **Todos**, **Agotados**, **Stock bajo** o **Normal**.
   3. Marque **Incluir productos inactivos** si también quiere verlos.
   4. Haga clic en **Consultar**.

### Resultado esperado

Arriba aparecen cuatro totales: **Productos**, **Unidades en existencia**, **Valor a precio de venta** y **Agotados / stock bajo**. Abajo, la tabla con una fila por producto:

![Tabla del reporte de inventario](capturas/gerente/ger-invrep-02.png)

| Columna | Qué muestra |
|---|---|
| **Producto** y **Categoría** | El nombre y la categoría del producto. |
| **Existencias** y **Mínimo** | Las unidades que hay y las que debería tener como mínimo. |
| **Precio** y **Valor** | El precio de venta y el valor de las existencias (precio por unidades). |
| **Estado** | **Normal**, **Bajo** (por debajo del mínimo) o **Agotado** (sin unidades). |
| **Vence** | La fecha de vencimiento, si el producto la tiene. |

> [!CONSEJO] Para decidir qué comprar, combine este reporte con [Inteligencia de inventario](#mod-inteligencia), que sugiere cantidades y avisa de los productos que no se venden.

### Si algo sale mal

| Mensaje | Qué hacer |
|---|---|
| No hay productos para los filtros seleccionados. | Cambie el texto de búsqueda o el estado. Si el producto está inactivo, marque **Incluir productos inactivos**. |

## Exportar el inventario a Excel {#mod-reporte-inventario-exportar}

### Para qué sirve

Llevar el inventario a una hoja de cálculo, por ejemplo para imprimir la lista del conteo físico.

### Quién puede usarlo

Gerente y Administrador.

### Pasos

1. Consulte el reporte con los filtros que necesita (vea el procedimiento anterior).
2. Haga clic en **Exportar Excel**, arriba a la derecha.

### Resultado esperado

El navegador descarga un archivo con nombre como `inventario-2026-11-11.xlsx` y una hoja **Inventario**. Arriba trae los totales del reporte y, debajo, una fila por producto con las columnas **Producto**, **Categoría**, **Existencias**, **Mínimo**, **Precio (₡)**, **Valor (₡)**, **Estado** y **Vence**. Trae solo los productos que están a la vista con sus filtros.

### Si algo sale mal

| Situación | Qué hacer |
|---|---|
| No se descarga el archivo. | Revise que el navegador no haya bloqueado la descarga. Si el reporte dice que no hay productos, no hay nada que exportar. |
| Falta un producto en el archivo. | El archivo respeta los filtros. Haga clic en **Consultar** con los filtros vacíos y vuelva a exportar. |
