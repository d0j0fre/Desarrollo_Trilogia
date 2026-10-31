# Rollback de 0200 — contrato de sp_Admin_GetOrderHeader

## Alcance del cambio

0200 sustituye únicamente la definición de `dbo.sp_Admin_GetOrderHeader`. No
crea, altera ni elimina tablas, columnas, índices, restricciones, permisos ni
filas de negocio. No hay datos que restaurar. Se aplicó con `CREATE OR ALTER`,
así que el `object_id` y los `GRANT EXECUTE` se conservan.

## Procedimiento de reversión

No se recomienda: la definición anterior es la que provoca que el detalle de
pedido (Administración, portal del cliente y confirmación de Venta móvil) falle
con `InvalidCastException`. Si aun así hay que volver atrás, ejecutar como
compensación, por el ejecutor designado:

```sql
SET XACT_ABORT ON;
BEGIN TRANSACTION;

EXEC sys.sp_executesql N'
CREATE OR ALTER PROCEDURE dbo.sp_Admin_GetOrderHeader
    @PedidoId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT p.PedidoId, p.UsuarioId, u.NombreCompleto, u.Correo, p.FechaPedido, p.Estado, p.TipoEntrega, p.DireccionEntrega, p.Total, p.Observaciones
    FROM dbo.Pedidos p
    INNER JOIN dbo.Usuarios u ON u.UsuarioId = p.UsuarioId
    WHERE p.PedidoId = @PedidoId;
END';

UPDATE dbo.SchemaMigrationHistory
SET Status = N'RolledBack',
    Notes = CONCAT(Notes, N' | Revertida el ', CONVERT(NVARCHAR(30), SYSUTCDATETIME(), 126), N' UTC.')
WHERE MigrationId = N'0200_order_header_contract';

COMMIT TRANSACTION;
```

Esa es la definición histórica de `database/sp_admin_modulo.sql`.

## Restauración de BACPAC

No procede: un cambio de definición de procedimiento se revierte con el bloque
anterior.
