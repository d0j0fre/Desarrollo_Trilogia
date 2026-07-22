# QA final de la rama de saneamiento

## Resultado automatizado local

Ejecutado el 22 de julio de 2026:

- Escaneo de secretos: aprobado.
- Restauración .NET: aprobada.
- Build Release: 0 errores y 0 advertencias.
- Tests: 33 aprobados, 0 fallidos, 0 omitidos.
- SQL: 70 archivos y 821 lotes analizados con ScriptDom, 0 errores.
- `git diff --check`: aprobado.

Estos resultados no ejecutan migraciones ni sustituyen las pruebas con SQL Server, SignalR, navegador o Azure.

## Preparación obligatoria de entorno

- [ ] Crear BACPAC verificable de la base objetivo.
- [ ] Aplicar 0001–0006, en orden, con un único ejecutor y registrar SHA-256 real.
- [ ] Configurar `ConnectionStrings__DefaultConnection`.
- [ ] Configurar correo solo después de rotar la credencial expuesta.
- [ ] Configurar `EvidenceStorage__RootPath` en almacenamiento persistente fuera de `wwwroot`.
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
