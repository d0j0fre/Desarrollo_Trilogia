# Reversión de 0341

0341 cambió, dentro de la definición vigente de cada objeto, el texto que calculaba «hoy» con el reloj del
servidor SQL. No cambia tablas ni datos. Para revertir se hace el reemplazo inverso (con el mismo método de
0341: leer `OBJECT_DEFINITION`, `REPLACE` y `ALTER`), o se vuelve a ejecutar el `CREATE OR ALTER` de la última
migración que definió el objeto (0221, 0230, 0236, 0250, 0257, 0263, 0300-0328 según el caso).

| Objeto | Texto de 0341 | Texto anterior |
|---|---|---|
| `sp_Asistente_Metricas` | `dbo.fn_HoyCR()` · `CAST(dbo.fn_FechaHoraCR(FechaFactura) AS DATE)` · `CAST(dbo.fn_FechaHoraCR(FechaPedido) AS DATE)` | `CAST(SYSDATETIME() AS DATE)` · `CAST(FechaFactura AS DATE)` · `CAST(FechaPedido AS DATE)` |
| `sp_Chofer_ResumenDia` | `dbo.fn_HoyCR()` · `CAST(dbo.fn_FechaHoraCR(rp.FechaEntrega) AS DATE)` | `CAST(SYSDATETIME() AS DATE)` · `CAST(rp.FechaEntrega AS DATE)` |
| `sp_FacturasVencidas_GenerarAvisos` | `dbo.fn_HoyCR()` · `CAST(dbo.fn_FechaHoraCR(n.FechaCreacion) AS DATE)` | `CAST(SYSDATETIME() AS DATE)` · `CAST(n.FechaCreacion AS DATE)` |
| `sp_Flota_Alertas`, `sp_Mantenimiento_Create`, `sp_Mantenimiento_MarcarCompletada`, `sp_VehiculoDocumento_List`, `sp_Promociones_List`, `sp_Promociones_Vigentes`, `sp_Comodato_List` | `dbo.fn_HoyCR()` | `CAST(SYSDATETIME() AS DATE)` |
| `fn_Comodato_Rentabilidad` | `dbo.fn_HoyCR()` · `CAST(ISNULL(dbo.fn_FechaHoraCR(compras.UltimaCompra), c.FechaAsignacion) AS DATE)` · `p.FechaPedido >= dbo.fn_DiaCRaReloj(c.FechaAsignacion)` | `CAST(SYSDATETIME() AS DATE)` · `CAST(ISNULL(compras.UltimaCompra, c.FechaAsignacion) AS DATE)` · `p.FechaPedido >= c.FechaAsignacion` |
| `sp_Store_CreateOrderWithPromotions`, `sp_Admin_GetPurchaseSuggestions`, `sp_Admin_GetSlowMovingProducts` | `dbo.fn_HoyCR()` | `CONVERT(DATE, SYSDATETIME())` |
| `sp_Movil_Bodega_PedidosPorPreparar` | `CAST(dbo.fn_FechaHoraCR(pp.FechaPreparacion) AS DATE) = dbo.fn_HoyCR()` | `CAST(pp.FechaPreparacion AS DATE) = CAST(SYSDATETIME() AS DATE)` |
| `sp_Admin_UpdateEmployeeLeaveRequestStatus` | `dbo.fn_HoyCR()` | `CONVERT(DATE, GETDATE())` |

Después de revertir, marcar la fila `0341_reloj_costa_rica_hoy` como `RolledBack` en `dbo.SchemaMigrationHistory`.
Efecto de revertir: «hoy» vuelve a cambiar a las 6 p. m. de Costa Rica en Azure.

Restaurar un BACPAC es el último recurso.
