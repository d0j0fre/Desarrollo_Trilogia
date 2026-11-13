# Movimientos de inventario {#app-movimientos}

En **Movimientos de inventario** se registran las entradas, salidas y ajustes de existencias que no vienen de una venta ni de una compra: devoluciones, productos dañados, consumo interno o conteos físicos.

**Quién la ve:** Bodeguero, Empleado y Administrador (sección **Bodega**). En el sitio web, vea [Movimientos de inventario](#mod-movimientos).

## Registrar un movimiento {#app-movimientos-registrar}

### Para qué sirve

Mantener el stock del sistema igual al de la bodega.

### Quién puede usarlo

Bodeguero, Empleado y Administrador.

### Pasos

1. En la pantalla de inicio, toque **Movimientos de inventario**.
2. Toque **Toque para elegir el producto**, búsquelo por nombre y tóquelo.
3. Complete el movimiento:

   ![Registrar una salida de inventario](capturas/app/app-movimientos-01.png "ancho=6")

   1. **Producto**: el elegido, con su **Stock actual**.
   2. **Tipo**: **Entrada** (suma unidades), **Salida** (resta unidades) o **Ajuste** (fija el stock en la cantidad contada).
   3. **Cantidad**: escríbala o use **−** y **+**. Debajo se ve cómo queda el stock (por ejemplo, **88 → 86**).
   4. **Motivo**: toque un motivo rápido (por ejemplo, **Producto dañado**) o escríbalo en **Detalle del movimiento**. Es obligatorio en salidas y ajustes.
   5. Toque **Registrar movimiento**.

4. Revise el resumen y toque **Registrar**:

   ![Confirmar el movimiento](capturas/app/app-movimientos-02.png "ancho=6")

### Resultado esperado

Aparece «Movimiento registrado. Se envía en cuanto haya señal.». El stock del producto cambia y el movimiento queda en su historial (vea [Consulta de inventario](#app-inventario)).

Motivos rápidos por tipo:

| Tipo | Motivos rápidos |
|---|---|
| Entrada | Devolución de cliente, Reingreso de mercadería, Corrección de conteo |
| Salida | Producto dañado, Producto vencido, Consumo interno, Muestra comercial |
| Ajuste | Conteo físico, Inventario mensual, Corrección de sistema |

### Si algo sale mal

| Mensaje | Qué hacer |
|---|---|
| Elija el producto. | Toque **Toque para elegir el producto**. |
| La cantidad debe ser mayor a cero. | Escriba una cantidad. |
| No hay suficiente stock para esa salida. | La salida es mayor que el stock. Revise el conteo; si el stock del sistema está mal, use **Ajuste**. |
| Indique el motivo. | Elija o escriba el motivo. |
| El ajuste no cambia el stock actual. | La cantidad contada es igual al stock. No hace falta ajustar. |
| El producto no existe o está inactivo. | El producto se inactivó. Consulte a administración. |
