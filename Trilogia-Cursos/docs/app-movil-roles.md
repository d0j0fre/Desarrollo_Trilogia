# Aplicación móvil por rol

Desde la versión 1.2.0 (compilación 5) cada perfil del personal tiene su sección
en la aplicación. La pantalla de inicio se arma con lo que responde
`GET /api/mobile/v1/me/capabilities`: cada módulo se habilita **por permiso**, no
por nombre de rol. Si administración le da a un perfil un permiso nuevo desde el
módulo web **Permisos**, la tarjeta aparece sin publicar otra versión.

## Qué ve cada perfil

| Perfil | Módulos | Permisos que los habilitan |
|---|---|---|
| Chofer | Mis rutas, Kilometraje, Por enviar, Mis jornadas | rol Chofer, `FLOTA_KILOMETRAJE_PROPIO`, `RRHH_JORNADAS_REGISTRAR` |
| Bodeguero | Pedidos por preparar, Recepción de compras, Movimientos de inventario, Consulta de inventario, Mis jornadas | `PEDIDOS_PREPARAR`, `COMPRAS_ORDENES_RECIBIR`, `INVENTARIO_MOVIMIENTOS`, `INVENTARIO_VER` |
| Administrador | Todo lo de gestión, bodega, ventas, oficina y personal (no las rutas propias de chofer) | el perfil pasa todos los filtros |
| Gerente | Métricas, Pedidos retenidos, Rutas y choferes, Pedidos, Inventario, Liquidación de rutas, Mis jornadas | `REPORTES_DASHBOARD`, `PEDIDOS_AUTORIZAR_RECHAZAR`, `RUTAS_GESTIONAR`, `PEDIDOS_VER`, `INVENTARIO_VER`, `LIQUIDACION_FINANCIERA` |
| Vendedor | Ventas en campo, Mis jornadas | `VENTA_MOVIL_CREAR_PEDIDO` |
| Empleado | Ventas en campo, Pedidos, Inventario, Movimientos, Facturas, Consultas, Mis jornadas | los que ya tenía en la base |
| Supervisor | Aprobar jornadas, Mis jornadas | `RRHH_JORNADAS_APROBAR` |
| Cajero | Liquidación de rutas, Mis jornadas | `LIQUIDACION_FINANCIERA` |
| Facturador | Facturas, Pedidos, Mis jornadas | `FACTURACION_VER`, `PEDIDOS_VER` |
| Crédito y Cobro | Crédito de clientes, Mis jornadas | `CREDITOS_VER` |
| Compras | Compras (órdenes y sugerencias), Inventario, Mis jornadas | `COMPRAS_ORDENES_VER`, `COMPRAS_SUGERENCIAS_VER`, `INVENTARIO_VER` |
| Soporte | Consultas de clientes, Mis jornadas | `CONSULTAS_VER`, `CONSULTAS_ATENDER` |
| Auditor Interno | Bitácora, Mis jornadas | `AUDITORIA_VER` |
| Cliente | Sin acceso: el login responde "Tu perfil no tiene acceso a la aplicación móvil" | no tiene `MOVIL_ACCESO` |

Los permisos de oficina y de bodega los asignó la migración 0028. **También
habilitan el módulo web equivalente**: esos perfiles no tenían ningún permiso y
tampoco podían usar sus pantallas en el sitio web.

## Qué hace cada módulo

**Bodega**
- *Pedidos por preparar*: pedidos vigentes que no salieron en ruta. Las líneas son
  una lista de chequeo; "Marcar como preparado" se habilita con todo marcado.
  Se registra en `dbo.PedidoPreparacion` y **no cambia el estado del pedido**:
  un pedido facturado solo puede pasar a Entregado.
- *Recepción de compras*: órdenes pendientes o parciales; se recibe por línea
  (mismo `sp_Compras_RecibirDetalle` del sitio web, suma stock y deja auditoría).
- *Movimientos*: entrada, salida o ajuste con motivo, mostrando el stock
  resultante antes de guardar. Nunca deja stock negativo.
- *Consulta de inventario*: filtros de stock bajo, agotados e inactivos, y los
  últimos movimientos de cada producto.

Movimientos, recepciones, preparaciones y ventas **se guardan en el teléfono y
salen por la cola** cuando hay señal. La API las acepta repetidas sin duplicar.

**Gestión**
- *Métricas*: ventas facturadas, facturas, ticket promedio, cobros pendientes,
  rutas en calle, entregas, retenidos, pedidos abiertos por estado, más vendidos
  y existencias en riesgo; rangos hoy, 7 días, mes y 30 días.
- *Rutas y choferes*: seguimiento de rutas, despacho de las planificadas (con aviso
  por correo al cliente, igual que la web) y **reasignación de chofer o vehículo**
  con motivo obligatorio, en rutas planificadas o despachadas.
- *Pedidos*: búsqueda y cambio de estado con las mismas transiciones que el sitio
  web. Un pedido facturado solo ofrece "Entregado".
- *Pedidos retenidos*: aprobar (descuenta inventario y factura) o rechazar con motivo.
- *Productos*: activar o inactivar del catálogo.

**Ventas**: pedido en tres pasos (cliente, productos, entrega) con
`sp_Seller_CreateOrder`; el umbral de retención y la facturación automática son
los del sitio web. Sin señal se guarda con canal `Venta móvil offline`.

**Oficina** (solo lectura, salvo atender consultas): liquidaciones de ruta,
facturas con detalle, crédito de clientes con movimientos, órdenes y
sugerencias de compra, consultas de clientes y bitácora (con filtro "Desde la
aplicación").

**Personal**: registrar la jornada propia y aprobar o rechazar las del personal
(nadie resuelve la suya).

Gestión, oficina y jornadas funcionan **en línea**: son decisiones que dependen
del estado actual del servidor.

## Verificación hecha

- Migración 0028: ensayo en seco contra Azure DEV dentro de una transacción
  revertida, luego aplicada (ledger con SHA-256) y verificada con
  `0028_mobile_roles_surface.verify.sql`.
- API: 312 pruebas automáticas, y batería de integración por perfil con usuarios
  reales de Azure DEV (lecturas, límites de permiso y escrituras reversibles que
  se revirtieron).
- Aplicación: 47 pruebas y revisión visual de todas las pantallas en emulador
  como Administrador y como Bodeguero, con un servidor simulado.

## Prueba pendiente en teléfono real

Entrar con una cuenta de cada perfil y recorrer su sección. Como mínimo:
Bodeguero (preparar un pedido, un movimiento de entrada y su reversión),
Gerente (métricas y reasignar una ruta ida y vuelta), Vendedor (un pedido de
prueba) y Supervisor (aprobar una jornada).
