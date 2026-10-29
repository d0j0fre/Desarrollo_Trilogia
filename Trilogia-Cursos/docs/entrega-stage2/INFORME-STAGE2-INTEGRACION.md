# Distribuidora JJ - Informe de trabajo e integración Stage 2

**Corte de evidencia:** 29 de octubre de 2026, hora de Costa Rica.
**Finalidad:** entregar al equipo una base para construir la matriz de integración, conocer el alcance implementado y continuar el cierre.
**Repositorio:** `d0j0fre/Desarrollo_Trilogia`; rama solicitada `restructuracion-2.0`.
**Base del trabajo:** rama local `codex/stage2-design-system`, commit `c54be538bbf518673ea6fbd19817e75bfdc9bd86`, más los cambios locales de esta entrega. El SHA definitivo de la entrega se consulta en el último commit de la rama.

## 1. Lectura del estado

Stage 2 ha modernizado amplias zonas del sistema y varias integraciones se probaron realmente en la base local principal. Hay funciones que existen solo como candidatos y se probaron en bases aisladas. El sistema **no está certificado al 100 %**: no existe evidencia que permita afirmar cero errores ni convertir una compilación o una prueba simulada en una certificación integral.

En este informe, **operativo con evidencia local** significa que existen registros de SQL/HTTP/navegador en el escenario indicado; **candidato** significa código o SQL probado solo en copia o en pruebas aisladas; **pendiente** significa que falta implementar, integrar o verificar. La información histórica corresponde al momento de cada prueba y debe repetirse si se cambian los contratos o el entorno.

## 2. Arquitectura y alcance general

- Aplicación universitaria .NET 9: MVC en `Proyecto_Final`, API en `Proyecto_FinalAPI`, contratos compartidos en `shared/JJ.Commerce`, SQL Server mediante ADO.NET/SqlClient, pruebas xUnit y scripts de interfaz. La interfaz Stage 2 separa Storefront, Workspace y Field.
- Se trabajaron tokens y estilos, layouts compartidos, navegación, responsive, acciones visibles, tablas, indicadores, dashboards y reportes. Se mantuvieron rutas, información y comportamientos existentes al migrar vistas.
- Áreas modernizadas: dashboards e indicadores; combos y promociones; pedidos de vendedor y supervisor; flota, rutas y entregas; activos y comodatos; documentos; consultas, reclamos y garantías; roles, permisos, auditoría y asistente; superficies públicas y de error.
- La base local de integración conserva el historial y utiliza migraciones con identidad, hash y verificación. Los candidatos privados no son migraciones oficiales. La base principal no debe sobrescribirse con una restauración y los respaldos y claves deben custodiarse fuera del repositorio.
- Las cuatro vistas `Fleet/Mileage.cshtml` y `Vehicles/Create.cshtml`, `Edit.cshtml`, `Index.cshtml` eran modificaciones previas protegidas. Se conservan en la versión de trabajo para revisión por su autor; incluirlas en la entrega no certifica que pertenezcan a los bloques Stage 2 ni decide su adopción frente a `main`.

## 3. Cronología resumida y base Git

La referencia histórica de Stage 2 era `c0c6b326465403f31c2c82c3fe1cf04c3831efe1`. Los checkpoints locales incluyen `ea4a854` fundamentos, `5b9d424` dashboards, `facafb9` combos y promociones, `f05ee9f` pedidos, `0f37885` logística, `eb14551` activos, `a0f4283` documentos, `adc5b85` servicio al cliente, `119d425` seguridad/asistente y `c54be53` superficies públicas. Existen cambios posteriores sin commit incorporados a esta entrega; el commit final de la rama será su fotografía de integración.

La referencia local `origin/main` observada fue `e8d3f7be3c838a8061a5aacdc8efd25c3fd8e180`. La comparación local registró dos commits solo en `origin/main` y 63 solo en el HEAD de Stage 2. Esta observación no sustituye sincronizar y revisar el estado remoto al integrar. No se hizo merge automático y no se debe eliminar el trabajo exclusivo de `main`.

La documentación histórica registra colisiones en las migraciones 0023 y 0024-0028, además de una identidad local 0029. Un mismo número no demuestra equivalencia. Comparar identificador completo, archivo, hash, objetos, dependencias y ledger antes de seleccionar o reordenar cambios. Los SQL históricos y `database/candidates` no deben ejecutarse automáticamente.

## 4. Funciones con evidencia real en la integración local

### 4.1 Acceso, sesiones y seguridad

Se vinculó la autorización al login original, actor, perfil, sello y huella de sesión; se acordó vigencia absoluta de ocho horas y revocación transaccional. Se prepararon identidades separadas para aplicación, lectura, emisión y autenticación, con permisos mínimos. Se probaron límites de cuenta/sesión, antiforgery y denegaciones en rutas acreditadas. La aprobación y revocación de permisos no convierten a Administrador en lector universal de chat o documentos.

### 4.2 Pedidos, aprobación, facturación y stock

El cálculo aprobado usa el total final facturable del servidor con descuentos e impuestos. Solo un total **mayor** a ₡2.000.000 exige aprobación; exactamente ₡2.000.000 no se retiene por monto. Liberado, Facturado y Entregado son estados distintos: al liberar o aprobar se conserva la facturación automática y el pedido termina Facturado; solo la confirmación logística acredita entrega. Se documentaron pruebas de cotización, revisión/aprobación/rechazo, factura y descuento de stock atómicos, UUID y reintentos con un solo efecto, junto con negativas de propiedad y sesión.

La integración local documenta migraciones 0047-0054 y relaciones comerciales autorizadas. Sus asignaciones reales son datos del entorno, no deben copiarse ni suponerse válidas en otra base. Faltan una revisión transversal final, conciliación con el destino y la verificación de casos comerciales no cubiertos por el escenario QA.

### 4.3 Entregas, cliente, reclamos y garantías

Se registraron pruebas reales de entrega QA, acceso a información propia, portal de cliente y garantía condicionada a entrega física y propiedad. Las pruebas de reclamos incluyen crear, seguimiento, cierre, recibo UUID, dos solicitudes concurrentes con un solo efecto y negativas por sesión, actor, payload y correspondencia factura/pedido. No se inyectó una pérdida real de respuesta a nivel de red: se probó el reintento después de un commit confirmado. Los datos QA se trataron separadamente de personas y pedidos reales.

### 4.4 Documentos y conservación del historial

La autorización documentada sigue propietario **o** permiso explícito por operación: lectura, descarga, edición y versiones; no hay bypass de contenido por administración. Se probó publicación concurrente, versiones monotónicas, recuperación de archivo y resultados ambiguos, con 0055/0056 y 0061 según evidencia local. La recuperación cuando el SQL permanece caído o hay cargas Pending canceladas continúa pendiente de conciliación controlada. No se ha autorizado purgar datos reales.

### 4.5 Chat V2 personal, legacy y tiempo real

Chat personal P tiene evidencia local de login original, SQL/HTTP/navegador, broker y WebSockets con recuperación por polling, envío, lectura, recibos, idempotencia y revocación. Se archivó la transición legacy preservando historial; no usar legacy como fallback inseguro. El TTL natural del intento fue comprobado con 900 segundos de vigencia y una espera real documentada de 932 segundos, con sesión activa y sin efectos tras vencer.

La evidencia de P no certifica los dominios departamental D o cliente/atención C. El outbox de seguridad durable y la causa de la pausa histórica aproximada de 45,8 segundos siguen abiertos; no se atribuye sin prueba a SqlClient ni a SQL Server.

### 4.6 Correo y otros módulos

Hay evidencia de envío y recepción controlados para contacto, facturas, entrega y despacho de ruta, con límites por propósito, recibos, validaciones y migraciones 0057-0059. No se extrapola esa evidencia a boletas, alertas documentales ni checkout. Las cuentas presupuestarias legacy tienen una ruta de lectura documentada que preserva el presupuesto moderno.

## 5. Trabajo reciente probado solo en copias aisladas

### 5.1 Chat D/C: identidad, recursos y mensajes

Se desarrolló un candidato basado en el login original y capacidades explícitas. En una copia SQL con perfiles y recursos ficticios se probaron: cliente en caso propio y rechazo de caso ajeno; agente sin acceso hasta asignación; gestor con metadata de cola sin contenido; prohibición de autoasignación; transferencia que revoca al agente anterior; membresía departamental, revocación y rechazo de versión antigua. No se inventaron relaciones de personas reales para estas pruebas.

El candidato de mensajes pasó en SQL aislado Prepare, Send, Receipt, retry sin duplicados, History, rechazo de otra sesión y contenido modificado. Se probó el mismo UUID entre P/C y P/D con rechazo de colisión, y P siguió enviando en la copia. La persistencia C# y su composición pasaron sobre SQL real usando `EXECUTE AS` de un usuario QA de base; se comprobó acceso y revocación. Diecinueve huellas instaladas coincidieron con sus fuentes privadas.

**Límites:** no está instalado en principal, no se probó un login SQL técnico independiente ni el camino HTTP/broker/SignalR D/C, no está habilitada la gestión completa en interfaz y no se ensayaron todas las carreras o transferencias durante envío. El registro común de UUID añade un trigger al esquema P y cambia su manifiesto; debe revisarse, actualizarse y validar P de extremo a extremo antes de promoverlo. Los archivos `docs/entrega-stage2/candidatos-referencia/chat-*` son referencia no ejecutable, no una migración oficial.

### 5.2 Boletas

El emisor anterior de boletas puede tener resultado SMTP incierto y riesgo de duplicación si se reintenta indebidamente. Se prepararon contrato, cliente y autoridad nuevos, sin activarlos. En una copia restaurada y con un perfil, empleado y cálculos ficticios autorizados, SQL pasó Request/retry, aislamiento de otra sesión, un solo claim, Receipt Unknown, bloqueo de nuevo UUID y revoke/regrant. Una prueba con dos conexiones en espera simultánea produjo **una sola** autorización de envío y terminó Unknown sin enviar correo.

**Límites:** faltan registro seguro de la autoridad, manifiestos y permisos, integración MVC/API/HTTP, continuidad de UUID en la interfaz, conciliación del historial 0020, corte ordenado del emisor previo y entrega real al buzón de prueba autorizado. No se modificaron salarios o personas reales para estas pruebas. El candidato SQL se incluye únicamente como referencia.

### 5.3 Alertas de documentos

Se preparó un núcleo de notificación con estados y recuperación de pendientes; las pruebas fueron aisladas/en memoria. El emisor anterior basado en MVC/SMTP y generación de alertas nuevas puede dejar sin recuperar envíos fallidos o deshabilitados. Falta persistencia durable, integración, resultado incierto, autorización vigente y entrega real verificada.

## 6. Problemas observados y resolución o estado

- **Desajuste histórico pedidos/DB:** faltaban procedimientos gerenciales y de factura y el procedimiento de vendedor instalado tenía otro contrato. Se reconstruyeron contratos y luego se aplicaron y probaron evoluciones locales comerciales; se mantienen requisitos de conciliación por destino y ledger.
- **Reclamos y garantías:** faltaban procedimientos y los resultsets no coincidían. Se diseñaron contratos y se integraron flujos locales con pruebas QA; las pruebas históricas iniciales no certificaban SQL/E2E.
- **SQL 8141 y 103 en laboratorios:** un CHECK multicolumna se declaró como restricción de columna y una ruta de archivos era excesiva. Se corrigió el candidato y el preflight; en esos primeros runs los casos funcionales no se ejecutaron y no cuentan como PASS.
- **SQL error 2 tras espera:** se investigaron proceso, endpoint y conexiones; no se atribuyó automáticamente a LocalDB. Ensayos posteriores incorporaron observación y conexión restringida retenida. La prueba final de TTL se documentó separadamente.
- **Conversión de @@SPID:** SqlClient devuelve Int16 para smallint; el cast directo fallaba. Se corrigió el harness y se añadieron verificaciones deterministas.
- **Memoria, LocalDB y CHECKDB:** una instancia temporal dejó de aparecer en el registro y hubo presión de memoria; se preservaron archivos y respaldos, se recuperó un entorno acreditado sin sobrescribir principal y la comprobación completa reportó cero errores de asignación y consistencia. Esto valida esa copia, no toda la aplicación.
- **ACL e identidades Windows:** se revisaron permisos efectivos, aislamiento de procesos, DPAPI y certificados con restricciones del sistema. No usar elevación, cuenta owner o cambios globales de confianza como atajos.
- **SMTP 535:** intentos históricos con una credencial fueron rechazados. Los canales que posteriormente tienen evidencia de entrega se distinguen de boletas y alertas, que aún requieren sus pruebas propias; nunca incluir contraseñas en el repositorio o en este informe.
- **Resultados ambiguos:** solicitudes y correos pueden haberse confirmado aunque se pierda la respuesta. Se preservaron UUID y recibos, se bloquearon reenvíos automáticos inseguros y se añadieron controles transaccionales y pruebas negativas según módulo. Para boletas y alertas sigue pendiente activar la solución nueva.
- **Metadata SQL de boletas:** se comprobó que no se otorga VIEW DEFINITION directamente a triggers DML y que una supuesta capacidad de servidor no existe en ese motor. Se ajustaron controles en copia; no se consideró eso una certificación de permisos productivos.
- **Entrega y datos de prueba:** el detector señaló literales sintéticos de pruebas. Se reemplazaron por marcadores y se corrigió una expectativa de identificación ficticia que había quedado desactualizada; la suite completa se volvió a ejecutar y pasó. No se cambiaron credenciales reales.
- **Pausa histórica de broker:** se investigaron cancelación, colas, SQL y presión de host. La causa exacta de la pausa conjunta no quedó demostrada. No aumentar timeouts ni declarar resuelto el problema sin una medición reproducible.

## 7. Matriz de integración sugerida

Estas son recomendaciones de revisión, no autorizaciones para descartar automáticamente cambios del equipo:

| Grupo | Propuesta | Condición para decidir |
| --- | --- | --- |
| Sistema visual, layouts, vistas y componentes Stage 2 | Adoptar con revisión | Comparar con main, conservar información, validar responsive y accesibilidad. |
| Checkpoints y pruebas existentes | Adoptar con ajustes | Mantener evidencia y actualizar pruebas según contrato final; no sumar cifras de corridas distintas. |
| Seguridad, login, Chat P y archivo legacy | Adoptar con ajustes | Comparar permisos, secretos y manifiestos; conservar historia y ejecutar negativas. |
| SQL 0030-0061 y contratos comerciales/documentales | Adoptar con ajustes | Conciliar ledger, hashes y colisiones; probar backup, migración y rollback en copia. |
| Candidatos privados D/C, boletas y alertas | Adoptar con ajustes | Revisar diseño, crear migración oficial nueva, acreditar identidades y probar SQL/HTTP real; no ejecutar los .txt. |
| Cambios exclusivos de main y cuatro vistas anteriores | Mantener y revisar | Revisar procedencia y comparar con el autor antes de combinar; no sobrescribir por defecto. |
| Datos QA, respaldos, claves, logs, capturas privadas y bin/obj | No incorporar como producto | Mantener evidencia privada y recrear fixtures autorizados sin datos reales. |

## 8. Pendientes concretos para cerrar Stage 2

1. Reconciliar la rama con `main`: revisar ambos diffs y colisiones SQL, escoger cambios por objeto y conservar historial; después repetir pruebas afectadas.
2. Completar Chat D/C: migración y manifiestos revisados, proveedor restringido, creación/gestión de casos y departamentos, broker/HTTP/SignalR, interfaz, negativas, concurrencia y revocación. Comprobar que P y su archivo sigan funcionando. No inferir asignaciones reales.
3. Cerrar notificación de boletas: contrato definitivo, UUID por sesión, reconciliación del historial anterior, desplegar con permisos mínimos, probar HTTP y un correo real al destinatario autorizado, incluyendo pérdida de respuesta y reintentos.
4. Completar alertas de documentos y política de notificación de checkout; probar pendientes, autorización revocada, resultado incierto y entrega efectiva.
5. Resolver cargas documentales Pending bajo caída prolongada o cancelación sin borrar archivos o metadatos indebidamente.
6. Definir excepciones a la conservación objetivo de 12 meses y su procedimiento antes de cualquier purga real. Conservar respaldos e historial legal aplicable.
7. Completar recuperación fuera de la PC: ya se verificó restauración SQL separada y lectura del respaldo cifrado, y el usuario confirmó que ve el respaldo y gestor desde su teléfono. Falta comprobar desbloqueo criptográfico y reconstrucción completa en otra computadora; un teléfono por sí solo no restaura SQL Server.
8. Revisar el outbox de seguridad durable y el bloqueo histórico de broker; ejecutar pruebas enfocadas de concurrencia, cancelación y reconexión sin presentar hipótesis como causa demostrada.
9. Ejecutar una regresión integral final con fuentes y configuración definitivas: compilación, pruebas .NET/JS, verificación de secretos y sintaxis SQL, navegación con roles autorizados, acceso negativo, recepción de correo y restauración. Registrar fallos, datos usados y versión exacta.

## 9. Evidencias y límites para el compañero

- Informe histórico de fundamentos: `docs/qa/stage2-foundations-20261005/INFORME.md`; informes por bloque bajo `docs/qa/` y documentación de Stage 2 en `docs/stage2-design-system.md`.
- Fuentes de la entrega: controladores, vistas, servicios, pruebas, `database/migrations`, `shared` y `tools/ChatV2Broker`. Las migraciones tienen sus archivos de verificación y notas de reversión cuando existen; cotejar contra el destino.
- Evidencia reciente de continuidad y límites del broker: 78 pruebas .NET focalizadas; 13 verificaciones offline de identidad/ruteo. Son pruebas de un corte concreto, no la suite completa ni aprobación de seguridad global.
- Verificación previa a esta entrega: restauración de paquetes satisfactoria, compilación Release con cero advertencias y errores, **1.262 pruebas .NET y 133 pruebas JavaScript aprobadas**, más sintaxis SQL de 195 archivos y 1.111 lotes sin errores. El analizador SQL revisa sintaxis, no ejecución ni compatibilidad semántica con una base concreta. La revisión automatizada de secretos del snapshot pasó; no sustituye una revisión humana antes de combinar.
- Pruebas SQL reales en copia: Chat D/C y concurrencia de boletas descritas arriba. El usuario QA fue suplantado con `EXECUTE AS`; no equivale a demostrar un login técnico independiente. HTTP, broker D/C, correo real de boletas y su instalación en principal siguen NOT RUN.
- El inventario de archivos acompaña el informe. Se excluyen material privado y regenerable, no el trabajo funcional: sin `bin`, `obj`, secretos, respaldos, archivos de base, logs/capturas privadas, claves ni el directorio interno de Git. La entrega conserva la historia de origen y los cuatro archivos previamente protegidos en su estado de trabajo, claramente señalados.

## 10. Criterio de aceptación

Concluir Stage 2 requiere que los módulos acordados estén integrados y funcionando con sus roles y permisos, pruebas negativas y evidencia real acordes al riesgo; que migraciones y recuperación se hayan verificado; y que los errores abiertos tengan una resolución o decisión explícita. La expresión «0 % errores» debe interpretarse como cero fallos conocidos pendientes dentro de la validación realizada, no como una garantía absoluta de ausencia de defectos futuros.
