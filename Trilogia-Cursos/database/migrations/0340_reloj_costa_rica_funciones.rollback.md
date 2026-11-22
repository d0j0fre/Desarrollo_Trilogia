# Reversión de 0340

Aditiva: solo agrega cuatro funciones escalares y no toca tablas ni datos.

1. Revertir primero 0342 y 0341 (usan estas funciones).
2. `DROP FUNCTION dbo.fn_DiaCRaReloj; DROP FUNCTION dbo.fn_FechaHoraCR; DROP FUNCTION dbo.fn_HoyCR; DROP FUNCTION dbo.fn_AhoraCR;`
3. Marcar la fila `0340_reloj_costa_rica_funciones` como `RolledBack` en `dbo.SchemaMigrationHistory`.

Restaurar un BACPAC es el último recurso.
