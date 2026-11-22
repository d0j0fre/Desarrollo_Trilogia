# Reversión de 0331

Aditiva: solo agrega las funciones `dbo.fn_Promociones_Aplicar` y `dbo.fn_Venta_Lineas`. No hay tablas ni datos.

1. Revertir primero 0332 (sus procedimientos dependen de estas funciones).
2. `DROP FUNCTION dbo.fn_Venta_Lineas; DROP FUNCTION dbo.fn_Promociones_Aplicar;` (en ese orden).
3. Marcar `0331_promociones_calculo_compartido` como `RolledBack` en `dbo.SchemaMigrationHistory`.
