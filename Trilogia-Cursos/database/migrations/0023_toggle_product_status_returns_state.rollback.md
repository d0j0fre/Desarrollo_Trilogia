# Rollback de 0023 — estado devuelto por sp_Admin_ToggleProductStatus

## Alcance del cambio

0023 sustituye únicamente la definición de `dbo.sp_Admin_ToggleProductStatus`.
No crea, altera ni elimina tablas, columnas, índices, restricciones, permisos ni
filas de negocio. No hay datos que restaurar.

Se aplicó con `CREATE OR ALTER`, de modo que el `object_id` y los `GRANT EXECUTE`
existentes se conservan: revertir tampoco los pierde.

## Procedimiento de reversión

Revertir primero el código de aplicación si se desplegó junto con esta
migración. El endurecimiento de `InventoryController.ToggleStatus` tolera que el
procedimiento no devuelva fila, así que la aplicación sigue operando con la
definición antigua; sólo se pierde la distinción entre activar e inactivar en la
auditoría.

Ejecutar como compensación, en una ventana controlada y por el ejecutor
designado:

```sql
SET XACT_ABORT ON;
BEGIN TRANSACTION;

EXEC sys.sp_executesql N'
CREATE OR ALTER PROCEDURE dbo.sp_Admin_ToggleProductStatus
    @ProductoId INT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Productos
    SET Activo = CASE WHEN Activo = 1 THEN 0 ELSE 1 END
    WHERE ProductoId = @ProductoId;
END';

UPDATE dbo.SchemaMigrationHistory
SET Status = N'RolledBack',
    Notes = CONCAT(Notes, N' | Revertida el ', CONVERT(NVARCHAR(30), SYSUTCDATETIME(), 126), N' UTC.')
WHERE MigrationId = N'0023_toggle_product_status_returns_state';

COMMIT TRANSACTION;
```

Esa definición es la histórica de `database/sp_admin_modulo.sql`. Tras aplicarla,
`ExecuteScalar` vuelve a recibir `NULL` y toda reactivación se registra de nuevo
como inactivación: es una regresión conocida y aceptada de la reversión.

## Restauración de BACPAC

No procede. Un cambio de definición de procedimiento no justifica restaurar un
respaldo completo. El BACPAC previo sigue siendo obligatorio antes de aplicar,
según el flujo general, pero la reversión se resuelve con el bloque anterior.
