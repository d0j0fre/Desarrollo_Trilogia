# Reversión operativa de UUID de Reclamos

Detener nuevas altas de Reclamos antes de volver a binarios anteriores. El contrato
`dbo.sp_Reclamos_Create` mantiene su nombre y exige el contexto completo del login
original y un UUID; consumidores anteriores fallan cerrado por firma incompatible.
No reinstalar 0033 ni habilitar un escritor sin UUID como alternativa.

Conservar `ComplaintRequests.Receipts`, su trigger inmutable, cada reclamo,
auditoría y el ledger Applied. No eliminar ni modificar recibos para permitir
reenvíos. Una corrección SQL posterior usa una migración nueva revisada.
Los registros anteriores no reciben UUID ni propietario de sesión inferidos.

Antes de instalar: acreditar destino, hashes exactos 0033/0036, huellas de módulos,
respaldo CHECKSUM, VERIFYONLY y restauración separada. Ensayar con APP restringida
los casos mismo UUID/sesión/cuerpo, respuesta perdida, otra sesión del mismo actor,
otro actor, cambio de contenido, revoke/regrant y concurrencia. Confirmar una sola
fila de Reclamos, receipt y auditoría. ScriptDom no valida comportamiento SQL.

La migración no crea identidades ni grants. Conservar EXECUTE sobre el procedimiento
existente; APP no necesita acceso directo a la tabla de recibos ni permiso sobre
helpers. El aprovisionamiento y manifiesto de arranque deben acreditar el contrato
0060 y retirar cualquier permiso amplio preexistente mediante operación revisada.
No restaurar sobre principal ni purgar datos como rollback automático.
