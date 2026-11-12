# Movimientos de inventario {#mod-movimientos}

Cada vez que entra o sale mercadería de la bodega fuera de un pedido (una compra recibida sin orden, un producto dañado, un conteo físico), se registra un movimiento. El sistema ajusta el stock del producto y guarda quién lo hizo, cuándo y por qué. Los pedidos, las devoluciones y las transformaciones generan sus movimientos solos: también aparecen en esta lista.

Se abre en **Administración → Inventario → Movimientos**. Desde el teléfono, el bodeguero también puede registrarlos (vea [Movimientos de inventario en la aplicación](#app-movimientos)).

## Consultar y filtrar el historial de movimientos {#mod-movimientos-consultar}

### Para qué sirve

Saber por qué cambió el stock de un producto: quién lo movió, cuánto había antes y cuánto quedó. Por ejemplo, revisar todas las salidas de un producto en la semana.

### Quién puede usarlo

Bodeguero, Empleado y Administrador. Gerente y Compras solo consultan: no ven el botón **Registrar movimiento**.

### Pasos

1. Abra **Movimientos**. La tabla **Historial de movimientos** muestra los movimientos más recientes primero.

   ![Historial de movimientos de inventario](capturas/bodeguero/bod-movimientos-01.png)

2. Para ver solo algunos movimientos, use los filtros de arriba de la tabla:

   ![Filtros del historial de movimientos](capturas/bodeguero/bod-movimientos-07.png)

   1. Escriba parte del nombre del **Producto**.
   2. Elija el tipo: **Entrada**, **Salida** o **Ajuste** (o **Todos los tipos**).
   3. Si quiere, indique el rango de fechas en **Desde** y **Hasta**.
   4. Haga clic en **Buscar**. Para volver a ver todo, haga clic en **Limpiar**.

### Resultado esperado

Cada fila muestra el producto, el **Tipo** (**Entrada**, **Salida** o **Ajuste**), la **Cantidad**, el **Stock anterior**, el **Stock nuevo**, el **Motivo**, el **Usuario** y la **Fecha**.


## Registrar un movimiento {#mod-movimientos-registrar}

### Para qué sirve

Mantener el stock del sistema igual al de la bodega: sumar lo que entra, restar lo que sale por daño o consumo, o corregir el número después de un conteo físico.

### Quién puede usarlo

Bodeguero, Empleado y Administrador.

### Pasos

1. En **Movimientos**, haga clic en **Registrar movimiento** (vea la imagen anterior).
2. Complete el formulario:

   ![Formulario Registrar movimiento](capturas/bodeguero/bod-movimientos-02.png)

   1. **Producto**: elija el producto. Entre paréntesis aparece su stock actual.
   2. **Tipo de movimiento**:
      - **Entrada**: suma la cantidad al stock.
      - **Salida**: resta la cantidad del stock.
      - **Ajuste**: reemplaza el stock por la cantidad que escriba (úselo después de un conteo físico). Puede ser 0.
   3. **Cantidad**: un número entero. En **Entrada** y **Salida** debe ser mayor que 0; en **Ajuste** es el stock final.
   4. **Esta entrada representa una compra**: márquelo solo en una **Entrada** que se pagó con dinero de la empresa (vea el paso 3).
   5. **Motivo**: explique el movimiento, por ejemplo el número de factura o «botella quebrada al descargar».
   6. Haga clic en **Guardar movimiento**.

3. Si marcó **Esta entrada representa una compra**, aparecen dos campos más:

   ![Datos de la compra](capturas/bodeguero/bod-movimientos-03.png)

   1. La casilla marcada.
   2. **Costo unitario**: lo que costó cada unidad, en colones.
   3. **Categoría del gasto**: **Compra de inventario**, **Transporte** u **Otro**.

   El sistema registra el gasto multiplicando la cantidad por el costo unitario.

### Resultado esperado

Aparece el mensaje «Movimiento registrado correctamente.» y el movimiento queda de primero en el historial, con el stock anterior y el nuevo.

![Movimiento registrado en el historial](capturas/bodeguero/bod-movimientos-04.png)

> [!ADVERTENCIA] Si la mercadería llega con una orden de compra, no la registre aquí: use la recepción de la orden (vea [Órdenes de compra](#mod-ordenes-compra) o [Recepción de compras](#app-recepcion) en la aplicación). Si la registra aquí y también en la orden, el stock se suma dos veces.

### Si algo sale mal

![Mensaje cuando la salida deja el stock en negativo](capturas/bodeguero/bod-movimientos-05.png)

| Mensaje | Qué hacer |
|---|---|
| El movimiento deja el stock en negativo. | La **Salida** es mayor que el stock actual. Revise el stock que aparece junto al producto y la cantidad. |
| El producto seleccionado no existe o está inactivo. | Elija un producto activo de la lista. |
| Debe indicar un costo unitario válido para registrar el gasto. | Marcó **Esta entrada representa una compra**: escriba el **Costo unitario**. |
| Debe seleccionar un producto. | Elija el producto de la lista. |
| La cantidad debe ser mayor que cero. | En **Entrada** y **Salida**, escriba una cantidad entera mayor que 0. |
| La cantidad no puede ser negativa. | Escriba 0 o más. |
| El costo unitario debe ser mayor que cero. | Escriba el costo por unidad. |
| El motivo no puede superar los 300 caracteres. | Resúmalo. |
| Ocurrió un error al registrar el movimiento. Intente nuevamente. | Falla temporal. Vuelva a intentar; si continúa, avise a Soporte. |
