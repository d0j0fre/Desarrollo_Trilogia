# Órdenes de compra {#mod-ordenes-compra}

Una orden de compra es el pedido que la empresa le hace a un proveedor: qué productos, cuántas unidades y a qué precio. Cuando llega la mercadería, se registra la recepción y el inventario sube solo.

Se abre en **Administración → Finanzas → Órdenes de compra**.

## Consultar las órdenes de compra {#mod-ordenes-compra-consultar}

### Para qué sirve

Saber qué compras están pendientes de llegar, cuáles llegaron incompletas y cuánto se ha comprado a cada proveedor.

### Quién puede usarlo

Administrador. Compras tiene permiso de consulta (vea [Compras](#app-compras) en la aplicación móvil). El Bodeguero registra las recepciones desde la aplicación móvil (vea [Recepción de compras](#app-recepcion)).

### Pasos

1. Abra **Administración → Finanzas → Órdenes de compra**.

   ![Lista de órdenes de compra](capturas/administrador/adm-ordenes-01.png)

   1. Elija un **estado** para filtrar (vea la tabla de abajo).
   2. Elija un **proveedor**.
   3. Haga clic en **Filtrar**.
   4. **Sugerencias** abre una orden con las cantidades sugeridas (vea [Crear una orden desde las sugerencias](#mod-ordenes-compra-sugerencias)).
   5. **Nueva orden** abre una orden en blanco (vea [Crear una orden de compra](#mod-ordenes-compra-crear)).
   6. **Ver** abre el detalle de la orden.

### Resultado esperado

La tabla muestra cada orden con su número, proveedor, **Estado**, **Monto**, cuántas unidades se han **Recibido** del total y la fecha.

| Estado | Qué significa |
|---|---|
| Pendiente | La orden se hizo y todavía no llega nada. |
| Recibida parcial | Llegó una parte; faltan unidades. |
| Recibida | Llegó todo. |
| Cerrada con faltantes | Se cerró sin recibir todo; el motivo quedó anotado. |
| Cancelada | Se anuló antes de recibir mercadería. |

## Crear una orden de compra {#mod-ordenes-compra-crear}

### Para qué sirve

Pedirle mercadería a un proveedor y dejar registrado el precio pactado, para luego controlar lo que llega.

### Quién puede usarlo

Administrador.

### Pasos

1. En **Órdenes de compra**, haga clic en **Nueva orden**.
2. Complete la orden. La tabla muestra todos los productos con su **Stock**, su **Promedio mensual** de ventas y el **Último precio** pagado:

   ![Formulario para crear una orden de compra](capturas/administrador/adm-ordenes-02.png)

   1. **Proveedor activo**: elija el proveedor.
   2. **Notas**: una referencia para la orden (opcional), por ejemplo «Pedido semanal de snacks».
   3. Marque la casilla de cada producto que va a pedir.
   4. **Cantidad**: cuántas unidades.
   5. **Precio compra**: el precio unitario pactado con el proveedor, en colones.
   6. Haga clic en **Crear orden**.

   El **Subtotal** de cada línea y el **Total** se calculan mientras escribe.

### Resultado esperado

Aparece el mensaje «Orden de compra creada correctamente.» y se abre el detalle de la orden nueva, en estado **Pendiente**.

![Orden de compra creada](capturas/administrador/adm-ordenes-03.png)

### Si algo sale mal

| Mensaje | Qué hacer |
|---|---|
| Debe seleccionar un proveedor. | Elija el proveedor en la lista. |
| Debe seleccionar al menos un producto. | Marque la casilla de uno o más productos. |
| Ingrese la cantidad de cada producto seleccionado. / Ingrese el precio de compra de cada producto seleccionado. | Escriba la cantidad y el precio de cada producto marcado: no pueden quedar vacíos. |
| La cantidad debe ser mayor a cero. / El precio debe ser mayor a cero. | Revise la cantidad y el precio de cada producto marcado. |
| El proveedor no existe o está inactivo. | Reactive el proveedor en [Proveedores](#mod-proveedores-editar) o elija otro. |
| Uno de los productos no existe o está inactivo. | Quite ese producto de la orden o reactívelo en [Inventario](#mod-inventario-inactivar). |
| La orden debe contener productos válidos y sin duplicados. | Revise que cada producto esté marcado una sola vez. |
| No fue posible crear la orden de compra. | Vuelva a abrir **Nueva orden** e intente de nuevo. |

## Crear una orden desde las sugerencias {#mod-ordenes-compra-sugerencias}

### Para qué sirve

Empezar la orden con los productos que conviene reabastecer, ya marcados y con una cantidad sugerida. Usa el mismo cálculo que [Inteligencia de inventario](#mod-inteligencia-consultar).

### Quién puede usarlo

Administrador.

### Pasos

1. En **Órdenes de compra**, haga clic en **Sugerencias**.

   ![Sugerencias de compra](capturas/administrador/adm-ordenes-07.png)

2. Si lo desea, cambie **Meses para promedio** y **Meses de cobertura** (de 1 a 12) y haga clic en **Actualizar**: la lista se calcula otra vez con esos valores, igual que en Inteligencia de inventario.
3. Revise los productos y sus cantidades. La pantalla solo lista los productos que conviene reabastecer. Cada fila muestra el **Promedio mensual** real y, debajo de la cantidad, **Sugerido: N**. Los productos con venta suficiente vienen marcados.
4. Un producto que dice **Datos insuficientes: ingrese la cantidad manualmente** no tiene ventas para calcular un promedio: viene sin marcar y con la cantidad vacía. Si quiere pedirlo, marque su casilla y escriba la cantidad que usted decida.
5. Elija el **Proveedor activo**, escriba el **Precio compra** de cada producto marcado y haga clic en **Crear orden**.

### Resultado esperado

Igual que en [Crear una orden de compra](#mod-ordenes-compra-crear): «Orden de compra creada correctamente.». Ninguna compra se crea sola: la orden existe solo cuando usted hace clic en **Crear orden**.

> [!NOTA] Si ningún producto necesita reabastecerse con esos parámetros, la pantalla lo dice («Ningún producto necesita reabastecerse con estos parámetros…») y le ofrece **Nueva orden de compra** para comprar de todos modos.

## Registrar la recepción de mercadería {#mod-ordenes-compra-recibir}

### Para qué sirve

Anotar lo que realmente llegó del proveedor. Las unidades recibidas se suman al inventario en ese momento.

### Quién puede usarlo

Administrador. En bodega, el Bodeguero lo hace desde la aplicación móvil (vea [Recepción de compras](#app-recepcion)).

### Pasos

1. Abra la orden (**Ver**).
2. En la fila de cada producto que llegó:

   ![Recibir un producto de la orden](capturas/administrador/adm-ordenes-04.png)

   1. Revise la columna **Pendiente**: lo que falta por llegar.
   2. En **Recepción**, escriba cuántas unidades llegaron. El sistema propone la cantidad pendiente; cámbiela si llegó menos.
   3. Haga clic en **Recibir**.

3. Repita con cada producto. Si un producto llega en varias entregas, registre cada entrega por separado.

### Resultado esperado

Aparece el mensaje «Recepción registrada e inventario actualizado.». La columna **Recibido** aumenta, el stock del producto sube y queda un movimiento de tipo **Compra** en [Movimientos de inventario](#mod-movimientos). La orden pasa a **Recibida parcial** o, cuando llega todo, a **Recibida**.

![Recepción registrada](capturas/administrador/adm-ordenes-05.png)

### Si algo sale mal

| Mensaje | Qué hacer |
|---|---|
| La recepción no puede exceder la cantidad ordenada. | Escriba como máximo la cantidad **Pendiente**. Si el proveedor mandó de más, haga otra orden por la diferencia. |
| La cantidad recibida debe ser mayor a cero. | Escriba una cantidad mayor que 0. |
| La orden ya no admite recepciones. | La orden está **Recibida**, **Cancelada** o **Cerrada con faltantes**. |
| No fue posible registrar la recepción. | Vuelva a cargar la orden e intente de nuevo. |

## Cerrar una orden incompleta o cancelarla {#mod-ordenes-compra-cerrar}

### Para qué sirve

Terminar una orden que no se va a completar (el proveedor no tiene el producto) o anular una orden que se hizo por error.

### Quién puede usarlo

Administrador.

### Pasos

1. Abra la orden (**Ver**). Debajo de la tabla aparece el recuadro **Finalizar sin recibir todo**:

   ![Cerrar una orden con faltantes](capturas/administrador/adm-ordenes-06.png)

   1. Escriba el motivo (al menos 10 caracteres).
   2. Haga clic en el botón del recuadro:
      - **Cerrar con discrepancia**, si ya llegó una parte de la orden.
      - **Cancelar orden sin recepciones**, si no ha llegado nada.

### Resultado esperado

- Al cerrar: «Orden cerrada con faltantes documentados.». La orden queda **Cerrada con faltantes** con el **Motivo de cierre** a la vista. Las unidades que faltaban no entran al inventario.
- Al cancelar: «Orden cancelada sin alterar inventario.».

![Orden cerrada con discrepancia](capturas/administrador/adm-ordenes-08.png)

### Si algo sale mal

| Mensaje | Qué hacer |
|---|---|
| Debe indicar un motivo de al menos diez caracteres. | Explique mejor el motivo. |
| Solo se puede cancelar una orden pendiente sin recepciones. | Si ya llegó algo, use **Cerrar con discrepancia**. |
| La orden ya no puede cerrarse con discrepancias. / La orden no tiene cantidades pendientes. | La orden ya está cerrada o completa. |
