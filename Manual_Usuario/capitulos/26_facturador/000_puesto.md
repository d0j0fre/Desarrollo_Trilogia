# Su trabajo en el sistema {#puesto-facturador}

El Facturador consulta las facturas y los pedidos para atender a los clientes (por ejemplo, reenviarles un comprobante o aclarar un cobro) y para conciliar lo facturado con lo entregado. Su acceso es de **consulta**: no cambia el estado de los pedidos ni genera facturas; eso lo hace el Administrador.

Todos los módulos que usa se explican en otras partes del manual; la tabla del inicio de esta parte los enlaza.

Al iniciar sesión (vea [Iniciar sesión](#gen-login)), haga clic en **Administración** en el menú superior. En el área **Ventas y Pedidos** están sus dos módulos: **Pedidos** y **Facturación** (vea [El panel Administración](#gen-navegacion-admin)).

![Panel Administración del Facturador con el área Ventas y Pedidos](capturas/facturador/fac-menu-01.png "ancho=8")

En el menú superior también tiene **Mi portal** (vea [Mi portal de empleado](#mod-mi-portal)), **Mis jornadas** (vea [Mis jornadas](#mod-mis-jornadas)) y **Mis boletas** (vea [Mis boletas de pago](#mod-mis-boletas)). Además usa los botones **Chat** (vea [Chat interno](#mod-chat)) y **Aplicación móvil**.

## Una jornada típica {#puesto-facturador-rutina}

| Momento | Tarea | Dónde |
|---|---|---|
| Cuando un cliente pide su factura | Buscar la factura y reenviarle el comprobante. | [Facturación](#mod-facturacion) |
| Cuando un cliente pregunta por un cobro | Revisar el pedido: productos, total, método y estado de pago. | [Pedidos](#mod-pedidos-consultar) |
| Al final del día | Comparar los pedidos **Entregados** con las facturas emitidas y avisar al Administrador de los que falten. | [Pedidos](#mod-pedidos-consultar), [Facturación](#mod-facturacion) |
| Fuera de la oficina | Consultar facturas y pedidos desde el teléfono. | [Facturas](#app-facturas), [Pedidos](#app-pedidos) (aplicación) |
| Al terminar | Registrar sus horas. | [Mis jornadas](#mod-mis-jornadas) o [en la aplicación](#app-mis-jornadas) |

> [!NOTA] Al abrir un pedido, el cuadro **Actualizar estado** le dice «Su perfil puede consultar el pedido, pero no cambiar su estado.». Si el pedido ya tiene factura, el botón **Ver factura** lo lleva a ella.
