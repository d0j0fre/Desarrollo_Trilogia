# Rollback de 0220

## Qué introduce

- Procedimiento nuevo `dbo.sp_Pedidos_SincronizarEstadoEntrega` (BUG-10-06).
  Ningún procedimiento ni tabla existente cambia.
- Efecto en datos: desde que la API lo invoca, `dbo.Pedidos.Estado` pasa a
  `EnRuta` al despachar la ruta o marcar la parada En ruta, y a `Fallido` al
  marcarla No entregado. Son los nombres del CHECK de
  `dbo.RutaPedidos.EstadoEntrega`.

## Revertir

```sql
DROP PROCEDURE IF EXISTS dbo.sp_Pedidos_SincronizarEstadoEntrega;
DELETE FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0220_pedido_estado_desde_entrega';
```

Antes de borrarlo, desplegar una API que ya no lo invoque: la API actual lo
llama después de cambiar una entrega y respondería 503.

Los pedidos que ya quedaron en `EnRuta` o `Fallido` no se devuelven solos a
`Pendiente`. Si hiciera falta (no se recomienda: perdería la trazabilidad):

```sql
UPDATE dbo.Pedidos SET Estado = N'Pendiente', FechaActualizacion = SYSDATETIME()
WHERE Estado IN (N'EnRuta', N'Fallido');
```
