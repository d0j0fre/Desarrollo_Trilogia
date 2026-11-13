# Devoluciones {#mod-devoluciones}

En Devoluciones se registra la mercadería que un cliente devuelve: una botella dañada, un producto vencido o un error de despacho. Todo producto devuelto queda primero en [cuarentena](#mod-cuarentena), apartado del inventario, hasta que alguien decida si se puede volver a vender.

Se abre en **Administración → Inventario → Devoluciones**.

## Registrar una devolución {#mod-devoluciones-registrar}

### Para qué sirve

Dejar constancia de cada producto que regresa a la bodega, de quién lo devolvió y por qué, para que no se vuelva a vender por error y para analizar las causas más comunes.

### Quién puede usarlo

Gerente y Administrador.

### Pasos

1. Abra **Devoluciones**. A la izquierda está el formulario **Registrar devolución**.
2. Complete los datos:

   ![Formulario Registrar devolución](capturas/gerente/ger-devoluciones-01.png "ancho=8")

   1. **Producto**: elija el producto devuelto. La lista muestra solo productos activos.
   2. **Cantidad**: cuántas unidades devolvió el cliente.
   3. **Pedido asociado (opcional)**: el número del pedido en que se vendió, si lo conoce (por ejemplo, **8**).
   4. **Cliente / referencia (opcional)**: el nombre del cliente o una referencia que ayude a identificar el caso.
   5. **Motivo**: la causa de la devolución, por ejemplo «Botellas con la tapa floja». Es obligatorio.
   6. Haga clic en **Registrar devolución**.

### Resultado esperado

Aparece el mensaje «Devolución #13 registrada. Vinculada al pedido #8. El producto quedó en cuarentena.» (con el número que corresponda). Si no indicó pedido, el mensaje no menciona el pedido.

La devolución aparece de primera en **Historial de devoluciones**, con el estado **En cuarentena**.

![La devolución nueva en el historial, en cuarentena](capturas/gerente/ger-devoluciones-02.png "ancho=12")

| Estado | Qué significa |
|---|---|
| En cuarentena | El producto está apartado. Todavía no se decide qué hacer con él. |
| Reintegrada | Se revisó, estaba en buen estado y volvió al inventario para la venta. |
| Descartada | No se puede vender. No volvió al inventario. |

> [!NOTA] Registrar la devolución **no** suma unidades al inventario. Las unidades se suman solo cuando alguien libera el producto en [Cuarentena](#mod-cuarentena-resolver).

### Si algo sale mal

![Mensajes cuando faltan datos](capturas/gerente/ger-devoluciones-05.png "ancho=8")

| Mensaje | Causa | Qué hacer |
|---|---|---|
| Debe seleccionar un producto. | No eligió un producto. | Elija el producto en la lista **Producto**. |
| El motivo es obligatorio. | Dejó el **Motivo** vacío. | Escriba la causa de la devolución. |
| La cantidad debe ser mayor a cero. | Escribió 0 o un número negativo. | Escriba al menos 1. |
| El pedido indicado no existe. | El número de **Pedido asociado** está mal escrito. | Revise el número del pedido en [Pedidos](#mod-pedidos). Si no lo conoce, deje el campo vacío y escriba el nombre del cliente. |
| El producto seleccionado no existe o está inactivo. | Alguien inactivó el producto mientras usted llenaba el formulario. | Vuelva a cargar la página y elija el producto otra vez. Si ya no aparece, consulte al Administrador. |
| No fue posible registrar la devolución. | Falla de conexión con el sistema. | Vuelva a intentar. Si continúa, avise a Soporte. |

![Mensaje cuando el pedido no existe](capturas/gerente/ger-devoluciones-06.png)

## Buscar devoluciones {#mod-devoluciones-buscar}

### Para qué sirve

Encontrar rápido la devolución de un cliente o de un producto, o ver solo las que siguen en cuarentena.

### Quién puede usarlo

Gerente y Administrador.

### Pasos

1. Use la barra que está arriba del historial:

   ![Barra para buscar devoluciones](capturas/gerente/ger-devoluciones-03.png)

   1. Escriba parte del nombre del producto, del motivo o del cliente.
   2. Si quiere, elija un estado: **En cuarentena**, **Reintegrada** o **Descartada**.
   3. Haga clic en **Buscar**.

2. Para volver a ver todas las devoluciones, haga clic en **Limpiar**.

### Resultado esperado

El historial muestra solo las devoluciones que coinciden. Arriba a la derecha, **Total** indica cuántas son. En las devoluciones ya resueltas, debajo del estado aparece la observación que se escribió al liberarlas o descartarlas en [Cuarentena](#mod-cuarentena-resolver).

### Si algo sale mal

| Situación | Qué hacer |
|---|---|
| La tabla dice «No hay devoluciones registradas.» | No hay devoluciones con ese texto o estado. Revise lo escrito o haga clic en **Limpiar**. |

## Consultar el reporte de devoluciones {#mod-devoluciones-reporte}

### Para qué sirve

Saber qué productos, qué clientes y qué motivos generan más devoluciones en un período, para corregir la causa (por ejemplo, un proveedor con empaques débiles o un error frecuente de despacho).

### Quién puede usarlo

Gerente y Administrador.

### Pasos

1. En **Devoluciones** (o en **Cuarentena**), haga clic en **Reporte de devoluciones** (la flecha de la imagen anterior).
2. Elija el período y genere el reporte:

   ![Período del reporte de devoluciones](capturas/gerente/ger-devoluciones-04.png)

   1. **Desde**: el primer día del período. Al abrir, el sistema propone el primer día del mes actual.
   2. **Hasta**: el último día. Al abrir, propone el día de hoy.
   3. Haga clic en **Generar**.
   4. Si necesita los datos en Excel, haga clic en **Descargar CSV**.

### Resultado esperado

- Arriba, dos totales: **Devoluciones** (cuántos registros) y **Unidades devueltas**.
- Tres tablas: **Por producto**, **Por cliente / referencia** y **Por motivo**, cada una con el número de devoluciones (**Devol.**) y de unidades (**Unid.**).
- Abajo, **Detalle del período** con cada devolución.
- **Descargar CSV** baja un archivo llamado, por ejemplo, `devoluciones-20261001-20261102.csv`, que se abre en Excel.

> [!CONSEJO] Revise el reporte cada mes, con el filtro del mes anterior completo. Si un mismo motivo se repite, coméntelo con Bodega o con Compras.

### Si algo sale mal

| Mensaje | Qué hacer |
|---|---|
| La fecha final no puede ser anterior a la fecha inicial. | Corrija las fechas: **Hasta** debe ser igual o posterior a **Desde**. |
| Sin devoluciones en el período. | No hubo devoluciones en esas fechas. Amplíe el período. |
