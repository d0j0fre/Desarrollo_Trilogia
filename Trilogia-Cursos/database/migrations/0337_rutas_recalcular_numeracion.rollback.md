# Reversión de 0337

Solo reemplaza `dbo.sp_Rutas_Recalcular`; no cambia tablas ni datos.

1. Volver a la definición de `0281_rutas_prioridad_secuenciacion.sql` (ejecutar solo su `CREATE OR ALTER PROCEDURE dbo.sp_Rutas_Recalcular`). Con eso la numeración de una ruta recalculada vuelve a poder repetirse (1, 1, 2, 2, 3); no afecta datos.
2. Marcar la fila `0337_rutas_recalcular_numeracion` como `RolledBack` en `dbo.SchemaMigrationHistory`.

Las rutas que ya se recalcularon con 0337 conservan su numeración corregida (1..N): es válida con ambas versiones.

Restaurar un BACPAC es el último recurso.
