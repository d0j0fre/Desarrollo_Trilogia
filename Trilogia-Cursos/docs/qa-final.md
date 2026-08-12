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

- [x] Crear y verificar el BACPAC previo de Azure DEV sin versionarlo ni publicar
  su ruta o hash completo.
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

## Corrección final PR #115 — evidencia local

- El combo con componente inactivo se excluye de tienda y detalle público; la
  consulta administrativa indica el estado textual y disponibilidad cero.
- El carrito refresca desde el catálogo autoritativo y descarta combos que ya
  no son vendibles. `AddCombo` mantiene antiforgery y el checkout vuelve a
  validar los componentes para rechazar solicitudes manipuladas.
- El comprobante se genera sin SMTP real mediante `OrderReceiptHtmlBuilder`.
  Recibe el total confirmado por SQL, presenta descuentos, combos y regalos en
  cero, escapa HTML y una falla SMTP no revierte un pedido confirmado.
- LocalDB debe ejecutar también
  `database/verify/0012_combo_inactive_component_local.sql`; valida componente
  inactivo, la llamada directa a `sp_Store_GetComboById` antes y después de la
  inactivación, exclusión pública, administración en cero, rechazo de checkout,
  inventario intacto, reactivación y stock cero. Cada escritura se revierte.
- El ejecutor 0012 se prueba sin conexión con
  `scripts/database/Test-InvokeMigration0012.ps1`: Entra construye `-G`,
  Windows construye `-E`, el hash llega como `MigrationSha256=<SHA-256>` y
  `DryRun` no ejecuta `sqlcmd`.
- El hash de 0012 se calcula con
  `scripts/database/Invoke-Migration0012.ps1 -DryRun`; la migración y su
  `verify.sql` rechazan hashes no expandidos o de manifiesto.
- El smoke público aislado de `Home/Shop`, detalle de combo y agregado al
  carrito respondió correctamente, sin alertas ni entradas `error` o `warning`
  en la consola del navegador.

## Reconciliación legada de 0012 — 28 de julio de 2026

- El BACPAC previo se importó en una nueva base LocalDB desechable con
  autenticación integrada; la restauración original y Azure DEV no se
  modificaron.
- 0012 conservó 1 combo, 3 `ComboDetalle`, 4 `PedidoCombos`, 153
  `PedidoDetalle`, todas sus claves y los agregados de pedidos, facturas e
  inventario.
- `PedidoComboDetalle` se reconstruyó en 12 filas, exactamente una por
  `PedidoComboId + ProductoId`, con cantidades enteras y sin duplicados.
- `FacturaCombos` permaneció vacía para facturas históricas; solo los flujos
  posteriores a 0012 generan snapshots de combo.
- El verify oficial, la comparación legada, checkout mixto, reintento,
  facturación, cancelación/restauración, transformación, componente inactivo,
  conflicto 54609 y segunda ejecución rechazada aprobaron.
- Dos sesiones concurrentes sobre una unidad produjeron un éxito, un 54615 y
  stock final cero. `DBCC CHECKDB` aprobó en las copias de reconciliación,
  concurrencia y ruta limpia.
- Gate local final: build Release sin errores ni advertencias; 117/117 pruebas
  .NET; ScriptDom focalizado 56 archivos/517 lotes; ScriptDom equivalente a CI
  83 archivos/916 lotes; escaneo de secretos aprobado.

## Azure

La evidencia en `docs/azure-despliegue-final-qa.md` corresponde a una versión anterior. Repetir smoke tests de MVC, API, SQL, checkout, chat, evidencias, garantías y SMTP después de desplegar esta rama; no marcarla validada por herencia.

## Cierre integral CU-084, CU-101–104, CU-132, CU-134, CU-262 e imágenes — 3 de agosto de 2026

Evidencia local ejecutada:

- Build Release completo: 0 errores y 0 advertencias.
- Suite completa: 158 aprobadas, 0 fallidas y 0 omitidas (41 pruebas nuevas sobre la base de 117).
- ScriptDom recursivo equivalente a CI: 94 archivos, 951 lotes, 0 errores.
- LocalDB: 0013 validada desde esquema mínimo y legado; prueba funcional de recepción parcial, reintento, exceso, discrepancia, inventario y auditoría aprobada.
- LocalDB: secuencia 0013–0016, `verify.sql` e invocaciones vacías de ambos reportes y venta cruzada aprobadas en base desechable.

QA manual pendiente antes de marcar Completada:

- [ ] Aplicar 0013–0016 en orden tras BACPAC, con SHA-256 real y ejecutor único; no volver a aplicar migraciones registradas.
- [ ] Probar perfiles con y sin cada permiso exacto, antiforgery y rate limits.
- [ ] Recibir parcialmente una orden, reintentar el mismo token, rechazar exceso y cerrar discrepancia; contrastar stock y auditoría.
- [ ] Abrir tablero de entregas con dos sesiones y confirmar actualización sin datos de cliente.
- [ ] Comparar reportes diario/mensual/categoría y vendedores con SQL fuente; abrir impresión y CSV en Excel/LibreOffice.
- [ ] Verificar carrito con historial suficiente, insuficiente, cliente nuevo, producto agotado y producto ya incluido; nunca debe autoagregar ni modificar precios.
- [ ] Cargar JPG/JPEG/PNG/WEBP válidos e inválidos, superar 2 MB, simular fallo DB y reemplazar imagen; confirmar ausencia de huérfanos y traversal.
- [ ] Revisar tienda, detalle, carrito e inventario en móvil/escritorio: fallback, alt, lazy loading y dimensiones sin salto de layout.

## Puerta A — cierre local RRHH, planilla e imágenes — 4 de agosto de 2026

Evidencia ejecutada en `codex/cierre-rrhh-backlog-20260804`, basada exactamente en `107bce2b9fe5e509ca221ae67da4bf008f33e274`:

- PR #117 continúa abierta, mergeable y sin cambio de cabeza; sus cinco checks remotos permanecen aprobados.
- CU-111–114 tienen flujo vertical local, permisos exactos, antiforgery, contratos de servicio, pruebas y migraciones incrementales 0017–0020.
- Las reglas de planilla no incluyen tasas legales inventadas: factor, valor, fuente y vigencia son configuración obligatoria; el cálculo conserva snapshot y huella.
- La boleta se deriva del snapshot, exige propietario o permiso, no se guarda en `wwwroot` y el correo contiene solo un enlace HTTPS autenticado.
- Las imágenes nuevas usan `ProductImages:StoragePath` fuera de `wwwroot`; reemplazo, retiro y eliminación limpian el archivo sin invalidar un commit de datos si falla el filesystem.
- Harness LocalDB 0013–0016 aprobado: `verify.sql`, compra funcional, hash inválido sin residuos, segunda aplicación rechazada, rollback documentado, invocaciones vacías, ledger y `DBCC CHECKDB WITH PHYSICAL_ONLY`.
- Suite completa con cobertura: 189 aprobadas, 0 fallidas, 0 omitidas.
- Cobertura global: 895/15.407 líneas (5,80%) y 417/10.631 ramas (3,92%); se documenta como brecha real.
- ScriptDom: 103 archivos, 982 lotes y 0 errores.
- Escaneo de secretos: 755 archivos rastreados, 724 archivos de texto y 12 placeholders/vacíos aprobados; 0 hallazgos.
- Build Release final: 0 errores y 0 advertencias.

Pendiente humano/entorno:

- [ ] Configurar `PaySlips:PublicBaseUrl` con HTTPS y credenciales SMTP mediante secretos de entorno.
- [ ] Validar factores, fuentes y reglas de planilla con la persona responsable; no precargar valores por inferencia.
- [ ] Tomar BACPAC, designar ejecutor y aplicar 0017–0020 en orden con SHA real; ejecutar cada `verify.sql`.
- [ ] Probar con sesiones separadas calculador/aprobador/pagador, propietario/no propietario y permisos denegados.
- [ ] Probar SMTP real controlado y confirmar que un fallo no modifica una planilla pagada.
- [ ] Configurar y respaldar el volumen de imágenes; inventariar/migrar archivos legados antes de retirar la ruta anterior.
- [ ] Ejecutar smoke de navegador, responsive, accesibilidad y ausencia de errores de consola.
- [ ] Ejecutar QA y migraciones en Azure DEV solo después de autorización y backup.

## Ejecución controlada en Azure DEV — 4 de agosto de 2026

Ejecución posterior a la autorización operativa, sobre los recursos DEV
confirmados `rg-trilogia-cursos-dev`, `sql-trilogia-cursos-dev-cr01`,
`DistribuidoraJJ_DB_DEV` y `sttrilogiadevcr01`. No se versionaron credenciales,
tokens, connection strings ni datos de producción.

- [x] BACPAC previo creado en el contenedor privado `bacpac`:
  `DistribuidoraJJ_DB_DEV_pre0013-0020_20260804T000000Z.bacpac`.
  Se validaron ZIP, `model.xml`, `origin.xml` y coincidencia de tamaño remoto
  (221.912 bytes); SHA-256 local de verificación:
  `1226F7A8F29B240E15A99A5276C65690BE04BAA9D9E24C5E01CD358985E3965F`.
- [x] Ledger leído antes de cambiar el esquema: llegaba hasta 0012; no había
  entradas para 0013–0020.
- [x] Migraciones 0013–0020 aplicadas una a una con el SHA-256 real del archivo,
  transacción propia, `verify.sql` inmediato y fila `Applied` coincidente en
  `dbo.SchemaMigrationHistory`.
- [x] `DBCC CHECKDB ... WITH PHYSICAL_ONLY` aprobado y ledger completo con ocho
  entradas aplicadas para 0013–0020.
- [x] Smoke público del MVC DEV y Swagger del API DEV disponibles por HTTPS.
- [ ] El QA autenticado no se marcó aprobado: faltan usuarios de prueba
  autorizados para comprobar roles positivos/negativos y segregación.
- [ ] SMTP y `ProductImages` no tienen configuración de entorno en los App
  Services. No se envió correo real, no se subió imagen real y no se agregaron
  secretos ni settings especulativos.
- [ ] La copia temporal local del BACPAC debe eliminarse con el procedimiento
  seguro del operador; el respaldo remoto validado es la copia de recuperación.

## Cierre integral de módulos y seguridad — 11 de agosto de 2026

Validación local sobre `fix/runtime-modules-20260806`, partiendo de `fa1e3dc`:

- Departamentos conserva las tablas heredadas de Chat; no se recrearon ni copiaron datos.
- Los verificadores de chat privado, departamentos y métricas confirmaron en Azure DEV 15 conversaciones, 24 mensajes privados, 12 departamentos, 18 membresías y 3 mensajes departamentales.
- Build Debug y Release: 0 errores y 0 advertencias.
- Suite completa con cobertura: 194 aprobadas, 0 fallidas y 0 omitidas; 966/15.705 líneas (6,15%) y 452/10.843 ramas (4,16%).
- ScriptDom: 109 archivos, 995 lotes y 0 errores.
- Escaneo de secretos: 758 archivos rastreados, 727 archivos de texto y 12 placeholders/vacíos aprobados; 0 hallazgos.
- La planilla continúa usando factores versionados y fuente/aprobación explícita; no se incorporaron tasas legales.
- La migración 0022 introduce PBKDF2-SHA256 con sal aleatoria y actualización gradual de credenciales legadas, manteniendo contratos SQL compatibles durante el despliegue.
- Recuperación de contraseña requiere `PasswordRecovery:PublicBaseUrl` HTTPS; si falta, responde de forma genérica y no genera token.

Pendiente hasta completar publicación:

- [ ] Ejecutar CI del PR y respetar la aprobación/protección de `main`.
- [ ] Crear respaldo de Azure SQL DEV, aplicar 0022 con SHA-256 real y ejecutar su verificador.
- [ ] Desplegar el commit integrado y ejecutar smoke público final.
- [ ] Ejecutar QA autenticado por rol; no había sesión ni credenciales QA autorizadas disponibles durante la revisión local.
