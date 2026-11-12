# Navegación y menú según el puesto {#gen-navegacion}

## Partes de la pantalla {#gen-navegacion-partes}

Todas las páginas del sitio tienen la misma barra superior. Lo que cambia según su puesto son los botones del menú. Si su puesto tiene muchos botones, la barra se acomoda en dos líneas.

![Barra de menú del sitio (ejemplo del puesto Empleado)](capturas/general/gen-nav-01.png)

1. **Inicio.** Lo lleva a la portada. Lo ve todo el mundo.
2. **Administración.** El panel con los módulos de oficina que su puesto puede abrir (vea [El panel Administración](#gen-navegacion-admin)).
3. **Tienda y Carrito.** El catálogo de productos y su carrito de compras. Los ve todo el mundo.
4. **Chat.** Mensajes con sus compañeros y con los departamentos (todo el personal; vea [Chat interno](#mod-chat)).
5. **Botones de su puesto.** Accesos directos a lo que usa todos los días. Cada puesto ve botones distintos (vea la tabla de abajo).

![Zona de su cuenta, arriba a la derecha](capturas/general/gen-nav-02.png)

6. **Su nombre y su puesto.** Haga clic aquí para abrir [Mi perfil](#gen-perfil).
7. **Campana de notificaciones.** El número rojo indica cuántos avisos tiene sin leer (vea [Notificaciones](#gen-notificaciones)).
8. **Cerrar sesión** (vea [Cerrar sesión](#gen-cerrar-sesion)).

A la izquierda del menú está el botón **Categorías**, que abre la lista de categorías de la tienda.

## El panel Administración {#gen-navegacion-admin}

### Para qué sirve

Reunir en un solo lugar los módulos de oficina del negocio, ordenados en áreas: Inventario, Ventas y Pedidos, Logística y Flota, Finanzas, Reportes y Gerencia, Clientes, Personal y Seguridad.

Cada persona ve solo las áreas y los módulos que su puesto puede abrir. El encabezado del panel indica cuántos tiene, por ejemplo «4 áreas · 6 módulos». El Administrador ve las 8 áreas completas.

### Quién puede usarlo

Todo el personal cuyo puesto tiene al menos un módulo de oficina. El Vendedor y los clientes no lo ven.

### Pasos

![Panel Administración del Empleado, con el área Inventario abierta](capturas/general/gen-nav-03.png "ancho=8")

1. Haga clic en **Administración**.
2. Haga clic en el nombre de un área, por ejemplo **Inventario**. Se despliegan sus módulos. Al abrir otra área, la anterior se cierra.
3. Haga clic en el módulo que quiere abrir.

### Resultado esperado

Se abre la página del módulo y el panel se cierra.

## Botones del menú según el puesto {#gen-navegacion-puestos}

Cada puesto ve en el menú solo los módulos que le corresponden. Por ejemplo, el Gerente tiene sus propios botones:

![Botones del menú propios del Gerente](capturas/general/gen-nav-04.png)

| Puesto | Panel **Administración** | Botones propios |
|---|---|---|
| Administrador | Las 8 áreas completas | **Mi portal**, **Mis boletas**, **Venta móvil**, **Asistente**, **Aplicación móvil** |
| Gerente | Inventario · Ventas y Pedidos · Logística y Flota · Finanzas · Reportes y Gerencia (incluye **Reporte de inventario** e **Inteligencia de inventario**) · Clientes (Reclamos) · Personal (Planilla) | **Mi portal**, **Mis boletas**, **Pedidos retenidos**, **Tablero**, **Ventas**, **Vendedores**, **KPIs**, **Asistente**, **Aplicación móvil** |
| Supervisor | Personal: Empleados, Solicitudes (licencias), Aprobar jornadas, Boletas de pago | **Mi portal**, **Mis boletas**, **Aplicación móvil** |
| Empleado | Inventario, Movimientos · Pedidos, Facturación · Dashboard · Consultas | **Mi portal**, **Mis jornadas**, **Mis boletas**, **Venta móvil**, **Asistente**, **Aplicación móvil** |
| Vendedor | No tiene panel | **Mi portal**, **Mis jornadas**, **Mis boletas**, **Venta móvil**, **Mi meta**, **Asistente**, **Aplicación móvil** |
| Chofer | Logística y Flota: Kilometraje | **Mi portal**, **Mis boletas**, **Mis entregas**, **Kilometraje**, **Asistente**, **Aplicación móvil** |
| Cajero | Finanzas: Liquidación de cobros, Ingresos y Egresos | **Mi portal**, **Mis boletas**, **Liquidación de cobros**, **Aplicación móvil** |
| Bodeguero | Inventario, Movimientos · Pedidos · Finanzas: Órdenes de compra | **Mi portal**, **Mis boletas**, **Asistente**, **Aplicación móvil** |
| Facturador | Ventas y Pedidos: Pedidos, Facturación | **Mi portal**, **Mis boletas**, **Aplicación móvil** |
| Crédito y Cobro | Ventas y Pedidos: Cuentas por Cobrar · Clientes: Clientes, Créditos | **Mi portal**, **Mis boletas**, **Aplicación móvil** |
| Compras | Inventario, Movimientos · Finanzas: Proveedores, Órdenes de compra | **Mi portal**, **Mis boletas**, **Aplicación móvil** |
| Soporte | Clientes: Consultas | **Mi portal**, **Mis boletas**, **Aplicación móvil** |
| Auditor Interno | Logística y Flota: Rentabilidad de comodatos · Seguridad: Auditoría | **Mi portal**, **Mis boletas**, **Aplicación móvil** |
| Cliente | No tiene panel | Menú **Mi cuenta** (Mis pedidos, Mis garantías, Estado de cuenta) |

Todo el personal ve además **Chat**, y todos ven **Inicio**, **Tienda** y **Carrito**. **Mi portal** y **Mis boletas** los ve todo el personal que tiene ficha de empleado. **Mis jornadas** lo ve el personal que tiene el permiso para registrar su jornada (hoy, el Empleado y el Vendedor).

> [!NOTA] Los botones de cada pantalla también dependen de sus permisos: si su puesto puede consultar un módulo pero no cambiarlo, el sistema muestra los datos y no dibuja los botones de crear, editar o inactivar. Si su puesto cambia, el sistema cierra su sesión; al iniciarla de nuevo verá los botones y el panel nuevos.

## Página «Acceso denegado» {#gen-navegacion-denegado}

Si intenta abrir un módulo que no corresponde a su puesto (por ejemplo, desde un enlace que alguien le envió), el sistema muestra **403 · Acceso denegado**. No se pierde nada ni se modifica ningún dato.

![Página de acceso denegado](capturas/general/gen-nav-05.png)

1. Haga clic en **Volver al inicio** para regresar.

Si necesita ese módulo para su trabajo, pida a su jefatura que solicite el permiso al Administrador.
