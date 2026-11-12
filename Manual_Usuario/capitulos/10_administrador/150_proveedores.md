# Proveedores {#mod-proveedores}

En Proveedores se registran las empresas a las que se les compra mercadería, con su persona de contacto, teléfono y correo. Solo los proveedores activos aparecen al crear una [orden de compra](#mod-ordenes-compra).

Se abre en **Administración → Finanzas → Proveedores**.

## Registrar un proveedor {#mod-proveedores-crear}

### Para qué sirve

Dar de alta una empresa nueva para poder hacerle órdenes de compra y comparar sus precios.

### Quién puede usarlo

Administrador. Compras lo consulta en **Administración → Finanzas → Proveedores** o en la aplicación móvil (vea [Compras](#app-compras)).

### Pasos

1. Abra **Administración → Finanzas → Proveedores**. A la izquierda está el formulario **Nuevo proveedor**.
2. Complete los datos:

   ![Formulario Nuevo proveedor](capturas/administrador/adm-proveedores-01.png)

   1. **Nombre**: el nombre comercial. No puede repetirse.
   2. **Contacto**: la persona con quien se coordina.
   3. **Telefono**.
   4. **Email**: el correo para enviar los pedidos.
   5. Deje marcado **Proveedor activo**.
   6. Haga clic en **Guardar proveedor**.

### Resultado esperado

Aparece el mensaje «Proveedor guardado correctamente.» y el proveedor se agrega a la tabla de la derecha con el estado **Activo**.

![Proveedor registrado en la lista](capturas/administrador/adm-proveedores-02.png)

### Si algo sale mal

![Mensaje cuando el nombre ya existe](capturas/administrador/adm-proveedores-04.png)

| Mensaje | Qué hacer |
|---|---|
| Ya existe un proveedor con ese nombre. | Busque el proveedor en la tabla (puede estar inactivo) y edítelo en vez de crearlo de nuevo. |
| El nombre del proveedor es obligatorio. | Escriba el nombre. |
| El correo no tiene un formato válido. | Revise que el correo tenga **@** y no tenga espacios. |
| No fue posible guardar el proveedor. | Vuelva a intentar. Si continúa, avise a Soporte. |

## Editar, buscar o inactivar un proveedor {#mod-proveedores-editar}

### Para qué sirve

Mantener al día los datos de contacto y dejar de usar un proveedor sin perder sus compras anteriores.

### Quién puede usarlo

Administrador.

### Pasos

1. Para buscar, escriba el nombre en **Buscar proveedor**, elija **Todos**, **Activos** o **Inactivos** y haga clic en **Filtrar**.
2. Para editar o inactivar:

   ![Editar un proveedor desde la tabla](capturas/administrador/adm-proveedores-03.png)

   1. Haga clic en **Editar** en la fila del proveedor.
   2. El formulario de la izquierda cambia a **Editar proveedor #…** con sus datos. Corrija lo que necesite.
   3. Haga clic en **Guardar proveedor**.

   Para dejar de usarlo, haga clic en **Inactivar** en su fila. Para volver a usarlo, haga clic en **Reactivar**. Si se arrepiente de editar, haga clic en **Limpiar / nuevo**.

### Resultado esperado

- Al editar: «Proveedor guardado correctamente.».
- Al inactivar: «Proveedor desactivado.» y el estado cambia a **Inactivo**. Al reactivar: «Proveedor reactivado.».

> [!NOTA] Un proveedor inactivo no aparece en la lista **Proveedor activo** de las órdenes de compra nuevas, pero sus órdenes anteriores y su [historial de precios](#mod-historico-precios) se conservan.

### Si algo sale mal

| Mensaje | Qué hacer |
|---|---|
| El proveedor indicado no existe. | Vuelva a cargar la página y búsquelo de nuevo. |
| No fue posible cambiar el estado del proveedor. | Vuelva a intentar. Si continúa, avise a Soporte. |
