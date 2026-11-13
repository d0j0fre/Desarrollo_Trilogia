# Gastos operativos {#mod-gastos}

En Gastos operativos se registran los gastos del día a día que no son compras de mercadería: alquiler, electricidad y agua, combustible, mantenimiento de los camiones, suministros de oficina. Cada gasto se carga a un departamento y una categoría, se aprueba y se marca como pagado. Así el sistema sabe cuánto se lleva gastado de cada [presupuesto anual](#mod-presupuestos) y avisa cuando un gasto lo excede.

Se abre en **Administración → Finanzas → Gastos operativos**.

Un gasto pasa por estos estados:

| Estado | Qué significa | ¿Cuenta como gastado? |
|---|---|---|
| Registrado | Se anotó y espera aprobación. | Aparece como **Pendiente**. |
| Requiere Aprobación | Se anotó, pero hace que el departamento pase su presupuesto del mes en esa categoría (o no tiene presupuesto). Para aprobarlo hay que autorizar el exceso. | Aparece como **Pendiente**. |
| Aprobado | Se autorizó el gasto. | Sí. |
| Pagado | Ya se pagó al proveedor. | Sí. |
| Rechazado | No se autorizó; el motivo queda anotado. | No. |
| Anulado | Se canceló (por ejemplo, una factura duplicada). | No. |

## Consultar los gastos {#mod-gastos-consultar}

### Para qué sirve

Ver cuánto se ha gastado, qué falta aprobar o pagar, y encontrar un gasto concreto.

### Quién puede usarlo

Administrador. El Gerente puede consultar los gastos en **Administración → Finanzas → Gastos operativos**.

### Pasos

1. Abra **Administración → Finanzas → Gastos operativos**.

   ![Gastos operativos: totales, registro rápido y lista](capturas/administrador/adm-gastos-01.png)

   Arriba están los totales **Registrado**, **Aprobado**, **Pagado**, **Pendiente** y el presupuesto **Disponible**. Debajo hay un formulario para registrar un gasto rápido (el mismo de [Registrar un gasto](#mod-gastos-registrar)).

   1. **Registrar gasto** abre el formulario de registro en una página aparte.
   2. Escriba el documento, el proveedor o parte de la descripción. También puede elegir fechas **desde** y **hasta**, un **Departamento** y una **Categoría**.
   3. Elija el estado (por ejemplo, **Requiere Aprobación** para ver lo que espera autorización).
   4. Haga clic en **Filtrar**.
   5. **Ver** abre el detalle del gasto.

### Resultado esperado

La lista muestra cada gasto con su fecha y número de documento, el departamento y la categoría, la descripción y el proveedor, el **TOTAL**, el **ESTADO** y el **IMPACTO**: el porcentaje del presupuesto de ese mes y categoría que ya se usó. La etiqueta se pone en rojo (**Crítico**, **Excedido**) cuando el gasto se acerca o pasa del presupuesto.

## Registrar un gasto {#mod-gastos-registrar}

### Para qué sirve

Dejar constancia de un gasto con su comprobante, cargado al departamento y la categoría correctos.

### Quién puede usarlo

Administrador.

### Pasos

1. Haga clic en **Registrar gasto**.
2. Complete el formulario:

   ![Formulario para registrar un gasto](capturas/administrador/adm-gastos-02.png)

   1. **Fecha** del gasto (no puede ser futura ni de hace más de 2 años) y **Departamento**.
   2. **Categoría** del gasto y **Proveedor**.
   3. **N.º documento** (el número de la factura o el recibo) y **Tipo**: **Factura**, **Recibo**, **Nota de débito** u **Otro**.
   4. **Descripción**: qué se pagó.
   5. **Subtotal** e **Impuesto**. El **Total calculado** se suma solo. Elija el **Método de pago** (**Transferencia**, **Tarjeta**, **Efectivo** o **Crédito**) y, si lo tiene, adjunte el **Comprobante privado opcional** (PDF, JPG o PNG).
   6. Haga clic en **Registrar gasto**.

### Resultado esperado

Se abre el detalle del gasto. Si el gasto cabe en el presupuesto, aparece «Gasto registrado.» y el estado es **Registrado**. Si acerca el gasto al límite, el mensaje lo dice: «Gasto registrado. Nivel presupuestario: Crítico (…%)».

![Gasto registrado dentro del presupuesto](capturas/administrador/adm-gastos-03.png)

Si el gasto pasa el presupuesto del mes en esa categoría, o el departamento no tiene presupuesto, el gasto queda en **Requiere Aprobación** y el detalle muestra el aviso rojo **Presupuesto Excedido**. Cuando el departamento no tiene presupuesto, el mensaje dice «Gasto registrado. Nivel presupuestario: Excedido (sin presupuesto).»:

![Gasto que excede el presupuesto: Requiere Aprobación](capturas/administrador/adm-gastos-04.png)

> [!NOTA] Si hace clic dos veces en registrar, el sistema no duplica el gasto: muestra «La operación ya estaba registrada como gasto #…; no se duplicó.».

### Si algo sale mal

| Mensaje | Qué hacer |
|---|---|
| La fecha no puede ser futura ni tener más de dos años. | Revise la fecha. |
| Seleccione un departamento. / Seleccione una categoría. | Elija una opción en cada lista. |
| No fue posible registrar el gasto. | Probablemente ya existe un gasto con el mismo proveedor y número de documento. Búsquelo en la lista. |
| El archivo supera el tamaño máximo permitido de 10 MB. / El archivo debe estar en formato PDF, JPG, JPEG o PNG. | Revise el comprobante. |

## Aprobar, rechazar, pagar o anular un gasto {#mod-gastos-aprobar}

### Para qué sirve

Controlar que cada gasto sea revisado por otra persona antes de pagarse, y que los excesos de presupuesto se autoricen a conciencia.

### Quién puede usarlo

Administrador. **Quien registró el gasto no puede aprobarlo.** Para aprobar un gasto en **Requiere Aprobación** se necesita, además, el permiso de autorizar excesos.

### Pasos

1. Abra el gasto (**Ver**). El recuadro **Acciones** muestra lo que se puede hacer según su estado:

   ![Acciones de un gasto registrado](capturas/administrador/adm-gastos-05.png "ancho=9")

   1. **Autorizar exceso (requiere permiso)**: márquelo antes de aprobar un gasto que pasa el presupuesto.
   2. **Aprobar**: aparece «Gasto aprobado.».
   3. **Motivo**: obligatorio para rechazar.
   4. **Rechazar**: aparece «Gasto rechazado.».
   5. **Motivo de anulación**: obligatorio para anular.
   6. **Anular**: aparece «Gasto anulado; ya no afecta el presupuesto.».

2. Confirme en la ventana que aparece.
3. Cuando el gasto esté **Aprobado**, el recuadro muestra **Marcar pagado**: haga clic cuando se haya pagado al proveedor. Aparece «Gasto marcado como pagado.».
4. Mientras el gasto está **Registrado** o **Requiere Aprobación**, puede corregirlo con **Editar** («Gasto actualizado.»).

### Resultado esperado

El estado del gasto cambia, el cambio queda en el recuadro **Auditoría** del gasto y los totales de [Presupuesto vs. real](#mod-presupuesto-real) e [Ingresos y egresos](#mod-ingresos-egresos) se actualizan.

![Gasto anulado](capturas/administrador/adm-gastos-07.png)

### Si algo sale mal

![Mensaje al intentar aprobar un gasto propio](capturas/administrador/adm-gastos-06.png)

| Mensaje | Causa más común | Qué hacer |
|---|---|---|
| No fue posible aprobar el gasto. | Usted registró el gasto, o el gasto pasa el presupuesto y no marcó **Autorizar exceso**. | Pida a otra persona que lo apruebe, o marque la autorización si corresponde. |
| No cuenta con permiso para autorizar un exceso presupuestario. | Su puesto no tiene ese permiso. | Pida a quien tenga el permiso que lo apruebe. |
| No fue posible rechazar el gasto. / No fue posible anular el gasto. | Falta el motivo, o el estado no lo permite. | Escriba el motivo. Un gasto pagado no se anula. |
| No fue posible pagar el gasto. | El gasto no está **Aprobado**. | Apruébelo primero. |
