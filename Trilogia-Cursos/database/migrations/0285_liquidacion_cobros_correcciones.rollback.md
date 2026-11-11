# Rollback de 0285

## Qué introduce

- `dbo.RutaLiquidacionCobros`: columnas `ChoferUsuarioId`, `Anulada`, `FechaAnulacion`,
  `AnuladaPorUsuarioId`, `AnuladaPorNombre`, `MotivoAnulacion`. La restricción única
  `UQ_RutaLiquidacionCobros_Ruta` se reemplaza por el índice único filtrado
  `UX_RutaLiquidacionCobros_RutaVigente` (una liquidación **vigente** por ruta).
- Tabla nueva `dbo.ChoferFaltantes` (faltante a cargo del chofer, uno por liquidación).
- `sp_LiquidacionCobros_Preparar`, `_Registrar` y `_List` (columnas nuevas al final; el
  registro solo acepta rutas `Completada`, valida el detalle contra el total y crea el
  faltante del chofer) y procedimientos nuevos `sp_LiquidacionCobros_Anular`,
  `sp_LiquidacionCobros_GetRecibo` y `sp_ChoferFaltantes_List`.

## Revertir

Antes de volver a la restricción única hay que decidir qué hacer con las liquidaciones
anuladas (la restricción original no admite dos filas por ruta):

```sql
-- 1. Si hay liquidaciones anuladas, respaldarlas y quitarlas.
SELECT * INTO dbo.RutaLiquidacionCobros_Anuladas_0285 FROM dbo.RutaLiquidacionCobros WHERE Anulada = 1;
DELETE FROM dbo.ChoferFaltantes WHERE LiquidacionCobrosId IN (SELECT LiquidacionCobrosId FROM dbo.RutaLiquidacionCobros WHERE Anulada = 1);
DELETE c FROM dbo.RutaLiquidacionComprobantes c
  INNER JOIN dbo.RutaLiquidacionCobros l ON l.LiquidacionCobrosId = c.LiquidacionCobrosId WHERE l.Anulada = 1;
DELETE FROM dbo.RutaLiquidacionCobros WHERE Anulada = 1;

-- 2. Restaurar la restricción y quitar lo nuevo.
DROP INDEX UX_RutaLiquidacionCobros_RutaVigente ON dbo.RutaLiquidacionCobros;
ALTER TABLE dbo.RutaLiquidacionCobros ADD CONSTRAINT UQ_RutaLiquidacionCobros_Ruta UNIQUE (RutaId);
DROP TABLE dbo.ChoferFaltantes;
ALTER TABLE dbo.RutaLiquidacionCobros DROP CONSTRAINT DF_LiqCob_Anulada;
ALTER TABLE dbo.RutaLiquidacionCobros DROP COLUMN ChoferUsuarioId, Anulada, FechaAnulacion, AnuladaPorUsuarioId, AnuladaPorNombre, MotivoAnulacion;
DROP PROCEDURE IF EXISTS dbo.sp_LiquidacionCobros_Anular;
DROP PROCEDURE IF EXISTS dbo.sp_LiquidacionCobros_GetRecibo;
DROP PROCEDURE IF EXISTS dbo.sp_ChoferFaltantes_List;
DELETE FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0285_liquidacion_cobros_correcciones';
```

3. Volver a crear `sp_LiquidacionCobros_Preparar`, `_Registrar` y `_List` con su definición
   anterior (`database_Esteban/cu106_liquidacion_financiera.sql`, secciones 3.1 a 3.3).

Se pierden los faltantes cargados a choferes y los motivos de anulación. Sin los
procedimientos nuevos, el recibo, la anulación y la lista de faltantes de la pantalla fallan
con el mensaje genérico: revertir también el código o reaplicar la 0285.
