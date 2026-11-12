# Cuarentena {#mod-cuarentena}

La cuarentena es la lista de productos devueltos que todavía no se pueden vender. Cada vez que se registra una [devolución](#mod-devoluciones), el producto entra aquí. El Gerente revisa cada caso y decide si el producto vuelve al inventario o se descarta.

Se abre en **Administración → Inventario → Cuarentena**.

## Liberar o descartar un producto {#mod-cuarentena-resolver}

### Para qué sirve

Resolver cada devolución: si el producto está en buen estado, se **libera** y sus unidades vuelven al inventario para la venta; si no sirve, se **descarta** y no vuelve a la venta.

### Quién puede usarlo

Gerente y Administrador.

### Pasos

1. Abra **Cuarentena**. La tabla **En cuarentena** muestra cada producto devuelto, quién lo registró y cuándo. Para encontrar uno, escriba el producto o el motivo y haga clic en **Buscar**.
2. Revise físicamente el producto en la bodega.
3. Resuelva la fila del producto:

   ![Observación y botones Liberar y Descartar de un producto en cuarentena](capturas/gerente/ger-cuarentena-01.png)

   1. **Observación (opcional)**: escriba lo que encontró al revisar el producto, por ejemplo «Sello intacto, se revisaron las 2 botellas». Queda guardada con la decisión.
   2. Haga clic en **Liberar** si el producto se puede vender, o
   3. haga clic en **Descartar** si no se puede vender.

4. El sistema le pide confirmar. Lea el mensaje y haga clic en **Sí, liberar** (o en **Sí, descartar**). Para no hacer nada, haga clic en **Cancelar**.

   ![Confirmación antes de liberar un producto](capturas/gerente/ger-cuarentena-02.png "ancho=8")

### Resultado esperado

- Al liberar: aparece «Producto liberado y reintegrado al inventario.». Las unidades se suman al inventario del producto y el movimiento queda registrado en [Movimientos de inventario](#mod-movimientos) como una entrada «Reintegro desde cuarentena».
- Al descartar: aparece «Producto descartado. No se reintegró al inventario.». El inventario no cambia.

![Mensaje después de liberar un producto](capturas/gerente/ger-cuarentena-03.png)

En ambos casos el producto sale de la tabla **En cuarentena**. En [Devoluciones](#mod-devoluciones-buscar) su estado cambia a **Reintegrada** o **Descartada**, y debajo del estado aparece la observación que escribió.

![La devolución liberada aparece como Reintegrada en el historial](capturas/gerente/ger-cuarentena-04.png "ancho=12")

> [!ADVERTENCIA] Liberar o descartar no se puede deshacer desde el sistema. Antes de liberar, revise que el producto esté sellado y en buen estado: una vez liberado, se puede vender a cualquier cliente.

### Si algo sale mal

| Mensaje | Causa | Qué hacer |
|---|---|---|
| La devolución ya fue resuelta. | Otra persona ya liberó o descartó esa devolución. | Vuelva a cargar la página. Revise su estado en [Devoluciones](#mod-devoluciones-buscar). |
| No se encontró la devolución indicada. | La página quedó abierta mucho tiempo. | Vuelva a cargar la página. |
| No fue posible liberar el producto. | Falla de conexión con el sistema. | Vuelva a intentar. Si continúa, avise a Soporte. |
| No fue posible descartar el producto. | Falla de conexión con el sistema. | Vuelva a intentar. Si continúa, avise a Soporte. |
| No hay productos en cuarentena. | No hay devoluciones pendientes. | No tiene que hacer nada. |
