# Reversión de 0335

Aditiva: una tabla nueva y procedimientos nuevos; solo `sp_DocumentAlert_RegisterNotification` se redefine.

1. Devolver `dbo.sp_DocumentAlert_RegisterNotification` a la versión de `0008_document_expiration_alerts.sql` (ejecutar solo su `CREATE OR ALTER PROCEDURE`). Con eso un intento de correo fallido ya no se reintenta.
2. `DROP PROCEDURE dbo.sp_DocumentAlert_PendingNotifications, dbo.sp_FlotaDocumentos_AvisosPendientes, dbo.sp_FlotaDocumentos_RegistrarAviso;`
3. `DROP TABLE dbo.VehiculoDocumentoAvisos;` (solo guarda el registro de avisos de correo ya enviados; borrarla hace que, si se vuelve a desplegar el sitio nuevo, se reenvíen los avisos vigentes).
4. Marcar la fila `0335_correo_alertas_pendientes` como `RolledBack` en `dbo.SchemaMigrationHistory`.

El sitio web publicado con esta rama llama a esos procedimientos: al revertir la base hay que volver también a la versión anterior del sitio (o dejar `DocumentAlerts__EmailEnabled` en `false`, con lo que el sitio no los invoca).

Restaurar un BACPAC es el último recurso.
