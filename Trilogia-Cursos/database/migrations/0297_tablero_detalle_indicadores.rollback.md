# Reversión de 0297

`DROP PROCEDURE dbo.sp_Reportes_DashboardDetalle;` Nada más depende de él salvo la pantalla Tablero gerencial (`ReportsDbService.GetDashboardDetailAsync`); sin el procedimiento los indicadores dejan de abrir su detalle y el resto del tablero sigue funcionando. Marcar la fila `0297_tablero_detalle_indicadores` como `RolledBack` en `dbo.SchemaMigrationHistory`.
