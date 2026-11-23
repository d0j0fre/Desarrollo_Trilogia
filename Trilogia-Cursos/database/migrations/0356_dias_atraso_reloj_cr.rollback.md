# Reversión de 0356

0356 cambió, dentro de la definición vigente de dos funciones, el texto que calculaba los días de atraso de una
factura a crédito vencida con el reloj del servidor SQL (UTC). No cambia tablas ni datos. Para revertir se hace el
reemplazo inverso (leer `OBJECT_DEFINITION`, `REPLACE` y `ALTER`, como en 0341), o se vuelve a ejecutar el
`CREATE OR ALTER` de 0230 (`fn_Cliente_EstadoDeuda`) y 0326 (`fn_Cliente_FacturasVencidas`).

| Objeto | Texto de 0356 | Texto anterior |
|---|---|---|
| `fn_Cliente_EstadoDeuda` | `DATEDIFF(DAY, CAST(dbo.fn_FechaHoraCR(v.VencimientoMasAntiguo) AS DATE), dbo.fn_HoyCR())` | `DATEDIFF(DAY, v.VencimientoMasAntiguo, SYSDATETIME())` |
| `fn_Cliente_FacturasVencidas` | `DATEDIFF(DAY, CAST(dbo.fn_FechaHoraCR(p.Vencimiento) AS DATE), dbo.fn_HoyCR())` | `DATEDIFF(DAY, p.Vencimiento, SYSDATETIME())` |

Después de revertir, marcar la fila `0356_dias_atraso_reloj_cr` como `RolledBack` en `dbo.SchemaMigrationHistory`.
Efecto de revertir: desde las 6:00 p. m. de Costa Rica los días de atraso vuelven a salir con un día de más.
