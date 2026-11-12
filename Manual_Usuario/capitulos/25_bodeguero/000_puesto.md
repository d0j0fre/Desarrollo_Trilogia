# Su trabajo en el sistema {#puesto-bodeguero}

El Bodeguero cuida las existencias de la bodega: recibe la mercadería de los proveedores, prepara los pedidos que salen en las rutas y registra cualquier entrada, salida o ajuste del inventario. La mayor parte de su trabajo la hace desde la **aplicación móvil**, en la bodega; desde la computadora registra y consulta los movimientos.

Al iniciar sesión en el sitio web (vea [Iniciar sesión](#gen-login)), sus módulos están en el botón **Administración** del menú superior, en 3 áreas (vea [El panel Administración](#gen-navegacion-admin)):

![Panel Administración del Bodeguero con el área Inventario](capturas/bodeguero/bod-menu-01.png "ancho=8")

| Área | Módulos | Dónde se explica |
|---|---|---|
| **Inventario** | Inventario, Movimientos | [Inventario](#mod-inventario) (solo consulta: no ve los botones para crear, editar o inactivar productos), [Movimientos de inventario](#mod-movimientos) |
| **Ventas y Pedidos** | Pedidos | [Pedidos](#mod-pedidos) (consulta) |
| **Finanzas** | Órdenes de compra | [Órdenes de compra](#mod-ordenes-compra) (consulta) |

> [!NOTA] Crear, editar, inactivar o eliminar un producto exige un permiso aparte, que el Bodeguero no tiene. Si un producto necesita un cambio (por ejemplo, inactivarlo), pídaselo al Administrador. Lo que sí hace el Bodeguero es corregir el stock con un movimiento de tipo **Ajuste**.

En el menú superior también tiene **Mi portal** (vea [Mi portal de empleado](#mod-mi-portal)), **Mis jornadas** (vea [Mis jornadas](#mod-mis-jornadas)) y **Mis boletas** (vea [Mis boletas de pago](#mod-mis-boletas)). Además usa los botones **Chat** (vea [Chat interno](#mod-chat)), **Asistente** (vea [Asistente virtual](#mod-asistente)) y **Aplicación móvil**.

## Una jornada típica {#puesto-bodeguero-rutina}

| Momento | Tarea | Dónde |
|---|---|---|
| Al iniciar el día | Revisar los pedidos que hay que preparar para las rutas. | [Pedidos por preparar](#app-preparar) (aplicación) |
| Cuando llega un camión con orden de compra | Recibir la mercadería contra la orden. | [Recepción de compras](#app-recepcion) (aplicación) |
| Cuando entra o sale mercadería sin orden ni pedido | Registrar la entrada, la salida o el ajuste. | [Movimientos de inventario](#mod-movimientos-registrar) o [en la aplicación](#app-movimientos) |
| Cuando alguien pregunta por un producto | Consultar el stock. | [Consulta de inventario](#app-inventario) (aplicación) |
| Al final de la semana | Contar físicamente y ajustar las diferencias. | [Registrar un movimiento](#mod-movimientos-registrar) (tipo **Ajuste**) |
| Al terminar | Registrar sus horas. | [Mis jornadas](#mod-mis-jornadas) o [en la aplicación](#app-mis-jornadas) |
