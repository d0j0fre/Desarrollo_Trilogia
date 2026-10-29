# Reversión operativa de notificaciones de factura

Deshabilitar `InvoiceNotifications:Enabled` en MVC y API y detener ordenadamente la autoridad antes de cambiar sus permisos. Restaurar la configuración y los binarios anteriores acreditados; no reactivar SMTP en MVC como alternativa.

Revocar únicamente los cuatro permisos EXECUTE de `InvoiceNotifications.Request`, `Claim`, `Complete` y `Receipt` concedidos a la identidad técnica acreditada de notificaciones. Mantener separadas Auth, Issuer, Pricing y MVC. La migración no crea usuarios, logins, grants ni credenciales.

Conservar `Requests`, `Attempts`, `Events`, sus restricciones, triggers y el ledger Applied. No eliminar ni alterar recibos, propietarios, UUID ni auditoría. Una reversión SQL posterior requiere una nueva migración revisada; no ejecutar DROP, TRUNCATE ni restaurar sobre principal.

Los estados Sending/Unknown nunca vuelven a Pending ni habilitan reintento automático. El servidor SMTP puede haber aceptado el mensaje aunque se pierda su respuesta. Una aceptación SMTP no se puede deshacer y no acredita entrega al buzón. Una copia adicional requiere una nueva intención explícita y la política operativa correspondiente.

Antes de la instalación: respaldo CHECKSUM, VERIFYONLY, restauración separada acreditada, verificación de hash/prerrequisitos y ensayo con la autoridad restringida. Preservar respaldos y evidencias durante la reversión.
