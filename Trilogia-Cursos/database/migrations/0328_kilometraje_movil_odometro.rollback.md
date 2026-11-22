# Reversión de 0328

Aditiva y sin tablas ni datos nuevos.

1. Devolver `dbo.sp_Chofer_Kilometraje_Abrir` a la versión de `0025_mobile_driver_surface.sql` (ejecutar solo su `CREATE OR ALTER PROCEDURE dbo.sp_Chofer_Kilometraje_Abrir`). Con eso la aplicación vuelve a aceptar un kilometraje inicial menor al odómetro.
2. Marcar la fila `0328_kilometraje_movil_odometro` como `RolledBack` en `dbo.SchemaMigrationHistory`.

Restaurar un BACPAC es el último recurso.
