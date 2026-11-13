# Mapa de puestos y módulos — Manual de usuario

Generado por `node herramientas/estructura.js` el 2/11/2026, 4:54:19 p. m.. Fuente: `mapa.json` (reparto) y los sondeos en vivo contra el código de la rama `correcciones-esteban-manual` (8f8ffcd) y una base v2 con las migraciones 0251 a 0257 (menú visible, incluidas las opciones del panel «Administración», y acceso real de cada perfil).

**Regla:** cada módulo tiene un **puesto dueño** (la parte del manual donde se escribe su procedimiento, una sola vez). Los demás puestos que lo usan no lo repiten: el generador del Word pone automáticamente, al inicio de cada parte, la tabla «Módulos de otras partes que también usa este puesto» con un hipervínculo a la sección del dueño. Las funciones de la app móvil tienen su propia parte (XVI), dueña de todas ellas.

## 1. Reparto propuesto en 3 grupos (3 sesiones)

### Grupo 1 — Administración · 24 módulos

| Parte | Carpeta | Módulos que escribe (ancla) |
|---|---|---|
| II. Administrador | `capitulos/10_administrador` | Dashboard administrativo (`#mod-dashboard`); Inventario (catálogo de productos) (`#mod-inventario`); Transformar producto (`#mod-transformar`); Combos (`#mod-combos`); Inteligencia de inventario (`#mod-inteligencia`); Facturación (`#mod-facturacion`); Clientes (`#mod-clientes`); Créditos (`#mod-creditos`); Cuentas por cobrar (`#mod-cxc`); Solicitudes de garantía (`#mod-garantias`); Presupuestos anuales (`#mod-presupuestos`); Gastos operativos (`#mod-gastos`); Presupuesto vs. real (`#mod-presupuesto-real`); Ingresos y egresos (`#mod-ingresos-egresos`); Proveedores (`#mod-proveedores`); Órdenes de compra (`#mod-ordenes-compra`); Histórico de precios (`#mod-historico-precios`); Planilla (`#mod-planilla`); Roles (`#mod-roles`); Permisos (`#mod-permisos`); Documentos digitales (`#mod-documentos`); Alertas documentales (`#mod-alertas-documentales`); Departamentos del chat (`#mod-chat-departamentos`); Publicar versiones de la aplicación móvil (`#mod-app-publicar`) |

### Grupo 2 — Gestión y oficina · 30 módulos

| Parte | Carpeta | Módulos que escribe (ancla) |
|---|---|---|
| III. Gerente | `capitulos/20_gerente` | Devoluciones (`#mod-devoluciones`); Cuarentena (`#mod-cuarentena`); Activos (equipos) (`#mod-activos`); Comodatos (`#mod-comodatos`); Promociones (`#mod-promociones`); Reclamos (`#mod-reclamos`); Pedidos retenidos (`#mod-pedidos-retenidos`); Rutas y entregas (`#mod-rutas`); Tablero de entregas (`#mod-tablero-entregas`); Vehículos (`#mod-vehiculos`); Mantenimiento de flota (`#mod-mantenimiento`); Alertas de flota (`#mod-alertas-flota`); Tablero gerencial (`#mod-tablero-gerencial`); Reporte de ventas (`#mod-reporte-ventas`); Desempeño de vendedores (`#mod-desempeno`); Metas de ventas (`#mod-metas`); Reporte de KPIs (`#mod-kpis`) |
| IV. Supervisor | `capitulos/21_supervisor` | Empleados (`#mod-empleados`); Solicitudes de licencia (`#mod-licencias`); Aprobar jornadas (`#mod-aprobar-jornadas`); Boletas de pago (`#mod-boletas`) |
| V. Auditor Interno | `capitulos/22_auditor` | Auditoría (`#mod-auditoria`) |
| VI. Empleado | `capitulos/23_empleado` | Pedidos (administración) (`#mod-pedidos`); Mi portal de empleado (`#mod-mi-portal`); Mis jornadas (web) (`#mod-mis-jornadas`); Mis boletas de pago (`#mod-mis-boletas`); Chat interno (`#mod-chat`) |
| VII. Cajero | `capitulos/24_cajero` | Liquidación de cobros (`#mod-liquidacion`) |
| VIII. Bodeguero | `capitulos/25_bodeguero` | Movimientos de inventario (`#mod-movimientos`) |
| IX. Facturador | `capitulos/26_facturador` | — (solo intro del puesto; usa módulos de otras partes) |
| X. Crédito y Cobro | `capitulos/27_credito` | — (solo intro del puesto; usa módulos de otras partes) |
| XI. Compras | `capitulos/28_compras` | — (solo intro del puesto; usa módulos de otras partes) |
| XII. Soporte | `capitulos/29_soporte` | Consultas de clientes (`#mod-consultas`) |

### Grupo 3 — Operación, clientes y app móvil · 32 módulos

| Parte | Carpeta | Módulos que escribe (ancla) |
|---|---|---|
| XIII. Vendedor | `capitulos/30_vendedor` | Venta móvil (web) (`#mod-venta-movil`); Mi meta (`#mod-mi-meta`); Asistente virtual (`#mod-asistente`) |
| XIV. Chofer | `capitulos/31_chofer` | Mis entregas (web) (`#mod-mis-entregas`); Kilometraje (web) (`#mod-kilometraje`) |
| XV. Cliente | `capitulos/32_cliente` | Crear una cuenta de cliente (`#mod-registro`); Tienda en línea (`#mod-tienda`); Carrito y pago (`#mod-carrito`); Mis pedidos (`#mod-mis-pedidos`); Mis garantías (`#mod-mis-garantias`); Estado de cuenta (`#mod-estado-cuenta`) |
| XVI. Aplicación móvil | `capitulos/40_app` | Mis rutas (`#app-mis-rutas`); Kilometraje (`#app-kilometraje`); Por enviar (trabajo sin señal) (`#app-por-enviar`); Pedidos por preparar (`#app-preparar`); Recepción de compras (`#app-recepcion`); Movimientos de inventario (`#app-movimientos`); Consulta de inventario (`#app-inventario`); Métricas del negocio (`#app-metricas`); Pedidos retenidos (`#app-retenidos`); Rutas y choferes (`#app-rutas`); Pedidos (`#app-pedidos`); Productos (`#app-productos`); Ventas en campo (`#app-ventas`); Liquidación de rutas (`#app-liquidacion`); Facturas (`#app-facturas`); Crédito de clientes (`#app-credito`); Compras (`#app-compras`); Consultas de clientes (`#app-consultas`); Bitácora (`#app-bitacora`); Aprobar jornadas (`#app-aprobar-jornadas`); Mis jornadas (`#app-mis-jornadas`) |

La Parte I (Información general) y los Anexos los escribió la sesión base. Los tres grupos terminaron sus partes.

## 2. Qué ve cada perfil

Columnas: **Menú web** = botones visibles tras iniciar sesión (sondeo). **Acceso web real** = módulos que abren sin «Acceso denegado» (sondeo por URL). **App móvil** = funciones habilitadas por la API para ese perfil.

### Administrador — Parte II

- **Menú web:** Administración, Inventario, Movimientos, Transformar producto, Combos, Devoluciones, Cuarentena, Activos, Pedidos, Facturación, Cuentas por Cobrar, Promociones, Solicitudes de Garantía, Rutas y Entregas, Tablero de entregas, Vehículos, Kilometraje, Mantenimiento, Alertas de flota, Comodatos, Rentabilidad de comodatos, Aplicación móvil, Liquidación de cobros, Presupuestos anuales, Gastos operativos, Presupuesto vs. real, Ingresos y Egresos, Proveedores, Órdenes de compra, Histórico de precios, Dashboard, Tablero gerencial, Reporte de ventas, Desempeño de vendedores, Metas de ventas, Reporte de KPIs, Inteligencia de inventario, Clientes, Créditos, Reclamos, Consultas, Empleados, Solicitudes (licencias), Aprobar jornadas, Planilla, Boletas de pago, Roles, Permisos, Documentos digitales, Alertas documentales, Auditoría, Chat, Administrar departamentos, Venta móvil, Asistente
- **Acceso web real:** Dashboard administrativo, Inventario, Movimientos de inventario, Registrar movimiento, Transformar producto, Combos, Devoluciones, Cuarentena, Activos, Pedidos, Facturación, Cuentas por cobrar, Promociones, Solicitudes de garantía, Rutas y entregas, Tablero de entregas, Vehículos, Kilometraje, Mantenimiento, Alertas de flota, Comodatos, Aplicación móvil, Liquidación de cobros, Presupuestos anuales, Gastos operativos, Presupuesto vs. real, Ingresos y egresos, Proveedores, Órdenes de compra, Histórico de precios, Tablero gerencial, Reporte de ventas, Desempeño de vendedores, Metas de ventas, Reporte de KPIs, Inteligencia de inventario, Clientes, Créditos, Reclamos, Consultas, Empleados, Solicitudes (licencias), Aprobar jornadas, Planilla, Boletas de pago, Roles, Permisos, Documentos digitales, Alertas documentales, Auditoría, Administrar departamentos de chat, Pedidos retenidos, Venta móvil, Asistente, Mis entregas, Mis boletas
- **Con acceso pero sin botón en el menú:** Registrar movimiento, Mis entregas, Mis boletas
- **App móvil:** Bodega: Pedidos por preparar · Bodega: Recepción de compras · Bodega: Movimientos de inventario · Bodega: Consulta de inventario · Gestión: Métricas del negocio · Gestión: Pedidos retenidos · Gestión: Rutas y choferes · Gestión: Pedidos · Gestión: Productos · Ventas: Ventas en campo · Oficina: Liquidación de rutas · Oficina: Facturas · Oficina: Crédito de clientes · Oficina: Compras · Oficina: Consultas de clientes · Oficina: Bitácora · Personal: Aprobar jornadas · Personal: Mis jornadas
- **Escribe en su parte:** Dashboard administrativo, Inventario (catálogo de productos), Transformar producto, Combos, Inteligencia de inventario, Facturación, Clientes, Créditos, Cuentas por cobrar, Solicitudes de garantía, Presupuestos anuales, Gastos operativos, Presupuesto vs. real, Ingresos y egresos, Proveedores, Órdenes de compra, Histórico de precios, Planilla, Roles, Permisos, Documentos digitales, Alertas documentales, Departamentos del chat, Publicar versiones de la aplicación móvil
- **Enlaza a otras partes:** Devoluciones → Gerente; Cuarentena → Gerente; Activos (equipos) → Gerente; Comodatos → Gerente; Promociones → Gerente; Reclamos → Gerente; Pedidos retenidos → Gerente; Rutas y entregas → Gerente; Tablero de entregas → Gerente; Vehículos → Gerente; Mantenimiento de flota → Gerente; Alertas de flota → Gerente; Tablero gerencial → Gerente; Reporte de ventas → Gerente; Desempeño de vendedores → Gerente; Metas de ventas → Gerente; Reporte de KPIs → Gerente; Empleados → Supervisor; Solicitudes de licencia → Supervisor; Aprobar jornadas → Supervisor; Boletas de pago → Supervisor; Auditoría → Auditor Interno; Pedidos (administración) → Empleado; Chat interno → Empleado; Liquidación de cobros → Cajero; Movimientos de inventario → Bodeguero; Consultas de clientes → Soporte; Venta móvil (web) → Vendedor; Asistente virtual → Vendedor; Mis entregas (web) → Chofer; Kilometraje (web) → Chofer; Pedidos por preparar → Aplicación móvil; Recepción de compras → Aplicación móvil; Movimientos de inventario → Aplicación móvil; Consulta de inventario → Aplicación móvil; Métricas del negocio → Aplicación móvil; Pedidos retenidos → Aplicación móvil; Rutas y choferes → Aplicación móvil; Pedidos → Aplicación móvil; Productos → Aplicación móvil; Ventas en campo → Aplicación móvil; Liquidación de rutas → Aplicación móvil; Facturas → Aplicación móvil; Crédito de clientes → Aplicación móvil; Compras → Aplicación móvil; Consultas de clientes → Aplicación móvil; Bitácora → Aplicación móvil; Aprobar jornadas → Aplicación móvil; Mis jornadas → Aplicación móvil

### Gerente — Parte III

- **Menú web:** Administración, Inventario, Movimientos, Devoluciones, Cuarentena, Activos, Pedidos, Promociones, Rutas y Entregas, Tablero de entregas, Vehículos, Mantenimiento, Alertas de flota, Comodatos, Rentabilidad de comodatos, Liquidación de cobros, Presupuestos anuales, Gastos operativos, Presupuesto vs. real, Ingresos y Egresos, Tablero gerencial, Reporte de ventas, Desempeño de vendedores, Metas de ventas, Reporte de KPIs, Reclamos, Chat, Pedidos retenidos, Tablero, Ventas, Vendedores, KPIs, Asistente, Aplicación móvil
- **Acceso web real:** Inventario, Movimientos de inventario, Devoluciones, Cuarentena, Activos, Pedidos, Promociones, Rutas y entregas, Tablero de entregas, Vehículos, Mantenimiento, Alertas de flota, Comodatos, Aplicación móvil, Liquidación de cobros, Presupuestos anuales, Gastos operativos, Presupuesto vs. real, Ingresos y egresos, Tablero gerencial, Reporte de ventas, Desempeño de vendedores, Metas de ventas, Reporte de KPIs, Reclamos, Pedidos retenidos, Asistente, Mis boletas
- **Con acceso pero sin botón en el menú:** Mis boletas
- **App móvil:** Bodega: Consulta de inventario · Gestión: Métricas del negocio · Gestión: Pedidos retenidos · Gestión: Rutas y choferes · Gestión: Pedidos · Oficina: Liquidación de rutas · Personal: Mis jornadas
- **Escribe en su parte:** Devoluciones, Cuarentena, Activos (equipos), Comodatos, Promociones, Reclamos, Pedidos retenidos, Rutas y entregas, Tablero de entregas, Vehículos, Mantenimiento de flota, Alertas de flota, Tablero gerencial, Reporte de ventas, Desempeño de vendedores, Metas de ventas, Reporte de KPIs
- **Enlaza a otras partes:** Inventario (catálogo de productos) → Administrador; Presupuestos anuales → Administrador; Gastos operativos → Administrador; Presupuesto vs. real → Administrador; Ingresos y egresos → Administrador; Pedidos (administración) → Empleado; Chat interno → Empleado; Liquidación de cobros → Cajero; Movimientos de inventario → Bodeguero; Asistente virtual → Vendedor; Consulta de inventario → Aplicación móvil; Métricas del negocio → Aplicación móvil; Pedidos retenidos → Aplicación móvil; Rutas y choferes → Aplicación móvil; Pedidos → Aplicación móvil; Liquidación de rutas → Aplicación móvil; Mis jornadas → Aplicación móvil

### Supervisor — Parte IV

- **Menú web:** Administración, Empleados, Solicitudes (licencias), Aprobar jornadas, Boletas de pago, Chat, Aplicación móvil
- **Acceso web real:** Aplicación móvil, Empleados, Solicitudes (licencias), Aprobar jornadas, Boletas de pago, Mis boletas
- **Con acceso pero sin botón en el menú:** Mis boletas
- **App móvil:** Personal: Aprobar jornadas · Personal: Mis jornadas
- **Escribe en su parte:** Empleados, Solicitudes de licencia, Aprobar jornadas, Boletas de pago
- **Enlaza a otras partes:** Chat interno → Empleado; Aprobar jornadas → Aplicación móvil; Mis jornadas → Aplicación móvil

### Auditor Interno — Parte V

- **Menú web:** Administración, Rentabilidad de comodatos, Auditoría, Chat, Aplicación móvil
- **Acceso web real:** Aplicación móvil, Auditoría, Mis boletas
- **Con acceso pero sin botón en el menú:** Mis boletas
- **App móvil:** Oficina: Bitácora · Personal: Mis jornadas
- **Escribe en su parte:** Auditoría
- **Enlaza a otras partes:** Comodatos → Gerente; Chat interno → Empleado; Bitácora → Aplicación móvil; Mis jornadas → Aplicación móvil

### Empleado — Parte VI

- **Menú web:** Administración, Inventario, Movimientos, Pedidos, Facturación, Dashboard, Consultas, Chat, Mi portal, Mis jornadas, Mis boletas, Venta móvil, Asistente, Aplicación móvil
- **Acceso web real:** Dashboard administrativo, Inventario, Movimientos de inventario, Registrar movimiento, Pedidos, Facturación, Aplicación móvil, Consultas, Venta móvil, Asistente, Mi portal, Mis jornadas, Mis boletas
- **Con acceso pero sin botón en el menú:** Registrar movimiento
- **App móvil:** Bodega: Movimientos de inventario · Bodega: Consulta de inventario · Gestión: Pedidos · Ventas: Ventas en campo · Oficina: Facturas · Oficina: Consultas de clientes · Personal: Mis jornadas
- **Escribe en su parte:** Pedidos (administración), Mi portal de empleado, Mis jornadas (web), Mis boletas de pago, Chat interno
- **Enlaza a otras partes:** Dashboard administrativo → Administrador; Inventario (catálogo de productos) → Administrador; Facturación → Administrador; Movimientos de inventario → Bodeguero; Consultas de clientes → Soporte; Venta móvil (web) → Vendedor; Asistente virtual → Vendedor; Movimientos de inventario → Aplicación móvil; Consulta de inventario → Aplicación móvil; Pedidos → Aplicación móvil; Ventas en campo → Aplicación móvil; Facturas → Aplicación móvil; Consultas de clientes → Aplicación móvil; Mis jornadas → Aplicación móvil

### Cajero — Parte VII

- **Menú web:** Administración, Liquidación de cobros, Ingresos y Egresos, Chat, Aplicación móvil
- **Acceso web real:** Aplicación móvil, Liquidación de cobros, Ingresos y egresos, Mis boletas
- **Con acceso pero sin botón en el menú:** Mis boletas
- **App móvil:** Oficina: Liquidación de rutas · Personal: Mis jornadas
- **Escribe en su parte:** Liquidación de cobros
- **Enlaza a otras partes:** Ingresos y egresos → Administrador; Chat interno → Empleado; Liquidación de rutas → Aplicación móvil; Mis jornadas → Aplicación móvil

### Bodeguero — Parte VIII

- **Menú web:** Administración, Inventario, Movimientos, Pedidos, Órdenes de compra, Chat, Asistente, Aplicación móvil
- **Acceso web real:** Inventario, Movimientos de inventario, Registrar movimiento, Pedidos, Aplicación móvil, Órdenes de compra, Asistente, Mis boletas
- **Con acceso pero sin botón en el menú:** Registrar movimiento, Mis boletas
- **App móvil:** Bodega: Pedidos por preparar · Bodega: Recepción de compras · Bodega: Movimientos de inventario · Bodega: Consulta de inventario · Personal: Mis jornadas
- **Escribe en su parte:** Movimientos de inventario
- **Enlaza a otras partes:** Inventario (catálogo de productos) → Administrador; Órdenes de compra → Administrador; Pedidos (administración) → Empleado; Chat interno → Empleado; Asistente virtual → Vendedor; Pedidos por preparar → Aplicación móvil; Recepción de compras → Aplicación móvil; Movimientos de inventario → Aplicación móvil; Consulta de inventario → Aplicación móvil; Mis jornadas → Aplicación móvil

### Facturador — Parte IX

- **Menú web:** Administración, Pedidos, Facturación, Chat, Aplicación móvil
- **Acceso web real:** Pedidos, Facturación, Aplicación móvil, Mis boletas
- **Con acceso pero sin botón en el menú:** Mis boletas
- **App móvil:** Gestión: Pedidos · Oficina: Facturas · Personal: Mis jornadas
- **Escribe en su parte:** —
- **Enlaza a otras partes:** Facturación → Administrador; Pedidos (administración) → Empleado; Chat interno → Empleado; Pedidos → Aplicación móvil; Facturas → Aplicación móvil; Mis jornadas → Aplicación móvil

### Crédito y Cobro — Parte X

- **Menú web:** Administración, Cuentas por Cobrar, Clientes, Créditos, Chat, Aplicación móvil
- **Acceso web real:** Cuentas por cobrar, Aplicación móvil, Clientes, Créditos, Mis boletas
- **Con acceso pero sin botón en el menú:** Mis boletas
- **App móvil:** Oficina: Crédito de clientes · Personal: Mis jornadas
- **Escribe en su parte:** —
- **Enlaza a otras partes:** Clientes → Administrador; Créditos → Administrador; Cuentas por cobrar → Administrador; Chat interno → Empleado; Crédito de clientes → Aplicación móvil; Mis jornadas → Aplicación móvil

### Compras — Parte XI

- **Menú web:** Administración, Inventario, Movimientos, Proveedores, Órdenes de compra, Chat, Aplicación móvil
- **Acceso web real:** Inventario, Movimientos de inventario, Aplicación móvil, Proveedores, Órdenes de compra, Mis boletas
- **Con acceso pero sin botón en el menú:** Mis boletas
- **App móvil:** Bodega: Consulta de inventario · Oficina: Compras · Personal: Mis jornadas
- **Escribe en su parte:** —
- **Enlaza a otras partes:** Inventario (catálogo de productos) → Administrador; Proveedores → Administrador; Órdenes de compra → Administrador; Chat interno → Empleado; Movimientos de inventario → Bodeguero; Consulta de inventario → Aplicación móvil; Compras → Aplicación móvil; Mis jornadas → Aplicación móvil

### Soporte — Parte XII

- **Menú web:** Administración, Consultas, Chat, Aplicación móvil
- **Acceso web real:** Aplicación móvil, Consultas, Mis boletas
- **Con acceso pero sin botón en el menú:** Mis boletas
- **App móvil:** Oficina: Consultas de clientes · Personal: Mis jornadas
- **Escribe en su parte:** Consultas de clientes
- **Enlaza a otras partes:** Chat interno → Empleado; Consultas de clientes → Aplicación móvil; Mis jornadas → Aplicación móvil

### Vendedor — Parte XIII

- **Menú web:** Chat, Mi portal, Mis jornadas, Mis boletas, Venta móvil, Mi meta, Asistente, Aplicación móvil
- **Acceso web real:** Aplicación móvil, Venta móvil, Mi meta, Asistente, Mi portal, Mis jornadas, Mis boletas
- **App móvil:** Ventas: Ventas en campo · Personal: Mis jornadas
- **Escribe en su parte:** Venta móvil (web), Mi meta, Asistente virtual
- **Enlaza a otras partes:** Mi portal de empleado → Empleado; Mis jornadas (web) → Empleado; Mis boletas de pago → Empleado; Chat interno → Empleado; Ventas en campo → Aplicación móvil; Mis jornadas → Aplicación móvil

### Chofer — Parte XIV

- **Menú web:** Administración, Kilometraje, Chat, Mis entregas, Asistente, Aplicación móvil
- **Acceso web real:** Kilometraje, Aplicación móvil, Asistente, Mis entregas, Mis boletas
- **Con acceso pero sin botón en el menú:** Mis boletas
- **App móvil:** Operación en ruta: Mis rutas · Operación en ruta: Kilometraje · Operación en ruta: Por enviar · Personal: Mis jornadas
- **Escribe en su parte:** Mis entregas (web), Kilometraje (web)
- **Enlaza a otras partes:** Chat interno → Empleado; Asistente virtual → Vendedor; Mis rutas → Aplicación móvil; Kilometraje → Aplicación móvil; Por enviar (trabajo sin señal) → Aplicación móvil; Mis jornadas → Aplicación móvil

### Cliente — Parte XV

- **Menú web:** Mi cuenta, Mis pedidos, Mis garantías, Estado de cuenta
- **Acceso web real:** Aplicación móvil, Mis boletas, Mis pedidos (cliente), Mis garantías (cliente), Estado de cuenta (cliente)
- **Con acceso pero sin botón en el menú:** Aplicación móvil, Mis boletas
- **App móvil:** sin acceso (Tu perfil no tiene acceso a la aplicación móvil. Podés usar el sitio web.)
- **Escribe en su parte:** Crear una cuenta de cliente, Tienda en línea, Carrito y pago, Mis pedidos, Mis garantías, Estado de cuenta
- **Enlaza a otras partes:** —

## 3. Tabla maestra de módulos

| Ancla | Módulo | Plataforma | Dónde está | Dueño (parte) | Grupo | También lo usan | Nota |
|---|---|---|---|---|---|---|---|
| `mod-dashboard` | Dashboard administrativo | Web | Administración → Reportes y Gerencia → Dashboard | II. Administrador | 1 | Empleado (consulta) |  |
| `mod-inventario` | Inventario (catálogo de productos) | Web | Administración → Inventario → Inventario | II. Administrador | 1 | Gerente (consulta), Empleado (consulta), Bodeguero (consulta), Compras (consulta) |  |
| `mod-transformar` | Transformar producto | Web | Administración → Inventario → Transformar producto | II. Administrador | 1 | — |  |
| `mod-combos` | Combos | Web | Administración → Inventario → Combos | II. Administrador | 1 | — |  |
| `mod-inteligencia` | Inteligencia de inventario | Web | Administración → Reportes y Gerencia → Inteligencia de inventario | II. Administrador | 1 | — |  |
| `mod-facturacion` | Facturación | Web | Administración → Ventas y Pedidos → Facturación | II. Administrador | 1 | Empleado (consulta), Facturador (consulta) |  |
| `mod-clientes` | Clientes | Web | Administración → Clientes → Clientes | II. Administrador | 1 | Crédito y Cobro (consulta) |  |
| `mod-creditos` | Créditos | Web | Administración → Clientes → Créditos | II. Administrador | 1 | Crédito y Cobro (consulta) |  |
| `mod-cxc` | Cuentas por cobrar | Web | Administración → Ventas y Pedidos → Cuentas por Cobrar | II. Administrador | 1 | Crédito y Cobro (consulta) |  |
| `mod-garantias` | Solicitudes de garantía | Web | Administración → Ventas y Pedidos → Solicitudes de Garantía | II. Administrador | 1 | — |  |
| `mod-presupuestos` | Presupuestos anuales | Web | Administración → Finanzas → Presupuestos anuales | II. Administrador | 1 | Gerente (parcial) | El Gerente consulta y aprueba (permisos del PR #131, ya en main). |
| `mod-gastos` | Gastos operativos | Web | Administración → Finanzas → Gastos operativos | II. Administrador | 1 | Gerente (consulta) | El Gerente consulta (PR #131, ya en main). |
| `mod-presupuesto-real` | Presupuesto vs. real | Web | Administración → Finanzas → Presupuesto vs. real | II. Administrador | 1 | Gerente (consulta) | El Gerente consulta (PR #131, ya en main). |
| `mod-ingresos-egresos` | Ingresos y egresos | Web | Administración → Finanzas → Ingresos y Egresos | II. Administrador | 1 | Gerente (consulta), Cajero (consulta) |  |
| `mod-proveedores` | Proveedores | Web | Administración → Finanzas → Proveedores | II. Administrador | 1 | Compras (consulta) |  |
| `mod-ordenes-compra` | Órdenes de compra | Web | Administración → Finanzas → Órdenes de compra | II. Administrador | 1 | Compras (consulta), Bodeguero (parcial) |  |
| `mod-historico-precios` | Histórico de precios | Web | Administración → Finanzas → Histórico de precios | II. Administrador | 1 | — | Compras no tiene COMPRAS_PRECIOS_VER: el panel ya no le muestra la opción. |
| `mod-planilla` | Planilla | Web | Administración → Personal → Planilla | II. Administrador | 1 | — | El Supervisor no tiene PLANILLA_VER: el panel ya no le muestra la opción. |
| `mod-roles` | Roles | Web | Administración → Seguridad → Roles | II. Administrador | 1 | — |  |
| `mod-permisos` | Permisos | Web | Administración → Seguridad → Permisos | II. Administrador | 1 | — |  |
| `mod-documentos` | Documentos digitales | Web | Administración → Seguridad → Documentos digitales | II. Administrador | 1 | — |  |
| `mod-alertas-documentales` | Alertas documentales | Web | Administración → Seguridad → Alertas documentales | II. Administrador | 1 | — | Solo Administrador (el Gerente no tiene el permiso; el panel no se lo muestra). |
| `mod-chat-departamentos` | Departamentos del chat | Web | Chat → Administrar departamentos | II. Administrador | 1 | — |  |
| `mod-app-publicar` | Publicar versiones de la aplicación móvil | Web | Administración → Logística y Flota → Aplicación móvil | II. Administrador | 1 | — |  |
| `mod-devoluciones` | Devoluciones | Web | Administración → Inventario → Devoluciones | III. Gerente | 2 | Administrador (completo) |  |
| `mod-cuarentena` | Cuarentena | Web | Administración → Inventario → Cuarentena | III. Gerente | 2 | Administrador (completo) |  |
| `mod-activos` | Activos (equipos) | Web | Administración → Inventario → Activos | III. Gerente | 2 | Administrador (completo) |  |
| `mod-comodatos` | Comodatos | Web | Administración → Logística y Flota → Comodatos | III. Gerente | 2 | Administrador (completo), Auditor Interno (consulta (rentabilidad)) |  |
| `mod-promociones` | Promociones | Web | Administración → Ventas y Pedidos → Promociones | III. Gerente | 2 | Administrador (completo) |  |
| `mod-reclamos` | Reclamos | Web | Administración → Clientes → Reclamos | III. Gerente | 2 | Administrador (completo) |  |
| `mod-pedidos-retenidos` | Pedidos retenidos | Web | Menú superior → Pedidos retenidos | III. Gerente | 2 | Administrador (completo) |  |
| `mod-rutas` | Rutas y entregas | Web | Administración → Logística y Flota → Rutas y Entregas | III. Gerente | 2 | Administrador (completo) |  |
| `mod-tablero-entregas` | Tablero de entregas | Web | Administración → Logística y Flota → Tablero de entregas | III. Gerente | 2 | Administrador (completo) |  |
| `mod-vehiculos` | Vehículos | Web | Administración → Logística y Flota → Vehículos | III. Gerente | 2 | Administrador (completo) |  |
| `mod-mantenimiento` | Mantenimiento de flota | Web | Administración → Logística y Flota → Mantenimiento | III. Gerente | 2 | Administrador (completo) |  |
| `mod-alertas-flota` | Alertas de flota | Web | Administración → Logística y Flota → Alertas de flota | III. Gerente | 2 | Administrador (completo) |  |
| `mod-tablero-gerencial` | Tablero gerencial | Web | Menú superior → Tablero | III. Gerente | 2 | Administrador (completo) |  |
| `mod-reporte-ventas` | Reporte de ventas | Web | Menú superior → Ventas | III. Gerente | 2 | Administrador (completo) |  |
| `mod-desempeno` | Desempeño de vendedores | Web | Menú superior → Vendedores | III. Gerente | 2 | Administrador (completo) |  |
| `mod-metas` | Metas de ventas | Web | Administración → Reportes y Gerencia → Metas de ventas | III. Gerente | 2 | Administrador (completo) |  |
| `mod-kpis` | Reporte de KPIs | Web | Menú superior → KPIs | III. Gerente | 2 | Administrador (completo) |  |
| `mod-empleados` | Empleados | Web | Administración → Personal → Empleados | IV. Supervisor | 2 | Administrador (completo) |  |
| `mod-licencias` | Solicitudes de licencia | Web | Administración → Personal → Solicitudes (licencias) | IV. Supervisor | 2 | Administrador (completo) |  |
| `mod-aprobar-jornadas` | Aprobar jornadas | Web | Administración → Personal → Aprobar jornadas | IV. Supervisor | 2 | Administrador (completo) |  |
| `mod-boletas` | Boletas de pago | Web | Administración → Personal → Boletas de pago | IV. Supervisor | 2 | Administrador (completo) |  |
| `mod-auditoria` | Auditoría | Web | Administración → Seguridad → Auditoría | V. Auditor Interno | 2 | Administrador (completo) |  |
| `mod-pedidos` | Pedidos (administración) | Web | Administración → Ventas y Pedidos → Pedidos | VI. Empleado | 2 | Administrador (completo), Gerente (consulta), Bodeguero (consulta), Facturador (consulta) |  |
| `mod-mi-portal` | Mi portal de empleado | Web | Menú superior → Mi portal | VI. Empleado | 2 | Vendedor (completo) |  |
| `mod-mis-jornadas` | Mis jornadas (web) | Web | Menú superior → Mis jornadas | VI. Empleado | 2 | Vendedor (completo) |  |
| `mod-mis-boletas` | Mis boletas de pago | Web | Menú superior → Mis boletas | VI. Empleado | 2 | Vendedor (completo) |  |
| `mod-chat` | Chat interno | Web | Menú superior → Chat | VI. Empleado | 2 | Administrador (completo), Gerente (completo), Supervisor (completo), Auditor Interno (completo), Cajero (completo), Bodeguero (completo), Facturador (completo), Crédito y Cobro (completo), Compras (completo), Soporte (completo), Vendedor (completo), Chofer (completo) |  |
| `mod-liquidacion` | Liquidación de cobros | Web | Menú superior → Liquidación de cobros | VII. Cajero | 2 | Administrador (completo), Gerente (completo) |  |
| `mod-movimientos` | Movimientos de inventario | Web | Administración → Inventario → Movimientos | VIII. Bodeguero | 2 | Administrador (completo), Empleado (completo), Gerente (consulta), Compras (consulta) |  |
| `mod-consultas` | Consultas de clientes | Web | Administración → Clientes → Consultas | XII. Soporte | 2 | Empleado (completo), Administrador (completo) |  |
| `mod-venta-movil` | Venta móvil (web) | Web | Menú superior → Venta móvil | XIII. Vendedor | 3 | Empleado (completo), Administrador (completo) |  |
| `mod-mi-meta` | Mi meta | Web | Menú superior → Mi meta | XIII. Vendedor | 3 | — |  |
| `mod-asistente` | Asistente virtual | Web | Menú superior → Asistente | XIII. Vendedor | 3 | Administrador (completo), Gerente (completo), Empleado (completo), Chofer (completo), Bodeguero (completo) |  |
| `mod-mis-entregas` | Mis entregas (web) | Web | Menú superior → Mis entregas | XIV. Chofer | 3 | Administrador (consulta) |  |
| `mod-kilometraje` | Kilometraje (web) | Web | Menú superior → Kilometraje (también en Administración → Logística y Flota) | XIV. Chofer | 3 | Administrador (completo) |  |
| `mod-registro` | Crear una cuenta de cliente | Web | Menú superior → Registro | XV. Cliente | 3 | — |  |
| `mod-tienda` | Tienda en línea | Web | Menú superior → Tienda | XV. Cliente | 3 | — |  |
| `mod-carrito` | Carrito y pago | Web | Menú superior → Carrito | XV. Cliente | 3 | — |  |
| `mod-mis-pedidos` | Mis pedidos | Web | Mi cuenta → Mis pedidos | XV. Cliente | 3 | — |  |
| `mod-mis-garantias` | Mis garantías | Web | Mi cuenta → Mis garantías | XV. Cliente | 3 | — |  |
| `mod-estado-cuenta` | Estado de cuenta | Web | Mi cuenta → Estado de cuenta | XV. Cliente | 3 | — |  |
| `app-mis-rutas` | Mis rutas | App | Inicio → Operación en ruta → Mis rutas | XVI. Aplicación móvil | 3 | Chofer (completo) |  |
| `app-kilometraje` | Kilometraje | App | Inicio → Operación en ruta → Kilometraje | XVI. Aplicación móvil | 3 | Chofer (completo) |  |
| `app-por-enviar` | Por enviar (trabajo sin señal) | App | Inicio → Operación en ruta → Por enviar | XVI. Aplicación móvil | 3 | Chofer (completo) |  |
| `app-preparar` | Pedidos por preparar | App | Inicio → Bodega → Pedidos por preparar | XVI. Aplicación móvil | 3 | Bodeguero (completo), Administrador (completo) |  |
| `app-recepcion` | Recepción de compras | App | Inicio → Bodega → Recepción de compras | XVI. Aplicación móvil | 3 | Bodeguero (completo), Administrador (completo) |  |
| `app-movimientos` | Movimientos de inventario | App | Inicio → Bodega → Movimientos de inventario | XVI. Aplicación móvil | 3 | Bodeguero (completo), Empleado (completo), Administrador (completo) |  |
| `app-inventario` | Consulta de inventario | App | Inicio → Bodega → Consulta de inventario | XVI. Aplicación móvil | 3 | Bodeguero (consulta), Gerente (consulta), Empleado (consulta), Compras (consulta), Administrador (consulta) |  |
| `app-metricas` | Métricas del negocio | App | Inicio → Gestión → Métricas del negocio | XVI. Aplicación móvil | 3 | Gerente (consulta), Administrador (consulta) |  |
| `app-retenidos` | Pedidos retenidos | App | Inicio → Gestión → Pedidos retenidos | XVI. Aplicación móvil | 3 | Gerente (completo), Administrador (completo) |  |
| `app-rutas` | Rutas y choferes | App | Inicio → Gestión → Rutas y choferes | XVI. Aplicación móvil | 3 | Gerente (completo), Administrador (completo) |  |
| `app-pedidos` | Pedidos | App | Inicio → Gestión → Pedidos | XVI. Aplicación móvil | 3 | Empleado (completo), Gerente (consulta), Facturador (consulta), Administrador (completo) |  |
| `app-productos` | Productos | App | Inicio → Gestión → Productos | XVI. Aplicación móvil | 3 | Administrador (completo) |  |
| `app-ventas` | Ventas en campo | App | Inicio → Ventas → Ventas en campo | XVI. Aplicación móvil | 3 | Vendedor (completo), Empleado (completo), Administrador (completo) |  |
| `app-liquidacion` | Liquidación de rutas | App | Inicio → Oficina → Liquidación de rutas | XVI. Aplicación móvil | 3 | Cajero (completo), Gerente (completo), Administrador (completo) |  |
| `app-facturas` | Facturas | App | Inicio → Oficina → Facturas | XVI. Aplicación móvil | 3 | Facturador (consulta), Empleado (consulta), Administrador (consulta) |  |
| `app-credito` | Crédito de clientes | App | Inicio → Oficina → Crédito de clientes | XVI. Aplicación móvil | 3 | Crédito y Cobro (consulta), Administrador (consulta) |  |
| `app-compras` | Compras | App | Inicio → Oficina → Compras | XVI. Aplicación móvil | 3 | Compras (consulta), Administrador (consulta) |  |
| `app-consultas` | Consultas de clientes | App | Inicio → Oficina → Consultas de clientes | XVI. Aplicación móvil | 3 | Soporte (completo), Empleado (completo), Administrador (completo) |  |
| `app-bitacora` | Bitácora | App | Inicio → Oficina → Bitácora | XVI. Aplicación móvil | 3 | Auditor Interno (consulta), Administrador (consulta) |  |
| `app-aprobar-jornadas` | Aprobar jornadas | App | Inicio → Personal → Aprobar jornadas | XVI. Aplicación móvil | 3 | Supervisor (completo), Administrador (completo) |  |
| `app-mis-jornadas` | Mis jornadas | App | Inicio → Personal → Mis jornadas | XVI. Aplicación móvil | 3 | Gerente (completo), Supervisor (completo), Auditor Interno (completo), Empleado (completo), Cajero (completo), Bodeguero (completo), Facturador (completo), Crédito y Cobro (completo), Compras (completo), Soporte (completo), Vendedor (completo), Chofer (completo), Administrador (completo) |  |

Anclas de la Parte I que los capítulos pueden enlazar: `#gen-login` (Iniciar sesión), `#gen-recuperar` (Recuperar la contraseña), `#gen-navegacion` (Navegación y menú según el puesto), `#gen-perfil` (Mi perfil), `#gen-notificaciones` (Notificaciones), `#gen-cerrar-sesion` (Cerrar sesión), `#gen-app-instalar` (Instalar la aplicación móvil), `#gen-app-login` (Iniciar sesión en la aplicación móvil).

## 4. Hallazgos que afectan al manual (para el gerente)

Estado al 02/11/2026, verificado con el código de la rama `correcciones-esteban-manual` (`8f8ffcd`) y una base v2 con las migraciones 0251 a 0257 aplicadas en orden (`(localdb)\Trilogia_manualfinal2`). Los ocho hallazgos de la primera ronda están resueltos en esa rama. Los hallazgos de cada grupo, con su estado, están en `_hallazgos_grupo1.md`, `hallazgos_grupo2.md` y `_hallazgos_grupo3.md`.

| N.º | Hallazgo | Estado |
|---|---|---|
| 1 | Perfiles de oficina (7–14) sin permisos en la base demo | Resuelto por la migración 0251. **Producción debe tener aplicadas las migraciones 0251 a 0257**: el manual documenta esos permisos y las pantallas que cambian con ellas. |
| 2 | Módulos permitidos sin botón en el menú web | Resuelto: un solo panel «Administración» que muestra a cada puesto las áreas y opciones de su permiso. |
| 3 | Botones del menú que llevaban a «Acceso denegado» (Finanzas, Planilla, Histórico de precios) | Resuelto: el panel solo muestra lo que el puesto puede abrir. |
| 4 | Presupuestos y Gastos caían (503) con Windows en español | Resuelto (PR #131): las listas y los formularios de creación abren con cultura es-CR. |
| 5 | Mis jornadas (web): borrador o rechazada no se podían completar | Resuelto: botón «Completar». El capítulo de ejemplo ya lo documenta. |
| 6 | App: «Marcar leídas» casi invisible | Resuelto: ahora en color vino. |
| 7 | El logo se encimaba con «Inicio/Tienda» en pantallas medianas | Resuelto: la barra pasa a dos líneas. |
| 8 | Datos del PR #131 aún no en `main` | Resuelto: #131 está en `main` y en la base v2. Sondeos rehechos con 8f8ffcd. |

### Cambio de acceso con 8f8ffcd

- **Registrar movimiento** exige ahora `INVENTARIO_MOVIMIENTOS` (G2-40): solo lo abren Administrador, Empleado y Bodeguero. Gerente y Compras ven la lista de Movimientos, pero sin el botón, y por dirección reciben «Acceso denegado». El sondeo web lo prueba desde esta versión.
- El menú visible y el resto del acceso por perfil (web y app) no cambiaron respecto de 0e4fb19.

### Permisos reales por perfil (base v2 + 0251–0257, 02/11/2026)

Sustituyen a los conteos viejos que circulan en el equipo (95, 18, 12…) y a la tabla anterior «con matriz 0028». Es lo que documenta el manual. Las migraciones 0252 a 0257 no cambian ningún conteo.

| Perfil | Permisos |
|---|---|
| Administrador | 100 |
| Gerente | 28 |
| Empleado | 13 |
| Bodeguero | 9 |
| Supervisor | 8 |
| Chofer | 7 |
| Compras | 7 |
| Cajero | 5 |
| Facturador | 5 |
| Crédito y Cobro | 5 |
| Soporte | 5 |
| Auditor Interno | 5 |
| Vendedor | 5 |
| Cliente | 0 |
