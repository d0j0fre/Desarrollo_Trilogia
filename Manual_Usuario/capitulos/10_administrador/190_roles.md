# Roles {#mod-roles}

Un rol es un puesto de trabajo en el sistema: Administrador, Gerente, Cajero, Bodeguero, Chofer, Cliente, etc. Cada usuario tiene un rol, y el rol decide qué ve y qué puede hacer (sus [permisos](#mod-permisos)).

Se abre en **Administración → Seguridad → Roles**.

## Consultar los roles {#mod-roles-consultar}

### Para qué sirve

Ver qué puestos existen, cuántos usuarios tiene cada uno y si están activos.

### Quién puede usarlo

Administrador.

### Pasos

1. Abra **Administración → Seguridad → Roles**.

   ![Lista de roles](capturas/administrador/adm-roles-01.png)

   1. Para buscar, escriba parte del nombre o la descripción y haga clic en **Buscar**.
   2. **Nuevo rol** abre el formulario para crear uno.
   3. **Editar** abre los datos del rol.
   4. **Inactivar** (o **Activar**) cambia su estado.

### Resultado esperado

La tabla muestra el **ID**, el **Rol**, la **Descripción**, cuántos **Usuarios** lo tienen, la fecha de creación y el **Estado**. Los roles marcados **Base** son los del sistema. **Administrador** y **Cliente** dicen **Protegido**: no se pueden inactivar, porque sin ellos nadie podría administrar el sistema ni comprar en la tienda.

## Crear un rol {#mod-roles-crear}

### Para qué sirve

Agregar un puesto nuevo, por ejemplo cuando la empresa crea un cargo distinto de los que ya existen.

### Quién puede usarlo

Administrador.

### Pasos

1. En **Roles**, haga clic en **Nuevo rol**.
2. Complete el formulario:

   ![Formulario para crear un rol](capturas/administrador/adm-roles-02.png)

   1. **Nombre del rol**: el nombre del puesto. Debe ser único (máximo 50 caracteres).
   2. **Descripción**: qué hace ese puesto.
   3. Deje marcado **Rol activo**.
   4. Haga clic en **Guardar rol**.

### Resultado esperado

Aparece el mensaje «Rol creado correctamente.» y el rol se agrega a la lista con 0 usuarios.

![Rol creado en la lista](capturas/administrador/adm-roles-03.png)

> [!ADVERTENCIA] Un rol nuevo no tiene ningún permiso. Antes de asignarlo a alguien, dele sus permisos en [Permisos](#mod-permisos-asignar). El rol de cada empleado se asigna en [Empleados](#mod-empleados).

### Si algo sale mal

| Mensaje | Qué hacer |
|---|---|
| El nombre del rol es obligatorio. | Escriba el nombre. |
| No se pudo crear el rol. Revise los datos e intente nuevamente. | Probablemente ya existe un rol con ese nombre. Búsquelo en la lista. |
| Ocurrió un error al crear el rol. Intente nuevamente. | Vuelva a intentar. Si continúa, avise a Soporte. |

## Editar, inactivar o reactivar un rol {#mod-roles-editar}

### Para qué sirve

Corregir la descripción de un rol, o dejar de usar un rol que ya no corresponde a ningún puesto.

### Quién puede usarlo

Administrador.

### Pasos

1. En **Roles**, haga clic en **Editar** en la fila del rol.
2. Cambie la **Descripción** o desmarque **Rol activo**, y haga clic en **Guardar cambios**.

   ![Edición de un rol base](capturas/administrador/adm-roles-04.png)

   En los roles base del sistema el **Nombre del rol** no se puede cambiar: el aviso del formulario lo indica.

3. Para inactivar desde la lista, haga clic en **Inactivar** y confirme en la ventana **Confirmar cambio de rol**.

### Resultado esperado

Aparece «Rol actualizado correctamente.» (al editar) o «Estado del rol actualizado correctamente.» (al inactivar o activar). Los cambios quedan en [Auditoría](#mod-auditoria).

Solo se puede inactivar un rol que no tiene usuarios (columna **Usuarios** en 0). Primero cambie el rol de esas personas en [Empleados](#mod-empleados).

> [!NOTA] Los cambios de acceso se aplican de inmediato. Si un usuario queda inactivo, o su rol queda inactivo, o le cambian el rol o la contraseña, el sistema cierra su sesión en su siguiente clic y lo devuelve a la página **Iniciar sesión**, aunque la tenga abierta en ese momento. Avise a la persona antes de hacer el cambio, para que guarde su trabajo.

### Si algo sale mal

![Mensaje al intentar inactivar un rol con usuarios](capturas/administrador/adm-roles-05.png)

| Mensaje | Qué hacer |
|---|---|
| No se puede inactivar el rol porque tiene usuarios asignados. | Asigne otro rol a esos usuarios en [Empleados](#mod-empleados) (o [Clientes](#mod-clientes)) y vuelva a intentarlo. |
| No se pudo actualizar el rol. Revise los datos e intente nuevamente. | Revise que el nombre no esté repetido. |
| No se pudo cambiar el estado del rol. | El rol está protegido o se cambió mientras lo editaba. Vuelva a cargar la lista. |
| No se encontró el rol solicitado. | Vuelva a la lista y ábralo de nuevo. |
