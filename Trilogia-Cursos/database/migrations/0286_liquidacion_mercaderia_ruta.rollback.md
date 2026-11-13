# Rollback de 0286

## Qué introduce

- Tablas nuevas `dbo.RutaLiquidacionMercaderia` (una por ruta) y
  `dbo.RutaLiquidacionMercaderiaDetalle` (producto por pedido no entregado, con el
  movimiento de inventario que generó).
- Procedimientos nuevos `sp_RutaMercaderia_Preparar`, `sp_RutaMercaderia_Registrar` y
  `sp_RutaMercaderia_List`.
- No modifica tablas, procedimientos ni datos existentes. Al registrar una liquidación
  actualiza `Productos.Stock`, agrega movimientos `Entrada` a `MovimientosInventario` y marca
  `Rutas.Liquidada = 1` (igual que `sp_Rutas_Liquidar`).

## Revertir

```sql
DROP PROCEDURE IF EXISTS dbo.sp_RutaMercaderia_List;
DROP PROCEDURE IF EXISTS dbo.sp_RutaMercaderia_Registrar;
DROP PROCEDURE IF EXISTS dbo.sp_RutaMercaderia_Preparar;
DROP TABLE IF EXISTS dbo.RutaLiquidacionMercaderiaDetalle;
DROP TABLE IF EXISTS dbo.RutaLiquidacionMercaderia;
DELETE FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0286_liquidacion_mercaderia_ruta';
```

Las liquidaciones ya registradas dejaron stock y movimientos de inventario que **no** se
revierten (son datos de negocio); las rutas siguen marcadas como liquidadas. Si hay que
deshacer una liquidación concreta, registrar antes un movimiento `Salida` por la misma
cantidad y poner `Rutas.Liquidada = 0` de esa ruta. Sin los procedimientos, la pantalla
«Liquidación de mercadería» falla con el mensaje genérico: revertir también el código o
reaplicar la 0286.
