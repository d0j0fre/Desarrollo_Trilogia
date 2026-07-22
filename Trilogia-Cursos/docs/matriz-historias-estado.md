# Matriz de historias, implementación y verificación

Estado al 22 de julio de 2026. “Pendiente entorno” significa que existe código/SQL y validación estática, pero falta aplicar migraciones y ejecutar el flujo real. No se cerraron issues con ese estado.

| CU | Issue | Estado | Evidencia principal | Verificación / pendiente |
|---|---:|---|---|---|
| 081 | #28 | Implementada; pendiente entorno | `RoutesAdminController`, `cu081_rutas_entregas_esquema.sql` | Build/SQL; QA de rutas |
| 101 | #36 | Duplicada de CU-081 | Mismo flujo que CU-081 | Confirmar/cerrar como duplicada manualmente |
| 082 | #29 | Implementada; pendiente entorno | `DriverDeliveriesController`, `cu082_entregas_estado_offline_sps.sql` | Build/SQL; QA chofer/offline |
| 102 | #37 | Duplicada de CU-082 | Mismo flujo que CU-082 | Confirmar/cerrar como duplicada manualmente |
| 083 | #30 | Reforzada; pendiente entorno | `EvidenceStorageService`, `DeliveryEvidenceController`, migración 0004 | Tests de archivos; QA SQL/almacenamiento |
| 103 | #38 | Duplicada de CU-083 | Mismo flujo que CU-083 | Confirmar/cerrar como duplicada manualmente |
| 105 | #40 | Implementada; pendiente entorno | `ReturnsController`, `cu141_142_105_devoluciones_cuarentena_liquidacion.sql` | Build/SQL; QA liquidación |
| 106 | #41 | Implementada; pendiente entorno | `FinanceController`, `cu106_liquidacion_financiera.sql` | Build/SQL; QA financiera |
| 131 | #50 | Implementada; pendiente entorno | `ManagementDashboardController`, `cu131_reportes_dashboard_gerencial.sql` | Build/SQL; QA dashboard |
| 141 | #54 | Implementada; pendiente entorno | `ReturnsController`, `cu141_142_105_devoluciones_cuarentena_liquidacion.sql` | Build/SQL; QA devolución |
| 142 | #55 | Implementada; pendiente entorno | `ReturnsController`, mismo script CU-141 | Build/SQL; QA cuarentena |
| 143 | #56 | Implementada; pendiente entorno | `ReturnsController`, mismo script CU-141 | Build/SQL; QA seguimiento |
| 144 | #57 | Reforzada; pendiente entorno | `ClientPortalController`, `WarrantyRequestsAdminController`, migración 0006 | Tests de filtros; QA SQL completo |
| 151 | #58 | Implementada; pendiente entorno | `VehiclesController`, `cu151_vehiculos_marca.sql` | Build/SQL; QA flotilla |
| 152 | #59 | Implementada; pendiente entorno | `FleetController`, `cu152_153_154_161_flota_activos.sql` | Build/SQL; QA kilometraje |
| 153 | #60 | Implementada; pendiente entorno | `FleetController`, mismo script CU-152 | Build/SQL; QA mantenimiento |
| 154 | #61 | Implementada; pendiente entorno | `FleetController`, mismo script CU-152 | Build/SQL; QA alertas |
| 161 | #62 | Implementada; pendiente entorno | `AssetsController`, mismo script CU-152 | Build/SQL; QA activos |
| 162 | #63 | Implementada; pendiente entorno | `ComodatosController`, `cu162_163_164_comodatos.sql` | Build/SQL; QA asignación |
| 163 | #64 | Implementada; pendiente entorno | `ComodatosController`, mismo script CU-162 | Build/SQL; QA devolución |
| 164 | #65 | Implementada; pendiente entorno | `ComodatosController`, mismo script CU-162 | Build/SQL; QA rentabilidad |
| 171 | #66 | Reforzada; pendiente entorno | `PromotionsController`, `PromotionEngine`, migración 0005 | Tests de prioridad; QA SQL |
| 172 | #67 | Reforzada; pendiente entorno | Segmento en `PromotionEngine`/0005 | Test de segmento; QA SQL |
| 173 | #68 | Reforzada; pendiente entorno | `CartController`, `StoreDbService`, migración 0005 | Tests de promociones; QA concurrencia |
| 174 | #69 | Implementada; pendiente entorno | `PromotionsController`, `cu171_174_promociones.sql` | Build/SQL; QA inactivación |
| 191 | #72 | Implementada; pendiente entorno | `ReclamosController`, `cu191_192_reclamos.sql` | Build/SQL; QA registro |
| 192 | #73 | Implementada; pendiente entorno | `ReclamosController`, mismo script CU-191 | Build/SQL; QA cierre/resolución |
| 211 | #76 | Implementada; pendiente entorno | `KpisController`, `cu211_213_metas_kpis.sql` | Build/SQL; QA metas |
| 212 | #77 | Implementada; pendiente entorno | `KpisController`, mismo script CU-211 | Build/SQL; QA progreso |
| 213 | #78 | Implementada; pendiente entorno | `KpisController`, mismo script CU-211 | Build/SQL; QA reporte |
| 221 | #79 | Implementada; pendiente entorno | `FinanceController`/`ExpensesController`, `cu222_gastos_presupuesto.sql` | Build/SQL; QA presupuesto |
| 222 | #80 | Implementada; pendiente entorno | `ExpensesController`, mismo script CU-221 | Build/SQL; QA gasto |
| 223 | #81 | Implementada; pendiente entorno | `FinanceController`, mismo script CU-221 | Build/SQL; QA comparativa |
| 231 | #82 | Corregida y probada; pendiente entorno | `ChatController`, `ChatHub`, migración 0002 | Tests autorización; QA SignalR/SQL |
| 232 | #83 | Corregida y probada; pendiente entorno | `ChatDbService`, vista admin, migración 0003 | Tests filtros; QA CRUD/SignalR |
| 233 | #84 | Corregida y probada; pendiente entorno | `chat.js`, `sp_Chat_SearchMessages` | Paginación implementada; QA SQL/UI |
| 251 | #88 | Implementada; pendiente entorno | `RoutesAdminController`, `cu251_252_253_rutas_inteligentes.sql` | Build/SQL; QA secuenciación |
| 252 | #89 | Implementada; pendiente entorno | Vistas/servicio de rutas, mismo script CU-251 | Build/SQL; QA mapa móvil |
| 253 | #90 | Implementada; pendiente entorno | `RoutesAdminController`, mismo script CU-251 | Build/SQL; QA recálculo |
| 261 | #91 | Implementada; pendiente entorno | `AssistantController`, `cu261_263_asistente.sql` | Asistente por reglas; QA intenciones |
| 262 | #92 | No implementada | No existe cross-selling en tiempo real basado en contexto de venta | Requiere diseño y alcance nuevo |
| 263 | #93 | Implementada; pendiente entorno | `AssistantController`, `cu261_263_asistente.sql` | Build/SQL; QA ayuda por módulo |

## Cobertura automatizada de esta rama

- Chat: pertenencia/administración, política de mensajes y filtros de controladores.
- Evidencias: firmas, extensión/MIME, claves seguras, traversal y ciclo stage/commit/read.
- Promociones: prioridad, redondeo, segmento, vigencia y límite por stock de regalía.
- Seguridad MVC: filtros de sesión/admin y antiforgery en POST críticos.

Total local: 33 pruebas aprobadas. El detalle de QA de entorno está en `docs/qa-final.md`.
