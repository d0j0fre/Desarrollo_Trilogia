# Reversión de 0321

Aditiva y sin tablas ni datos nuevos.

1. Devolver `dbo.sp_Admin_GetClients` y `dbo.sp_Admin_GetClientById` a la versión de `database/cu061_cu063_clientes_patch.sql` (ejecutar los dos `CREATE OR ALTER` de ese script). Con eso «Total comprado» vuelve a contar los pedidos Rechazados (solo excluye los Cancelados).
2. Marcar la fila `0321_total_comprado_sin_rechazados` como `RolledBack` en `dbo.SchemaMigrationHistory`.

No cambia datos. Restaurar un BACPAC es el último recurso.
