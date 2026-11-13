# Combos {#mod-combos}

Un combo es una oferta que agrupa varios productos con un precio especial, por ejemplo «Combo Fiesta Cacique: un Cacique, dos Coca-Cola y una bolsa de hielo». El sistema calcula cuántos combos se pueden vender según las existencias de cada producto que lo forma.

Se abre en **Administración → Inventario → Combos**.

## Consultar los combos {#mod-combos-consultar}

### Para qué sirve

Ver qué combos existen, su precio y cuántos se pueden vender hoy con las existencias actuales.

### Quién puede usarlo

Administrador.

### Pasos

1. Abra **Administración → Inventario → Combos**.

   ![Pantalla Combos comerciales](capturas/administrador/adm-combos-01.png)

   1. **Tarjetas**: **TOTAL** de combos, **ACTIVOS** y **VENDIBLES HOY** (activos y con existencias suficientes).
   2. **Catálogo de combos**: cada combo con su precio, cuántos productos lo forman (**COMPONENTES**), cuántos se pueden vender (**DISPONIBLES**) y su **ESTADO**.
   3. **Nuevo combo**: abre el formulario para crear uno (vea [Crear un combo](#mod-combos-crear)).

2. Para ver de qué está hecho un combo, haga clic en su nombre o en **Detalle**.

   ![Detalle de un combo: disponibilidad y componentes](capturas/administrador/adm-combos-04.png)

### Resultado esperado

La pantalla de detalle muestra el **PRECIO DE VENTA**, la **DISPONIBILIDAD REAL** y la tabla **Composición y cobertura**. La columna **COMBOS POSIBLES** dice cuántos combos alcanzan con las existencias de cada producto. La disponibilidad del combo es el número más bajo de esa columna: en el ejemplo, las Coca-Cola alcanzan para 17 combos, así que solo se pueden vender 17.

> [!CONSEJO] Si un combo muestra pocas unidades disponibles, revise en el detalle qué producto lo limita y repóngalo.

## Crear un combo {#mod-combos-crear}

### Para qué sirve

Armar una oferta nueva para la tienda en línea con varios productos y un precio especial.

### Quién puede usarlo

Administrador.

### Pasos

1. En **Combos comerciales**, haga clic en **Nuevo combo**.
2. Complete el formulario. La tabla **Productos del combo** muestra todos los productos activos con su stock actual.

   ![Formulario para crear un combo](capturas/administrador/adm-combos-02.png)

   1. **Nombre del combo**: como lo verá el cliente.
   2. **Descripción**: qué incluye. Es opcional (máximo 500 caracteres).
   3. **Precio del combo**: el precio de venta de todo el paquete.
   4. Marque la casilla de cada producto que forma el combo.
   5. En **CANTIDAD EN EL COMBO**, escriba cuántas unidades de ese producto lleva cada combo.
   6. Haga clic en **Crear combo**.

### Resultado esperado

Aparece el mensaje «Combo creado correctamente y disponible para la tienda.» y se abre el detalle del combo nuevo con su disponibilidad.

![Combo creado: mensaje y composición](capturas/administrador/adm-combos-03.png)

### Si algo sale mal

| Mensaje | Qué hacer |
|---|---|
| El nombre es obligatorio. | Escriba el nombre del combo. |
| El precio debe ser mayor que cero. | Escriba un precio mayor que 0. |
| Debe seleccionar al menos un producto para armar el combo. | Marque la casilla de uno o más productos. |
| Cada componente seleccionado debe tener un producto y una cantidad mayor que cero. | Revise la cantidad de cada producto marcado. |
| Un producto no puede repetirse dentro del mismo combo. | Marque cada producto una sola vez y ajuste su cantidad. |
| No fue posible crear el combo con los datos indicados. | Algún producto se inactivó mientras llenaba el formulario. Vuelva a abrir **Nuevo combo**. |

## Inactivar o activar un combo {#mod-combos-inactivar}

### Para qué sirve

Retirar un combo de la tienda cuando termina la oferta, o volver a ofrecerlo, sin perder su historial.

### Quién puede usarlo

Administrador.

### Pasos

1. En **Combos comerciales**, busque el combo en la tabla.

   ![Botones Detalle e Inactivar de un combo](capturas/administrador/adm-combos-05.png)

2. Haga clic en **Inactivar** (o en **Activar**, si está inactivo).
3. Confirme el cambio en la ventana que aparece.

### Resultado esperado

Aparece el mensaje «Estado del combo actualizado.». El combo inactivo deja de mostrarse en la tienda y las tarjetas **ACTIVOS** y **VENDIBLES HOY** se actualizan.

### Si algo sale mal

| Mensaje | Qué hacer |
|---|---|
| No fue posible cambiar el estado del combo. | Vuelva a cargar la página e intente de nuevo. |
