# Reversión de 0273

Eliminar `dbo.sp_Admin_GetAuditLogsUnificado` y `dbo.sp_Admin_GetAuditFilterOptions` (`DROP PROCEDURE`). Solo son lecturas: no hay datos que restaurar y `dbo.sp_Admin_GetAuditLogs` (que usa el panel móvil) no se modificó. Antes de revertir hay que volver a publicar la versión anterior de la pantalla Auditoría, que solo llama a `sp_Admin_GetAuditLogs`. Marcar después la fila `0273_auditoria_unificada` como `RolledBack` en `dbo.SchemaMigrationHistory`.
