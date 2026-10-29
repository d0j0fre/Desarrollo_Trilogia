# Reversión operativa de avisos de entrega

Deshabilitar `DeliveryNotifications:Enabled` en MVC y API y detener ordenadamente la autoridad antes de cambiar permisos. Restaurar la configuración y los binarios anteriores acreditados; no reactivar el envío SMTP directo desde MVC como alternativa.

Revocar únicamente EXECUTE de `DeliveryNotifications.Request`, `Claim`, `Complete` y `Receipt` a la identidad técnica acreditada. La migración no crea usuarios, logins, grants ni capacidades de perfiles, y no modifica el procedimiento que confirma entregas.

Conservar `Requests`, `Attempts`, `Events`, sus restricciones, triggers y el ledger Applied. No eliminar ni alterar UUID, propietario original, recibos ni auditoría. Una reversión SQL posterior requiere una nueva migración revisada; no ejecutar DROP, TRUNCATE ni restaurar sobre principal.

`SyncGuid` identifica el evento de negocio ya confirmado; `OperationId` es otro UUID, propiedad de la sesión original que solicitó el correo. Los estados Sending/Unknown no regresan a Pending ni habilitan reintento automático. `SmtpAccepted` acredita aceptación del servidor, no entrega al buzón.

Antes de instalar: respaldo CHECKSUM, VERIFYONLY, restauración separada acreditada, hash/prerrequisitos y ensayo con identidad restringida. El rollback operativo no revierte la entrega física, el pedido, inventario, facturación, asignaciones ni correos ya aceptados.
