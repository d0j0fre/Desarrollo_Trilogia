# Rollback de 0241

## Qué introduce

- `dbo.sp_Comodato_Rentabilidad` devuelve además `Ubicacion` al final del
  resultado (BUG-16-06). Las columnas anteriores no cambian. No toca tablas ni
  datos.

## Revertir

Si 0242 está aplicada, revertir primero 0242 (su rollback deja esta versión).
La aplicación anterior funciona con la versión nueva. Para volver a la
definición de `database_Esteban/cu162_163_164_comodatos.sql`:

```sql
CREATE OR ALTER PROCEDURE dbo.sp_Comodato_Rentabilidad
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        c.ComodatoId, a.CodigoActivo, a.Nombre AS ActivoNombre, a.Tipo AS ActivoTipo,
        c.ClienteNombre, c.ClienteUsuarioId, c.FechaAsignacion,
        DATEDIFF(DAY, c.FechaAsignacion, CAST(SYSDATETIME() AS DATE)) AS DiasEnComodato,
        ISNULL(compras.NumPedidos, 0)   AS NumPedidos,
        ISNULL(compras.TotalComprado, 0) AS TotalComprado,
        compras.UltimaCompra
    FROM dbo.Comodatos c
    INNER JOIN dbo.Activos a ON a.ActivoId = c.ActivoId
    OUTER APPLY (
        SELECT COUNT(*) AS NumPedidos, SUM(p.Total) AS TotalComprado, MAX(p.FechaPedido) AS UltimaCompra
        FROM dbo.Pedidos p
        WHERE c.ClienteUsuarioId IS NOT NULL
          AND p.UsuarioId = c.ClienteUsuarioId
          AND p.Estado IN (N'Entregado', N'Aprobado', N'EnProceso')
          AND p.FechaPedido >= c.FechaAsignacion
    ) compras
    WHERE c.Estado = N'Activo'
    ORDER BY ISNULL(compras.TotalComprado, 0) ASC;
END;
GO
DELETE FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0241_comodato_rentabilidad_ubicacion';
```
