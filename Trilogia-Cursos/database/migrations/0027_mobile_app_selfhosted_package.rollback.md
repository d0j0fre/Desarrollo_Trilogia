# Rollback de 0027

## Qué introduce

- Columnas `ArchivoAlmacenado` y `ArchivoNombre` en `dbo.AppMovilVersiones`,
  ambas anulables.
- `UrlDescarga` pasa de obligatoria a opcional.
- La restricción `CK_AppMovilVersiones_Url` se reemplaza por
  `CK_AppMovilVersiones_Origen`, que exige archivo **o** dirección HTTPS.
- `sp_AppMovil_GetArchivo`, y actualización de `sp_AppMovil_Publicar`,
  `sp_AppMovil_GetVigente` y `sp_AppMovil_List`.

Ninguna fila existente deja de ser válida: las que tenían dirección HTTPS la
siguen cumpliendo bajo la restricción nueva.

## Rollback ordinario

Retirar la versión vigente con `sp_AppMovil_Despublicar`. La página deja de
ofrecer descarga y nada más cambia.

## Rollback completo

Solo si hay que volver al esquema de la 0026. **Antes de ejecutarlo hay que
comprobar que ninguna versión dependa de un archivo subido al sistema**, porque
esas filas quedarían sin dirección de descarga:

```sql
SELECT VersionId, VersionNombre, ArchivoAlmacenado, UrlDescarga
FROM dbo.AppMovilVersiones
WHERE ArchivoAlmacenado IS NOT NULL AND UrlDescarga IS NULL;
```

Si hay filas, primero hay que publicar esos archivos en un lugar HTTPS y
completarles `UrlDescarga`. Después:

```sql
SET XACT_ABORT ON;
BEGIN TRANSACTION;

ALTER TABLE dbo.AppMovilVersiones DROP CONSTRAINT CK_AppMovilVersiones_Origen;
ALTER TABLE dbo.AppMovilVersiones ALTER COLUMN UrlDescarga NVARCHAR(500) NOT NULL;
ALTER TABLE dbo.AppMovilVersiones ADD CONSTRAINT CK_AppMovilVersiones_Url
    CHECK (UrlDescarga LIKE N'https://%');

ALTER TABLE dbo.AppMovilVersiones DROP COLUMN ArchivoAlmacenado;
ALTER TABLE dbo.AppMovilVersiones DROP COLUMN ArchivoNombre;

DROP PROCEDURE IF EXISTS dbo.sp_AppMovil_GetArchivo;

UPDATE dbo.SchemaMigrationHistory
SET Status = N'RolledBack'
WHERE MigrationId = N'0027_mobile_app_selfhosted_package';

COMMIT TRANSACTION;
```

Después hay que volver a crear `sp_AppMovil_Publicar`, `sp_AppMovil_GetVigente`
y `sp_AppMovil_List` con el cuerpo de la migración 0026.

**Los archivos APK guardados en el disco del App Service no se borran con este
rollback.** Quedan en el almacén privado y hay que retirarlos aparte si ya no
se van a usar.
