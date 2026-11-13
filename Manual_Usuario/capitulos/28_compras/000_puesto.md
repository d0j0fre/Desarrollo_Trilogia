# Su trabajo en el sistema {#puesto-compras}

El puesto de Compras se asegura de que no falte mercadería: revisa el inventario y las sugerencias de reposición, consulta a los proveedores y da seguimiento a las órdenes de compra hasta que llegan a bodega. Su acceso es de **consulta**: puede ver las órdenes (con estados como **Pendiente**, **Recibida parcial**, **Recibida**, **Cerrada con faltantes** y **Cancelada**) y las **Sugerencias** de compra, pero el Administrador crea las órdenes y la bodega las recibe. Las sugerencias usan el mismo cálculo que **Inteligencia de inventario**, así que las cantidades coinciden en las dos pantallas; cuando un producto no tiene ventas suficientes, la sugerencia dice «Datos insuficientes: ingrese la cantidad manualmente».

Todos los módulos que usa se explican en otras partes del manual; la tabla del inicio de esta parte los enlaza.

Al iniciar sesión (vea [Iniciar sesión](#gen-login)), haga clic en **Administración** en el menú superior (vea [El panel Administración](#gen-navegacion-admin)). El panel tiene 2 áreas:

- **Finanzas**: **Proveedores** y **Órdenes de compra**.
- **Inventario**: **Inventario** y **Movimientos** (consulta).

![Panel Administración de Compras con el área Finanzas](capturas/compras/com-menu-01.png "ancho=8")

En el menú superior también tiene **Mi portal** (vea [Mi portal de empleado](#mod-mi-portal)), **Mis jornadas** (vea [Mis jornadas](#mod-mis-jornadas)) y **Mis boletas** (vea [Mis boletas de pago](#mod-mis-boletas)). Además usa los botones **Chat** (vea [Chat interno](#mod-chat)) y **Aplicación móvil**.

> [!NOTA] En **Órdenes de compra**, el botón **Nueva orden** y los botones **Recibir** del detalle todavía se ven aunque el puesto de Compras no tiene permiso para usarlos: si hace clic, aparece «Acceso denegado». Pídale al Administrador que cree la orden.

## Una jornada típica {#puesto-compras-rutina}

| Momento | Tarea | Dónde |
|---|---|---|
| Al iniciar el día | Revisar los productos con stock bajo y las sugerencias de compra. | [Inventario](#mod-inventario), [Órdenes de compra](#mod-ordenes-compra) |
| Antes de pedir | Consultar los datos de contacto del proveedor. | [Proveedores](#mod-proveedores) |
| Durante la semana | Dar seguimiento a las órdenes enviadas y avisar a bodega de las entregas. | [Órdenes de compra](#mod-ordenes-compra) |
| Cuando llega mercadería | Verificar en el historial que bodega registró la recepción. | [Movimientos de inventario](#mod-movimientos-consultar) |
| Fuera de la oficina | Consultar compras e inventario desde el teléfono. | [Compras](#app-compras), [Consulta de inventario](#app-inventario) (aplicación) |
| Al terminar | Registrar sus horas. | [Mis jornadas](#mod-mis-jornadas) o [en la aplicación](#app-mis-jornadas) |
