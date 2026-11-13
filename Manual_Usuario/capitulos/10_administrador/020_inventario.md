# Inventario (catálogo de productos) {#mod-inventario}

El módulo Inventario es el catálogo de todos los productos que vende la empresa: nombre, categoría, precio, existencias y stock mínimo. Lo que se cambia aquí es lo que ven los clientes en la tienda en línea y los vendedores en la venta móvil.

Se abre en **Administración → Inventario → Inventario**.

> [!NOTA] Las entradas y salidas de mercadería (compras, ajustes, mermas) se registran en [Movimientos de inventario](#mod-movimientos). Aquí se administra la ficha de cada producto.

## Buscar un producto {#mod-inventario-buscar}

### Para qué sirve

Encontrar rápido un producto para revisar su precio, sus existencias o su estado.

### Quién puede usarlo

Administrador. Gerente, Empleado, Bodeguero y Compras lo consultan en **Administración → Inventario → Inventario** o en la aplicación móvil (vea [Consulta de inventario](#app-inventario)). Los botones **Nuevo producto**, **Editar**, **Inactivar** y **Destacado** solo se muestran a quien tiene el permiso para hacer ese cambio; quien solo consulta ve la lista sin esos botones.

### Pasos

1. Abra **Administración → Inventario → Inventario**.
2. Use el buscador de la parte de arriba:

   ![Buscador del inventario y alerta de stock bajo](capturas/administrador/adm-inventario-01.png)

   1. Escriba parte del nombre, el código SKU o la categoría. Por ejemplo, **ron**.
   2. Haga clic en **Buscar**.

   El recuadro amarillo **Alerta de inventario** aparece cuando hay productos agotados o con stock bajo, y dice cuáles son.

   Arriba a la derecha están los botones **Reporte / Exportar** (vea [Reporte de inventario](#mod-reporte-inventario)) y **Nuevo producto**.

### Resultado esperado

La tabla muestra solo los productos que coinciden. Para volver a ver todos, borre el texto del buscador y haga clic en **Buscar**.

![Resultado de la búsqueda: columnas Stock y Estado](capturas/administrador/adm-inventario-02.png)

| Columna | Qué indica |
|---|---|
| **Código** | Código SKU. El sistema lo asigna solo y no se puede cambiar. |
| **Imagen** | La foto del producto. Si el producto no tiene foto, el sistema muestra la imagen de su categoría (por ejemplo, la de cervezas), o un dibujo neutro si la categoría no tiene imagen. |
| **Destacado** | **Sí** si el producto aparece en la sección de destacados de la tienda. |
| **Stock** | Unidades en bodega, con la etiqueta **Disponible**, **Stock bajo** (igual o menos que el mínimo) o **Agotado** (0). |
| **Mínimo** | Stock mínimo: cuando las existencias llegan a este número, el sistema avisa. |
| **Estado** | **Activo** (se vende) o **Inactivo** (oculto en la tienda). |

## Crear un producto {#mod-inventario-crear}

### Para qué sirve

Agregar al catálogo un producto nuevo para que se pueda vender en la tienda, en la venta móvil y en los pedidos.

### Quién puede usarlo

Administrador.

### Pasos

1. En la pantalla **Inventario**, haga clic en **Nuevo producto**.

   ![Botón Nuevo producto](capturas/administrador/adm-inventario-03.png)

2. Complete el formulario **Crear producto**:

   ![Formulario para crear un producto](capturas/administrador/adm-inventario-04.png)

   1. **Nombre del producto**: como lo verá el cliente, con la presentación. Por ejemplo, «Ron Centenario 7 Años 750ml».
   2. **Categoría**: elija la categoría de la tienda donde aparecerá.
   3. **Descripción**: una o dos frases para el cliente. Es opcional (máximo 500 caracteres).
   4. **Precio (₡)**, **Stock inicial** (unidades que ya hay en bodega) y **Stock mínimo** (a partir de cuántas unidades el sistema avisa que hay que reponer).
   5. **Imagen del producto**: haga clic en **Seleccionar archivo** y elija la foto. Formatos JPG, JPEG, PNG o WEBP, de 2 MB como máximo. Es opcional: si no sube una foto, la tienda muestra la imagen de la categoría del producto.
   6. Haga clic en **Guardar producto**.

   Si el producto vence, escriba la **Fecha de vencimiento**: el inventario avisará cuando falten 30 días o menos. Deje marcado **Producto activo** para que se venda de inmediato. Marque **Mostrar como producto destacado** si quiere que aparezca en la portada de la tienda.

### Resultado esperado

Aparece el mensaje «Producto creado correctamente.» y el producto se agrega a la tabla con su código SKU nuevo.

![El producto nuevo aparece en el inventario](capturas/administrador/adm-inventario-05.png)

Si escribió un stock inicial mayor que 0, el sistema registra sola una entrada en [Movimientos de inventario](#mod-movimientos).

### Si algo sale mal

| Mensaje | Qué hacer |
|---|---|
| El nombre es obligatorio. / La categoría es obligatoria. / El precio es obligatorio. | Complete los campos marcados con asterisco (*). |
| El precio debe ser mayor a cero. | Escriba un precio mayor que 0, solo con números (por ejemplo, 14500). |
| El stock no puede ser negativo. / El stock mínimo no puede ser negativo. | Escriba 0 o un número positivo. |
| La imagen supera el tamaño máximo permitido de 2 MB. | Reduzca la foto o tome otra de menor tamaño. |
| La imagen debe estar en formato JPG, JPEG, PNG o WEBP. | Guarde la foto en uno de esos formatos. |
| El tipo MIME de la imagen no coincide con su extensión. / La firma binaria no corresponde a una imagen permitida. | El archivo no es realmente una imagen (o se le cambió la extensión). Use la foto original. |

## Editar un producto {#mod-inventario-editar}

### Para qué sirve

Cambiar el precio, la descripción, las existencias, el stock mínimo, la fecha de vencimiento o la imagen de un producto.

### Quién puede usarlo

Administrador.

### Pasos

1. Busque el producto (vea [Buscar un producto](#mod-inventario-buscar)). En la columna **Acciones** de su fila están los botones para administrarlo:

   ![Botones de cada producto en la tabla](capturas/administrador/adm-inventario-06.png)

   1. **Destacado** (**Sí** / **No**): cambia si el producto aparece en los destacados de la tienda (vea [Destacar un producto](#mod-inventario-destacar)).
   2. **Editar**: abre la ficha del producto.
   3. **Inactivar** / **Activar**: oculta o vuelve a mostrar el producto (vea [Inactivar o reactivar un producto](#mod-inventario-inactivar)).

2. Haga clic en **Editar**.
3. Cambie los datos que necesite:

   ![Formulario para editar un producto](capturas/administrador/adm-inventario-07.png)

   1. **Precio (₡)**: el precio nuevo. Se aplica a los pedidos que se hagan desde ahora.
   2. **Stock**: las existencias reales. Cambiarlo aquí registra un ajuste automático en el historial de movimientos.
   3. **Stock mínimo**: el sistema avisa cuando el stock llega a este número o menos.
   4. **Fecha de vencimiento**: opcional.
   5. **Producto activo**: desmárquelo para ocultar el producto (solo si no tiene existencias).
   6. Haga clic en **Guardar cambios**.

### Resultado esperado

Aparece el mensaje «Producto actualizado correctamente.» y la tabla muestra los datos nuevos. El cambio queda registrado en [Auditoría](#mod-auditoria).

> [!ADVERTENCIA] Para corregir existencias por una compra, una merma o un conteo físico, prefiera [Movimientos de inventario](#mod-movimientos): ahí queda anotado el motivo. Use el campo **Stock** de esta pantalla solo para ajustes puntuales.

### Si algo sale mal

| Mensaje | Qué hacer |
|---|---|
| No se puede inactivar «…»: todavía tiene N unidad(es) en bodega. Primero debe vaciarse el inventario. | Desmarcó **Producto activo** en un producto con existencias. Vuelva a marcarlo, o primero saque las unidades con un movimiento de salida. |
| El precio debe ser mayor a cero. | Escriba un precio mayor que 0. |
| Los mismos mensajes de la imagen que en [Crear un producto](#mod-inventario-crear). | Revise el archivo de la foto. |

## Destacar un producto {#mod-inventario-destacar}

### Para qué sirve

Mostrar un producto en la sección **Productos recomendados** de la portada de la tienda, por ejemplo para impulsar una marca o una temporada.

### Quién puede usarlo

Administrador.

### Pasos

1. Busque el producto.
2. En la columna **Destacado**, haga clic en el botón **No** para destacarlo, o en **Sí** para quitarlo de los destacados (paso 1 de la imagen de [Editar un producto](#mod-inventario-editar)).

### Resultado esperado

El botón cambia de **No** a **Sí** (o al revés) y la tienda muestra el cambio de inmediato.

## Inactivar o reactivar un producto {#mod-inventario-inactivar}

### Para qué sirve

Ocultar de la tienda y de las ventas un producto que ya no se vende, sin borrar su historial. Un producto inactivo se puede reactivar cuando vuelva a venderse.

### Quién puede usarlo

Administrador.

### Pasos

1. Busque el producto.
2. En la columna **Acciones**, haga clic en **Inactivar** (o en **Activar**, si está inactivo).
3. En la ventana **Confirmar cambio de producto**, haga clic en **Sí, continuar**.

   ![Confirmación para inactivar un producto](capturas/administrador/adm-inventario-08.png "ancho=9")

### Resultado esperado

Aparece el mensaje «Producto inactivado correctamente.» (o «Producto reactivado correctamente.») y la columna **Estado** cambia.

![Producto inactivado: mensaje y estado](capturas/administrador/adm-inventario-09.png)

> [!NOTA] Solo se puede inactivar un producto con **Stock** en 0. Así no quedan unidades en bodega que nadie puede vender.

### Si algo sale mal

![Mensaje al intentar inactivar un producto con existencias](capturas/administrador/adm-inventario-10.png)

| Mensaje | Qué hacer |
|---|---|
| No se puede inactivar «…»: todavía tiene N unidad(es) en bodega. Primero debe vaciarse el inventario. | Venda, devuelva o dé de baja las unidades con un [movimiento de salida](#mod-movimientos) y vuelva a intentarlo. |
| No fue posible cambiar el estado del producto. | Vuelva a cargar la página e intente de nuevo. Si continúa, avise a Soporte. |
