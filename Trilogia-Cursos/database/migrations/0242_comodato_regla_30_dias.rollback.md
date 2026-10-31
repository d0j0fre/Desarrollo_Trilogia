# Rollback de 0242

## Qué introduce

- Función `dbo.fn_Comodato_Rentabilidad(@DiasUmbral)`: regla de 30 días sin
  compras (BUG-16-04). Fórmula en el encabezado de la migración.
- `dbo.sp_Comodato_Rentabilidad` lee la función y agrega `DiasSinCompra` y
  `SugerirRetiro` al final; ordena las alertas primero.
- `dbo.sp_Comodato_GenerarAlertasRentabilidad`: avisos `ComodatoBajaRentabilidad`
  en `dbo.NotificacionesUsuario` para los usuarios con `COMODATOS_CONSULTAR`.

No cambia tablas. Los avisos generados quedan en `NotificacionesUsuario`.

## Revertir

Primero desplegar la MVC sin la llamada a `sp_Comodato_GenerarAlertasRentabilidad`
(si no, la pantalla de Rentabilidad la registraría como fallida y seguiría
mostrando el reporte). Luego volver a la versión de 0241:

```sql
DROP PROCEDURE IF EXISTS dbo.sp_Comodato_GenerarAlertasRentabilidad;
GO
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
        compras.UltimaCompra,
        ISNULL(c.Ubicacion, N'') AS Ubicacion
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
DROP FUNCTION IF EXISTS dbo.fn_Comodato_Rentabilidad;
DELETE FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0242_comodato_regla_30_dias';
```

Para retirar además los avisos generados:

```sql
DELETE FROM dbo.NotificacionesUsuario WHERE Tipo = N'ComodatoBajaRentabilidad';
```
