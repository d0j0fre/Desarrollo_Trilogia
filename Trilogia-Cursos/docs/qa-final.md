# QA final de la rama de saneamiento

## Diagnóstico Azure y autenticación — 23 de julio de 2026

- Azure efectivo: servidor DEV y base `DistribuidoraJJ_DB_DEV` confirmados sin exponer la connection string.
- Administradores: dos cuentas activas con perfil `Administrador`; no se seleccionaron contraseñas.
- `sp_Auth_ValidateUser`: firma y seis columnas compatibles; ejecutable con la credencial almacenada.
- Causa del `401`: la credencial presentada no coincide con el valor que continúa almacenado; el restablecimiento citado no quedó comprobado en esta cuenta/base.
- Correcciones: parámetros tipados, contraseña sin `Trim()`, mapeo por nombres y logging seguro.
- Migraciones 0007–0011: ledger y objetos reales verificados; 55 columnas obligatorias sin faltantes, constraints confiables y permisos Administrador completos.
- Almacenamiento: `documents` y `expense-receipts`, cada uno con `staging/files`, creados y escribibles fuera de `wwwroot` al propagar la configuración al proceso.
- Navegador: login carga, credencial inválida muestra mensaje genérico y las cinco rutas redirigen a login sin sesión.
- Login válido y flujos autenticados: pendientes de contraseña de prueba definida fuera del repositorio.

## Resultado automatizado local

Ejecutado el 22 de julio de 2026:

- Escaneo de secretos: aprobado.
- Restauración .NET: aprobada.
- Build Release: 0 errores y 0 advertencias.
- Tests de la entrega original: 87 aprobados. Después del diagnóstico de autenticación: 92 aprobados, 0 fallidos, 0 omitidos.
- SQL: 75 archivos y 876 lotes analizados con ScriptDom, 0 errores.
- `git diff --check`: aprobado.

Estos resultados no ejecutan migraciones ni sustituyen las pruebas con SQL Server, SignalR, navegador o Azure.

## Preparación obligatoria de entorno

- [ ] Crear BACPAC verificable de la base objetivo.
- [ ] No repetir 0007–0011 en Azure DEV; verificar por separado 0002–0006 antes de cualquier aplicación.
- [ ] Configurar `ConnectionStrings__DefaultConnection`.
- [ ] Configurar correo solo después de rotar la credencial expuesta.
- [ ] Configurar `EvidenceStorage__RootPath` en almacenamiento persistente fuera de `wwwroot`.
- [ ] Configurar `PrivateStorage__RootPath` en almacenamiento persistente fuera de `wwwroot` y con backup.
- [ ] Mantener `DocumentAlerts__EmailEnabled=false` hasta validar SMTP; luego habilitarlo por configuración segura.
- [ ] Confirmar permisos `CHAT_DEPARTAMENTOS_GESTIONAR`, garantías y módulos operativos.

## Seguridad y autorización

- [ ] Usuario anónimo no accede a chat, evidencias, portal cliente ni administración.
- [ ] Cliente no puede consultar pedido, garantía o evidencia ajenos alterando IDs.
- [ ] Chofer solo ve rutas/evidencias asignadas.
- [ ] Usuario no puede leer/enviar/unirse por SignalR a conversaciones o departamentos ajenos.
- [ ] Administrador sin permiso exacto no gestiona departamentos, garantías ni otros módulos.
- [ ] POST sin token antiforgery es rechazado.
- [ ] Errores visibles no contienen SQL, stack trace, rutas o nombres internos.
- [ ] Rate limits de login, recuperación, chat, búsqueda, asistente y evidencias responden de forma controlada.
- [ ] Rate limits de cargas privadas, escritura financiera y generación de alertas responden 429 sin alterar datos.

## QA Sprint 4 — Danny — CU-201, CU-202, CU-221, CU-222 y CU-223

Precondiciones: 0007–0011 verificadas en DEV, dependencias históricas auditadas, `PrivateStorage__RootPath` persistente fuera de `wwwroot`, una credencial de prueba autorizada y perfiles con/sin cada permiso. Usar datos genéricos, nunca documentos reales.

| Bloque | Rol requerido | Resultado esperado | Resultado obtenido | Evidencia pendiente | Estado |
|---|---|---|---|---|---|
| Documentos/alertas | Administrador o perfil con permisos `DOCUMENTOS_*` | Flujo positivo y denegaciones seguras | No ejecutado en entorno | Capturas, filas SQL, logs sin secretos | Pendiente entorno |
| Presupuestos | Creador y aprobador distintos con permisos `PRESUPUESTOS_*` | Totales exactos, locks y estados válidos | No ejecutado en entorno | Consultas, concurrencia y auditoría | Pendiente entorno |
| Gastos/comparativa | Perfiles con permisos `GASTOS_*` y `PRESUPUESTOS_COMPARAR` | Idempotencia, afectación y reportes consistentes | No ejecutado en entorno | IDs, consultas, CSV y capturas | Pendiente entorno |

### Flujo documental CU-201/CU-202

- [ ] Crear PDF/JPG/PNG válidos; rechazar extensión, MIME o firma discordante y archivos mayores a 10 MB.
- [ ] Confirmar que ningún documento/comprobante queda bajo `wwwroot` y que path traversal no funciona.
- [ ] Editar metadatos, reemplazar archivo, descargar versiones anteriores y ejecutar borrado lógico/reactivación.
- [ ] Simular fallo entre staging, commit y `Ready`; no debe quedar registro listo ni archivo huérfano.
- [ ] Generar umbrales 30/15/7/1/0 con fecha de negocio Costa Rica dos veces; la segunda ejecución no duplica.
- [ ] Marcar alerta atendida y comprobar indicador/listado. Verificar que fallo SMTP no revierte alertas internas ni expone detalle.
- [ ] Probar permisos `DOCUMENTOS_VER`, `DOCUMENTOS_GESTIONAR`, `DOCUMENTOS_ALERTAS_GENERAR` y `DOCUMENTOS_ALERTAS_ATENDER` con permitidos/denegados.

### Flujo financiero CU-221/CU-222/CU-223

- [ ] Crear presupuesto anual y comprobar 12 meses, suma decimal exacta y ajuste de centavos en diciembre.
- [ ] Presentar sólo cuando el detalle suma el total; impedir editar aprobado, autoaprobar y duplicar aprobado activo por año/departamento.
- [ ] Rechazar con motivo, cerrar aprobado y copiar a un año sin presupuesto activo.
- [ ] Registrar gasto dos veces con el mismo token: debe devolverse el mismo `GastoId` sin duplicar.
- [ ] Validar subtotal+impuesto del servidor, comprobante privado y transiciones Registrado/Aprobado/Rechazado/Pagado/Anulado.
- [ ] Validar alertas 80/90/100; exceso requiere `GASTOS_EXCEDER_PRESUPUESTO`; registrador no autoaprueba.
- [ ] Confirmar que aprobados+pagados forman real, registrados quedan pendientes y anulados se excluyen.
- [ ] Comparar anual/mensual/departamento/categoría, drill-down, departamentos sin presupuesto, categorías excedidas y proyección `acumulado / meses transcurridos * 12`.
- [ ] Abrir vista de impresión y CSV en Excel/LibreOffice; celdas con `=`, `+`, `-` o `@` deben quedar neutralizadas.
- [ ] Ejecutar dos aprobaciones concurrentes y dos gastos concurrentes para verificar locks, unicidad e idempotencia.

## Chat CU-231/CU-232/CU-233

- [ ] Abrir/reutilizar conversación sin duplicados y enviar/recibir por SignalR.
- [ ] Reconectar SignalR y reingresar solo a grupos autorizados.
- [ ] Cargar páginas anteriores del historial.
- [ ] Crear/editar/activar departamento; añadir/retirar miembros y configurar publicación.
- [ ] Buscar palabra/frase en conversación actual y en todo el historial autorizado.
- [ ] Paginar resultados y abrir su conversación/departamento.
- [ ] Confirmar que contenido se renderiza como texto, no HTML.

## Evidencia de entrega

- [ ] JPEG, PNG y WEBP válidos se guardan fuera de `wwwroot` con nombre GUID.
- [ ] Extensión, MIME o firma discordantes son rechazados; máximo 5 MB.
- [ ] Fallo de base o movimiento final no deja registro `Ready` ni archivo huérfano.
- [ ] Endpoint autorizado devuelve MIME correcto, `nosniff` y `Cache-Control: no-store`.
- [ ] Ruta manipulada/path traversal no accede a otros archivos.
- [ ] Evidencias legadas no se sirven hasta migrarlas y verificarlas.

## Checkout, promociones e inventario

- [ ] Carrito manipulado usa precio/producto/stock vigente del servidor.
- [ ] Dos compras concurrentes no generan stock negativo.
- [ ] Pedido, descuento, regalía y stock se confirman o revierten juntos.
- [ ] Segmento, vigencia, prioridad y stock de regalía seleccionan la promoción correcta.
- [ ] Cancelar pedido pendiente restaura stock una sola vez; facturar no vuelve a descontarlo.
- [ ] Confirmación y correo muestran total/regalías retornados por SQL.

## Garantías y otros módulos

- [ ] Cliente solo crea garantía para detalle propio entregado y no duplica una abierta.
- [ ] Administrador autorizado lista, cambia estado, registra resolución y deja auditoría.
- [ ] Probar rutas/entregas, liquidaciones, reportes, devoluciones, flotilla, activos, comodatos, reclamos, KPIs y gastos con roles permitidos y denegados.
- [ ] Confirmar **Asistente conversacional basado en reglas e interpretación de intenciones** y sus límites; CU-262 no debe declararse implementada.

## Regresión general

- [ ] Login, registro, recuperación y logout.
- [ ] Tienda, carrito, checkout, pedidos, comprobante y cancelación.
- [ ] Inventario, facturación, clientes, créditos, empleados, roles, permisos y auditoría.
- [ ] API auth/productos y respuestas 400/404 documentadas.
- [ ] Sin 404 de scripts, errores de consola ni mojibake visible.
- [ ] Responsive e impresión de comprobante/factura.

## Azure

La evidencia en `docs/azure-despliegue-final-qa.md` corresponde a una versión anterior. Repetir smoke tests de MVC, API, SQL, checkout, chat, evidencias, garantías y SMTP después de desplegar esta rama; no marcarla validada por herencia.
