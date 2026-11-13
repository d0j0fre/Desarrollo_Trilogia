# Reclamos {#mod-reclamos}

En Reclamos se registra cada inconformidad de un cliente (un producto dañado, una entrega tarde, un cobro mal hecho) y se le da seguimiento hasta cerrarla con la solución documentada.

Se abre en **Administración → Clientes → Reclamos**.

## Registrar un reclamo {#mod-reclamos-registrar}

### Para qué sirve

Dejar por escrito lo que reporta el cliente, con su factura si aplica, para que nadie olvide atenderlo y para medir qué tipo de problemas se repiten.

### Quién puede usarlo

Gerente y Administrador.

### Pasos

1. Abra **Reclamos** y haga clic en **Nuevo reclamo** (flecha).

   ![Lista de reclamos con el botón Nuevo reclamo](capturas/gerente/ger-reclamos-01.png)

2. Complete el formulario **Registrar reclamo**:

   ![Formulario Registrar reclamo](capturas/gerente/ger-reclamos-02.png "ancho=11")

   1. **Cliente**: elija el cliente. La lista muestra solo clientes activos.
   2. **Factura relacionada**: al elegir el cliente, la lista se llena con sus facturas (número, fecha y monto). Elija la del reclamo, o deje **— Sin factura asociada —**.
   3. **Categoría**: **Producto**, **Entrega**, **Facturación**, **Servicio** u **Otro**. **Prioridad**: **Baja**, **Media** o **Alta**.
   4. **Asunto**: un resumen de una línea (hasta 150 caracteres).
   5. **Descripción**: lo que el cliente reportó, con detalle (hasta 1000 caracteres).
   6. Haga clic en **Registrar reclamo**.

### Resultado esperado

Aparece «Reclamo registrado correctamente.» y se abre la ficha del reclamo, con el estado **Abierto**.

![Ficha del reclamo recién registrado](capturas/gerente/ger-reclamos-03.png)

### Si algo sale mal

| Mensaje | Qué hacer |
|---|---|
| Debe seleccionar el cliente. | Elija el cliente en la lista. |
| El asunto es obligatorio. | Escriba el asunto. |
| La descripción es obligatoria. | Escriba la descripción. |
| El asunto no puede superar los 150 caracteres. / La descripción no puede superar los 1000 caracteres. | Resuma el texto. |
| La factura indicada no existe o no pertenece a ese cliente. | Cambió de cliente después de elegir la factura. Vuelva a elegir el cliente y luego la factura. |
| El pedido indicado no existe o no pertenece a ese cliente. | El pedido no es de ese cliente. Revise el número del pedido. |
| El cliente indicado no existe. | El cliente se borró mientras usted llenaba el formulario. Vuelva a cargar la página. |
| No fue posible registrar el reclamo. | Vuelva a intentar. Si continúa, avise a Soporte. |

## Buscar reclamos {#mod-reclamos-buscar}

### Para qué sirve

Ver qué reclamos siguen pendientes y encontrar los de un cliente.

### Quién puede usarlo

Gerente y Administrador.

### Pasos

1. En **Reclamos**, use la barra de búsqueda:

   ![Búsqueda de reclamos y botón Ver](capturas/gerente/ger-reclamos-06.png)

   1. **Estado**: **Todos**, **Abierto**, **En proceso** o **Cerrado**.
   2. **Buscar**: parte del nombre o correo del cliente, del asunto o de la descripción.
   3. Haga clic en **Buscar**.
2. Para abrir un reclamo, haga clic en **Ver** en su fila (recuadro).

### Resultado esperado

La tabla **Reclamos registrados** muestra los que coinciden, con su fecha, cliente, asunto, categoría, prioridad, factura y estado. Arriba a la derecha se ve cuántos registros hay.

| Estado | Qué significa |
|---|---|
| Abierto | Se registró y nadie lo ha empezado a atender. |
| En proceso | Alguien lo está atendiendo. |
| Cerrado | Se resolvió y la solución quedó documentada. Ya no se puede cambiar. |

### Si algo sale mal

| Situación | Qué hacer |
|---|---|
| «No hay reclamos registrados con los filtros seleccionados.» | Elija **Todos** en **Estado** y borre el texto de **Buscar**. |

## Dar seguimiento y cerrar un reclamo {#mod-reclamos-cerrar}

### Para qué sirve

Marcar que el reclamo se está atendiendo y, al resolverlo, documentar qué se hizo por el cliente.

### Quién puede usarlo

Gerente y Administrador.

### Pasos

1. Abra el reclamo con **Ver**. A la derecha está **Seguimiento del caso**.
2. Para indicar que lo está atendiendo, elija **En proceso** en **Estado** y haga clic en **Guardar cambios**.
3. Para cerrarlo:

   ![Cerrar un reclamo con la resolución](capturas/gerente/ger-reclamos-04.png "ancho=8")

   1. En **Estado**, elija **Cerrado**. **Resolución** se marca con un asterisco: ahora es obligatoria.
   2. En **Resolución**, describa cómo se resolvió (por ejemplo, la reposición o la nota de crédito).
   3. Haga clic en **Guardar cambios**.

### Resultado esperado

- Al pasar a **En proceso**: «Estado del reclamo actualizado.».
- Al cerrar: «Reclamo cerrado y resolución documentada.». En la ficha aparece el cuadro verde **Resolución documentada** con la fecha y quién lo cerró, y **Seguimiento del caso** dice «Este reclamo está cerrado y no admite más cambios de estado.».

![Reclamo cerrado con la resolución documentada](capturas/gerente/ger-reclamos-05.png)

> [!ADVERTENCIA] Un reclamo cerrado no se puede volver a abrir. Antes de cerrarlo, confirme con el cliente que quedó satisfecho.

### Si algo sale mal

| Mensaje | Causa | Qué hacer |
|---|---|---|
| Revise el estado y la resolución ingresada. | La resolución pasa de 1000 caracteres. | Resuma el texto. |
| Para cerrar el reclamo debe documentar la resolución. | Quiso cerrar sin escribir la resolución. | Escriba la resolución y vuelva a guardar. |
| El reclamo ya está cerrado; no admite más cambios. | Otra persona ya lo cerró. | Vuelva a cargar la página. |
| Ocurrió un error al actualizar el reclamo. | Falla de conexión con el sistema. | Vuelva a intentar. Si continúa, avise a Soporte. |
| No se encontró el reclamo solicitado. | El enlace es viejo. | Vuelva a la lista y búsquelo de nuevo. |
