SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

/* BUG-24-05 · CU-243-02: los totales de la Tendencia estacional no coincidian con el
   Reporte de ventas del mismo mes. La tendencia sumaba lineas de pedidos sin IVA
   (Cantidad * PrecioUnitario, mas combos) y excluia solo Cancelado/Rechazado; el Reporte
   de ventas suma el Total (con IVA) de las facturas no anuladas.

   El equipo adopta una sola definicion de venta: FACTURAS NO ANULADAS. Se reemplaza
   dbo.sp_Admin_GetSeasonalSalesTrend (definicion de la 0012) para que:
   - TotalVendido = suma de Facturas.Total (IVA incluido) de las facturas con Estado <> 'Anulada';
   - UnidadesVendidas = suma de FacturaDetalle.Cantidad de esas mismas facturas;
   - el mes se toma de FechaFactura, igual que el Reporte de ventas.
   Mismos parametros, mismas columnas y misma validacion (error 54542). Solo lectura.
   Rollback en 0292_tendencia_estacional_facturas.rollback.md. */
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL THROW 56940,N'Falta el ledger 0001.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0292_tendencia_estacional_facturas' AND Status=N'Applied')
    THROW 56941,N'0292 ya figura aplicada.',1;
IF OBJECT_ID(N'dbo.sp_Admin_GetSeasonalSalesTrend',N'P') IS NULL OR OBJECT_ID(N'dbo.Facturas',N'U') IS NULL OR OBJECT_ID(N'dbo.FacturaDetalle',N'U') IS NULL
    THROW 56942,N'Faltan sp_Admin_GetSeasonalSalesTrend (0012) o las tablas de facturas.',1;

BEGIN TRANSACTION;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Admin_GetSeasonalSalesTrend
    @AnioInicio INT,
    @AnioFin INT
AS
BEGIN
    SET NOCOUNT ON;
    IF @AnioInicio NOT BETWEEN 2000 AND 2100 OR @AnioFin NOT BETWEEN 2000 AND 2100
       OR @AnioInicio > @AnioFin OR @AnioFin - @AnioInicio > 9
        THROW 54542, N'El período estacional no es válido.', 1;

    DECLARE @Desde DATETIME2 = DATEFROMPARTS(@AnioInicio, 1, 1);
    DECLARE @Hasta DATETIME2 = DATEFROMPARTS(@AnioFin + 1, 1, 1);

    ;WITH Months AS
    (
        SELECT number AS NumeroMes,
               CHOOSE(number, N'Enero', N'Febrero', N'Marzo', N'Abril', N'Mayo', N'Junio',
                      N'Julio', N'Agosto', N'Septiembre', N'Octubre', N'Noviembre', N'Diciembre') AS NombreMes
        FROM (VALUES (1),(2),(3),(4),(5),(6),(7),(8),(9),(10),(11),(12)) valuesTable(number)
    ), InvoiceTotals AS
    (
        SELECT MONTH(invoice.FechaFactura) AS NumeroMes, SUM(invoice.Total) AS Total
        FROM dbo.Facturas invoice
        WHERE invoice.Estado <> N'Anulada'
          AND invoice.FechaFactura >= @Desde AND invoice.FechaFactura < @Hasta
        GROUP BY MONTH(invoice.FechaFactura)
    ), InvoiceUnits AS
    (
        SELECT MONTH(invoice.FechaFactura) AS NumeroMes, SUM(detail.Cantidad) AS Units
        FROM dbo.Facturas invoice
        INNER JOIN dbo.FacturaDetalle detail ON detail.FacturaId = invoice.FacturaId
        WHERE invoice.Estado <> N'Anulada'
          AND invoice.FechaFactura >= @Desde AND invoice.FechaFactura < @Hasta
        GROUP BY MONTH(invoice.FechaFactura)
    )
    SELECT months.NumeroMes,
           months.NombreMes,
           CONVERT(DECIMAL(18,2), ISNULL(totals.Total, 0)) AS TotalVendido,
           CONVERT(INT, ISNULL(units.Units, 0)) AS UnidadesVendidas
    FROM Months months
    LEFT JOIN InvoiceTotals totals ON totals.NumeroMes = months.NumeroMes
    LEFT JOIN InvoiceUnits units ON units.NumeroMes = months.NumeroMes
    ORDER BY months.NumeroMes;
END;
GO

IF XACT_STATE()<>1 THROW 56943,N'La transacción 0292 no está disponible.',1;
DECLARE @MigrationSha256 NVARCHAR(128)=N'$(MigrationSha256)';
IF LEN(@MigrationSha256)<>64 OR @MigrationSha256 LIKE N'%[^0-9A-Fa-f]%' THROW 56944,N'SHA-256 inválido para 0292.',1;
INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes)
VALUES(N'0292_tendencia_estacional_facturas',N'0292_tendencia_estacional_facturas.sql',UPPER(@MigrationSha256),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),
       N'BUG-24-05: sp_Admin_GetSeasonalSalesTrend suma facturas no anuladas (IVA incluido), igual que el Reporte de ventas. Solo lectura.');
COMMIT TRANSACTION;
GO
