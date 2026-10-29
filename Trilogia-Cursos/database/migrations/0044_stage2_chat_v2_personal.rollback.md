# 0044 — candidato de Chat V2 personal

Preparado, sin instalar en la principal. El número queda sujeto a reconciliar el ledger real y las colisiones históricas; no reemplaza ninguna migración anterior.

El cuerpo se probó inicialmente en una base mínima. M3F ejecutó después el envoltorio completo sobre una restauración acreditada de principal, con rollback transaccional de un fallo provocado, ledger y verificador reales. El CHECKDB completo de esa copia finalizó sin errores el 18 de octubre de 2026. Esa evidencia no instala ni acredita automáticamente la composición de principal. El envoltorio exige hashes exactos de 0030, 0034, 0036 y 0043; esos controles no sustituyen comprobar el ledger actual, las colisiones históricas y los consumidores.

## Condiciones de promoción

1. Acreditar el ledger y esquema reales, versiones de los guards, claves, restricciones y ausencia de otro `rowversion` en Usuarios. Reconciliar cualquier conflicto sin reinterpretar hashes.
2. Respaldo CHECKSUM actual, VERIFYONLY y restauración aislada viable. CHECKDB de la base mínima no certifica la copia comercial histórica.
3. Probar el envoltorio completo, rollback transaccional, concurrencia y SQL/HTTP en una composición equivalente a la principal. No rellenar un ledger ficticio para superar este requisito.
4. Acreditar identidades SQL restringidas y separar despliegue/APP/Reader/emisión. M3F comprobó logins SQL con usuarios mapeados en una copia completa NONE; esa alternativa evita habilitar autenticación contenida y convertir la base. No modificar seguridad de la principal sin aprobación. No usar Windows integrado/owner como APP para superar un gate.
5. MVC recibe únicamente `DefaultConnection`/`ChatV2App` APP y `ChatV2Reader`, con `OriginalLogin:RemoteAuthority=true`. La API de autenticación recibe Auth como `DefaultConnection` y `OriginalLoginIssuer`, con `OriginalLogin:AuthorityEnabled=true`. No entregar Issuer a MVC. Mantener `ChatV2:PersonalSqlEnabled=false` hasta superar los gates y aprobar la promoción. Al habilitar P es obligatorio `ChatV2:LegacyReadOnly=true`; ese cierre persiste durante rollback aunque se desactive P. Los secretos y claves protegidas viven fuera del repositorio.

## Permisos mínimos del adaptador

| Identidad | EXECUTE autorizado |
|---|---|
| APP Chat | ChatV2P.OpenPrivate, Prepare, ExecuteIntent, Leave |
| Reader | ChatV2P.Resolve, Directory, Own, Membership, History |
| Emisor de login original | dbo.sp_Stage2_CreateOriginalLogin |
| Sesión MVC | dbo.sp_Stage2_ValidateOriginalLogin, sp_Stage2_RevokeOriginalLogin |

No conceder acceso directo a Intents, Messages, Requests, Outbox ni Stage2OriginalLogins. No conceder EXECUTE en RequireAccess ni en un esquema completo. Autenticación por credenciales y consultas de navegación conservan sus contratos y grants específicos; no se resuelven con SELECT general. Dispatcher y SignalR V2 no se habilitan en este alcance. El laboratorio prueba separación de permisos SQL; no certifica separación de procesos/privilegios Windows ni broker.

M3F y Maestro 4 acreditaron los permisos y operaciones expresamente registrados; los 284 checks efectivos no certifican todas las mutaciones históricas. No sustituir las conexiones compartidas hasta completar el conjunto pertinente y su conservación. El arranque opt-in valida configuración y permisos efectivos contra SQL, rechazando owner, roles amplios y cruces entre autoridades. Ese control no acredita aislamiento de procesos Windows ni sustituye las pruebas funcionales. Ver `docs/chat-v2-personal-promotion-gate.md`.

## Retirada segura

Ante fallo de instalación, una transacción abarca cuerpo y ledger; no confirmar resultados parciales. Ante fallo funcional posterior, deshabilitar el opt-in y detener nuevos envíos V2, preservando mensajes, UUID, recibos y auditoría. Conservar `ChatV2:LegacyReadOnly=true`: desactivar P no debe reabrir escritores ni transporte legacy. No borrar tablas, historial ni el nuevo rowversion como rollback automático. Una restauración exige ventana y autorización: primero conservar evidencia y datos posteriores al respaldo. No se establece borrado automático de doce meses.

La pareja personal es única y su cierre incrementa AccessVersion y deniega a ambos participantes. No existe reactivación automática ni reasignación de su historial. D/C requieren contratos separados y permanecen cerrados.
