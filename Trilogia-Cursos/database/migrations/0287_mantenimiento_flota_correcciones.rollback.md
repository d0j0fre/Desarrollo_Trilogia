# Rollback de 0287

## Qué introduce

- `dbo.Vehiculos.InactivoPorMantenimiento` (BIT, por defecto 0): marca los vehículos que
  están inactivos porque tienen una orden correctiva En proceso.
- `sp_Vehiculos_SincronizarMantenimiento` (interno) y `sp_Mantenimiento_Iniciar` (nuevos).
- `sp_Mantenimiento_Create` (valida estado, costo, kilometraje y fecha; guarda la fecha de
  una orden Completada; la correctiva En proceso saca el vehículo de circulación; devuelve
  una segunda columna `VehiculoFueraDeCirculacion`), `sp_Mantenimiento_MarcarCompletada`
  (rechaza una orden ya cerrada y devuelve el vehículo a circulación),
  `sp_Vehiculos_List` (columna `EnMantenimiento` al final), `sp_Vehiculos_ToggleStatus` y
  `sp_Vehiculos_Update` (52094: un vehículo en reparación no se reactiva a mano).
- Al aplicarla, los vehículos con una correctiva En proceso y sin rutas abiertas pasan a
  inactivos con la marca.

## Revertir

```sql
-- 1. Devolver a circulación los vehículos que la migración sacó.
UPDATE dbo.Vehiculos SET Activo = 1 WHERE InactivoPorMantenimiento = 1;
ALTER TABLE dbo.Vehiculos DROP CONSTRAINT DF_Vehiculos_InactivoMant;
ALTER TABLE dbo.Vehiculos DROP COLUMN InactivoPorMantenimiento;
DROP PROCEDURE IF EXISTS dbo.sp_Mantenimiento_Iniciar;
DROP PROCEDURE IF EXISTS dbo.sp_Vehiculos_SincronizarMantenimiento;
DELETE FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0287_mantenimiento_flota_correcciones';
```

2. Volver a crear `sp_Mantenimiento_Create`, `sp_Mantenimiento_MarcarCompletada`,
   `sp_Vehiculos_List`, `sp_Vehiculos_ToggleStatus` y `sp_Vehiculos_Update` con su definición
   anterior: `database_Esteban/cu152_153_154_161_flota_activos.sql` (mantenimiento),
   `database_Esteban/cu301_kilometraje_odometro.sql` (`sp_Vehiculos_List`) y
   `database_Esteban/cu151_vehiculos_marca.sql` / `cu082_entregas_estado_offline_sps.sql`
   (`sp_Vehiculos_Update`, `sp_Vehiculos_ToggleStatus`).

Las órdenes y sus fechas o costos no se pierden. Sin los procedimientos nuevos, el botón
«Iniciar» de Mantenimiento falla con el mensaje genérico: revertir también el código o
reaplicar la 0287.
