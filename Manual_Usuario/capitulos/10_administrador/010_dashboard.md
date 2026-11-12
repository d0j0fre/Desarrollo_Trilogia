# Dashboard administrativo {#mod-dashboard}

El Dashboard es el resumen del negocio en una sola pantalla: productos, pedidos, ventas y accesos rápidos a los módulos que más se usan. El Administrador lo revisa al empezar el día para saber qué necesita atención.

## Consultar el resumen del negocio {#mod-dashboard-consultar}

### Para qué sirve

Ver de un vistazo cuántos productos hay, cuáles se están agotando, cuántos pedidos esperan atención y cuánto se ha vendido.

### Quién puede usarlo

Administrador. El Empleado también lo consulta, en **Administración → Reportes y Gerencia → Dashboard**.

### Pasos

1. Abra el Dashboard desde el menú:

   ![Camino del menú para abrir el Dashboard](capturas/administrador/adm-dashboard-01.png "ancho=8")

   1. Haga clic en **Administración**.
   2. Haga clic en **Reportes y Gerencia**.
   3. Haga clic en **Dashboard**.

2. Revise las tarjetas de la parte de arriba:

   ![Tarjetas de productos, pedidos y ventas](capturas/administrador/adm-dashboard-02.png)

   1. **Productos:** total del catálogo, productos activos, con **Stock bajo** (quedan igual o menos unidades que el mínimo configurado) y **Productos agotados** (stock 0).
   2. **Pedidos:** total, **Pendientes** (nadie los ha empezado a preparar), **En proceso** y **Entregados**.
   3. **Ventas:** total facturado desde el inicio y **Ventas del mes actual**.

3. Baje en la página para ver el detalle:

   ![Productos críticos, pedidos recientes y accesos rápidos](capturas/administrador/adm-dashboard-03.png)

   4. **Productos críticos:** los productos agotados o con stock bajo. Son los primeros que hay que reponer.
   5. **Pedidos recientes:** los últimos 5 pedidos, con su cliente, fecha, estado y total.
   6. **Accesos rápidos:** botones que abren directamente Inventario, Movimientos, Roles, Permisos, Clientes, Créditos, Pedidos, Venta móvil, Facturación, Consultas, Auditoría, Planilla, Departamentos (del chat) y Asistente.

### Resultado esperado

La pantalla **Panel de administrador** muestra los datos al momento de abrirla. Para ver datos nuevos, vuelva a cargar la página (tecla **F5**).

> [!CONSEJO] Si **Productos críticos** muestra algún producto, revise las compras sugeridas en [Inteligencia de inventario](#mod-inteligencia) y prepare una [orden de compra](#mod-ordenes-compra-crear).

### Si algo sale mal

| Mensaje | Qué hacer |
|---|---|
| No fue posible cargar todos los datos del tablero. Puede utilizar los accesos rápidos mientras se restablece la información. | Una parte de los datos no se pudo leer. Use los accesos rápidos para seguir trabajando y vuelva a cargar la página en unos minutos. Si continúa, avise a Soporte. |
| 403 · Acceso denegado | Su usuario no tiene el permiso **Ver dashboard**. Revise los permisos de su puesto (vea [Permisos](#mod-permisos)). |
