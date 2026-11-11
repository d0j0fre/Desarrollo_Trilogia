# Migraciones de base de datos

## Responsabilidad

Danny, Esteban, Gerald y David forman el equipo administrativo DEV mediante usuarios individuales con `db_owner`. Danny permanece como administrador Microsoft Entra individual del servidor por una limitacion del tenant.

Cada migracion tiene un unico ejecutor designado y registrado en el PR. Los demas administradores no ejecutan simultaneamente el mismo script.

## Flujo obligatorio

1. Crear un script incremental, idempotente y revisable dentro de `database/migrations/`.
2. Abrir un pull request y obtener revision antes de ejecutar.
3. Validar el script con Microsoft ScriptDom.
4. Crear y verificar un BACPAC antes de cualquier cambio compartido.
5. Aplicar el script durante una ventana controlada por el ejecutor designado.
6. Registrar SHA-256, estado, fecha UTC, ejecutor, ambiente y notas en `dbo.SchemaMigrationHistory`.
7. Ejecutar consultas de verificacion y documentar el rollback.

## Rollback

Cada migracion debe indicar su estrategia de rollback antes de ejecutarse. Los cambios transaccionales deben usar `XACT_ABORT` y revertirse ante una validacion fallida. Restaurar un BACPAC es el ultimo recurso y requiere aprobacion expresa.

## Prohibiciones en DEV compartido

- No ejecutar `00_todo_en_uno.sql` ni `DistribuidoraJJ_DB.sql` sobre una base existente.
- No ejecutar scripts de `database_Esteban/`.
- No ejecutar seeds ni datos demo.
- No ejecutar CU090-CU100 sin evidencia, revision y bloque separado.
- No aplicar cambios manuales sin script, PR, backup y registro en el ledger.
- No compartir la cuenta administradora SQL.
- No ejecutar una migracion si otro administrador ya figura como ejecutor activo.

## Orden vigente

| Orden | Archivo | Propósito | Dependencia |
|---:|---|---|---|
| 0001 | `0001_create_schema_migration_history.sql` | Ledger de migraciones | Esquema base |
| 0002 | `0002_chat_private_security.sql` | Conversaciones privadas y pertenencia | Usuarios y perfiles |
| 0003 | `0003_chat_departments_and_search.sql` | Departamentos, miembros, búsqueda y auditoría | 0002 |
| 0004 | `0004_private_delivery_evidence.sql` | Evidencia privada y descarga autorizada | Rutas/entregas CU-081 a CU-083 |
| 0005 | `0005_atomic_checkout_promotions.sql` | Pedido, inventario y promociones atómicos | Checkout CU-097 y promociones CU-171 a CU-174 |
| 0006 | `0006_warranty_workflow.sql` | Garantías sin duplicados y resolución auditada | Pedidos, garantías y auditoría |
| 0007 | `0007_secure_document_management.sql` | Documentos privados, versiones y catálogos operativos | Usuarios y permisos |
| 0008 | `0008_document_expiration_alerts.sql` | Alertas documentales idempotentes | 0007 |
| 0009 | `0009_annual_department_budgets.sql` | Presupuestos anuales normalizados | 0007 (departamentos) |
| 0010 | `0010_operating_expenses_alignment.sql` | Gastos operativos, comprobantes privados y estados | 0009; esquema CU-222 legado opcional |
| 0011 | `0011_budget_actual_comparison.sql` | Comparación presupuesto versus real | 0009 y 0010 |
| 0012 | `0012_inventory_combos_transformations_intelligence.sql` | Combos vendibles, snapshots de pedido/factura, checkout idempotente, transformación atómica, inteligencia y permisos específicos | Esquema de productos/pedidos/usuarios/facturas/permisos; 0005 para checkout/promociones; dependencias Sprint 4 verificadas cuando correspondan |
| 0013 | `0013_purchasing_suppliers_orders.sql` | Proveedores, órdenes, recepción parcial/total, discrepancias, sugerencias e histórico de precios | 0012 y esquema base de productos/inventario/permisos |
| 0014 | `0014_delivery_board_permission.sql` | Permiso exacto del tablero agregado de entregas | Perfiles y permisos |
| 0015 | `0015_sales_and_seller_reports.sql` | Reportes de ventas y desempeño de vendedores | Facturas, pedidos, productos, usuarios, perfiles y permisos |
| 0016 | `0016_cross_sell_recommendations.sql` | Recomendaciones explicables por co-compra con fallback | Pedidos, detalle de pedido y productos |
| 0017 | `0017_rrhh_employee_records.sql` | Expediente laboral, concurrencia, historial y auditoría | Empleados, usuarios, perfiles y permisos |
| 0018 | `0018_rrhh_attendance.sql` | Jornadas propias, aprobación segregada e idempotencia | 0017 |
| 0019 | `0019_payroll_engine.sql` | Planilla configurable, reproducible y auditada | 0018 |
| 0020 | `0020_private_pay_slips.sql` | Boletas privadas y notificación idempotente | 0019 |
| 0021 | `0021_seller_goal_progress.sql` | Progreso mensual de la meta propia del vendedor | Metas, facturas, pedidos y usuarios |
| 0022 | `0022_password_hash_transition.sql` | Transición compatible de credenciales directas a PBKDF2 con actualización gradual | Usuarios, perfiles y API de autenticación |
| 0023 | `0023_toggle_product_status_returns_state.sql` | `sp_Admin_ToggleProductStatus` devuelve el estado resultante para que la auditoría distinga activar de inactivar | Tabla `dbo.Productos` con columna `Activo` |
| 0024–0027 | `0024_mobile_auth_jwt.sql` a `0027_mobile_app_selfhosted_package.sql` | Aplicación móvil: JWT, superficie del chofer, distribución y paquete propio | 0022 |
| 0028 | `0028_mobile_roles_surface.sql` | Aplicación móvil por rol: permisos mínimos por perfil, `PEDIDOS_PREPARAR`, idempotencia (`MovilOperaciones`), preparación de pedidos y `sp_Movil_*`. Ver `docs/app-movil-roles.md` | 0025 |
| 0200 | `0200_order_header_contract.sql` | `sp_Admin_GetOrderHeader` vuelve al contrato de cu097/cu098 (Observaciones antes que Total, columnas de pago y `MotivoRechazo`); el baseline `sp_admin_modulo.sql` lo dejaba con la versión antigua y el detalle de pedido fallaba (BUG-09-03, BUG-09-05, BUG-14-03) | Columnas de pago y retención de `dbo.Pedidos` (cu097/cu098) |
| 0201 | `0201_invoice_client_phone.sql` | `sp_Admin_GetInvoiceClientPhone`: teléfono del cliente de una factura para ofrecer el envío del comprobante por WhatsApp (BUG-09-01). La aplicación funciona sin ella | `dbo.Facturas`, `dbo.Pedidos`, `dbo.Usuarios.Telefono` |
| 0210 | `0210_product_save_procedures.sql` | BUG-05-01: `sp_Admin_CreateProduct` y `sp_Admin_UpdateProduct` aceptan `@StockMinimo`, `@ImagenUrl` y `@EsDestacado` (la creación llena `CategoriaId`); `GetProductById` y `GetProducts` devuelven esas columnas | Esquema base de productos y categorías |
| 0211 | `0211_product_expiration_date.sql` | BUG-05-02: `Productos.FechaVencimiento DATE NULL`; crear/editar/consultar productos la aceptan y devuelven. Aplicar antes de desplegar el código que envía `@FechaVencimiento` | 0210 |
| 0212 | `0212_product_search_by_sku.sql` | BUG-05-03: `sp_Admin_GetProducts` también filtra por código SKU (`SKU-` + `ProductoId` con cinco dígitos) | 0211 |
| 0213 | `0213_promotion_overlap_validation.sql` | BUG-17-01: `sp_Promociones_Create` y `sp_Promociones_Update` rechazan reglas traslapadas (mismo producto, activas, vigencias y segmentos que se cruzan) con el error 53102 | Promociones CU-171/174 |
| 0214 | `0214_promotion_unit_limit.sql` | BUG-17-02: `Promociones.LimiteUnidades`/`UnidadesConsumidas`; crear/editar/consultar promociones los manejan y el disparador `TR_PromocionAplicaciones_LimiteUnidades` suma las unidades aplicadas en cada pedido e inactiva la promoción al alcanzar el límite (sin modificar el checkout) | 0213; `PedidoDetalle.EsRegalo`/`PedidoComboId` (0012) |
| 0220 | `0220_pedido_estado_desde_entrega.sql` | BUG-10-06: `sp_Pedidos_SincronizarEstadoEntrega` copia al pedido el estado de su entrega (`EnRuta`, `Entregado`, `Fallido`); la API móvil lo llama al despachar y al marcar una entrega | Rutas y entregas (cu081/cu082) |
| 0221 | `0221_notificaciones_usuario.sql` | BUG-08-01 y BUG-10-08: tabla `NotificacionesUsuario`, disparador `TR_Rutas_NotificarChofer` (asignación y reasignación de ruta) y procedimientos de la bandeja de avisos | 0220 |
| 0222 | `0222_notificacion_mensaje_directo.sql` | BUG-23-05: disparador `TR_ChatMensajes_NotificarReceptor` deja un aviso al receptor de un mensaje directo | 0221; chat privado (0002) |
| 0230 | `0230_cliente_estado_deuda.sql` | BUG-06-02: `ClienteCreditos.PlazoCreditoDias` (30 días por defecto) y `ClienteCreditoMovimientos.PedidoId`; `fn_Cliente_EstadoDeuda` / `sp_Cliente_EstadoDeuda` (deuda actual y vencida, FIFO; reutilizable, p. ej. comodatos); `sp_Credito_CargarPedido`; Venta móvil acepta `@MetodoPago = N'Crédito'` (valida crédito activo, no bloqueado, sin pagos vencidos y disponible suficiente, y registra el cargo por el total facturado con IVA); `TR_Pedidos_AnularCargoCredito` reversa el cargo al cancelar o rechazar y `TR_Facturas_SincronizarCargoCredito` lo deja igual a la factura cuando un pedido retenido se factura después. Aplicar antes de desplegar el código que envía `@MetodoPago` y `@PlazoCreditoDias` | cu062 (créditos), cu098 (`sp_Seller_CreateOrder`) |
| 0231 | `0231_cliente_inactivar_con_saldo.sql` | BUG-06-01: `sp_Admin_UpdateClient` y `sp_Admin_ToggleClientStatus` rechazan con el error 51012 la inactivación de un cliente con deuda actual mayor que cero. La aplicación funciona sin ella (solo traduce el error) | 0230 |
| 0232 | `0232_cliente_estado_cuenta.sql` | BUG-06-03, BUG-12-01 y BUG-12-02: `sp_Cliente_EstadoCuenta` (solo lectura) devuelve el resumen (saldo de `fn_Cliente_EstadoDeuda`, el mismo de Créditos), las facturas del cliente y los cargos y pagos con saldo acumulado. Lo usan el detalle del cliente, el portal (Estado de cuenta y Mis facturas) y los PDF. Aplicar antes de desplegar ese código | 0230 |
| 0240 | `0240_comodato_historial_condicion.sql` | BUG-16-05: `sp_Comodato_HistorialPorActivo` devuelve al final la condición de entrega y de devolución | Comodatos (cu162/163/164) |
| 0241 | `0241_comodato_rentabilidad_ubicacion.sql` | BUG-16-06: `sp_Comodato_Rentabilidad` devuelve al final la ubicación del equipo (la fecha de entrega ya venía) | Comodatos (cu162/163/164) |
| 0242 | `0242_comodato_regla_30_dias.sql` | BUG-16-04: `fn_Comodato_Rentabilidad` aplica la regla de 30 días sin compras; `sp_Comodato_Rentabilidad` devuelve `DiasSinCompra`/`SugerirRetiro` y `sp_Comodato_GenerarAlertasRentabilidad` deja avisos de baja rentabilidad a quienes tienen `COMODATOS_CONSULTAR` | 0241; 0221 (`NotificacionesUsuario`) |
| 0243 | `0243_comodato_alerta_averia.sql` | BUG-16-03: `sp_Comodato_Devolver` deja un aviso `ComodatoAveria` a taller (`FLOTA_MANTENIMIENTO`/`ACTIVOS_GESTIONAR`) y cobros (perfil Crédito y Cobro o `CREDITOS_MOVIMIENTOS`) cuando el equipo vuelve con avería | 0221 (`NotificacionesUsuario`) |
| 0244 | `0244_comodato_contrato.sql` | BUG-16-01: `sp_Comodato_GetContrato` (solo lectura) con los datos del contrato de comodato que la MVC emite en PDF | Comodatos (cu162/163/164) |
| 0245 | `0245_comodato_bloqueo_morosidad.sql` | BUG-16-02: `sp_Comodato_ClientesParaAsignar` (clientes activos con su deuda vencida); `sp_Comodato_Asignar` exige cliente registrado (53087) y bloquea si `fn_Cliente_EstadoDeuda` indica deuda vencida (53086, con monto y días de atraso). La MVC lee la deuda con `sp_Cliente_EstadoDeuda` | **0230** (`fn_Cliente_EstadoDeuda`, rama de carrito/crédito; la migración la exige en el ledger) |
| 0250 | `0250_asistente_entregas_pendientes.sql` | BUG-26-04: `sp_Asistente_Metricas` cuenta como entregas pendientes las paradas `Pendiente`/`EnRuta` de las rutas `Despachada` (la columna "Pendientes" del listado de rutas); antes contaba solo `Pendiente` de cualquier ruta. Solo lectura | cu310; rutas y entregas (cu081/cu082) |
| 0251 | `0251_perfiles_oficina_permisos.sql` | Asigna a los perfiles de oficina (Supervisor, Cajero, Bodeguero, Facturador, Crédito y Cobro, Compras, Soporte, Auditor Interno), creados después de la 0028, la matriz de permisos que la 0028 preveía para ellos y `MOVIL_ACCESO` de la 0025 a Bodeguero; si la 0260 ya está aplicada, aplica también su regla de conservación de Finanzas a esos perfiles (Cajero recibe `FINANZAS_INGRESOS_EGRESOS_VER`), con el mismo resultado en cualquier orden. Solo inserta asignaciones que falten; no crea permisos ni toca otros perfiles | 0025 y 0028; perfiles de oficina (datos demo); 0260 si ya está aplicada |
| 0252 | `0252_reparar_texto_mal_codificado.sql` | G2-42: repara los pares de doble codificación UTF-8 («Ã±» → «ñ», «Ã©» → «é», «Â¿» → «¿») en nueve columnas de texto de negocio (productos, movimientos, detalle de factura, usuarios y pedidos) que trajo `00_todo_en_uno.sql`. Solo texto; idempotente | Página de códigos 1252 en la base (la migración lo exige) |
| 0253 | `0253_movimientos_inventario_filtros.sql` | G2-42: filtros opcionales (producto, tipo, fechas) en `sp_Admin_GetInventoryMovements`; sin parámetros devuelve lo mismo que antes. Solo lectura | `MovimientosInventario`; `sp_Admin_GetInventoryMovements` |
| 0254 | `0254_activo_marcar_disponible.sql` | G2-03: `sp_Activos_MarcarDisponible` pasa un activo de Mantenimiento a Disponible (equipo reparado). Aditiva | `Activos` (Comodatos, cu162–cu164) |
| 0255 | `0255_venta_movil_pago_contra_entrega.sql` | G2-39: `sp_Seller_CreateOrder` guarda «Efectivo contra entrega» (el nombre que ya usa la Tienda) en vez de «Crédito vendedor» para los pedidos contra entrega, así Liquidación de cobros los cuenta como efectivo. Copia la versión de la 0230 con solo ese cambio; no toca pedidos registrados | **0230** (la exige en el ledger) |
| 0256 | `0256_pedido_datos_cliente.sql` | G3-15: tabla `PedidoDatosCliente` y `sp_Pedido_GuardarDatosCliente` / `sp_Pedido_GetDatosCliente`: tipo de cliente, factura electrónica, correo del comprobante y teléfonos que el cliente indica al pagar en la Tienda. Aditiva; no toca `Pedidos` ni pedidos registrados | `Pedidos` |
| 0257 | `0257_promociones_vencidas_en_lista.sql` | G2-04: `sp_Promociones_List` muestra y filtra como «Vencida» la promoción activa cuya fecha de fin ya pasó (antes seguía «Activa», con botones de editar e inactivar). Solo lectura; no cambia datos | **0214** (la exige en el ledger) |
| 0260 | `0260_roles_finanzas_consulta.sql` | Impide inactivar roles con usuarios asignados y asigna permisos de Finanzas y consulta de comodatos | 0009 y 0010; tablas de usuarios, perfiles y permisos |
| 0261 | `0261_auditoria_planilla.sql` | Refleja cambios de estado y envíos de boletas en la auditoría general | 0019 y 0020; AuditoriaSistema |
| 0262 | `0262_empleado_identificacion.sql` | Identificación única para empleados, compatible con registros anteriores | Empleados |
| 0263 | `0263_vigencia_salarial.sql` | Historial salarial por fecha efectiva | 0017; historial de salarios |
| 0264 | `0264_gasto_sin_presupuesto.sql` | Detecta exceso cuando existe gasto sin presupuesto | 0010; vista de impacto de gastos |
| 0265 | `0265_estado_gasto_aprobacion.sql` | Estado persistente de aprobación y su inclusión en gastos pendientes | 0010, 0011 y 0264 |
| 0280 | `0280_rutas_cliente_inactivo.sql` | BUG-06-04: `sp_Rutas_GetAssignableOrders`, `sp_Rutas_AddOrder` y `sp_Rutas_Create` no ofrecen ni aceptan pedidos de un cliente inactivo (AddOrder lanza 52014 y Create 52006). Solo reemplaza procedimientos; no cambia datos | Rutas y entregas (cu081/cu302) |
| 0281 | `0281_rutas_prioridad_secuenciacion.sql` | BUG-10-01: columna `RutaPedidos.Prioridad` (1 Urgente, 2 Alta, 3 Normal) y secuenciación por prioridad y, dentro de cada nivel, por cercanía (`sp_Rutas_Secuenciar` y `sp_Rutas_Recalcular`). `sp_Rutas_Create` y `sp_Rutas_GuardarSecuencia` aceptan la prioridad; `sp_Rutas_GetOrders` la devuelve como última columna. Aditiva: las paradas existentes quedan en Normal | **0280** (la exige en el ledger) |
| 0282 | `0282_rutas_secuenciar_exige_ubicacion.sql` | BUG-25-01: `sp_Rutas_Secuenciar` rechaza con 54012 una ruta con paradas sin coordenada (dice cuántas faltan) y no cambia el orden; con todas las coordenadas ordena como la 0281. No cambia datos | **0281** (la exige en el ledger) |
| 0283 | `0283_tablero_entregas_pedidos.sql` | BUG-10-02: `sp_Entregas_PedidosTablero` devuelve los pedidos En ruta (rutas despachadas) y los pendientes de entrega (a domicilio, en ruta planificada o sin ruta) que lista el tablero de entregas. Solo lectura | **0281** (la exige en el ledger; lee `RutaPedidos.Prioridad`) |
| 0285 | `0285_liquidacion_cobros_correcciones.sql` | BUG-10-03/04/05, G2-37/38: `sp_LiquidacionCobros_Registrar` solo liquida rutas `Completada`, valida el detalle de comprobantes contra el total (53401/53402) y deja el faltante a cargo del chofer en `ChoferFaltantes`; `sp_LiquidacionCobros_Anular` (con motivo) corrige una liquidación y la restricción única pasa a índice filtrado de liquidaciones vigentes; `sp_LiquidacionCobros_GetRecibo` y `sp_ChoferFaltantes_List`. Aplicar antes de desplegar el código nuevo de Finanzas | cu106 (`RutaLiquidacionCobros`) |
| 0286 | `0286_liquidacion_mercaderia_ruta.sql` | BUG-10-07: `RutaLiquidacionMercaderia` y su detalle; `sp_RutaMercaderia_Preparar/Registrar/List`: compara la mercadería contada con la despachada en los pedidos no entregados de una ruta completada, bloquea el cierre si falta (53416), y al cuadrar suma al stock con movimientos `Entrada` y marca `Rutas.Liquidada` | cu105 (`sp_Rutas_Liquidar`), inventario |
| 0287 | `0287_mantenimiento_flota_correcciones.sql` | BUG-15-01, G2-18: `Vehiculos.InactivoPorMantenimiento`; una orden correctiva En proceso saca el vehículo de circulación y completarla lo devuelve; `sp_Mantenimiento_Create/MarcarCompletada` validan y guardan fecha y costo final; `sp_Mantenimiento_Iniciar`; `sp_Vehiculos_List/ToggleStatus/Update` (52094) | cu152/153/154 |
| 0288 | `0288_documentos_alertas_flota.sql` | G2-17, G2-20: `VehiculoDocumentos.DadoDeBaja`; `sp_VehiculoDocumento_List/GetById/Update/Baja`; `sp_Flota_Alertas` solo avisa del documento vigente (el más reciente por vehículo y tipo) y agrega alertas de mantenimiento en proceso, correctivos y kilometraje próximo (`@KmAviso`) | 0287 |
| 0289 | `0289_rutas_finalizar.sql` | BUG-10-07 / BUG-10-04: `sp_Rutas_Finalizar` cierra una ruta Despachada como Completada, deja las paradas sin entregar en Fallido y sincroniza los pedidos (0220) sin tocar el inventario ni liquidar; reemplaza al botón «Liquidar ruta» (`sp_Rutas_Liquidar` queda sin uso). Aplicar antes de desplegar el código | 0220, 0285, 0286 |
| 0290 | `0290_sugerencias_compra_promedio_real.sql` | BUG-24-02 / G1-13: `sp_Admin_GetPurchaseSuggestions` acepta `@IncluirTodos` (0 = igual que antes; 1 = todos los productos activos con el mismo cálculo de demanda) para que Sugerencias de compra e Inteligencia de inventario usen una sola fórmula. Solo lectura | **0012** (definición previa del procedimiento) |
| 0291 | `0291_gerente_inteligencia_inventario.sql` | BUG-24-03: asigna `INVENTARIO_INTELIGENCIA_VER` al perfil Gerente para que vea la alerta de productos estancados y la sugerencia de incluirlos en una promoción. Solo inserta la asignación si falta | 0012 (permiso) y perfiles |
| 0292 | `0292_tendencia_estacional_facturas.sql` | BUG-24-05: `sp_Admin_GetSeasonalSalesTrend` suma facturas no anuladas (importe con IVA, unidades de `FacturaDetalle`, mes por `FechaFactura`), la misma definición de venta que el Reporte de ventas; antes sumaba líneas de pedidos sin IVA. Solo lectura | **0012** (definición previa) y facturas |
| 0293 | `0293_tendencia_estacional_categoria.sql` | BUG-24-04: `sp_Admin_GetSeasonalSalesTrend` acepta `@Categoria` (NULL = todas); con categoría suma solo las líneas de factura de esa categoría, repartiendo el IVA de cada factura sobre sus líneas. Solo lectura | **0292** (la exige en el ledger) |
| 0295 | `0295_kpi_cumplimiento_solo_con_meta.sql` | BUG-21-03: la venta global del Reporte de KPIs solo suma vendedores con meta y devuelve aparte la venta sin meta | 0211–0213 (metas y KPIs) |
| 0296 | `0296_tablero_ventas_no_anuladas.sql` | G2-16: el Tablero gerencial y su serie suman facturas no anuladas (definición única de venta) | CU-131 (tablero gerencial) |
| 0297 | `0297_tablero_detalle_indicadores.sql` | BUG-13-02: `sp_Reportes_DashboardDetalle`, datos de los gráficos de cada indicador del tablero | 0296; rutas y crédito de clientes |
| 0298 | `0298_metas_reemplazo_confirmado.sql` | BUG-21-01: `sp_Metas_Upsert` exige `@Reemplazar` para sustituir una meta existente (error 53134) | 0211 (metas) |
| 0299 | `0299_chat_marcar_leidos.sql` | BUG-23-01: `sp_Chat_MarcarLeidos` para el doble check de los mensajes directos | 0002 (chat privado) |
| 0310 | `0310_credenciales_solo_hash.sql` | Seguridad: ninguna contraseña en texto plano es válida ni queda guardada (hash de transición con sal para los datos existentes, disparador `tr_Usuarios_SinTextoPlano`, login sin comparación de texto plano, clientes con hash PBKDF2) | 0022 (columnas de hash) |
| 0311 | `0311_revocacion_sesiones.sql` | Seguridad: `SecurityStamp` por usuario; el disparador `tr_Usuarios_RevocarSesiones` lo renueva y revoca los tokens de refresco al inactivar, cambiar de perfil o de contraseña; `sp_Auth_GetSessionState` para la web y los JWT | 0310 y 0024 móvil (`UsuarioTokensRefresco`) |

Los scripts no incluyen `USE`: el ejecutor debe seleccionar explícitamente la base antes de iniciar. Los hashes escritos por 0002–0011 son hashes de manifiesto para identificar versión; la evidencia de despliegue debe registrar además el SHA-256 real del archivo y actualizar el ledger si corresponde.

## Estado de Azure SQL DEV y fuente de verdad

`dbo.SchemaMigrationHistory` es la única fuente de verdad del estado aplicado en
Azure DEV. Las notas de QA, PR y este repositorio son evidencia histórica: no
autorizan una ejecución ni sustituyen una consulta actual al ledger. Antes de
cualquier migración, el ejecutor designado debe leer únicamente los campos no
sensibles necesarios (`MigrationId`, `FileName`, `FileSha256`, `Status` y
`AppliedAtUtc`) y contrastarlos con el SHA-256 del archivo que pretende aplicar.

La última evidencia documental archivada (2026-08-12) describe respaldo previo y
verificación de 0022, además de ejecuciones anteriores de 0013–0020. No se
reproduce aquí como una afirmación de estado actual porque la consulta directa
del ledger puede cambiar. 0021 también exige confirmación explícita en el ledger.
`database_Esteban/cu222_gastos_presupuesto.sql` es sólo referencia histórica.

## Ejecución controlada de 0012 cuando el ledger la reporte pendiente

No ejecutar 0012 si el ledger ya registra una fila `Applied`. Si está pendiente,
exige BACPAC verificado, ejecutor único y verificación posterior. Tampoco deben
ejecutarse las migraciones `0002`–`0006` por inferencia documental.

Use el ejecutor seguro para calcular el SHA-256 real del archivo final e
inyectarlo por `sqlcmd`:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/database/Invoke-Migration0012.ps1 -ServerInstance "<instancia>" -Database "<base>"
```

El modo predeterminado es `-AuthenticationMode Entra` y agrega `sqlcmd -G` para
usuarios individuales de Microsoft Entra. Para LocalDB se usa
`-AuthenticationMode Windows`, que agrega `sqlcmd -E`. El ejecutor no acepta
contraseñas, no usa `-P` y transmite el hash únicamente como una variable
SQLCMD: `-v "MigrationSha256=<SHA-256>"`.

`-DryRun` solo calcula y valida el hash, informa la ruta y el modo seleccionado
sin abrir conexión. El script usa `sqlcmd -b`, no recibe ni imprime connection
strings o contraseñas y falla si la expansión de hash no es válida. La prueba
sin conexión `scripts/database/Test-InvokeMigration0012.ps1` cubre los modos
Entra/Windows, la variable SQLCMD y las protecciones del ejecutor. Después debe ejecutarse
`0012_inventory_combos_transformations_intelligence.verify.sql` en modo de solo
lectura. El rollback continúa documentado en el archivo correspondiente.

### Compatibilidad legada validada localmente

La versión final de 0012 acepta tanto una instalación sin tablas de combos como
el esquema legado confirmado en el BACPAC previo de Azure DEV. La reconciliación
renombra columnas legadas, conserva claves y datos, rellena las columnas
canónicas, valida antes de crear restricciones y reconstruye
`PedidoComboDetalle` únicamente desde `PedidoDetalle.PedidoComboId`. Cualquier
cantidad no divisible, referencia huérfana, duplicado o snapshot contradictorio
produce `THROW` y rollback.

Los componentes legados dejan de contarse como productos sueltos en pedidos,
checkout, inteligencia, restauración y facturación. `FacturaCombos` no recibe
backfill de facturas antiguas para evitar doble contabilización; se usa para
facturas generadas después de 0012.

`database/verify/0012_legacy_reconciliation_local.sql` es una prueba en dos
fases exclusiva de LocalDB desechable: antes de 0012 captura evidencia agregada
sin datos personales y después compara conteos, claves, totales, inventario y
la reconstrucción, además de ejecutar `DBCC CHECKDB`.

## Migraciones 0013–0016

Estas migraciones no reutilizan los números 0007–0012 del prototipo de compras del PR #116. Cada archivo recibe el SHA-256 real mediante `sqlcmd -v "MigrationSha256=<SHA-256>"`, cuenta con `verify.sql` y rollback documentado, y rechaza una segunda aplicación registrada.

La ruta 0013 se validó en LocalDB desde esquema mínimo y desde la variante legada de compras; su prueba funcional cubre reintentos, recepción parcial, sobre-recepción, cierre con discrepancia, inventario y auditoría transaccional. La secuencia 0013–0016 también se ejecutó en una base LocalDB desechable, incluyendo la invocación vacía de los procedimientos de reportes y venta cruzada. El estado de Azure DEV debe obtenerse del ledger antes de cualquier operación; el QA autenticado sigue siendo una actividad separada.

El harness reproducible crea y elimina una base cuyo nombre empieza por `TrilogiaMigrations_`, se niega a operar fuera de LocalDB y cubre hash inválido, ausencia de residuos, migraciones y verificadores en orden, flujo funcional de compras, invocaciones vacías, segunda aplicación rechazada, documentos de rollback, ledger y `DBCC CHECKDB ... WITH PHYSICAL_ONLY`:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/database/Test-Migrations0013To0016.ps1 -ServerInstance "(localdb)\MSSQLLocalDB"
```

Si la instancia estándar no está disponible, se debe pasar explícitamente otra instancia LocalDB propia. Nunca usar este harness contra Azure o SQL compartido.

## Migraciones 0017–0020

Estas migraciones se validaron sintácticamente con ScriptDom y tienen `verify.sql` y rollback compensatorio documentado. El estado de Azure DEV no se infiere de esta sección: debe obtenerse del ledger. Antes de cualquier aplicación se exige BACPAC, ejecutor único, SHA-256 real, configuración responsable de factores/reglas y QA autenticado. No se precargan porcentajes, tasas ni fórmulas legales.

## Evidencia privada legada

Los registros anteriores a 0004 conservan `StorageStatus = Legacy` y no se sirven desde la aplicación. Antes de retirar cualquier carpeta pública histórica, un operador debe copiar cada archivo a `EvidenceStorage:RootPath`, asignar una clave generada, verificar la firma binaria y marcar el registro como `Ready`. No se debe marcar como listo un archivo inexistente o no validado.

## Migracion 0023

> **Estado: NO aplicada en Azure DEV.** El archivo se incorpora al repositorio
> para revision, pero nadie la ha ejecutado todavia. Antes de aplicarla,
> consultar `dbo.SchemaMigrationHistory`, que es la unica fuente de verdad.
> La aplicacion funciona con o sin ella: `ToggleProductStatusAsync` devuelve
> `bool?` y el controlador audita "Cambiar estado" cuando el procedimiento no
> informa el estado resultante.

Sustituye unicamente la definicion de `dbo.sp_Admin_ToggleProductStatus`. No
toca tablas, columnas, indices, restricciones, permisos ni filas de negocio.

Se aplica con `CREATE OR ALTER`, de modo que conserva el `object_id` y los
`GRANT EXECUTE` existentes; un `DROP` + `CREATE` los habria perdido. Normaliza
cualquier firma previa, incluida la variante de dos parametros
(`@ProductoId`, `@Activo`) de `database_Esteban/Fase3_1-3.sql`, que la
aplicacion nunca puede invocar porque solo envia `@ProductoId`.

Ensayada en LocalDB desechable sobre las dos variantes historicas: reproduccion
del defecto, aplicacion, verificacion, rechazo de segunda aplicacion, rechazo de
SHA-256 invalido con reversion completa de la transaccion, normalizacion de la
firma legada, rollback compensatorio y `DBCC CHECKDB WITH PHYSICAL_ONLY` sin
errores.

El codigo de aplicacion tolera ambas situaciones: `ToggleProductStatusAsync`
devuelve `bool?` y el controlador no afirma una direccion cuando el
procedimiento no informa el estado resultante.

El repositorio no normaliza fines de linea (`core.autocrlf` local decide), asi
que el SHA-256 del archivo puede variar entre clones. Calcularlo siempre sobre
el archivo real que se va a ejecutar, como hace el ejecutor de `scripts/database`.
