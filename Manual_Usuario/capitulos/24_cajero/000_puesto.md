# Su trabajo en el sistema {#puesto-cajero}

El Cajero recibe el dinero que cobran los choferes en cada ruta y lo cuadra contra lo que se esperaba cobrar. Su módulo es [Liquidación de cobros](#mod-liquidacion). También puede consultar los ingresos y egresos del negocio (vea [Ingresos y egresos](#mod-ingresos-egresos)).

Al iniciar sesión (vea [Iniciar sesión](#gen-login)), el menú superior le muestra el botón **Liquidación de cobros** (a la derecha), que abre su módulo principal, y el botón **Administración**:

![Menú del Cajero](capturas/cajero/caj-menu-01.png)

En **Administración → Finanzas** encontrará otra vez **Liquidación de cobros** y además **Ingresos y Egresos** (vea [El panel Administración](#gen-navegacion-admin)):

![Panel Administración del Cajero con el área Finanzas](capturas/cajero/caj-menu-02.png "ancho=8")

En el menú superior también tiene **Mi portal** (vea [Mi portal de empleado](#mod-mi-portal)), **Mis jornadas** (vea [Mis jornadas](#mod-mis-jornadas)) y **Mis boletas** (vea [Mis boletas de pago](#mod-mis-boletas)). Además usa los botones **Chat** (vea [Chat interno](#mod-chat)) y **Aplicación móvil**.

## Una jornada típica {#puesto-cajero-rutina}

| Momento | Tarea | Dónde |
|---|---|---|
| Cuando regresa cada chofer | Contar el efectivo y revisar los comprobantes de la ruta. | [Liquidar los cobros de una ruta](#mod-liquidacion-registrar) |
| Si hay faltante o sobrante | Anotar la explicación y avisar al Gerente el mismo día. | [Liquidación de cobros](#mod-liquidacion-registrar) |
| Si la ruta volvió con pedidos sin entregar | Contar la mercadería que regresó y devolverla al inventario. | [Liquidar la mercadería de una ruta](#mod-liquidacion-mercaderia) |
| Si se equivocó en una liquidación | Anularla con un motivo y hacerla de nuevo. | [Anular una liquidación](#mod-liquidacion-anular) |
| Al cierre del día | Revisar que no queden rutas completadas sin liquidar ni faltantes sin atender. | [Liquidación de cobros](#mod-liquidacion) |
| Fuera de la caja | Liquidar desde el teléfono. | [Liquidación de rutas](#app-liquidacion) |
| Al terminar | Registrar sus horas. | [Mis jornadas](#mod-mis-jornadas) o [en la aplicación](#app-mis-jornadas) |
