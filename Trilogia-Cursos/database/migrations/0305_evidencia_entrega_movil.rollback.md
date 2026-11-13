# Rollback de 0305

## Qué introduce

- `dbo.EntregaEvidencias.SyncGuid` (UNIQUEIDENTIFIER NULL) e índice único filtrado
  `UX_EntregaEvidencias_SyncGuid`. Lo llena solo la API móvil; las evidencias de la web quedan en NULL.
- `sp_Entrega_RegisterEvidence`: recibe `@SyncGuid` opcional (por defecto NULL). Con el parámetro
  omitido se comporta exactamente como en 0004.
- `sp_Chofer_PrepararEvidencia`: valida que la parada sea del chofer del token y que la ruta esté
  Despachada (o Completada hace 48 horas o menos), y reconoce el reenvío de un archivo por su `SyncGuid`.

## Revertir

Antes de quitar la columna, la API móvil debe estar en una versión que no la use (o la web sola
funciona igual: no la usa). Orden:

```sql
BEGIN TRANSACTION;
DROP PROCEDURE IF EXISTS dbo.sp_Chofer_PrepararEvidencia;
-- Restaurar sp_Entrega_RegisterEvidence a la definición de 0004_private_delivery_evidence.sql
-- (CREATE OR ALTER con esa definición: sin @SyncGuid ni la columna en el INSERT).
DROP INDEX IF EXISTS UX_EntregaEvidencias_SyncGuid ON dbo.EntregaEvidencias;
ALTER TABLE dbo.EntregaEvidencias DROP COLUMN IF EXISTS SyncGuid;
DELETE FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0305_evidencia_entrega_movil';
COMMIT TRANSACTION;
```

Las evidencias ya registradas desde la app se conservan (fila y archivo); solo pierden el identificador
de sincronización. Sin `sp_Chofer_PrepararEvidencia` el endpoint
`POST api/mobile/v1/driver/deliveries/{id}/evidence` responde 503: revertir también el código o
reaplicar la 0305. Los archivos en disco no se tocan.
