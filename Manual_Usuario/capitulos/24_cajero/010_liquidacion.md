# Liquidación de cobros {#mod-liquidacion}

Al volver de una ruta, el chofer entrega en caja el efectivo que cobró y los comprobantes de los pagos por SINPE, transferencia u otro medio. En esta pantalla el cajero cuenta lo recibido y lo compara con lo que se esperaba cobrar por los pedidos entregados. El sistema calcula la diferencia y marca la ruta como **Cuadrada**, **Faltante** o **Sobrante**.

Solo se liquidan las rutas **Completadas**, es decir, las que el chofer ya finalizó. Si la ruta volvió con pedidos sin entregar, también se liquida la mercadería que regresó (vea [Liquidar la mercadería de una ruta](#mod-liquidacion-mercaderia)).

El Cajero la abre con el botón **Liquidación de cobros** del menú superior. También está en **Administración → Finanzas → Liquidación de cobros**, que es el camino del Gerente y del Administrador.

## Liquidar los cobros de una ruta {#mod-liquidacion-registrar}

### Para qué sirve

Dejar constancia de cuánto dinero entregó el chofer por cada ruta y detectar a tiempo un faltante o un sobrante.

### Quién puede usarlo

Cajero, Gerente y Administrador. También se puede hacer desde el teléfono (vea [Liquidación de rutas](#app-liquidacion)).

### Pasos

1. Abra **Liquidación de cobros**. Arriba está la tabla **Rutas completadas pendientes de liquidar**, con las rutas que el chofer ya finalizó y que todavía no tienen liquidación.
2. Busque la ruta del chofer por su **Código** y revise la columna **Entregados** (pedidos entregados de los pedidos de la ruta). Haga clic en **Liquidar**.

   ![Rutas completadas pendientes de liquidar](capturas/cajero/caj-liquidacion-01.png)

3. Se abre **Liquidar cobros — Ruta …**. Arriba aparecen los montos que el sistema espera, calculados con los pedidos entregados:

   ![Montos esperados de la ruta](capturas/cajero/caj-liquidacion-03.png)

   - **Esperado en efectivo**: los pedidos entregados que se pagan en efectivo (por ejemplo, **Efectivo contra entrega**).
   - **Esperado otros medios**: los pedidos pagados por SINPE, transferencia, crédito u otro medio.
   - **Total esperado**: la suma de los dos.

   Al final de la página, la tabla **Pedidos entregados** muestra cada pedido con su cliente, su método de pago y su total. Úsela para revisar los comprobantes uno por uno.

   ![Pedidos entregados de la ruta](capturas/cajero/caj-liquidacion-02.png)

4. Cuente el dinero y complete el formulario:

   ![Formulario para liquidar la ruta](capturas/cajero/caj-liquidacion-04.png)

   1. **Efectivo recibido**: el efectivo que contó, en colones, sin puntos ni símbolos.
   2. **Total en comprobantes**: se calcula solo con la suma del detalle de comprobantes (paso 4). No lo escriba a mano si registra comprobantes.
   3. **Observaciones**: explique cualquier diferencia, por ejemplo quién repone un faltante.
   4. **Detalle de comprobantes**: en cada fila elija el tipo (**SINPE**, **Transferencia**, **Depósito**, **Cheque** u **Otro**), escriba la **Referencia** y el **Monto**. Hay 5 filas; si necesita más, haga clic en **Agregar comprobante** (hasta 10). Debajo, **Suma del detalle** muestra el total que va sumando.
   5. Haga clic en **Registrar liquidación**.

### Resultado esperado

Aparece el mensaje «Liquidación registrada. Resultado: … (diferencia …).» y, arriba, un aviso verde: «La liquidación quedó registrada y ya tiene recibo.» con el botón **Ver recibo**, que abre el recibo en PDF. La ruta sale de **Rutas completadas pendientes de liquidar** y aparece en **Liquidaciones registradas**.

![Mensaje de liquidación registrada](capturas/cajero/caj-liquidacion-05.png)

El resultado se calcula así: (efectivo recibido + comprobantes) − total esperado.

| Resultado | Qué significa | Qué hacer |
|---|---|---|
| Cuadrada | Lo recibido es igual a lo esperado. | Nada más. Guarde el dinero según el procedimiento de caja. |
| Faltante | Se recibió menos (diferencia negativa). | El mensaje agrega «El faltante de ₡… quedó a cargo del chofer …». Informe al Gerente el mismo día, con el detalle de las observaciones. |
| Sobrante | Se recibió más (diferencia positiva). | Revise si un cliente pagó de más o pagó un pedido de otra ruta, e informe al Gerente. |

> [!NOTA] Cuando hay un faltante, el sistema lo deja a cargo del chofer y lo suma en la tabla **Faltantes a cargo de choferes** de esta misma pantalla (vea [Consultar las liquidaciones y los faltantes](#mod-liquidacion-consultar)).

> [!CONSEJO] Cuente dos veces el efectivo y revise cada comprobante antes de hacer clic en **Registrar liquidación**. Si aun así se equivoca, puede anular la liquidación y hacerla de nuevo (vea [Anular una liquidación](#mod-liquidacion-anular)).

### Si algo sale mal

| Mensaje | Qué hacer |
|---|---|
| Solo se puede liquidar una ruta completada. La ruta … está despachada y todavía no finaliza. | La ruta sigue en la calle. Espere a que el chofer la finalice y vuelva a intentar. |
| El total en comprobantes (₡…) no coincide con la suma del detalle (₡…). Detalle cada comprobante o corrija el total. | Los montos del **Detalle de comprobantes** no suman el **Total en comprobantes**. Corrija una fila o el total. |
| Cada comprobante necesita su tipo y un monto mayor que cero. | Una fila tiene monto pero no tipo, o tipo pero no monto. Complétela o bórrela. |
| La ruta … ya tiene liquidación financiera registrada. Para corregirla, anúlela con un motivo desde Liquidaciones registradas. | Otra persona ya la liquidó (desde la web o desde la aplicación). Búsquela en **Liquidaciones registradas**; si hay un error, anúlela. |
| No se encontró la ruta solicitada. | Vuelva a la lista y elija la ruta de nuevo. |
| El efectivo recibido no puede ser negativo. / El monto de comprobantes no puede ser negativo. | Escriba 0 o un monto positivo. |
| No fue posible registrar la liquidación. | Vuelva a intentar. Si continúa, avise a Soporte. |
| No hay rutas completadas pendientes de liquidación financiera. Las rutas despachadas aparecen aquí cuando finalizan. | No hay rutas por liquidar. Si el chofer ya regresó, pida al Gerente que revise que la ruta esté finalizada (vea [Rutas y entregas](#mod-rutas)). |

## Consultar las liquidaciones y los faltantes {#mod-liquidacion-consultar}

### Para qué sirve

Revisar las liquidaciones anteriores, por ejemplo todas las que tuvieron faltante en el mes, y saber cuánto debe cada chofer.

### Quién puede usarlo

Cajero, Gerente y Administrador.

### Pasos

1. Abra **Liquidación de cobros** y baje hasta **Faltantes a cargo de choferes**. Ahí ve, por chofer, cuánto falta y de qué rutas, con el **Total pendiente** arriba a la derecha.
2. Baje hasta **Liquidaciones registradas** y filtre por resultado:

   ![Filtro y lista de liquidaciones registradas](capturas/cajero/caj-liquidacion-06.png)

   1. Elija **Cuadrada**, **Faltante** o **Sobrante**.
   2. Haga clic en **Filtrar**. Para ver todas, haga clic en **Limpiar**.

### Resultado esperado

La tabla muestra, por ruta, el **Chofer**, el **Esperado**, el **Efectivo** y los **Comprobantes** recibidos, la **Diferencia**, el **Resultado**, quién la liquidó y la fecha. En la columna **Acción**, el botón del PDF abre el recibo de esa liquidación.

### Si algo sale mal

| Mensaje | Qué hacer |
|---|---|
| Sin liquidaciones registradas. | No hay liquidaciones con ese resultado. Haga clic en **Limpiar**. |
| Ningún chofer tiene faltantes pendientes. | No es un error: ningún chofer debe dinero. |
| No se encontró la liquidación solicitada. | Al abrir un recibo: vuelva a la lista y búsquela de nuevo. |

## Anular una liquidación {#mod-liquidacion-anular}

### Para qué sirve

Corregir una liquidación que se registró con un error (por ejemplo, un monto mal contado). Al anularla, la ruta vuelve a quedar pendiente de liquidar y el faltante del chofer, si lo había, se anula.

### Quién puede usarlo

Cajero, Gerente y Administrador.

### Pasos

1. En **Liquidaciones registradas**, busque la ruta y haga clic en el botón **Anular** (el círculo tachado) de su fila. Debajo de la fila se abre un campo.
2. Escriba el **Motivo de la anulación** (obligatorio, máximo 300 caracteres).
3. Haga clic en **Anular liquidación**. El sistema pide confirmar: «¿Anular la liquidación de la ruta …?». Haga clic en **Sí, anular** (o en **Volver** para no anularla).

   ![Formulario para anular una liquidación](capturas/cajero/caj-liquidacion-07.png)

### Resultado esperado

Aparece «Liquidación anulada. La ruta … puede liquidarse de nuevo.». En la lista, la fila queda en gris con la marca **Anulada**, y debajo se lee quién la anuló, cuándo y el motivo. La ruta reaparece en **Rutas completadas pendientes de liquidar**: liquídela otra vez con los montos correctos.

> [!NOTA] Las liquidaciones anuladas no se borran: quedan en la lista y en la auditoría con su motivo. Una ruta solo puede tener una liquidación vigente a la vez.

### Si algo sale mal

| Mensaje | Qué hacer |
|---|---|
| Indique el motivo de la anulación. | El campo del motivo está vacío. Escriba por qué la anula. |
| Esa liquidación ya estaba anulada. | Otra persona la anuló antes. Recargue la lista. |
| No fue posible anular la liquidación. | Vuelva a intentar. Si continúa, avise a Soporte. |

## Liquidar la mercadería de una ruta {#mod-liquidacion-mercaderia}

### Para qué sirve

Cuando una ruta termina con pedidos que no se entregaron, esa mercadería regresa en el camión. Aquí se cuenta, se compara con lo que salió y vuelve al inventario. Mientras falte mercadería, la ruta no se cierra.

### Quién puede usarlo

Cajero, Gerente y Administrador.

### Pasos

1. En **Liquidación de cobros**, haga clic en **Liquidación de mercadería** (arriba a la derecha). Se abre la lista de rutas con mercadería por devolver y las ya liquidadas.

   ![Pantalla Liquidación de mercadería](capturas/cajero/caj-liquidacion-08.png)
2. En **Rutas completadas con mercadería por devolver**, busque la ruta y haga clic en **Liquidar mercadería**. La tabla dice cuántos **Pedidos sin entregar** y **Unidades** tiene.
3. Se abre **Liquidar mercadería — Ruta …**. Cuente lo que regresó en el camión:

   1. En cada fila, **Despachado** es lo que salió y **Contado** es lo que usted cuenta. El campo ya trae lo despachado: corrija solo lo que no llegó.
   2. Escriba **Observaciones** si hace falta.
   3. Haga clic en **Registrar liquidación de mercadería** y, en la confirmación, en **Sí, liquidar**.

### Resultado esperado

Aparece «Mercadería liquidada: … unidades regresaron al inventario (ruta …).». La ruta pasa a **Rutas con la mercadería ya liquidada**, con las unidades devueltas, quién liquidó y la fecha. El inventario sube y la entrada queda registrada en el historial de [Movimientos de inventario](#mod-movimientos-consultar).

### Si algo sale mal

| Mensaje | Qué hacer |
|---|---|
| Falta mercadería: la ruta … no se puede cerrar hasta que aparezca. Faltan: … | Contó menos de lo que salió. Busque la mercadería (en el camión o en bodega) y cuente de nuevo. Mientras falte, la ruta no se cierra. |
| Se contó más mercadería de la que salió en la ruta: … Revise el conteo. | Escribió una cantidad mayor a **Despachado**. Corrija el conteo. |
| Las cantidades contadas no pueden ser negativas. | Escriba 0 o una cantidad positiva. |
| La mercadería de la ruta … ya fue liquidada. | Otra persona la liquidó. Búsquela en **Rutas con la mercadería ya liquidada**. |
| Solo se puede liquidar la mercadería de una ruta completada. La ruta … está … | La ruta no ha finalizado. Espere a que el chofer la finalice. |
| La ruta … no tiene pedidos sin entregar: no hay mercadería que devolver. | La ruta entregó todo. No hay nada que liquidar aquí. |
