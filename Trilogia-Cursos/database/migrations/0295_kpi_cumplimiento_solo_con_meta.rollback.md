# Reversión de 0295

Restaurar `dbo.sp_Kpi_CumplimientoGlobal` con la definición de `database_Esteban/cu211_213_metas_kpis.sql` (sección 3.4), cuyo resumen global suma todas las ventas con vendedor, tengan meta o no. Es un `CREATE OR ALTER`, no toca datos. Después marcar la fila `0295_kpi_cumplimiento_solo_con_meta` como `RolledBack` en `dbo.SchemaMigrationHistory`. La pantalla Reporte de KPIs tolera la ausencia de la columna `VentaSinMeta` (la lee solo si existe).
