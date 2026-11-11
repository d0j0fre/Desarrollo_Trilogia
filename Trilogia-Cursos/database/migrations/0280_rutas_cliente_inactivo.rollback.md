# Rollback de 0280

## Qué introduce

- Reemplaza `dbo.sp_Rutas_GetAssignableOrders`, `dbo.sp_Rutas_AddOrder` y
  `dbo.sp_Rutas_Create` (BUG-06-04): ya no ofrecen ni aceptan pedidos de un
  cliente inactivo (`Usuarios.Activo = 0`). AddOrder lanza 52014 y Create 52006.
- No cambia tablas ni datos. Una ruta ya armada o despachada con pedidos de un
  cliente que se inactivó después sigue igual.

## Revertir

Reaplicar las definiciones anteriores de los tres procedimientos
(`CREATE OR ALTER`), que son las de `database_Esteban/cu081_rutas_entregas_esquema.sql`
y `database_Esteban/cu302_rutas_coordenadas_cliente.sql`, y borrar la fila del ledger:

```sql
DELETE FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0280_rutas_cliente_inactivo';
```

La pantalla Nueva ruta vuelve a listar los pedidos pendientes de clientes
inactivos.
