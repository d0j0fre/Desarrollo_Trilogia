# Reversión de 0333

Aditiva: `dbo.sp_Movil_Pedido_Detalle` devuelve dos columnas más (`Subtotal`, `Impuesto`). No cambia tablas ni datos.

1. Volver a crear `dbo.sp_Movil_Pedido_Detalle` con su definición de `0028_mobile_roles_surface.sql` (`CREATE OR ALTER`).
2. Marcar `0333_movil_detalle_pedido_iva` como `RolledBack` en `dbo.SchemaMigrationHistory`.

La API y la app siguen funcionando: sin las columnas, la app muestra solo el total.
