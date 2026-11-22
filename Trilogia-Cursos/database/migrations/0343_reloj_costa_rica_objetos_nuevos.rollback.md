# Reversión de 0343

0343 cambió, dentro de la definición vigente de tres objetos, el texto que calculaba «hoy» con el reloj del
servidor SQL. No cambia tablas ni datos. Para revertir se hace el reemplazo inverso (leer `OBJECT_DEFINITION`,
`REPLACE` y `ALTER`, como en 0341), o se vuelve a ejecutar el `CREATE OR ALTER` de la migración que definió el
objeto (0331 para `fn_Promociones_Aplicar`, 0335 para los otros dos).

| Objeto | Texto de 0343 | Texto anterior |
|---|---|---|
| `fn_Promociones_Aplicar` | `dbo.fn_HoyCR() BETWEEN promotion.FechaInicio AND promotion.FechaFin` | `CONVERT(DATE, SYSDATETIME()) BETWEEN promotion.FechaInicio AND promotion.FechaFin` |
| `sp_DocumentAlert_PendingNotifications`, `sp_FlotaDocumentos_AvisosPendientes` | `DECLARE @Hoy DATE = dbo.fn_HoyCR();` | `DECLARE @Hoy DATE = CAST(SYSDATETIME() AS DATE);` |

Después de revertir, marcar la fila `0343_reloj_costa_rica_objetos_nuevos` como `RolledBack` en `dbo.SchemaMigrationHistory`.
Efecto de revertir: la vigencia de las promociones y los avisos de vencimiento vuelven a cambiar de día a las 6 p. m. de Costa Rica en Azure.

Restaurar un BACPAC es el último recurso.
