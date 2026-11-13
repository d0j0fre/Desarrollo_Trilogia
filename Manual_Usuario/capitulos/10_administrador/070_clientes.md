# Clientes {#mod-clientes}

En Clientes se registra a los clientes de la empresa (pulperías, bares, hoteles, minisúpers) y se mantienen sus datos de contacto. Desde el detalle de cada cliente se ve su historial de compras, sus facturas, su saldo pendiente y su estado.

Se abre en **Administración → Clientes → Clientes**.

> [!NOTA] Un cliente también puede crear su propia cuenta desde la tienda en línea (vea [Crear una cuenta de cliente](#mod-registro)). Las dos formas crean el mismo tipo de cuenta.

## Buscar un cliente {#mod-clientes-buscar}

### Para qué sirve

Encontrar a un cliente para revisar sus datos, sus pedidos o su saldo.

### Quién puede usarlo

Administrador. Crédito y Cobro lo consulta en **Administración → Clientes → Clientes** o en la aplicación móvil (vea [Crédito de clientes](#app-credito)). Los botones **Nuevo cliente**, **Editar** e **Inactivar** solo se muestran a quien tiene el permiso para hacer ese cambio; quien solo consulta ve la lista y la ficha sin esos botones.

### Pasos

1. Abra **Administración → Clientes → Clientes**.

   ![Buscador y filtro de clientes](capturas/administrador/adm-clientes-01.png)

   1. Escriba parte del nombre, el correo, el teléfono o la dirección.
   2. Elija **Todos**, **Activos** o **Inactivos**.
   3. Haga clic en **Buscar**.
   4. **Nuevo cliente** abre el formulario para registrar uno (vea [Registrar un cliente](#mod-clientes-crear)).

### Resultado esperado

La tabla muestra los clientes que coinciden, con su contacto, cuántos pedidos ha hecho, el **Total comprado**, la fecha del **Último pedido** y el **Estado**. Cada fila tiene los botones **Detalle**, **Editar** e **Inactivar** (o **Activar**).

## Registrar un cliente {#mod-clientes-crear}

### Para qué sirve

Dar de alta a un cliente que compra por teléfono o con un vendedor, para poder registrarle pedidos, facturas y crédito.

### Quién puede usarlo

Administrador.

### Pasos

1. En **Clientes**, haga clic en **Nuevo cliente**.
2. Complete el formulario **Crear cliente**:

   ![Formulario para registrar un cliente](capturas/administrador/adm-clientes-02.png)

   1. **Nombre completo**: el nombre del negocio o de la persona.
   2. **Correo electrónico**: será el usuario del cliente para entrar a la tienda. Debe ser único en el sistema.
   3. **Teléfono**: se usa para enviarle comprobantes por WhatsApp.
   4. **Contraseña**: una contraseña temporal de al menos 4 caracteres. Désela al cliente y pídale que la cambie (vea [Recuperar la contraseña](#gen-recuperar)).
   5. **Dirección**: dirección de entrega.
   6. Haga clic en **Guardar cliente**.

   Deje marcado **Cliente activo**. Si lo registra como inactivo, debe escribir el **Motivo de inactivación**.

### Resultado esperado

Aparece el mensaje «Cliente registrado correctamente.» y el cliente se agrega a la tabla con el estado **Activo**. El registro queda en [Auditoría](#mod-auditoria).

![Cliente registrado en la lista](capturas/administrador/adm-clientes-03.png)

> [!NOTA] Para darle crédito al cliente nuevo, configúrelo en [Créditos](#mod-creditos-configurar).

### Si algo sale mal

![Mensaje cuando no se pudo registrar el cliente](capturas/administrador/adm-clientes-04.png)

| Mensaje | Causa más común | Qué hacer |
|---|---|---|
| No se pudo registrar el cliente. Revise los datos e intente nuevamente. | Ya existe un cliente (o un empleado) con ese correo. | Busque el correo en la lista. Si el cliente ya existe, edítelo en vez de crearlo de nuevo. |
| El nombre del cliente es obligatorio. / El correo es obligatorio. | Falta un dato. | Complete los campos marcados con asterisco (*). |
| Ingrese un correo válido. | El correo no tiene el formato nombre@dominio. | Revise que tenga **@** y no tenga espacios. |
| La contraseña es obligatoria para registrar un cliente. / La contraseña debe tener al menos 4 caracteres. | Falta la contraseña o es muy corta. | Escriba una contraseña temporal de 4 caracteres o más. |
| Debe indicar un motivo si registra el cliente como inactivo. | Desmarcó **Cliente activo**. | Escriba el motivo o vuelva a marcar **Cliente activo**. |

## Ver el detalle de un cliente {#mod-clientes-detalle}

### Para qué sirve

Revisar en una sola pantalla todo lo de un cliente: sus datos, cuánto ha comprado, cuánto debe, sus facturas y sus pedidos.

### Quién puede usarlo

Administrador. Crédito y Cobro, en consulta.

### Pasos

1. Busque al cliente y haga clic en **Detalle**.

   ![Detalle de un cliente](capturas/administrador/adm-clientes-05.png)

   1. **Información del cliente**: nombre, correo, teléfono, dirección y fecha de registro.
   2. **Historial comercial**: cantidad de pedidos, total comprado y fecha del último pedido.
   3. **Estado del cliente**: si está activo, y el botón para inactivarlo (vea [Inactivar o reactivar un cliente](#mod-clientes-inactivar)).
   4. **Saldo**: lo que debe (**Saldo pendiente**), su límite y su crédito disponible. **Ver cargos y pagos** abre su cuenta en [Créditos](#mod-creditos).
   5. **Facturas del cliente**: haga clic en el número de una factura para abrirla en [Facturación](#mod-facturacion).
   6. **Pedidos del cliente**: cada pedido con su estado y forma de entrega.

### Resultado esperado

La pantalla muestra la información del momento. Arriba a la derecha, **Editar** abre el formulario del cliente y **Volver** regresa a la lista.

## Editar los datos de un cliente {#mod-clientes-editar}

### Para qué sirve

Corregir el nombre, el correo, el teléfono o la dirección de un cliente, o asignarle una contraseña nueva.

### Quién puede usarlo

Administrador.

### Pasos

1. Busque al cliente y haga clic en **Editar** (o en **Editar** dentro de su detalle).
2. Cambie los datos:

   ![Formulario para editar un cliente](capturas/administrador/adm-clientes-06.png)

   1. **Nombre completo**.
   2. **Correo electrónico**: si lo cambia, el cliente deberá entrar con el correo nuevo.
   3. **Teléfono**.
   4. **Nueva contraseña**: déjela en blanco para no cambiarla. Escriba una solo si el cliente olvidó la suya y no puede recuperarla.
   5. **Cliente activo**: desmárquelo para inactivar al cliente. En ese caso escriba el **Motivo de inactivación**.
   6. Haga clic en **Guardar cambios**.

### Resultado esperado

Aparece el mensaje «Cliente actualizado correctamente.» y el detalle muestra los datos nuevos.

### Si algo sale mal

| Mensaje | Qué hacer |
|---|---|
| No se pudo actualizar el cliente. Revise los datos e intente nuevamente. | Probablemente el correo nuevo ya lo usa otra cuenta. Use otro correo. |
| Debe indicar un motivo para inactivar el cliente. | Escriba el motivo o vuelva a marcar **Cliente activo**. |
| El cliente tiene un saldo pendiente de ₡… Debe saldarse la cuenta antes de inactivar al cliente. | Vea [Inactivar o reactivar un cliente](#mod-clientes-inactivar). |

## Inactivar o reactivar un cliente {#mod-clientes-inactivar}

### Para qué sirve

Impedir que un cliente que ya no trabaja con la empresa inicie sesión o haga pedidos, sin borrar su historial. Un cliente inactivo se puede reactivar.

### Quién puede usarlo

Administrador.

### Pasos

1. Busque al cliente.
2. Haga clic en **Inactivar** en su fila (o, en su detalle, escriba el **Motivo** y haga clic en **Inactivar cliente**).
3. En la ventana **Confirmar cambio de estado**, haga clic en **Sí, continuar**.

### Resultado esperado

Aparece el mensaje «Cliente inactivado correctamente.» y el estado cambia a **Inactivo**. Para reactivarlo, haga clic en **Activar**: aparece «Cliente reactivado correctamente.».

![Cliente inactivado: mensaje y estado](capturas/administrador/adm-clientes-07.png)

> [!ADVERTENCIA] No se puede inactivar a un cliente que debe dinero. Primero registre los abonos hasta dejar su saldo en ₡0 (vea [Registrar un abono o un cargo](#mod-creditos-movimiento)).

### Si algo sale mal

![Mensaje al inactivar un cliente con saldo pendiente](capturas/administrador/adm-clientes-08.png)

| Mensaje | Qué hacer |
|---|---|
| El cliente tiene un saldo pendiente de ₡… Debe saldarse la cuenta antes de inactivar al cliente. | Cobre la deuda y registre el abono en [Créditos](#mod-creditos-movimiento). Luego vuelva a intentarlo. |
| No se pudo cambiar el estado del cliente. | Vuelva a cargar la página e intente de nuevo. |
