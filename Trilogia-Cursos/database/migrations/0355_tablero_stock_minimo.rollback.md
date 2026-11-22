# Reversión de 0355

0355 cambió, dentro de la definición vigente de tres procedimientos, el texto que contaba el stock bajo y
agotado con la columna `EstadoStock` (tope fijo de 5 unidades). No cambia tablas ni datos. Para revertir se
hace el reemplazo inverso (leer `OBJECT_DEFINITION`, `REPLACE` y `ALTER`, como en 0341), o se vuelve a
ejecutar el `CREATE OR ALTER` de 0296 (tablero), 0297 (detalle) y 0250 (Asistente) con los cambios de 0341.

| Objeto | Texto de 0355 | Texto anterior |
|---|---|---|
| `sp_Reportes_DashboardKpis`, `sp_Asistente_Metricas` | `(Stock > 0 AND Stock <= StockMinimo)` · `Stock <= 0` | `EstadoStock = N'Bajo'` · `EstadoStock = N'Agotado'` |
| `sp_Reportes_DashboardDetalle` | `(Stock <= 0 OR Stock <= StockMinimo)` · `CASE WHEN Stock <= 0 THEN N'Agotado' ELSE N'Bajo' END` | `EstadoStock IN (N'Bajo', N'Agotado')` · `EstadoStock` |

Después de revertir, marcar la fila `0355_tablero_stock_minimo` como `RolledBack` en `dbo.SchemaMigrationHistory`.
Efecto de revertir: el tablero vuelve a contar como «bajo» todo producto con 5 unidades o menos.
