# Tienda en línea {#mod-tienda}

La **Tienda** es el catálogo de productos: licores, vinos, cervezas, refrescos, hielo y abarrotes, con su precio y la cantidad disponible.

## Buscar y elegir productos {#mod-tienda-buscar}

### Para qué sirve

Encontrar rápido lo que necesita y agregarlo al carrito.

### Quién puede usarlo

Cualquier visitante puede ver la tienda y armar un carrito. Para comprar necesita una cuenta de cliente.

### Pasos

1. En el menú superior, haga clic en **Tienda** (o en **Ir a la tienda** en la página de inicio).
2. Busque el producto:

   ![Tienda filtrada por la categoría Cerveza](capturas/cliente/cli-tienda-02.png)

   1. **Buscar producto**: escriba parte del nombre, por ejemplo «whisky», «vino» o «combo».
   2. Haga clic en **Buscar**. **Limpiar filtros** vuelve a mostrar todo el catálogo.
   3. **Filtros**: o haga clic en una categoría para ver solo esos productos. **Todos** muestra el catálogo completo.
   4. **Detalle**: abre la ficha del producto.
   5. **Agregar**: pone 1 unidad en el carrito.

### Resultado esperado

Al hacer clic en **Agregar**, el sistema lo lleva al carrito con el mensaje «… fue agregado al carrito.». Para seguir comprando, haga clic en **Seguir comprando** o en **Tienda**.

Cada tarjeta indica la disponibilidad:

| Etiqueta | Qué significa |
|---|---|
| Disponible · Stock: N | Hay más de 5 unidades. |
| Stock bajo · Stock: N | Quedan de 1 a 5 unidades. |
| Agotado · Stock: 0 | No hay unidades. El botón dice **Agotado** y no se puede agregar. |

> [!NOTA] Cuando no hay una categoría elegida, la tienda muestra también los **Combos disponibles**: paquetes armados de varios productos a un precio especial. Se agregan igual, con **Agregar**.

### Si algo sale mal

| Mensaje | Qué hacer |
|---|---|
| No se encontraron productos — Pruebe con otra categoría o con otro texto de búsqueda. | Escriba menos palabras o revise la ortografía. Haga clic en **Ver todo el catálogo**. |
| El producto solicitado no está disponible. | El producto ya no se vende. Elija otro. |

## Ver el detalle de un producto {#mod-tienda-detalle}

### Para qué sirve

Revisar el precio, el estado y las unidades disponibles antes de agregarlo.

### Quién puede usarlo

Cualquier visitante.

### Pasos

1. En la tarjeta del producto, haga clic en **Detalle**.
2. Revise la ficha y haga clic en **Agregar al carrito**.

   ![Ficha de un producto](capturas/cliente/cli-tienda-03.png)

### Resultado esperado

El producto se agrega al carrito con 1 unidad. En el carrito puede cambiar la cantidad.

> [!NOTA] Las promociones (descuentos y regalías) no aparecen en la tienda. Se aplican solas en el carrito cuando su pedido cumple las condiciones (vea [Carrito y pago](#mod-carrito)).
