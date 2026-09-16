# Rollback de 0026

## Qué introduce la migración

- Tabla `dbo.AppMovilVersiones`, vacía al aplicarse.
- Procedimientos `sp_AppMovil_GetVigente`, `sp_AppMovil_List`,
  `sp_AppMovil_Publicar`, `sp_AppMovil_Despublicar`.
- Permiso `MOVIL_APP_PUBLICAR`, asignado solo al perfil `Administrador`.

No toca ninguna tabla ni procedimiento existente.

## Rollback ordinario

Retirar de circulación la versión vigente. La página del QR deja de ofrecer
descarga y la aplicación instalada sigue funcionando:

```sql
EXEC dbo.sp_AppMovil_Despublicar @VersionId = <id>;
```

## Rollback completo

```sql
SET XACT_ABORT ON;
BEGIN TRANSACTION;

DROP PROCEDURE IF EXISTS dbo.sp_AppMovil_Despublicar;
DROP PROCEDURE IF EXISTS dbo.sp_AppMovil_Publicar;
DROP PROCEDURE IF EXISTS dbo.sp_AppMovil_List;
DROP PROCEDURE IF EXISTS dbo.sp_AppMovil_GetVigente;
DROP TABLE IF EXISTS dbo.AppMovilVersiones;

DELETE assigned
FROM dbo.PerfilPermisos assigned
INNER JOIN dbo.Permisos permission ON permission.PermisoId = assigned.PermisoId
WHERE permission.Codigo = N'MOVIL_APP_PUBLICAR';

UPDATE dbo.Permisos SET Activo = 0 WHERE Codigo = N'MOVIL_APP_PUBLICAR';

UPDATE dbo.SchemaMigrationHistory
SET Status = N'RolledBack'
WHERE MigrationId = N'0026_mobile_app_distribution';

COMMIT TRANSACTION;
```

**Consecuencia a tener presente:** al borrar la tabla se pierde el registro de
qué versiones se publicaron y con qué SHA-256. Si alguna vez hay que auditar qué
archivo tenía instalado un teléfono, ese historial es la única evidencia.
Exportar la tabla antes de ejecutar el rollback completo.
