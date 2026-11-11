# Rollback de 0283

## Qué introduce

- Procedimiento nuevo `dbo.sp_Entregas_PedidosTablero` (solo lectura, BUG-10-02):
  los pedidos En ruta y los pendientes de entrega que lista el tablero de
  entregas. No cambia tablas, datos ni otros procedimientos.

## Revertir

Desplegar primero el sitio web anterior: el tablero actual llama a este
procedimiento y mostraría un error sin él.

```sql
DROP PROCEDURE IF EXISTS dbo.sp_Entregas_PedidosTablero;
DELETE FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0283_tablero_entregas_pedidos';
```
