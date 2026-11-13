# Promociones {#mod-promociones}

En Promociones se configuran las ofertas sobre un producto estratégico: un **descuento porcentual** o una **regalía por volumen** (un producto de regalo al llevar cierta cantidad). Cada promoción tiene vigencia, segmento de cliente y, si se quiere, un límite de unidades. Las promociones vigentes se aplican solas en el carrito de la [tienda en línea](#mod-carrito).

Se abre en **Administración → Ventas y Pedidos → Promociones**.

![Lista de promociones](capturas/gerente/ger-promociones-01.png)

- **Segmentos de cliente** (flecha izquierda) define qué clientes son mayoristas y cuáles minoristas.
- **Nueva promoción** (flecha derecha) crea una oferta.
- En cada promoción activa, el **lápiz** la edita y el **círculo tachado** la inactiva (recuadro).
- La marca **Vigente**, junto al nombre, indica que la promoción está activa y que hoy está dentro de sus fechas.
- Use el filtro de estado para ver solo las **Activas**, **Inactivas** o **Vencidas**.

## Crear una promoción {#mod-promociones-crear}

### Para qué sirve

Impulsar la venta de un producto con un descuento o una regalía, por un tiempo definido y para el tipo de cliente que usted elija.

### Quién puede usarlo

Gerente y Administrador.

### Pasos

1. En **Promociones**, haga clic en **Nueva promoción**.
2. Complete el formulario **Nueva promoción**:

   ![Formulario de una promoción con descuento](capturas/gerente/ger-promociones-02.png "ancho=11")

   1. **Nombre** de la promoción (obligatorio) y una **Descripción** corta para el equipo de ventas.
   2. **Tipo de promoción**: **Descuento porcentual** o **Regalía por volumen**. **Segmento de cliente**: **Todos los clientes**, **Solo mayoristas** o **Solo minoristas**.
   3. **Producto estratégico**: el producto en oferta. **Cantidad mínima**: cuántas unidades debe llevar el cliente para recibir la promoción.
   4. **Porcentaje de descuento**: por ejemplo **12** para un 12 %. Se aplica solo a la línea del producto estratégico.
   5. **Fecha de inicio** y **Fecha de fin** de la vigencia. **Prioridad**: el orden en que el carrito revisa las promociones; la de número mayor va primero. Si no está seguro, deje **0**. **Límite de unidades** (opcional): al venderse ese número de unidades en promoción, la promoción se inactiva sola, aunque no haya llegado la fecha de fin. Déjelo vacío si no quiere límite.
   6. Haga clic en **Guardar**.

3. Si eligió **Regalía por volumen**, el campo de porcentaje cambia por **Producto de regalía** y **Cantidad de regalía**: elija el producto que se regala y cuántas unidades se regalan por cada **Cantidad mínima** comprada.

   ![Campos de la regalía por volumen](capturas/gerente/ger-promociones-03.png "ancho=10")

### Resultado esperado

Aparece «Promoción creada correctamente.» y la promoción se agrega a la lista con el estado **Activa**. Si hoy está dentro de sus fechas, lleva la marca **Vigente** y empieza a aplicarse de inmediato en el carrito.

![La promoción nueva en la lista](capturas/gerente/ger-promociones-04.png)

En la columna **Tipo** se resume la regla (por ejemplo, «12,00% desde 3 u.») y, si tiene límite, cuántas unidades se han consumido («Unidades: 0 de 60»).

La columna **Estado** indica en qué está cada promoción:

| Estado | Qué significa |
|---|---|
| Activa | Se aplica en el carrito mientras esté dentro de sus fechas (marca **Vigente**). Se puede editar o inactivar. |
| Vencida | Pasó su **Fecha de fin**: ya no se aplica y no tiene botones de acción. Si quiere repetirla, cree otra promoción. |
| Inactiva | Alguien la inactivó o se agotó su límite de unidades. Ya no se aplica. |

### Si algo sale mal

![Mensaje de reglas traslapadas](capturas/gerente/ger-promociones-05.png "ancho=10")

| Mensaje | Causa | Qué hacer |
|---|---|---|
| Reglas traslapadas: el producto ya tiene una promoción activa que se cruza con estas fechas para el mismo segmento. Ajuste la vigencia o inactive la otra promoción. | Ese producto ya tiene otra promoción activa en esas fechas para el mismo segmento (una promoción para **Todos los clientes** se cruza con cualquier segmento). | Cambie las fechas para que no se crucen, o inactive la otra promoción. |
| El límite de unidades debe ser mayor que cero y mayor que las unidades ya consumidas por la promoción. | Escribió 0 en **Límite de unidades**, o al editar, un límite menor que lo ya vendido. | Escriba un límite mayor o deje el campo vacío. |
| El nombre es obligatorio. | Dejó vacío el nombre. | Escriba el nombre. |
| Seleccione el producto estratégico. | No eligió el producto. | Elija el producto en la lista. |
| Indique el porcentaje de descuento. / El porcentaje debe estar entre 0 y 100. | Falta el porcentaje o no es válido. | Escriba un número entre 0,01 y 100. |
| Seleccione el producto de regalía. / Indique la cantidad de regalía. | En una regalía faltan esos datos. | Complete **Producto de regalía** y **Cantidad de regalía**. |
| La fecha de inicio es obligatoria. / La fecha de fin es obligatoria. | Falta una fecha. | Complete las dos fechas. |
| La fecha fin no puede ser anterior a la de inicio. | La **Fecha de fin** es anterior a la **Fecha de inicio**. | Corrija las fechas. |
| El producto estratégico no existe. / El producto de regalía no existe. | El producto se inactivó mientras usted llenaba el formulario. | Vuelva a cargar la página y elija otro producto. |

## Editar o inactivar una promoción {#mod-promociones-editar}

### Para qué sirve

Ajustar una oferta que ya está corriendo (por ejemplo, extender la fecha o cambiar el porcentaje) o detenerla de inmediato si hay un problema, como falta de inventario.

### Quién puede usarlo

Gerente y Administrador.

### Pasos

1. En **Promociones**, busque la promoción (escriba el nombre o el producto y haga clic en **Buscar**).
2. Para editarla, haga clic en el **lápiz** de su fila. Se abre **Editar promoción** con los mismos campos que al crearla. Haga los cambios y haga clic en **Guardar cambios**. Debajo del límite de unidades se ve cuántas se han **Consumido hasta hoy**.
3. Para inactivarla, haga clic en el **círculo tachado** de su fila y confirme con **Sí, inactivar**.

   ![Confirmación antes de inactivar una promoción](capturas/gerente/ger-promociones-06.png "ancho=8")

### Resultado esperado

- Al editar: «Promoción actualizada correctamente.».
- Al inactivar: «Promoción inactivada.». La promoción queda **Inactiva**, deja de aplicarse en el carrito de inmediato y ya no tiene botones de acción.

> [!NOTA] Solo se pueden editar o inactivar promociones **Activas**. Una promoción **Inactiva** o **Vencida** no se puede reactivar: si la necesita de nuevo, cree otra.

### Si algo sale mal

| Mensaje | Qué hacer |
|---|---|
| No se encontró la promoción. | Vuelva a la lista y búsquela de nuevo. |
| Solo se pueden editar promociones activas. / Solo se pueden inactivar promociones activas. | Otra persona la inactivó o se agotó su límite. Vuelva a cargar la lista. |
| Los mensajes de la tabla anterior (traslape, límite, campos obligatorios). | Corrija los datos como se explica en [Crear una promoción](#mod-promociones-crear). |

## Cambiar el segmento de un cliente {#mod-promociones-segmentos}

### Para qué sirve

Marcar a cada cliente como **Minorista** o **Mayorista**, para que reciba las promociones dirigidas a su segmento.

### Quién puede usarlo

Gerente y Administrador.

### Pasos

1. En **Promociones**, haga clic en **Segmentos de cliente**.
2. Busque al cliente por nombre o correo y haga clic en **Buscar**.
3. En la columna **Segmento actual** (recuadro) vea cómo está marcado. Para cambiarlo, haga clic en el botón de la columna **Cambiar a** (flecha): dice **Mayorista** o **Minorista**, según el cambio que va a hacer.

   ![Segmentos de cliente](capturas/gerente/ger-promociones-07.png)

### Resultado esperado

Aparece «Segmento actualizado.» y la columna **Segmento actual** muestra el nuevo valor. Desde ese momento, el cliente recibe las promociones de su nuevo segmento.

> [!ADVERTENCIA] El cambio se hace con un solo clic, sin confirmación. Si se equivoca, haga clic de nuevo en el botón de la misma fila para devolverlo.

### Si algo sale mal

| Mensaje | Qué hacer |
|---|---|
| El cliente no existe. | Vuelva a cargar la página y búsquelo de nuevo. |
| No fue posible actualizar el segmento. | Vuelva a intentar. Si continúa, avise a Soporte. |
