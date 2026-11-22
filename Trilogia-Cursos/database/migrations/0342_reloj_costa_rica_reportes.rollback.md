# Reversión de 0342

0342 cambió, dentro de la definición vigente de cada objeto, las comparaciones de marcas (`FechaFactura`,
`FechaPedido`, `FechaMovimiento`, `FechaEntrega`) con los días que escribe el usuario. No cambia tablas ni datos.
Para revertir se hace el reemplazo inverso (leer `OBJECT_DEFINITION`, `REPLACE` y `ALTER`, como en 0341).

| Texto de 0342 | Texto anterior |
|---|---|
| `dbo.fn_HoyCR()` (en los tableros) | `CAST(GETDATE() AS DATE)` |
| `col >= dbo.fn_DiaCRaReloj(@Desde) AND col < dbo.fn_DiaCRaReloj(DATEADD(DAY, 1, @Hasta))` | `col >= @Desde AND col < DATEADD(DAY, 1, @Hasta)` |
| `FechaMovimiento >= dbo.fn_DiaCRaReloj(@Desde)` · `FechaMovimiento < dbo.fn_DiaCRaReloj(DATEADD(DAY, 1, CAST(@Hasta AS DATETIME2)))` | `FechaMovimiento >= @Desde` · `FechaMovimiento < DATEADD(DAY, 1, CAST(@Hasta AS DATETIME2))` |
| `CAST(dbo.fn_FechaHoraCR(col) AS DATE)`, `CONVERT(DATE,dbo.fn_FechaHoraCR(invoice.FechaFactura))` | `CAST(col AS DATE)`, `CONVERT(DATE,invoice.FechaFactura)` |
| `YEAR(dbo.fn_FechaHoraCR(FechaFactura)) = YEAR(dbo.fn_AhoraCR())` y `MONTH(...)` equivalente | `YEAR(FechaFactura) = YEAR(GETDATE())` y `MONTH(FechaFactura) = MONTH(GETDATE())` |
| `YEAR(dbo.fn_FechaHoraCR(f.FechaFactura)) = @Anio AND MONTH(dbo.fn_FechaHoraCR(f.FechaFactura)) = @Mes` | `YEAR(f.FechaFactura) = @Anio AND MONTH(f.FechaFactura) = @Mes` |
| `FORMAT(dbo.fn_FechaHoraCR(FechaFactura), 'yyyy-MM')`, `MONTH(dbo.fn_FechaHoraCR(invoice.FechaFactura))` | `FORMAT(FechaFactura, 'yyyy-MM')`, `MONTH(invoice.FechaFactura)` |

Objetos: `sp_Reportes_DashboardKpis`, `sp_Reportes_DashboardDetalle`, `sp_Reportes_DashboardVentasSerie`,
`sp_Reportes_DesempenoVendedores`, `sp_Reportes_VentasDetallado`, `sp_Admin_GetInventoryMovements`,
`sp_Movil_Gestion_Operacion`, `sp_Admin_DashboardSummary`, `sp_Admin_SalesSummary`, `sp_Admin_GetMonthlySales`,
`sp_Admin_GetSeasonalSalesTrend`, `sp_Kpi_CumplimientoGlobal`, `sp_Metas_List`, `sp_Metas_MiProgreso`.

Después de revertir, marcar la fila `0342_reloj_costa_rica_reportes` como `RolledBack` en `dbo.SchemaMigrationHistory`.
Efecto de revertir: ventas, metas y reportes vuelven a agrupar por día/mes UTC en Azure.

Restaurar un BACPAC es el último recurso.
