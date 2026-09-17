# Rollback de 0025

## Qué introduce la migración

- Columna anulable `SyncGuid` en `dbo.VehiculoKilometraje`, con índice único
  filtrado. Las jornadas que abre el módulo web no la usan y siguen igual.
- Permisos `MOVIL_ACCESO` y `FLOTA_KILOMETRAJE_PROPIO`, con sus asignaciones.
- Procedimientos nuevos: `sp_Chofer_GetVehiculosDisponibles`,
  `sp_Chofer_Kilometraje_Abrir`, `sp_Chofer_Kilometraje_Cerrar`,
  `sp_Chofer_Kilometraje_Abierto`, `sp_Chofer_ResumenDia`.

`sp_Kilometraje_Abrir`, `sp_Kilometraje_Cerrar` y `sp_Kilometraje_List` **no se
tocan**: el módulo web de flota funciona igual con la migración aplicada o sin
ella.

## Rollback ordinario

Quitarle el permiso `MOVIL_ACCESO` a los perfiles afectados. La aplicación móvil
deja de funcionar y el sistema web queda intacto. No requiere tocar el esquema.

```sql
DELETE assigned
FROM dbo.PerfilPermisos assigned
INNER JOIN dbo.Permisos permission ON permission.PermisoId = assigned.PermisoId
WHERE permission.Codigo = N'MOVIL_ACCESO';
```

## Rollback completo

```sql
SET XACT_ABORT ON;
BEGIN TRANSACTION;

DROP PROCEDURE IF EXISTS dbo.sp_Chofer_ResumenDia;
DROP PROCEDURE IF EXISTS dbo.sp_Chofer_Kilometraje_Abierto;
DROP PROCEDURE IF EXISTS dbo.sp_Chofer_Kilometraje_Cerrar;
DROP PROCEDURE IF EXISTS dbo.sp_Chofer_Kilometraje_Abrir;
DROP PROCEDURE IF EXISTS dbo.sp_Chofer_GetVehiculosDisponibles;

DELETE assigned
FROM dbo.PerfilPermisos assigned
INNER JOIN dbo.Permisos permission ON permission.PermisoId = assigned.PermisoId
WHERE permission.Codigo IN (N'MOVIL_ACCESO', N'FLOTA_KILOMETRAJE_PROPIO');

UPDATE dbo.Permisos SET Activo = 0
WHERE Codigo IN (N'MOVIL_ACCESO', N'FLOTA_KILOMETRAJE_PROPIO');

UPDATE dbo.SchemaMigrationHistory
SET Status = N'RolledBack'
WHERE MigrationId = N'0025_mobile_driver_surface';

COMMIT TRANSACTION;
```

**La columna `SyncGuid` se conserva.** Eliminarla borraría la trazabilidad de qué
jornadas entraron desde el teléfono, y al ser anulable no molesta a nadie. Si aun
así hay que quitarla, primero se elimina el índice
`UQ_VehiculoKilometraje_SyncGuid` y después la columna, fuera de horario de
operación.

Los permisos quedan desactivados en vez de borrados para no perder la
trazabilidad de quién los tuvo asignados.
