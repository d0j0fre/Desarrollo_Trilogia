# Rollback de 0324

## Qué introduce

- Vuelve a crear `dbo.sp_Entregas_PedidosTablero` (solo lectura, POST-qa2-07): el filtro de los pendientes de
  entrega acepta `TipoEntrega` = `Envío a domicilio` (lo que guardan la Tienda y Venta móvil) además de
  `Entrega a domicilio`. No cambia tablas, datos ni otros procedimientos; las columnas del resultado son las de 0283.

## Revertir

El sitio web no cambia con esta migración (el tablero llama al mismo procedimiento), así que se puede revertir sin
desplegar otra versión. Volver a la definición de la 0283 deja la lista «Pendientes de entrega» vacía para los
pedidos creados por el sitio.

1. Ejecutar de nuevo el `CREATE OR ALTER PROCEDURE dbo.sp_Entregas_PedidosTablero` de
   `0283_tablero_entregas_pedidos.sql` (solo el bloque del procedimiento, sin el registro del ledger).
2. Quitar el registro:

```sql
DELETE FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0324_tablero_entregas_envio_domicilio';
```
