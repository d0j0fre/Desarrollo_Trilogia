# Rollback de 0288

## Qué introduce

- `dbo.VehiculoDocumentos.DadoDeBaja` y `FechaBaja` (la baja conserva la fila) y el índice
  filtrado `IX_VehiculoDocumentos_Vehiculo_Tipo`.
- Procedimientos nuevos `sp_VehiculoDocumento_List`, `_GetById`, `_Update` y `_Baja`;
  `sp_VehiculoDocumento_Create` (ahora rechaza una fecha vacía o un duplicado exacto).
- `sp_Flota_Alertas` (solo documentos vigentes; alertas de mantenimiento en proceso,
  correctivos y kilometraje próximo; parámetro nuevo `@KmAviso`, 500 por defecto; severidades
  nuevas `EnCurso` y `Pendiente`).

## Revertir

```sql
DROP PROCEDURE IF EXISTS dbo.sp_VehiculoDocumento_Baja;
DROP PROCEDURE IF EXISTS dbo.sp_VehiculoDocumento_Update;
DROP PROCEDURE IF EXISTS dbo.sp_VehiculoDocumento_GetById;
DROP PROCEDURE IF EXISTS dbo.sp_VehiculoDocumento_List;
DROP INDEX IF EXISTS IX_VehiculoDocumentos_Vehiculo_Tipo ON dbo.VehiculoDocumentos;
-- Los documentos dados de baja volverían a contarse: restaurarlos solo si se quiere eso.
ALTER TABLE dbo.VehiculoDocumentos DROP CONSTRAINT DF_VehiculoDocumentos_Baja;
ALTER TABLE dbo.VehiculoDocumentos DROP COLUMN DadoDeBaja, FechaBaja;
DELETE FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0288_documentos_alertas_flota';
```

Volver a crear `sp_VehiculoDocumento_Create` y `sp_Flota_Alertas` con su definición anterior
(`database_Esteban/cu152_153_154_161_flota_activos.sql` y `database_Esteban/cu301_kilometraje_odometro.sql`).
Sin los procedimientos nuevos, la lista, la edición y la baja de documentos de Alertas de flota
fallan con el mensaje genérico: revertir también el código o reaplicar la 0288.
