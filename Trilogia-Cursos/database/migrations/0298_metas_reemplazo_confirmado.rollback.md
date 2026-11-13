# Reversión de 0298

Restaurar `dbo.sp_Metas_Upsert` con la definición de `database_Esteban/cu211_213_metas_kpis.sql` (sección 3.2), que actualiza la meta existente sin pedir confirmación (sin el parámetro `@Reemplazar`). `KpiDbService.UpsertMetaAsync` envía `@Reemplazar`; antes de revertir hay que quitar ese parámetro de la llamada o el procedimiento restaurado lo rechazará. Es un `CREATE OR ALTER`; no toca datos. Marcar la fila `0298_metas_reemplazo_confirmado` como `RolledBack` en `dbo.SchemaMigrationHistory`.
