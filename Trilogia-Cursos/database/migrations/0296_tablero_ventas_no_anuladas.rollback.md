# Reversión de 0296

Restaurar `dbo.sp_Reportes_DashboardKpis` y `dbo.sp_Reportes_DashboardVentasSerie` con las definiciones de `database_Esteban/cu131_reportes_dashboard_gerencial.sql` (bloques 1 y 2), que filtran por `Estado = N'Generada'`. Son `CREATE OR ALTER`; no tocan datos. Marcar después la fila `0296_tablero_ventas_no_anuladas` como `RolledBack` en `dbo.SchemaMigrationHistory`. Ojo: el panel móvil de gestión usa los mismos procedimientos y volvería a mostrar el total anterior.
