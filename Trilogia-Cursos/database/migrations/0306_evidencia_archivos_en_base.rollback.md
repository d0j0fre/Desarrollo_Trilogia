# Rollback de 0306

## Qué introduce

- `dbo.EvidenciaArchivos`: contenido de las fotos y firmas de entrega cuando
  `EvidenceStorage:Provider` es `Database` (sitio web y API).
- `sp_EvidenciaArchivo_Preparar`, `sp_EvidenciaArchivo_Confirmar`,
  `sp_EvidenciaArchivo_Descartar` y `sp_EvidenciaArchivo_Obtener`.

## Revertir

Antes de borrar la tabla, quite `EvidenceStorage__Provider` del sitio y de la API (vuelven a la
carpeta de `EvidenceStorage:RootPath`). Las evidencias cuyo contenido esté en la tabla se pierden
al borrarla: si hay que conservarlas, expórtelas primero.

```sql
BEGIN TRANSACTION;
DROP PROCEDURE IF EXISTS dbo.sp_EvidenciaArchivo_Obtener;
DROP PROCEDURE IF EXISTS dbo.sp_EvidenciaArchivo_Descartar;
DROP PROCEDURE IF EXISTS dbo.sp_EvidenciaArchivo_Confirmar;
DROP PROCEDURE IF EXISTS dbo.sp_EvidenciaArchivo_Preparar;
DROP TABLE IF EXISTS dbo.EvidenciaArchivos;
DELETE FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0306_evidencia_archivos_en_base';
COMMIT TRANSACTION;
```
