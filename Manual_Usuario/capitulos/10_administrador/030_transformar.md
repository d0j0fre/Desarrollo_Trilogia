# Transformar producto {#mod-transformar}

Esta pantalla pasa existencias de una presentación a otra en un solo paso: descuenta unidades del producto de origen y suma unidades al producto de destino. Se usa, por ejemplo, para abrir una caja de mayoreo y venderla como unidades sueltas, o para mover un paquete de la presentación de mayoreo a la de tienda.

Se abre en **Administración → Inventario → Transformar producto**.

## Transformar existencias de una presentación a otra {#mod-transformar-registrar}

### Para qué sirve

Que el inventario refleje lo que realmente hay en bodega cuando un producto cambia de presentación, sin hacer a mano una salida y una entrada por separado.

### Quién puede usarlo

Administrador.

### Pasos

1. Abra **Administración → Inventario → Transformar producto**.
2. Complete el formulario:

   ![Formulario para transformar un producto](capturas/administrador/adm-transformar-01.png)

   1. **Producto origen**: el producto del que salen las unidades. La lista muestra el stock actual de cada uno.
   2. **Cantidad a descontar del origen**: cuántas unidades salen. Por ejemplo, **2** paquetes.
   3. **Producto destino**: el producto que recibe las unidades. Debe ser distinto del origen.
   4. **Cantidad a sumar al destino**: cuántas unidades entran. Por ejemplo, si abre 1 caja de 12 botellas, escriba **12**.
   5. **Motivo**: por qué se transforma. Es opcional, pero ayuda en una revisión posterior (máximo 300 caracteres).
   6. Haga clic en **Transformar**.

### Resultado esperado

El sistema abre la pantalla **Movimientos de inventario** con el mensaje «Transformación registrada correctamente. Referencia …». La referencia es un código único de la operación.

![Mensaje de transformación registrada](capturas/administrador/adm-transformar-02.png)

En el historial de [Movimientos de inventario](#mod-movimientos) aparecen dos movimientos con esa misma referencia: la salida del origen y la entrada del destino. Las dos cosas se hacen juntas: si una falla, no se hace ninguna.

> [!ADVERTENCIA] Revise bien las cantidades antes de hacer clic en **Transformar**. Una transformación no se deshace desde esta pantalla: para corregirla, haga otra transformación en sentido contrario.

### Si algo sale mal

![Mensaje cuando el origen no tiene unidades suficientes](capturas/administrador/adm-transformar-03.png)

| Mensaje | Qué hacer |
|---|---|
| No hay stock suficiente en el producto de origen. | La cantidad a descontar es mayor que el stock del origen. Revise el stock que aparece entre paréntesis en la lista y corrija la cantidad. |
| Debes seleccionar el producto de origen. / Debes seleccionar el producto de destino. | Elija un producto en cada lista. |
| La cantidad de origen debe ser mayor que cero. / La cantidad de destino debe ser mayor que cero. | Escriba cantidades enteras mayores que 0. |
| El producto de origen y destino deben ser diferentes. | Elija dos productos distintos. |
| No fue posible completar la transformación. Revise los datos. | Algún producto está inactivo o cambió mientras llenaba el formulario. Vuelva a abrir la pantalla e intente de nuevo. |
