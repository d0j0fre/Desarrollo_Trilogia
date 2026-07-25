SET NOCOUNT ON;

CREATE OR ALTER PROCEDURE dbo.sp_Admin_GetPurchaseSuggestions
    @MesesHistorico INT = 3, @MesesCobertura INT = 1
AS
BEGIN
    SET NOCOUNT ON;
    SELECT p.ProductoId, p.Nombre, p.Stock AS StockActual,
           ISNULL(v.PromedioMensual,0) AS PromedioVentaMensual,
           CAST(ISNULL(v.PromedioMensual,0) * @MesesCobertura - p.Stock AS INT) AS CantidadSugerida
    FROM dbo.Productos p
    LEFT JOIN (
        SELECT fd.ProductoId, SUM(fd.Cantidad) * 1.0 / @MesesHistorico AS PromedioMensual
        FROM dbo.FacturaDetalle fd
        INNER JOIN dbo.Facturas f ON f.FacturaId = fd.FacturaId
        WHERE f.Estado = 'Generada' AND f.FechaFactura >= DATEADD(MONTH, -@MesesHistorico, SYSDATETIME())
        GROUP BY fd.ProductoId
    ) v ON v.ProductoId = p.ProductoId
    WHERE p.Activo = 1 AND (ISNULL(v.PromedioMensual,0) * @MesesCobertura - p.Stock) > 0
    ORDER BY CantidadSugerida DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Admin_GetSlowMovingProducts
    @MesesSinRotacion INT = 2
AS
BEGIN
    SET NOCOUNT ON;
    SELECT p.ProductoId, p.Nombre, p.Stock, ISNULL(v.CantidadVendida,0) AS VendidoUltimosMeses
    FROM dbo.Productos p
    LEFT JOIN (
        SELECT fd.ProductoId, SUM(fd.Cantidad) AS CantidadVendida
        FROM dbo.FacturaDetalle fd
        INNER JOIN dbo.Facturas f ON f.FacturaId = fd.FacturaId
        WHERE f.Estado = 'Generada' AND f.FechaFactura >= DATEADD(MONTH, -@MesesSinRotacion, SYSDATETIME())
        GROUP BY fd.ProductoId
    ) v ON v.ProductoId = p.ProductoId
    WHERE p.Activo = 1 AND p.Stock > 0 AND ISNULL(v.CantidadVendida,0) = 0
    ORDER BY p.Stock DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Admin_GetSeasonalSalesTrend
AS
BEGIN
    SET NOCOUNT ON;
    SELECT MONTH(FechaFactura) AS NumeroMes, DATENAME(MONTH, FechaFactura) AS NombreMes,
           SUM(Total) AS TotalVendido
    FROM dbo.Facturas
    WHERE Estado = 'Generada'
    GROUP BY MONTH(FechaFactura), DATENAME(MONTH, FechaFactura)
    ORDER BY NumeroMes;
END;
GO
