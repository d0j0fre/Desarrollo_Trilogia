# Rollback de 0326

## Qué introduce

- `dbo.fn_Cliente_FacturasVencidas(@UsuarioId)`: facturas a crédito vencidas de un cliente (mismo FIFO que `fn_Cliente_EstadoDeuda`, 0230).
- `dbo.sp_Facturas_Vencidas(@UsuarioId = NULL)`: las facturas vencidas de un cliente o de todos (solo lectura).
- `dbo.sp_FacturasVencidas_GenerarAvisos`: crea avisos `FacturaVencida` en `dbo.NotificacionesUsuario` (0221), una vez por cliente y día, para Administrador, Gerente y Crédito y Cobro.
- No cambia tablas ni `dbo.Facturas.Estado`. Los avisos ya creados son filas de la bandeja y no afectan a otros módulos.

## Revertir

Desplegar primero el sitio anterior: el servicio diario y las pantallas (Cuentas por cobrar, detalle del cliente y de la factura) llaman a estos procedimientos y mostrarían un error sin ellos.

```sql
DROP PROCEDURE IF EXISTS dbo.sp_FacturasVencidas_GenerarAvisos;
DROP PROCEDURE IF EXISTS dbo.sp_Facturas_Vencidas;
DROP FUNCTION  IF EXISTS dbo.fn_Cliente_FacturasVencidas;
-- Opcional: quitar los avisos generados.
-- DELETE FROM dbo.NotificacionesUsuario WHERE Tipo = N'FacturaVencida';
DELETE FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0326_facturas_vencidas_avisos';
```
