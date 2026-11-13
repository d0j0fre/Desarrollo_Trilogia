# Presupuestos anuales {#mod-presupuestos}

El presupuesto anual dice cuánto puede gastar cada departamento (Administración, Compras, Finanzas, Operaciones, Recursos Humanos) en el año, repartido por categoría de gasto y por mes. Un presupuesto nace como borrador, se presenta para aprobación y, una vez aprobado, queda bloqueado: contra él se comparan los [gastos operativos](#mod-gastos) en [Presupuesto vs. real](#mod-presupuesto-real).

Se abre en **Administración → Finanzas → Presupuestos anuales**.

El presupuesto pasa por estos estados:

| Estado | Qué significa | Qué se puede hacer |
|---|---|---|
| Borrador | Se está preparando. | Editar, repartir por mes y categoría, presentar. |
| Presentado | Espera la aprobación. | Aprobar o rechazar. |
| Rechazado | No se aprobó; el motivo queda anotado. | Corregir y volver a presentar. |
| Aprobado | Es el presupuesto oficial del año. Ya no se edita. | Cerrar, copiar a otro año. |
| Cerrado | El año terminó. | Copiar a otro año. |

## Crear un presupuesto {#mod-presupuestos-crear}

### Para qué sirve

Empezar el presupuesto de un departamento para un año.

### Quién puede usarlo

Administrador. El Gerente puede consultar y aprobar presupuestos en **Administración → Finanzas → Presupuestos anuales**.

### Pasos

1. Abra **Administración → Finanzas → Presupuestos anuales**. A la izquierda está el recuadro **Nuevo presupuesto** y, a la derecha, la lista con los filtros de año, departamento y estado.
2. Complete el recuadro **Nuevo presupuesto**:

   ![Recuadro Nuevo presupuesto y lista de presupuestos](capturas/administrador/adm-presupuestos-01.png)

   1. **Año** del presupuesto.
   2. **Departamento** responsable.
   3. **Categoría inicial**: la categoría de gasto principal (Alquiler, Combustible, Mantenimiento, Servicios públicos, Suministros u Otros).
   4. **Monto anual (CRC)**: el total del año, en colones, sin puntos ni comas de miles.
   5. **Notas**: una explicación breve (opcional).
   6. Haga clic en **Crear borrador**.

### Resultado esperado

Aparece el mensaje «Se creó correctamente. El presupuesto quedó en borrador y debe enviarse para aprobación.» y se abre el detalle del presupuesto. El sistema ya repartió el monto anual en partes iguales en los 12 meses, todo en la categoría inicial: las tarjetas **MONTO ANUAL** y **DISTRIBUIDO** coinciden y la **DIFERENCIA** es ₡0,00.

### Si algo sale mal

| Mensaje | Qué hacer |
|---|---|
| El año debe estar entre 2000 y 2100. | Revise el año. |
| Seleccione un departamento. / Seleccione una categoría inicial. | Elija una opción en cada lista. |
| El monto anual debe ser mayor que cero. | Escriba el monto, solo con números. |
| No fue posible crear el presupuesto. | Probablemente ya existe un presupuesto activo de ese departamento para ese año. Búsquelo con los filtros de la lista. |

## Repartir el presupuesto por mes y categoría {#mod-presupuestos-detalle}

### Para qué sirve

Indicar cuánto se espera gastar cada mes en cada categoría, para que el reporte Presupuesto vs. real compare mes a mes.

### Quién puede usarlo

Administrador.

### Pasos

1. En la lista, haga clic en **Gestionar** en la fila del presupuesto. Mientras está en **Borrador**, el detalle permite modificarlo:

   ![Detalle de un presupuesto en borrador](capturas/administrador/adm-presupuestos-02.png)

   1. **Presentar**: envía el presupuesto para aprobación (vea el procedimiento siguiente).
   2. **Distribuir monto anual**: elija una categoría y haga clic para repartir todo el monto anual en partes iguales en los 12 meses de esa categoría.
   3. **Agregar o editar línea**: elija la **Categoría**, escriba el **Mes** (1 a 12), el **Monto** y unas **Notas**, y haga clic en **Guardar línea**. Para cambiar una línea que ya está en la tabla **Distribución mensual**, corrija su monto y haga clic en **Actualizar** en esa fila.
   4. **Editar borrador**: cambie el **Año**, el **Monto anual** o las **Notas** y haga clic en **Actualizar encabezado**.

### Resultado esperado

Cada cambio muestra un mensaje verde («Línea presupuestaria guardada.», «Monto distribuido en los 12 meses…», «Encabezado actualizado…»). Las tarjetas **DISTRIBUIDO** y **DIFERENCIA** se recalculan.

> [!ADVERTENCIA] Antes de presentar, la **DIFERENCIA** debe ser ₡0,00: la suma de todas las líneas tiene que ser igual al monto anual. Si no, el sistema no deja presentar.

El recuadro **Auditoría** registra quién hizo cada cambio y cuándo.

## Presentar, aprobar o rechazar un presupuesto {#mod-presupuestos-aprobar}

### Para qué sirve

Que el presupuesto pase por una segunda revisión antes de volverse oficial.

### Quién puede usarlo

Administrador y Gerente. **Quien creó el presupuesto no puede aprobarlo**: lo aprueba otra persona, normalmente el Gerente.

### Pasos

1. En el detalle de un presupuesto en **Borrador**, haga clic en **Presentar** y confirme.

   ![Presupuesto presentado para aprobación](capturas/administrador/adm-presupuestos-03.png)

2. Otra persona con permiso de aprobación abre el presupuesto **Presentado**:

   ![Acciones de un presupuesto presentado](capturas/administrador/adm-presupuestos-04.png)

   1. **Aprobar**: lo vuelve oficial.
   2. **Motivo de rechazo**: obligatorio para rechazar.
   3. **Rechazar**: lo devuelve a quien lo creó.
   4. Escriba un año…
   5. …y haga clic en **Copiar a otro año** para usarlo como base del presupuesto de ese año.

3. Al terminar el año, abra el presupuesto aprobado y haga clic en **Cerrar**.

### Resultado esperado

| Acción | Mensaje |
|---|---|
| Presentar | «Presupuesto presentado para aprobación.» |
| Aprobar | «Presupuesto aprobado y bloqueado para edición.» |
| Rechazar | «Presupuesto rechazado.» |
| Cerrar | «Presupuesto cerrado.» |
| Copiar a otro año | «Presupuesto copiado como nuevo borrador.» |

El estado cambia y el cambio queda en el recuadro **Auditoría** del presupuesto. Solo los presupuestos **Aprobado** cuentan en [Presupuesto vs. real](#mod-presupuesto-real).

### Si algo sale mal

![Mensaje al intentar aprobar un presupuesto propio](capturas/administrador/adm-presupuestos-05.png)

| Mensaje | Causa más común | Qué hacer |
|---|---|---|
| No fue posible aprobar el presupuesto. | Usted creó el presupuesto, o ya hay otro aprobado para ese departamento y año. | Pida a otra persona (por ejemplo, al Gerente) que lo apruebe. |
| No fue posible presentar el presupuesto. | Las líneas no suman exactamente el monto anual. | Revise la **DIFERENCIA** y ajuste las líneas. |
| No fue posible rechazar el presupuesto. | Falta el motivo, o el presupuesto no está **Presentado**. | Escriba el motivo. |
| No fue posible cerrar el presupuesto. | Solo se cierra un presupuesto **Aprobado**. | Revise el estado. |
| El presupuesto ya no admite edición. | Está presentado, aprobado o cerrado. | Si necesita cambiarlo, pida que lo rechacen o cópielo. |
| No fue posible copiar el presupuesto. | Ya existe un presupuesto activo en el año destino. | Revise el año destino. |
