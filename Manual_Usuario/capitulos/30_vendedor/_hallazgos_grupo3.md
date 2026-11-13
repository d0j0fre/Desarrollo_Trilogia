# Hallazgos del grupo 3 (Vendedor, Chofer, Cliente y aplicación móvil)

Encontrados al documentar las Partes XIII–XVI, con la base `(localdb)\Trilogia_manualoper` (base de sesión + `fix_perfiles_oficina.sql` + `datos_demo_manual.sql`; migraciones 0200–0250 ya incluidas) y el código del worktree `correcciones-esteban-carrito-b54a54` (`30f92b6`). **No se corrigió código.** El manual documenta el comportamiento correcto previsto y, donde hoy falla, lo advierte en un cuadro ADVERTENCIA.

Datos de ejemplo creados por el grupo 3 (reproducibles): `capitulos/30_vendedor/_datos_grupo3_operacion.sql`, `capitulos/32_cliente/_datos_grupo3_clientes.sql`, `capitulos/40_app/_datos_grupo3_app.sql`.

## Estado al 02/11/2026 (rama `correcciones-esteban-manual`, c3da2e1)

Comprobado con la base v2 + 0251 (`(localdb)Trilogia_manualoper2`) y la app de esa rama en el emulador:

- **G3-01 corregido:** **Mi cuenta → Mis garantías** abre **Mis solicitudes de garantía**. Se quitó la advertencia y la captura `cli-garantias-05`; `cli-garantias-04` se tomó desde el menú.
- **G3-02 corregido:** el Vendedor abre `/SellerOrders/Detail/{id}`, de solo lectura. Se quitó la advertencia y se agregó la captura `ven-venta-13`.
- **G3-18 corregido:** la app abierta sin señal muestra el inicio con sus tarjetas. Se quitaron las dos advertencias.
- Menú: las capturas `ven-menu-01`, `ven-venta-01`, `ven-meta-01`, `cho-menu-01`, `cli-registro-01`, `cli-tienda-01` y `cli-pedidos-01` se rehicieron con la barra en dos líneas. El Chofer ahora tiene el botón **Administración** (Logística y Flota → Kilometraje).
- Los demás siguen abiertos con su advertencia o nota.

## Estado con 8f8ffcd (misma rama)

Comprobado con la base `(localdb)Trilogia_manualoper3` = v2 + 0251–0257 + datos. Se rehicieron todas las capturas del Cliente y `ven-venta-04`, `ven-venta-13` y `ven-asis-01` a `ven-asis-03`.

- **G3-05:** el asistente solo sugiere preguntas de las categorías del puesto. Se cambió la nota.
- **G3-06:** la sugerencia dice «…productos de este pedido…». Se actualizó la cita.
- **G3-10:** la garantía repetida muestra «Ya existe una solicitud abierta para este producto.». Nueva captura `cli-garantias-05` y tabla de errores.
- **G3-11:** tienda y carrito usan la imagen de la categoría o un ícono «Sin imagen».
- **G3-12, G3-13, G3-20:** detalle, portal y estados de garantía legibles; se corrigieron las citas («No hay crédito asignado actualmente.», «Sí, cancelar», «En proceso», «En ruta»). Queda un resto menor: el detalle del pedido del cliente dice «Lineas del pedido» sin tilde.
- **G3-14:** después de iniciar sesión desde el pago, el cliente vuelve a **Finalizar compra**. Se cambió la tabla de errores del pago.
- **G3-15:** tipo de cliente, factura electrónica y teléfonos se guardan con el pedido (se ven en el detalle de administración). Se agregó una línea en el paso 4 del pago.
- **G3-19:** el detalle muestra «Pago contra entrega»; los pedidos nuevos guardan «Efectivo contra entrega». Se quitó la nota.
- **Siguen abiertos:** G3-03 (sondeo), G3-04, G3-08 y G3-09 (Gerald), G3-07 y G3-17 (textos de todo el equipo) y G3-16 (no es defecto).

## Para decidir (afectan al texto del manual)

| # | Pantalla | Pasos | Qué pasa | Qué debería pasar | En el manual |
|---|---|---|---|---|---|
| G3-18 — **CORREGIDO** (c3da2e1) | App → pantalla de inicio **sin señal** | Con una sesión abierta, poner el teléfono en modo avión, cerrar la aplicación (o que Android la cierre) y volver a abrirla. | La pantalla de inicio queda en gris (error de dibujo de Flutter) y no aparecen las tarjetas, aunque lo pendiente sigue guardado. Causa: `_NotificationsBell.build` (`lib/features/home/presentation/home_page.dart:121`) lee `.value` de un `AsyncError` de la bandeja de avisos (`ApiException sinConexion`) y lanza una excepción al dibujar. Se recupera al abrir la app con señal. | La campana debe usar `valueOrNull` / `when(error: …)` y mostrarse sin número. El trabajo sin señal es la razón de ser de la app para el chofer: **prioridad alta**. | ADVERTENCIA en Por enviar y en la introducción de la Parte XVI. Las capturas sin señal se tomaron sin reiniciar la app. |
| G3-01 — **CORREGIDO** (c3da2e1) | Cliente → menú **Mi cuenta → Mis garantías** | Iniciar sesión como `cliente`, abrir **Mi cuenta → Mis garantías**. | Vuelve a **Mis pedidos** con «No fue posible cargar el producto solicitado.». El enlace apunta a `ClientPortal/Warranty` (formulario de solicitud) sin producto. La lista real (`/ClientPortal/Warranties`) no tiene ningún enlace. | El enlace debe abrir `/ClientPortal/Warranties` (**Mis solicitudes de garantía**). Cambio de una línea en `_Layout.cshtml`. | ADVERTENCIA con captura `cli-garantias-05` y el camino alterno (dirección escrita). |
| G3-02 — **CORREGIDO** (c3da2e1) | Vendedor → **Venta móvil → Ver detalle completo** y ojo de **Mis pedidos** | Registrar un pedido como `vendedor` y hacer clic en **Ver detalle completo**. | «Acceso denegado» (403): `/OrdersAdmin/Detail` exige permisos de administración de pedidos que el Vendedor no tiene. | Ocultar el botón al Vendedor o darle un detalle de solo lectura de sus propios pedidos. | ADVERTENCIA en Venta móvil. |
| G3-03 | Sondeo del mapa (`mapa_puestos_modulos.md`) | — | Dice «App móvil: sin acceso (429)» para Vendedor, Chofer y Cliente. Es el límite de 10 inicios de sesión por minuto del sondeo, no un bloqueo. | Vendedor: **Ventas en campo** y **Mis jornadas**. Chofer: **Mis rutas**, **Kilometraje**, **Por enviar** y **Mis jornadas** (comprobado en el emulador). Cliente: 403 «Tu perfil no tiene acceso a la aplicación móvil…». Volver a correr `sondeo_movil.js` con pausas. | La Parte XVI indica los perfiles reales. |
| G3-04 | App → **Mis rutas** (Chofer) | Abrir una parada. | La app solo permite **Entregado** y **No entregado**: no hay botón **En ruta** ni foto/firma de evidencia (existen en la web). | Decidir si la evidencia se agrega a la app; mientras tanto la web es el único camino. | La Parte XIV explica la evidencia en la web y la XVI lo aclara en una NOTA. |

## Defectos menores

| # | Pantalla | Qué pasa | Qué debería pasar |
|---|---|---|---|
| G3-05 — **CORREGIDO** (8f8ffcd) | Asistente (Vendedor, Empleado, Chofer, Bodeguero) | La pregunta sugerida inicial **¿Cuánto vendimos este mes?** se ofrece a todos, pero a estos puestos les responde «Información restringida» (Finanzas). | Mostrar solo preguntas sugeridas de las categorías permitidas al rol. |
| G3-06 — **CORREGIDO** (8f8ffcd) | Venta móvil → sugerencia de complementario | El motivo dice «Se compró junto con productos del carrito en N pedidos.» aunque en Venta móvil no hay carrito (texto compartido con la tienda, `CrossSellPolicy`). | «…junto con productos de este pedido…» en Venta móvil. |
| G3-07 | Mi meta / Confirmación de Venta móvil | Muestran códigos internos al usuario: «Meta de ventas — Noviembre 2026 (CU-212).» y «Factura generada (CU-091)». El menú dice **Mi meta** y la página **Mi progreso**. | Quitar los códigos de caso de uso y unificar el nombre. |
| G3-08 | Kilometraje (web) | Cualquier rechazo del procedimiento se muestra como «No fue posible completar la operación solicitada.», aunque el procedimiento da el motivo exacto (p. ej. «El kilometraje inicial (48000 km) no puede ser menor al odómetro actual del vehículo (48215 km).» o «El vehículo tiene una jornada de kilometraje sin cerrar.»). | Mostrar el mensaje del procedimiento (THROW ≥ 50000). |
| G3-09 | Mis entregas (web) | Igual que G3-08: «No fue posible aplicar el estado solicitado.» oculta «La entrega ya tiene un estado final.» o «Solo se pueden actualizar entregas de una ruta despachada.». | Mostrar el mensaje del procedimiento. |
| G3-10 — **CORREGIDO** (8f8ffcd) | Cliente → solicitud de garantía | Los mensajes del procedimiento (p. ej. «Ya existe una solicitud abierta para este producto.») se reemplazan por «No fue posible registrar la solicitud de garantía en este momento.». | Mostrar el mensaje del procedimiento. |
| G3-11 — **CORREGIDO** (8f8ffcd) | Cliente → Tienda y Carrito | Varios productos (cervezas, vinos) muestran la foto genérica de un whisky en la tienda; en el carrito la imagen sale rota (se ve el texto alternativo «Producto …»). | Usar la imagen real del producto o un ícono neutro; revisar la ruta de imagen del carrito. |
| G3-12 — **CORREGIDO** (8f8ffcd) | Cliente → detalle del pedido | El botón **Volver a Mis pedidos** del encabezado casi no se ve (texto oscuro sobre fondo oscuro) y la etiqueta **PENDIENTE** queda pegada debajo de **Cancelar pedido**. | Contraste suficiente; separar la etiqueta. |
| G3-13 — **CORREGIDO** (8f8ffcd) | Cliente → portal | Textos sin tildes: «Credito», «Telefono», «Direccion», «informacion», «Confirmar cancelacion», «Si, cancelar». Estados crudos: «EnProceso», «EnRuta». | Tildes y estados legibles («En proceso», «En ruta»). |
| G3-14 — **CORREGIDO** (8f8ffcd) | Cliente → Iniciar sesión desde el pago | «Debes iniciar sesión para finalizar la compra.» lleva al inicio de sesión, pero al entrar va a **Inicio** (se ignora `returnUrl`); hay que volver a abrir **Carrito**. El carrito se conserva. | Volver al pago después de iniciar sesión. |
| G3-15 — **CORREGIDO** (8f8ffcd) | Cliente → Pago | Tipo de cliente, factura electrónica, teléfonos y país no se guardan en el pedido (solo van en el correo). | Guardarlos si se usan para facturar. |
| G3-16 | Venta móvil (entorno local) | Sin servidor de correo, la confirmación muestra en rojo «El pedido se registró y facturó, pero no fue posible enviar el comprobante por correo.». No es un defecto: en las capturas se oculta y el manual explica el mensaje. | — |
| G3-19 — **CORREGIDO** (8f8ffcd) | Venta móvil → detalle del pedido (Vendedor) | Los pedidos de pago contra entrega muestran **Método de pago: Crédito vendedor** (valor histórico que guarda `sp_Seller_CreateOrder`). Se confunde con un pedido a crédito. | Mostrar «Pago contra entrega». El manual lo aclara en una NOTA. |
| G3-20 — **CORREGIDO** (8f8ffcd) | Cliente → Mis solicitudes de garantía | Las etiquetas de estado (**En revisión**, **Aprobada**) tienen texto oscuro sobre fondo de color y casi no se leen. | Texto blanco o colores suaves con texto oscuro, como en el resto del portal. |
| G3-17 | Textos de pantalla | Mezcla de voseo y tuteo («tenés», «Consultá», «Revisá» / «Escribe», «Inicia sesión»). El manual copia los textos tal cual. | Unificar el trato. |

## Comprobado y correcto

- Pedido a crédito en Venta móvil: rechaza «El cliente no tiene crédito activo…» y factura bien con crédito activo.
- Umbral de ₡50 000: el pedido queda **Retenido** y aparece en **Pedidos retenidos** de la app del Gerente.
- Modo sin conexión de Venta móvil y de Mis entregas: guarda, sincroniza y no duplica.
- Estado de cuenta: saldo, vencido (FIFO) y PDF coinciden; aparece «Descarga completada…».
- Promoción por segmento: el mayorista recibe el descuento exclusivo y el minorista no; la regalía «Todos» llega a ambos.

## Estado al 11/11/2026 (rama `correcciones-finales`, db392a2 + 0305) — manual 1.2

Todos corregidos en el sistema y reflejados en el manual:

- **G3-04 — CORREGIDO y reflejado:** al marcar **Entregado**, la app del chofer ofrece foto y firma (opcionales, con y sin señal; sin señal quedan en **Por enviar**). Las evidencias se ven en la web, en **Rutas → Evidencias**. Capítulos 31 «Mis entregas», 40 «Mis rutas» y «Por enviar». El botón **En ruta** de la app se quitó (rama `correcciones-fin-evidencia-2`): despachar la ruta ya deja todas las paradas en **En ruta**, y una parada **Pendiente** solo existe en una ruta **Planificada**, donde el servidor rechaza cualquier cambio.
- **G3-07 — CORREGIDO y reflejado:** sin «(CU-212)» ni «(CU-091)»; el menú y la pantalla se llaman **Mi meta**.
- **G3-08 — CORREGIDO y reflejado:** Kilometraje muestra el motivo exacto del rechazo. Además, cada chofer ve y cierra solo sus jornadas (0312).
- **G3-09 — CORREGIDO y reflejado:** Mis entregas muestra el motivo exacto («La entrega ya tiene un estado final.»…).
- **G3-17 — CORREGIDO y reflejado:** trato de usted en la web y la app; estados legibles («En ruta», «En proceso», «Recibida parcial»). Restos: «Registrate» en Login y Registro (corregido a «Regístrese» en `correcciones-fin-evidencia-2`) y el diálogo del botón **En ruta** de la app (el botón se quitó en esa misma rama).
- **G3-03:** sigue abierto (es del sondeo, no del sistema).

Capturas móviles (sesión evidencia-2): rehechas gen-app-login-01, gen-app-inicio-01, gen-app-notif-01, gen-app-cuenta-01 y app-inicio-chofer-01. PENDIENTES por falla del emulador (se caía a los 1-2 minutos de arrancar la app): hoja **Confirmar entrega** con evidencia (app-mis-rutas-08), **Firma del cliente** (app-mis-rutas-09) y **Por enviar** con foto y firma (app-por-enviar-05). Están especificadas en `capturas/specs/app_evidencia.json` (ruta demo RutaId 4, RUT-20261111170200; cambiar código y RutaId según la base). El botón **En ruta** ya no existe: no hay captura. Las capturas móviles son las de la versión 1.1; las de inicio y **Confirmar entrega** muestran textos anteriores.
