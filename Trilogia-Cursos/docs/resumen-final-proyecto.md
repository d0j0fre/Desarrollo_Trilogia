# Resumen de estabilización e integración

## Estado

La rama base `codex/p0-saneamiento-integracion-total-20260722` sanea la configuración y recupera CI. Encima, `codex/danny-sprint4-cu201-cu202-cu221-cu222-cu223` completa las cinco historias asignadas a Danny. El conjunto compila sin errores ni advertencias, aprueba 92 pruebas y valida sintácticamente los scripts SQL recursivos.

El trabajo está **pendiente de QA autenticado**. Las migraciones 0007–0011 y sus objetos se verificaron en Azure DEV; falta confirmar una contraseña de prueba autorizada y ejecutar los flujos funcionales en navegador.

## Cambios principales

- Eliminación de secretos/configuraciones reales del árbol actual, escáner reforzado y procedimiento de incidente/historial.
- CI en trabajos independientes: `security-scan`, `sql-validation`, `build`, `tests` y `final-gate`.
- Autorización, pertenencia de recurso, antiforgery, rate limiting, mensajes genéricos y logging estructurado en flujos sensibles.
- Chat separado en servicios, SignalR con pertenencia validada, departamentos reales, historial paginado y búsqueda autorizada paginada.
- Evidencias privadas fuera de `wwwroot`, firma mágica, escritura por etapas/atómica y descarga controlada.
- Checkout/promociones/inventario consolidados en una transacción SQL autoritativa.
- Flujo de garantías funcional con validación de pedido propio, control de duplicados y administración auditada.
- Precisión decimal explícita y CSP incremental en modo Report-Only.
- Inventario SQL, orden de migraciones y matriz de historias actualizados.

## Migraciones nuevas

1. `0002_chat_private_security.sql`
2. `0003_chat_departments_and_search.sql`
3. `0004_private_delivery_evidence.sql`
4. `0005_atomic_checkout_promotions.sql`
5. `0006_warranty_workflow.sql`
6. `0007_secure_document_management.sql`
7. `0008_document_expiration_alerts.sql`
8. `0009_annual_department_budgets.sql`
9. `0010_operating_expenses_alignment.sql`
10. `0011_budget_actual_comparison.sql`

Dependen del ledger `0001_create_schema_migration_history.sql` y del esquema base. Se validaron con ScriptDom; deben probarse primero en una base desechable, luego aplicarse con BACPAC y registro del SHA-256 real.

## Límites conocidos

- La búsqueda de chat usa `LIKE` parametrizado e índices de fecha/origen; no usa Full-Text Search.
- CSP es Report-Only para observar dependencias antes de forzarla.
- Evidencia legada permanece `Legacy` y no se sirve hasta migrarla/verificarla.
- El asistente es un **Asistente conversacional basado en reglas e interpretación de intenciones**. CU-262 (cross-selling en tiempo real) no está implementada.
- Quedan métodos históricos de chat en `AdminDbService`, pero el flujo vigente ya no depende de ellos; su retiro puede hacerse en una refactorización posterior con base desplegada.

## Acciones externas

1. Revocar/rotar la credencial SMTP expuesta y actualizar configuración segura.
2. Coordinar, si se aprueba, la reescritura del historial y reclonado de colaboradores.
3. Mantener la protección activa de `main` y revisar sus contextos si cambia el workflow.
4. Ejecutar 0001–0011 y QA funcional/negativo en SQL Server.
5. Desplegar a Azure y repetir smoke tests; la validación Azure antigua es histórica.
## Diagnóstico Sprint 4 y autenticación Azure — 23 de julio de 2026

- El API y MVC apuntan a Azure DEV y a la base `DistribuidoraJJ_DB_DEV`; el MVC consume el API local esperado.
- La cuenta administrativa informada existe, está activa y tiene perfil Administrador.
- `sp_Auth_ValidateUser` tiene la firma y resultado esperados. El `401` observado corresponde a una credencial que no coincide con el valor directo almacenado; no se cambió la contraseña.
- Se corrigieron transformación de contraseña, tipos SQL inferidos, lectura posicional y logging silencioso.
- Azure contiene 38 credenciales directas, ninguna con hash reconocible y sin columna `ContrasenaHash`; se documentó una migración gradual basada en restablecimiento, no una conversión improvisada.
- 0007–0011 y sus objetos/permisos Sprint 4 fueron verificados en Azure. El QA funcional autenticado sigue pendiente.
- Detalle reproducible: `docs/diagnostico-login-azure-sprint4-20260724.md`.
