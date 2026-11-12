# Cómo usar esta parte {#puesto-app}

La aplicación móvil para Android lleva al teléfono las tareas que se hacen fuera del escritorio: la ruta del chofer, la bodega, la aprobación de pedidos, las consultas rápidas de oficina y las jornadas del personal. Usa el mismo correo y la misma contraseña que el sitio web.

Para instalarla, iniciar sesión, conocer la pantalla de inicio, ver las notificaciones y cerrar sesión, vea la Parte I: [Instalar la aplicación móvil](#gen-app-instalar) e [Iniciar sesión en la aplicación](#gen-app-login).

## Cada puesto ve sus funciones {#puesto-app-perfiles}

La pantalla de inicio muestra solo las tarjetas del puesto de quien inició sesión, agrupadas en secciones. Por ejemplo, el chofer ve **Operación en ruta** y **Personal**:

![Pantalla de inicio de un chofer](capturas/app/app-inicio-chofer-01.png "ancho=6")

1. **Mis rutas**: sus rutas, el mapa y las paradas.
2. **Kilometraje**: la jornada del vehículo.
3. **Por enviar**: lo que marcó sin señal y falta enviar.
4. **Mis jornadas**: sus horas de trabajo.

Arriba, la tarjeta de resumen del día muestra cuántas entregas tiene **Pendientes**, **Entregadas** y **No entregadas**.

Esta tabla resume qué funciones ve cada puesto. El Administrador ve todas, salvo las de **Operación en ruta**, que son propias del chofer.

| Sección | Función | Puestos que la ven |
|---|---|---|
| Operación en ruta | [Mis rutas](#app-mis-rutas) | Chofer |
| Operación en ruta | [Kilometraje](#app-kilometraje) | Chofer |
| Operación en ruta | [Por enviar](#app-por-enviar) | Chofer (los demás llegan desde la franja de sincronización o **Mi cuenta**) |
| Bodega | [Pedidos por preparar](#app-preparar) | Bodeguero, Administrador |
| Bodega | [Recepción de compras](#app-recepcion) | Bodeguero, Administrador |
| Bodega | [Movimientos de inventario](#app-movimientos) | Bodeguero, Empleado, Administrador |
| Bodega | [Consulta de inventario](#app-inventario) | Bodeguero, Gerente, Empleado, Compras, Administrador |
| Gestión | [Métricas del negocio](#app-metricas) | Gerente, Administrador |
| Gestión | [Pedidos retenidos](#app-retenidos) | Gerente, Administrador |
| Gestión | [Rutas y choferes](#app-rutas) | Gerente, Administrador |
| Gestión | [Pedidos](#app-pedidos) | Empleado y Administrador (con cambio de estado); Gerente y Facturador (consulta) |
| Gestión | [Productos](#app-productos) | Administrador |
| Ventas | [Ventas en campo](#app-ventas) | Vendedor, Empleado, Administrador |
| Oficina | [Liquidación de rutas](#app-liquidacion) | Cajero, Gerente, Administrador |
| Oficina | [Facturas](#app-facturas) | Facturador, Empleado, Administrador |
| Oficina | [Crédito de clientes](#app-credito) | Crédito y Cobro, Administrador |
| Oficina | [Compras](#app-compras) | Compras, Administrador |
| Oficina | [Consultas de clientes](#app-consultas) | Soporte, Empleado, Administrador |
| Oficina | [Bitácora](#app-bitacora) | Auditor Interno, Administrador |
| Personal | [Aprobar jornadas](#app-aprobar-jornadas) | Supervisor, Administrador |
| Personal | [Mis jornadas](#app-mis-jornadas) | Todo el personal |

> [!NOTA] Las cuentas de **cliente** no usan la aplicación: al intentar entrar aparece «Su perfil no tiene acceso a la aplicación móvil. Puede usar el sitio web.».

## Trabajar con poca señal {#puesto-app-senal}

Varias funciones siguen andando sin Internet: lo que usted marca se guarda en el teléfono y se envía solo al volver la señal. Funcionan así **Mis rutas** (entregas), **Kilometraje**, **Movimientos de inventario**, **Recepción de compras**, **Pedidos por preparar** y **Ventas en campo**. Lo pendiente se ve en [Por enviar](#app-por-enviar).

Las demás (aprobar o rechazar, cambiar estados, despachar rutas, jornadas y consultas) necesitan señal en el momento.

> [!NOTA] Si abre la aplicación sin señal, ve la última información guardada en el teléfono: sus funciones, sus rutas y lo pendiente. La campana de avisos queda sin número hasta que vuelve la conexión.

## Mensajes comunes en toda la aplicación {#puesto-app-mensajes}

| Mensaje | Qué hacer |
|---|---|
| Sin conexión. Se guardó y se enviará al recuperar señal. | Nada: la acción está en **Por enviar** y sale sola. |
| No hay conexión con el servidor. Revise la señal e intente de nuevo. | Busque señal o wifi y toque **Reintentar**. |
| El servidor está tardando en responder. Intente de nuevo en unos segundos. | Espere unos segundos. La primera conexión del día puede tardar hasta un minuto. |
| La sesión expiró. Inicie sesión de nuevo. | Su sesión venció, o el administrador inactivó su usuario o le cambió la contraseña: la aplicación le pide entrar de nuevo. Antes, revise que **Por enviar** diga **Todo al día**. |
| Su perfil no tiene permiso para esta acción. / No tiene permiso para hacer esto. | Su puesto no puede hacer esa acción. Consulte a su jefatura. |
| Esto ya no está disponible. | Otra persona cambió ese dato. Deslice hacia abajo para actualizar la lista. |
| Demasiadas solicitudes seguidas. Espere un momento. | Espere un minuto y vuelva a intentar. |
| No se pudo cargar — Reintentar | Toque **Reintentar**. Si sigue, revise la señal. |

> [!CONSEJO] En casi todas las listas puede **deslizar hacia abajo** para actualizar.

> [!ADVERTENCIA] Los avisos de la aplicación (rutas asignadas, mensajes) llegan **solo dentro de la aplicación**: con la aplicación cerrada no suena ni aparece ningún aviso en el teléfono. Abra la aplicación o toque la campana para revisarlos. Los estados se muestran con nombres legibles, por ejemplo **En ruta**, **En proceso** o **Recibida parcial**.
