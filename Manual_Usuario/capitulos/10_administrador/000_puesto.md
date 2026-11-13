# Su trabajo en el sistema {#puesto-administrador}

El Administrador configura y controla el sistema. Mantiene el catálogo de productos, revisa la facturación, administra a los clientes y su crédito, lleva las finanzas y las compras, calcula la planilla y decide quién puede hacer qué (roles y permisos). También guarda los documentos legales de la empresa y publica las versiones de la aplicación móvil.

Todo el personal ve el botón **Administración** con las áreas y opciones que su puesto puede abrir. El Administrador lo ve completo: 8 áreas y 51 opciones. Puede abrir todos los módulos del sistema, incluidos los que este manual explica en las partes de otros puestos.

> [!ADVERTENCIA] Su cuenta puede cambiar precios, borrar permisos o inactivar clientes. Nunca preste su usuario ni deje la sesión abierta en una computadora compartida (vea [Cerrar sesión](#gen-cerrar-sesion)).

## Una jornada típica {#puesto-administrador-rutina}

| Momento | Tarea | Dónde |
|---|---|---|
| Al iniciar el día | Revisar el resumen del negocio: stock bajo, pedidos pendientes y ventas. | [Dashboard administrativo](#mod-dashboard) |
| Durante la mañana | Atender las alertas de inventario y preparar las compras sugeridas. | [Inteligencia de inventario](#mod-inteligencia), [Órdenes de compra](#mod-ordenes-compra) |
| Cuando llega un camión | Revisar que la orden de compra se haya recibido completa. | [Órdenes de compra](#mod-ordenes-compra) |
| Cuando un cliente pide crédito | Revisar su deuda y ajustar su límite. | [Créditos](#mod-creditos) |
| Cada semana | Revisar las solicitudes de garantía y los documentos por vencer. | [Solicitudes de garantía](#mod-garantias), [Alertas documentales](#mod-alertas-documentales) |
| Cada mes | Comparar lo gastado contra el presupuesto y revisar ingresos y egresos. | [Presupuesto vs. real](#mod-presupuesto-real), [Ingresos y egresos](#mod-ingresos-egresos) |
| Cada quincena | Calcular la planilla del personal y, cuando el Gerente la apruebe, pagarla (con un Administrador distinto del que calculó). | [Planilla](#mod-planilla) |
| Cuando entra o cambia un empleado | Darle el puesto correcto y revisar sus permisos. | [Roles](#mod-roles), [Permisos](#mod-permisos) |

## El menú Administración {#puesto-administrador-menu}

Todos los módulos de administración están en el botón **Administración** del menú superior. Al hacer clic se abre un panel con **8 áreas** (el encabezado dice «8 áreas · 51 módulos»). Cada área agrupa los módulos de un mismo tema. La forma de abrirlo se explica en [El panel Administración](#gen-navegacion-admin).

![Panel del menú Administración con sus 8 áreas](capturas/administrador/adm-menu-01.png "ancho=8")

La tabla muestra qué módulo está en cada área y dónde se explica. Los módulos sin enlace a otra parte se explican en esta Parte II.

| Área | Módulos | Dónde se explica |
|---|---|---|
| **Inventario** | Inventario, Movimientos, Transformar producto, Combos, Devoluciones, Cuarentena, Activos | [Inventario](#mod-inventario), [Movimientos de inventario](#mod-movimientos), [Transformar producto](#mod-transformar), [Combos](#mod-combos), [Devoluciones](#mod-devoluciones), [Cuarentena](#mod-cuarentena), [Activos](#mod-activos) |
| **Ventas y Pedidos** | Pedidos, Facturación, Cuentas por Cobrar, Promociones, Solicitudes de Garantía | [Pedidos](#mod-pedidos), [Facturación](#mod-facturacion), [Cuentas por cobrar](#mod-cxc), [Promociones](#mod-promociones), [Solicitudes de garantía](#mod-garantias) |
| **Logística y Flota** | Rutas y Entregas, Tablero de entregas, Vehículos, Kilometraje, Mantenimiento, Alertas de flota, Comodatos, Rentabilidad de comodatos, Aplicación móvil | [Rutas y entregas](#mod-rutas), [Tablero de entregas](#mod-tablero-entregas), [Vehículos](#mod-vehiculos), [Kilometraje](#mod-kilometraje), [Mantenimiento de flota](#mod-mantenimiento), [Alertas de flota](#mod-alertas-flota), [Comodatos](#mod-comodatos) (incluye la rentabilidad), [Publicar versiones de la aplicación móvil](#mod-app-publicar) |
| **Finanzas** | Liquidación de cobros, Presupuestos anuales, Gastos operativos, Presupuesto vs. real, Ingresos y Egresos, Proveedores, Órdenes de compra, Histórico de precios | [Liquidación de cobros](#mod-liquidacion), [Presupuestos anuales](#mod-presupuestos), [Gastos operativos](#mod-gastos), [Presupuesto vs. real](#mod-presupuesto-real), [Ingresos y egresos](#mod-ingresos-egresos), [Proveedores](#mod-proveedores), [Órdenes de compra](#mod-ordenes-compra), [Histórico de precios](#mod-historico-precios) |
| **Reportes y Gerencia** | Dashboard, Tablero gerencial, Reporte de ventas, Reporte de inventario, Desempeño de vendedores, Metas de ventas, Reporte de KPIs, Inteligencia de inventario | [Dashboard administrativo](#mod-dashboard), [Tablero gerencial](#mod-tablero-gerencial), [Reporte de ventas](#mod-reporte-ventas), [Reporte de inventario](#mod-reporte-inventario), [Desempeño de vendedores](#mod-desempeno), [Metas de ventas](#mod-metas), [Reporte de KPIs](#mod-kpis), [Inteligencia de inventario](#mod-inteligencia) |
| **Clientes** | Clientes, Créditos, Reclamos, Consultas | [Clientes](#mod-clientes), [Créditos](#mod-creditos), [Reclamos](#mod-reclamos), [Consultas de clientes](#mod-consultas) |
| **Personal** | Empleados, Solicitudes (licencias), Aprobar jornadas, Planilla, Boletas de pago | [Empleados](#mod-empleados), [Solicitudes de licencia](#mod-licencias), [Aprobar jornadas](#mod-aprobar-jornadas), [Planilla](#mod-planilla), [Boletas de pago](#mod-boletas) |
| **Seguridad** | Roles, Permisos, Documentos digitales, Alertas documentales, Auditoría | [Roles](#mod-roles), [Permisos](#mod-permisos), [Documentos digitales](#mod-documentos), [Alertas documentales](#mod-alertas-documentales), [Auditoría](#mod-auditoria) |

Fuera del menú Administración, el Administrador también usa estos botones del menú superior:

| Botón | Para qué | Dónde se explica |
|---|---|---|
| **Mi portal**, **Mis boletas** | Su propia información de empleado: tareas, solicitudes de días libres y boletas de pago. | [Mi portal de empleado](#mod-mi-portal), [Mis boletas de pago](#mod-mis-boletas) |
| **Chat** | Conversar con el personal. Como Administrador, además ve la opción **Administrar departamentos**. | [Chat interno](#mod-chat), [Departamentos del chat](#mod-chat-departamentos) |
| **Venta móvil** | Registrar un pedido a nombre de un cliente. | [Venta móvil (web)](#mod-venta-movil) |
| **Asistente** | Hacer preguntas sobre el negocio en lenguaje natural. | [Asistente virtual](#mod-asistente) |
| **Aplicación móvil** | Descargar la aplicación o publicar una versión nueva. | [Publicar versiones de la aplicación móvil](#mod-app-publicar) |

> [!CONSEJO] El [Dashboard administrativo](#mod-dashboard) tiene **Accesos rápidos** a los módulos que más se usan. Úselo como página de inicio de su jornada.

## Mensajes de confirmación {#puesto-administrador-confirmar}

Antes de una acción importante (inactivar un producto, retirar un miembro, cambiar un estado, aprobar o pagar una planilla), el sistema muestra una ventana para confirmar (vea un ejemplo en [Inactivar o reactivar un producto](#mod-inventario-inactivar)):

- Haga clic en **Sí, continuar** (o **Continuar**) para hacer el cambio.
- Haga clic en **Cancelar** o en la **X** para no hacer nada.

Después de cada acción, el sistema muestra arriba de la pantalla un mensaje verde si todo salió bien, o uno rojo si no se pudo hacer. Lea siempre ese mensaje antes de seguir.
