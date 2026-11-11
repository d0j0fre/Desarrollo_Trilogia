SET NOCOUNT ON;

/* Verificacion de 0292 (solo lectura): la tendencia coincide con las facturas no anuladas. */
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0292_tendencia_estacional_facturas' AND Status = N'Applied')
    THROW 56950, N'0292 no figura aplicada en el ledger.', 1;

DECLARE @Tendencia TABLE (NumeroMes INT, NombreMes NVARCHAR(20), TotalVendido DECIMAL(18,2), UnidadesVendidas INT);
INSERT @Tendencia EXEC dbo.sp_Admin_GetSeasonalSalesTrend @AnioInicio = 2000, @AnioFin = 2009;
INSERT @Tendencia EXEC dbo.sp_Admin_GetSeasonalSalesTrend @AnioInicio = 2020, @AnioFin = 2029;

IF (SELECT COUNT(*) FROM @Tendencia) <> 24
    THROW 56951, N'Cada consulta debe devolver los 12 meses.', 1;

-- Rango amplio de verificacion: 2017-2026.
DELETE @Tendencia;
INSERT @Tendencia EXEC dbo.sp_Admin_GetSeasonalSalesTrend @AnioInicio = 2017, @AnioFin = 2026;
IF (SELECT COUNT(*) FROM @Tendencia) <> 12
    THROW 56952, N'Deben salir 12 meses.', 1;

DECLARE @TotalFacturas DECIMAL(18,2) = ISNULL((SELECT SUM(Total) FROM dbo.Facturas
    WHERE Estado <> N'Anulada' AND FechaFactura >= '2017-01-01' AND FechaFactura < '2027-01-01'), 0);
DECLARE @UnidadesFacturas INT = ISNULL((SELECT SUM(d.Cantidad) FROM dbo.Facturas f INNER JOIN dbo.FacturaDetalle d ON d.FacturaId = f.FacturaId
    WHERE f.Estado <> N'Anulada' AND f.FechaFactura >= '2017-01-01' AND f.FechaFactura < '2027-01-01'), 0);
IF (SELECT SUM(TotalVendido) FROM @Tendencia) <> @TotalFacturas
    THROW 56953, N'El total de la tendencia no coincide con las facturas no anuladas.', 1;
IF (SELECT SUM(UnidadesVendidas) FROM @Tendencia) <> @UnidadesFacturas
    THROW 56954, N'Las unidades de la tendencia no coinciden con las facturas no anuladas.', 1;

-- Un mes concreto contra el Reporte de ventas: mismas facturas, mismo importe.
DECLARE @Mes INT = (SELECT TOP 1 MONTH(FechaFactura) FROM dbo.Facturas WHERE Estado <> N'Anulada' ORDER BY FechaFactura DESC);
IF @Mes IS NOT NULL
   AND (SELECT TotalVendido FROM @Tendencia WHERE NumeroMes = @Mes)
       <> (SELECT SUM(Total) FROM dbo.Facturas WHERE Estado <> N'Anulada' AND MONTH(FechaFactura) = @Mes
           AND FechaFactura >= '2017-01-01' AND FechaFactura < '2027-01-01')
    THROW 56955, N'El mes de comparacion no coincide.', 1;

-- La validacion del periodo se conserva.
BEGIN TRY
    EXEC dbo.sp_Admin_GetSeasonalSalesTrend @AnioInicio = 2026, @AnioFin = 2024;
    THROW 56956, N'Debio rechazar un año inicial mayor al final.', 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() <> 54542 THROW;
END CATCH;
PRINT N'Verificación 0292 completada.';
