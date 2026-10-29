# Gate operativo de Chat V2 personal

Este documento describe el candidato; no autoriza instalación ni activa configuración. El SQL aplicado 0044 permanece inmutable. La evidencia de CHECKDB de la copia completa M3F se conserva fuera del repositorio y no debe repetirse para acreditar cambios de C#.

## Composición

| Proceso | Configuración privada | Alcance |
|---|---|---|
| MVC | DefaultConnection y ChatV2App = APP; ChatV2Reader = Reader | Sesión y operaciones autorizadas de presentación/P; no emisión ni lectura de hashes de contraseñas |
| API de autenticación | DefaultConnection = Auth; OriginalLoginIssuer = Issuer | Verificar credenciales y emitir login original solo a partir del usuario autenticado |
| Bootstrap de instalación | Fuera de los procesos de aplicación | Solo durante despliegue autorizado, nunca como conexión MVC/API |

MVC requiere `OriginalLogin:RemoteAuthority=true`. La API requiere `OriginalLogin:AuthorityEnabled=true`. El login MVC envía al API la huella de su sesión/nonce; la autoridad resuelve actor/perfil/stamp mediante credenciales y SQL los vuelve a comprobar al emitir. MVC comprueba el ticket contra SQL y su vínculo antes de instalar la sesión. No adopta un ticket ausente, otra cuenta, otra vigencia ni emite localmente como fallback. La vigencia sigue siendo ocho horas absolutas; parámetros temporales datetime2(7) y TTL de intentos no cambian.

Los consumidores API que no solicitan emisión mantienen el contrato de autenticación anterior, sin ticket. Pedir emisión con contexto inválido o autoridad ausente falla cerrado. El transporte MVC→API para P debe ser HTTPS en el mismo host, con certificado validado; la configuración no admite un destino remoto no aprobado.

Al activar las opciones, ambos hosts ejecutan un preflight SQL efectivo antes de aceptar solicitudes. Se rechazan Windows integrado en estas conexiones, owner/sysadmin y roles amplios, EXECUTE global/de esquema, SELECT directo del almacenamiento privado, y capacidades cruzadas de credenciales, emisión, lectura y escritura de P. No se conceden permisos para superar ese preflight.

**La separación de roles SQL y variables de entorno no demuestra aislamiento Windows.** Si MVC y autoridad comparten SID, pueden compartir acceso a archivos, procesos y DPAPI CurrentUser. El despliegue debe acreditar las identidades de proceso, custodia de secretos/claves, ACL y recuperación correspondientes. No crear cuentas ni cambiar ACL del host como efecto secundario de instalar 0044.

## Legacy y transportes

`ChatV2:LegacyReadOnly=true` es obligatorio al activar P y se conserva al desactivarlo. El filtro de recursos del controlador legacy permite las lecturas históricas y la búsqueda existente bajo su autorización, y rechaza mutaciones antiguas o futuras. El transporte `/chatHub` no se publica mientras ese cierre esté activo. La composición no registra SignalR V2 ni dispatcher/broker como alternativa.

No se copian, borran ni reasignan historiales antiguos; no se presentan como mensajes de las tablas V2. El adaptador P directo y polling no certifican broker/SignalR ni reemplazan el contrato pendiente de fronteras de procesos. D/C permanecen pendientes; este cierre no aprueba su exclusión del alcance de Stage 2.

## Antes de solicitar aprobación de principal

1. Identidad física exacta de TrilogiaDev/DistribuidoraJJ_DB; ledger completo, colisiones por ID+hash y ausencia de una instalación 0044 incompatible.
2. Respaldo COPY_ONLY/CHECKSUM nuevo, SHA256, VERIFYONLY y restauración a archivos nuevos en la instancia de laboratorio existente. Comparar ledger y módulos con principal, preservar la restauración; no aplicar SQL histórico global.
3. Reutilizar rollback transaccional 0044 de M3F, probar los cambios nuevos en M3F con APP/Reader/Auth/Issuer efectivos y registrar qué mutaciones históricas sí se ejecutaron.
4. Identidades de proceso y custodia/recuperación de secretos; compatibilidad real del método de autenticación de principal. No dar por compatibles dos instancias solo por compartir versión.
5. Aprovisionamiento exacto por objeto/columna, sin roles amplios y sin credenciales dentro de scripts/documentación. Configuración segura por host y claves persistentes recuperables.
6. Aprobación específica del usuario para crear logins/grants en principal, instalar 0044 y activar P. Si existe un P0, mantener NO-GO; no pedir aprobación para saltarlo.

## Instalación y retirada previstas

Con gates aprobados, una ventana controlada acreditará nuevamente destino/ledger/backup, aprovisionará las identidades revisadas, aplicará el envoltorio 0044 exacto con su SHA256 y verificará esquema/ledger. La configuración se activa al final; nunca usar bootstrap para conseguir el primer login.

Ante un fallo de instalación, rollback transaccional de cuerpo+ledger. Ante un fallo posterior, detener nuevos envíos, desactivar P conservando LegacyReadOnly, cerrar procesos/pools y revocar accesos técnicos recién habilitados según el plan autorizado. Preservar mensajes, intenciones, recibos y auditoría; no DROP ni restauración sobre principal como rollback automático. Toda restauración exige conservar actividad posterior, ventana y aprobación independiente.

Retención de doce meses, excepciones, transición integral legacy, D/C, autoridad comercial y delegaciones documentales mantienen sus gates independientes. No se ejecutan purgas.
