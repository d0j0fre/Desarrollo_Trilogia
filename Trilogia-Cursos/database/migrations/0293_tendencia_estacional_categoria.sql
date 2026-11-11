SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

/* BUG-24-04 · CU-243-01: la Tendencia estacional siempre sumaba todas las categorias; no se
   podia elegir la categoria de producto cuyos picos historicos se querian ver.

   Se reemplaza dbo.sp_Admin_GetSeasonalSalesTrend (definicion de la 0292) para agregar
   @Categoria NVARCHAR(100) = NULL:
   - NULL (valor por defecto): igual que antes, todas las categorias; el total del mes es la
     suma de Facturas.Total y coincide con el Reporte de ventas.
   - Con categoria: solo las lineas de factura de productos de esa categoria. El importe de
     cada linea es su Subtotal repartido sobre el Total de su factura (asi incluye su parte de
     IVA y las categorias suman el total de la factura, salvo centimos de redondeo); las
     unidades son FacturaDetalle.Cantidad.
   Mismas columnas y misma validacion (error 54542). La lista de categorias del selector sale
   de sp_Store_GetCategories, que ya existe. Solo lectura.
   Rollback en 0293_tendencia_estacional_categoria.rollback.md. */
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL THROW 56960,N'Falta el ledger 0001.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0293_tendencia_estacional_categoria' AND Status=N'Applied')
    THROW 56961,N'0293 ya figura aplicada.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0292_tendencia_estacional_facturas' AND Status=N'Applied')
    THROW 56962,N'0293 parte de la versión de sp_Admin_GetSeasonalSalesTrend de la 0292. Aplicar 0292 primero.',1;

BEGIN TRANSACTION;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Admin_GetSeasonalSalesTrend
    @AnioInicio INT,
    @AnioFin INT,
    @Categoria NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF @AnioInicio NOT BETWEEN 2000 AND 2100 OR @AnioFin NOT BETWEEN 2000 AND 2100
       OR @AnioInicio > @AnioFin OR @AnioFin - @AnioInicio > 9
        THROW 54542, N'El período estacional no es válido.', 1;

    SET @Categoria = NULLIF(LTRIM(RTRIM(@Categoria)), N'');
    DECLARE @Desde DATETIME2 = DATEFROMPARTS(@AnioInicio, 1, 1);
    DECLARE @Hasta DATETIME2 = DATEFROMPARTS(@AnioFin + 1, 1, 1);

    CREATE TABLE #Monthly (NumeroMes INT NOT NULL PRIMARY KEY, Total DECIMAL(18,2) NOT NULL, Units INT NOT NULL);

    IF @Categoria IS NULL
    BEGIN
        ;WITH InvoiceTotals AS
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
        INSERT #Monthly (NumeroMes, Total, Units)
        SELECT totals.NumeroMes, CONVERT(DECIMAL(18,2), totals.Total), CONVERT(INT, ISNULL(units.Units, 0))
        FROM InvoiceTotals totals
        LEFT JOIN InvoiceUnits units ON units.NumeroMes = totals.NumeroMes;
    END
    ELSE
    BEGIN
        INSERT #Monthly (NumeroMes, Total, Units)
        SELECT MONTH(invoice.FechaFactura),
               CONVERT(DECIMAL(18,2), SUM(detail.Subtotal * invoice.Total / NULLIF(lines.SumaDetalle, 0))),
               CONVERT(INT, SUM(detail.Cantidad))
        FROM dbo.Facturas invoice
        INNER JOIN dbo.FacturaDetalle detail ON detail.FacturaId = invoice.FacturaId
        INNER JOIN dbo.Productos product ON product.ProductoId = detail.ProductoId
        CROSS APPLY (SELECT SUM(other.Subtotal) AS SumaDetalle
                     FROM dbo.FacturaDetalle other WHERE other.FacturaId = invoice.FacturaId) lines
        WHERE invoice.Estado <> N'Anulada'
          AND invoice.FechaFactura >= @Desde AND invoice.FechaFactura < @Hasta
          AND product.Categoria = @Categoria
        GROUP BY MONTH(invoice.FechaFactura);
    END;

    SELECT months.NumeroMes,
           months.NombreMes,
           CONVERT(DECIMAL(18,2), ISNULL(monthly.Total, 0)) AS TotalVendido,
           CONVERT(INT, ISNULL(monthly.Units, 0)) AS UnidadesVendidas
    FROM (SELECT number AS NumeroMes,
                 CHOOSE(number, N'Enero', N'Febrero', N'Marzo', N'Abril', N'Mayo', N'Junio',
                        N'Julio', N'Agosto', N'Septiembre', N'Octubre', N'Noviembre', N'Diciembre') AS NombreMes
          FROM (VALUES (1),(2),(3),(4),(5),(6),(7),(8),(9),(10),(11),(12)) valuesTable(number)) months
    LEFT JOIN #Monthly monthly ON monthly.NumeroMes = months.NumeroMes
    ORDER BY months.NumeroMes;
END;
GO

IF XACT_STATE()<>1 THROW 56963,N'La transacción 0293 no está disponible.',1;
DECLARE @MigrationSha256 NVARCHAR(128)=N'$(MigrationSha256)';
IF LEN(@MigrationSha256)<>64 OR @MigrationSha256 LIKE N'%[^0-9A-Fa-f]%' THROW 56964,N'SHA-256 inválido para 0293.',1;
INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes)
VALUES(N'0293_tendencia_estacional_categoria',N'0293_tendencia_estacional_categoria.sql',UPPER(@MigrationSha256),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),
       N'BUG-24-04: sp_Admin_GetSeasonalSalesTrend acepta @Categoria (NULL = todas). Solo lectura.');
COMMIT TRANSACTION;
GO
