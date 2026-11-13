# Empleados {#mod-empleados}

En Empleados se lleva el expediente laboral de cada persona: sus datos, su puesto, su salario, las tareas que se le asignan y sus solicitudes de días libres. Al registrar un empleado, el sistema también le crea su usuario para entrar al sistema.

Se abre en **Administración → Personal → Empleados**.

## Buscar un empleado {#mod-empleados-buscar}

### Para qué sirve

Encontrar rápido el expediente de una persona para revisarlo o actualizarlo.

### Quién puede usarlo

Supervisor y Administrador.

### Pasos

1. Abra **Administración → Personal → Empleados**. La lista muestra a todo el personal, primero los activos.
2. Use la barra de búsqueda:

   ![Barra de búsqueda de empleados](capturas/supervisor/sup-empleados-01.png)

   1. Escriba parte del nombre, el correo, el puesto o el departamento.
   2. Si quiere, elija **Activos** o **Inactivos**.
   3. Haga clic en **Buscar**.

### Resultado esperado

La lista muestra solo los empleados que coinciden. En cada fila verá el puesto, el rol, el salario, cuántas tareas y solicitudes tiene pendientes, el estado y los botones **Ver**, **Editar** e **Inactivar**.

> [!NOTA] Los botones **Editar** e **Inactivar** aparecen solo en las cuentas que usted puede administrar: las de un rol menor al de Supervisor (por ejemplo, Cajero, Bodeguero o Empleado). En las cuentas de **Administrador**, **Gerente** y **Supervisor** solo aparece **Ver**. En su propia fila no aparece **Inactivar**.

![Resultado de la búsqueda con las acciones de cada empleado](capturas/supervisor/sup-empleados-02.png)

> [!CONSEJO] La búsqueda también revisa el correo. Si escribe una palabra que está en todos los correos (por ejemplo, «labodega»), saldrá todo el personal. Busque por el nombre o por el puesto.

### Si algo sale mal

| Mensaje | Qué hacer |
|---|---|
| No hay empleados para mostrar. | Revise lo que escribió o elija **Todos los estados**. |

## Registrar un empleado {#mod-empleados-crear}

### Para qué sirve

Dar de alta a una persona que entra a trabajar, con su usuario para el sistema, su puesto y su salario inicial.

### Quién puede usarlo

Supervisor y Administrador.

### Pasos

1. En **Empleados**, haga clic en **Nuevo empleado** (arriba a la derecha).
2. Complete los **Datos de usuario**:

   ![Datos de usuario del nuevo empleado](capturas/supervisor/sup-empleados-03.png)

   1. **Identificación**: el número de cédula. No puede repetirse.
   2. **Nombre completo**.
   3. **Correo electrónico**: con este correo entrará al sistema. No puede repetirse.
   4. **Teléfono**.
   5. **Rol del sistema**: el puesto que define qué módulos verá (por ejemplo, **Bodeguero** o **Cajero**). La lista solo ofrece los roles que usted puede asignar.
   6. **Contraseña**: una clave inicial de al menos 4 caracteres. Entréguela a la persona en privado y pídale que la cambie (vea [Mi perfil](#gen-perfil)).

3. Complete los **Datos laborales**:

   ![Datos laborales del nuevo empleado](capturas/supervisor/sup-empleados-04.png)

   1. **Puesto**: el nombre del cargo, por ejemplo «Auxiliar de bodega».
   2. **Departamento**.
   3. **Salario**: el salario mensual en colones, sin puntos ni símbolos.
   4. **Fecha de contratación**.
   5. **Responsabilidades**: las tareas principales del puesto. La persona las verá en su portal.
   6. Haga clic en **Guardar empleado**.

> [!NOTA] El Supervisor puede asignar **Auditor Interno**, **Bodeguero**, **Cajero**, **Chofer**, **Compras**, **Crédito y Cobro**, **Empleado**, **Facturador**, **Soporte** y **Vendedor**. Los roles **Administrador**, **Gerente** y **Supervisor** solo los asigna el Administrador. Elija siempre el rol del puesto real: cada rol da acceso a módulos distintos.

### Resultado esperado

Aparece el mensaje «Empleado registrado correctamente.» y se abre el expediente de la persona. Desde ese momento puede iniciar sesión con su correo y la contraseña inicial.

![Expediente del empleado recién registrado](capturas/supervisor/sup-empleados-05.png)

### Si algo sale mal

![Mensaje cuando no se pudo registrar el empleado](capturas/supervisor/sup-empleados-06.png)

| Mensaje | Causa más común | Qué hacer |
|---|---|---|
| Ya existe un usuario registrado con ese correo. | El correo ya pertenece a otro usuario. | Busque el correo en la lista de empleados. Si la persona ya existe, edítela; si no, use otro correo. |
| No tiene permiso para asignar ese rol. | Eligió un rol de nivel igual o mayor al suyo (por ejemplo, desde una pantalla que quedó abierta). | Elija un rol de la lista. Para registrar un Administrador, un Gerente o un Supervisor, pídaselo al Administrador. |
| Ocurrió un error al registrar el empleado. Intente nuevamente. | Falló algo que el sistema no pudo explicar. | Intente de nuevo. Si continúa, avise a Soporte. |
| La identificación, el correo o la fecha de vigencia ya se encuentran registrados. | La cédula o el correo ya existen. | Busque a la persona en la lista antes de registrarla de nuevo. |
| La contraseña es obligatoria para crear el empleado. | Dejó vacía la contraseña. | Escriba una clave inicial. |
| La contraseña debe tener al menos 4 caracteres. | La clave es muy corta. | Escriba una clave más larga. |
| Debe seleccionar un rol del sistema. | No eligió **Rol del sistema**. | Elija el rol del puesto. |
| La identificación es obligatoria. / El nombre completo es obligatorio. / El correo es obligatorio. / El puesto es obligatorio. | Falta un dato obligatorio. | Complete el campo marcado en rojo. |
| Ingrese un correo válido. | El correo no tiene el formato correcto. | Revise que tenga **@** y no tenga espacios. |
| El salario no puede ser negativo. | Escribió un salario menor que 0. | Escriba el salario correcto. |

## Editar un empleado o cambiar su salario {#mod-empleados-editar}

### Para qué sirve

Mantener al día los datos de la persona y registrar los aumentos o ajustes de salario con su motivo, para que quede el historial.

### Quién puede usarlo

Supervisor y Administrador.

### Pasos

1. En la lista, haga clic en **Editar** en la fila del empleado (o en **Editar** dentro de su expediente). Solo puede editar cuentas de un rol menor al de Supervisor.
2. Corrija los datos que necesite. La **Contraseña** puede quedar vacía: así se conserva la actual.
3. Si cambia el salario, complete también estos campos:

   ![Campos para un cambio de salario](capturas/supervisor/sup-empleados-07.png)

   1. **Salario**: el salario nuevo.
   2. **Motivo de cambio salarial**: por ejemplo «Ajuste anual por desempeño».
   3. **Fecha de vigencia del salario**: desde cuándo rige el salario nuevo.

4. Haga clic en **Guardar cambios**, al final del formulario.

### Resultado esperado

Aparece el mensaje «Empleado actualizado correctamente.» y se abre el expediente. Si cambió el salario, el monto nuevo aparece en **Historial salarial** con la fecha y el motivo.

### Si algo sale mal

| Mensaje | Qué hacer |
|---|---|
| El expediente cambió mientras lo editaba. Recargue la página e intente nuevamente. | Otra persona guardó cambios al mismo tiempo. Recargue la página, revise los datos y vuelva a guardar. |
| No tiene permiso para modificar esa cuenta: solo puede administrar cuentas de un rol inferior al suyo. | Intentó editar una cuenta de Administrador, Gerente o Supervisor. | Pídale el cambio al Administrador. |
| No puede cambiar su propio rol. | Intentó cambiar su propia cuenta. | Pídale el cambio al Administrador. |
| No tiene permiso para asignar ese rol. | Eligió un rol de nivel igual o mayor al suyo. | Elija un rol de la lista. |
| La fecha de vigencia debe ser posterior al 1 de enero de 1900. | Revise la fecha de vigencia. |
| La identificación o el correo ya se encuentran registrados. | Ese dato pertenece a otro empleado. Revise lo que escribió. |
| No se encontró el empleado solicitado. | Vuelva a la lista y búsquelo de nuevo. |

## Asignar y seguir tareas {#mod-empleados-tareas}

### Para qué sirve

Encargar un trabajo concreto a una persona, con prioridad y fecha límite, y seguir su avance. La persona ve sus tareas en [Mi portal de empleado](#mod-mi-portal).

### Quién puede usarlo

Supervisor y Administrador.

### Pasos

1. En la lista, haga clic en **Ver** en la fila del empleado. Se abre su expediente.
2. Baje hasta **Asignar tarea** y complete el formulario:

   ![Formulario Asignar tarea](capturas/supervisor/sup-empleados-08.png)

   1. **Título**: qué debe hacer, en pocas palabras.
   2. **Descripción**: los detalles.
   3. **Prioridad**: **Baja**, **Media**, **Alta** o **Urgente**.
   4. **Fecha límite**.
   5. Haga clic en **Asignar tarea**.

3. Para cambiar el estado de una tarea, en **Tareas asignadas**:

   ![Cambiar el estado de una tarea](capturas/supervisor/sup-empleados-09.png)

   1. Elija el estado nuevo: **Pendiente**, **En proceso**, **Completada** o **Cancelada**.
   2. Haga clic en **Actualizar**.

### Resultado esperado

- Al asignar: «Tarea asignada correctamente.» y la tarea aparece en **Tareas asignadas** con el estado **Pendiente**.
- Al cambiar el estado: «Estado de la tarea actualizado.».

En la lista de empleados, la columna **Seguimiento** cuenta las tareas pendientes o en proceso de cada persona.

### Si algo sale mal

| Mensaje | Qué hacer |
|---|---|
| Debe completar correctamente los datos de la tarea. | Escriba el **Título** (máximo 150 caracteres) y elija la prioridad. |
| No fue posible asignar la tarea. / No fue posible actualizar la tarea. | Vuelva a intentar. Si continúa, avise a Soporte. |

## Inactivar o reactivar un empleado {#mod-empleados-inactivar}

### Para qué sirve

Cuando una persona deja la empresa, inactivarla le quita el acceso al sistema sin borrar su expediente ni su historial.

### Quién puede usarlo

Supervisor y Administrador.

### Pasos

1. En la lista, busque al empleado y haga clic en **Inactivar** (o en **Activar**, si está inactivo).
2. El sistema pide confirmar. Haga clic en **Sí, continuar**.

   ![Confirmación antes de cambiar el estado de un empleado](capturas/supervisor/sup-empleados-10.png "ancho=9")

### Resultado esperado

Aparece «Empleado inactivado correctamente.» (o «Empleado reactivado correctamente.») y el estado de la fila cambia a **Inactivo** (o **Activo**). Una persona inactiva no puede iniciar sesión.

> [!NOTA] El botón **Inactivar** solo aparece en las cuentas de un rol menor al de Supervisor, y nunca en su propia fila. Las cuentas de Administrador, Gerente y Supervisor las inactiva el Administrador.

### Si algo sale mal

| Mensaje | Qué hacer |
|---|---|
| No puede inactivar su propia cuenta. | Pídale al Administrador que haga el cambio. |
| No tiene permiso para modificar esa cuenta: solo puede administrar cuentas de un rol inferior al suyo. | La cuenta es de Administrador, Gerente o Supervisor. Pídale el cambio al Administrador. |
| No se encontró el empleado solicitado. | Vuelva a la lista y búsquelo de nuevo. |
| Ocurrió un error al cambiar el estado del empleado. | Recargue la página y vuelva a intentar. Si continúa, avise a Soporte. |
| La solicitud no es válida. | Recargue la lista y vuelva a intentar. |
