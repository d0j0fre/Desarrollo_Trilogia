# Permisos {#mod-permisos}

Los permisos deciden qué puede hacer cada rol: ver el inventario, crear productos, aprobar planilla, publicar la aplicación, etc. El sistema tiene 102 permisos agrupados por tema. Todos los usuarios de un rol tienen los mismos permisos.

Se abre en **Administración → Seguridad → Permisos**.

## Asignar permisos a un rol {#mod-permisos-asignar}

### Para qué sirve

Dar o quitar acceso a un puesto completo de una sola vez, por ejemplo cuando el Cajero empieza a hacer también la facturación.

### Quién puede usarlo

Administrador.

### Pasos

1. Abra **Administración → Seguridad → Permisos**.

   ![Lista de roles en Permisos](capturas/administrador/adm-permisos-01.png)

   1. Para buscar un rol, escriba su nombre y haga clic en **Buscar**.
   2. Haga clic en **Administrar** en la fila del rol.

2. Revise y marque los permisos:

   ![Permisos de un rol, agrupados por tema](capturas/administrador/adm-permisos-02.png)

   Arriba se ve cuántos **Permisos asignados** tiene el rol (por ejemplo, 5 de 102). Los permisos están agrupados en recuadros por tema (Dashboard, Inventario, Pedidos, Facturación, Planilla…). Cada uno tiene su nombre, una explicación y su código.

   1. Marque la casilla de cada permiso que el rol debe tener; desmarque los que no.

3. Baje hasta el final de la página y haga clic en **Guardar permisos**.

   ![Botón Guardar permisos](capturas/administrador/adm-permisos-03.png)

### Resultado esperado

Aparece el mensaje «Permisos actualizados correctamente.». El cambio queda en [Auditoría](#mod-auditoria). Los usuarios de ese rol ven el cambio la próxima vez que inicien sesión.

Para quitarle **todo** el acceso a una persona, no le borre los permisos al rol (afectaría a todos sus compañeros): inactívela en [Empleados](#mod-empleados). El sistema cierra su sesión en su siguiente clic, aunque la tenga abierta.

> [!ADVERTENCIA] Dé a cada rol solo los permisos que necesita para su trabajo. Por ejemplo, quien registra los gastos no debería aprobarlos. Si duda, consulte el [Mapa de puestos y módulos](#anx-mapa).

> [!NOTA] Cada acción tiene su propio permiso, además del permiso para consultar el módulo. Por ejemplo, un rol con **Ver inventario** pero sin **Crear productos** ni **Editar productos** ve la lista de productos sin los botones **Nuevo producto**, **Editar** ni **Destacado**. Lo mismo pasa con clientes (**Crear clientes**, **Editar clientes**, **Inactivar clientes**), crédito (**Configurar crédito**, **Registrar movimientos de crédito**) y planilla (**Aprobar planilla**, **Configurar reglas de planilla**). Si a una persona le falta un botón, revise el permiso de su rol.

> [!NOTA] El rol **Administrador Base** conserva siempre todos los permisos. Si intenta cambiarlos, el sistema muestra «El rol Administrador mantiene todos los permisos por seguridad.».

### Si algo sale mal

| Mensaje | Qué hacer |
|---|---|
| No se encontró el rol solicitado. | Vuelva a la lista y elija el rol de nuevo. |
| El rol Administrador mantiene todos los permisos por seguridad. | No se pueden quitar permisos al Administrador. |
| No se pudieron actualizar los permisos. / Ocurrió un error al actualizar los permisos. | Vuelva a intentar. Si continúa, avise a Soporte. |
| Un usuario sigue sin ver un módulo después del cambio. | Pídale que cierre sesión y vuelva a entrar (vea [Cerrar sesión](#gen-cerrar-sesion)). El panel **Administración** de cada persona muestra solo las opciones que sus permisos dejan abrir (vea [Botones del menú según el puesto](#gen-navegacion-puestos)). |
