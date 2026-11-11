SET NOCOUNT ON;

/* Verificacion de 0293 (solo lectura). */
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0293_tendencia_estacional_categoria' AND Status = N'Applied')
    THROW 56970, N'0293 no figura aplicada en el ledger.', 1;
IF NOT EXISTS (SELECT 1 FROM sys.parameters WHERE object_id = OBJECT_ID(N'dbo.sp_Admin_GetSeasonalSalesTrend') AND name = N'@Categoria')
    THROW 56971, N'sp_Admin_GetSeasonalSalesTrend no tiene @Categoria.', 1;

DECLARE @Todas TABLE (NumeroMes INT, NombreMes NVARCHAR(20), TotalVendido DECIMAL(18,2), UnidadesVendidas INT);
DECLARE @Cat TABLE (NumeroMes INT, NombreMes NVARCHAR(20), TotalVendido DECIMAL(18,2), UnidadesVendidas INT);
DECLARE @Suma DECIMAL(18,2) = 0, @SumaUnidades INT = 0, @CategoriaActual NVARCHAR(100);

-- Sin categoria: 12 meses y el total coincide con las facturas no anuladas del rango.
INSERT @Todas EXEC dbo.sp_Admin_GetSeasonalSalesTrend @AnioInicio = 2017, @AnioFin = 2026;
IF (SELECT COUNT(*) FROM @Todas) <> 12 THROW 56972, N'Deben salir 12 meses.', 1;
IF (SELECT SUM(TotalVendido) FROM @Todas) <> ISNULL((SELECT SUM(Total) FROM dbo.Facturas
        WHERE Estado <> N'Anulada' AND FechaFactura >= '2017-01-01' AND FechaFactura < '2027-01-01'), 0)
    THROW 56973, N'El total sin categoria no coincide con las facturas.', 1;

-- Con cada categoria: las unidades de todas las categorias suman las unidades sin filtro, y el
-- importe suma el total (con tolerancia de centimos por el reparto del IVA por linea).
DECLARE categorias CURSOR LOCAL FAST_FORWARD FOR SELECT DISTINCT Categoria FROM dbo.Productos;
OPEN categorias;
FETCH NEXT FROM categorias INTO @CategoriaActual;
WHILE @@FETCH_STATUS = 0
BEGIN
    DELETE @Cat;
    INSERT @Cat EXEC dbo.sp_Admin_GetSeasonalSalesTrend @AnioInicio = 2017, @AnioFin = 2026, @Categoria = @CategoriaActual;
    IF (SELECT COUNT(*) FROM @Cat) <> 12 THROW 56974, N'Con categoria tambien deben salir 12 meses.', 1;
    SET @Suma += (SELECT SUM(TotalVendido) FROM @Cat);
    SET @SumaUnidades += (SELECT SUM(UnidadesVendidas) FROM @Cat);
    FETCH NEXT FROM categorias INTO @CategoriaActual;
END;
CLOSE categorias; DEALLOCATE categorias;

IF @SumaUnidades <> (SELECT SUM(UnidadesVendidas) FROM @Todas)
    THROW 56975, N'Las unidades por categoria no suman las unidades totales.', 1;
IF ABS(@Suma - (SELECT SUM(TotalVendido) FROM @Todas)) > 1.00
    THROW 56976, N'El importe por categoria no suma el total.', 1;

-- Una categoria inexistente no devuelve ventas (12 meses en cero).
DELETE @Cat;
INSERT @Cat EXEC dbo.sp_Admin_GetSeasonalSalesTrend @AnioInicio = 2017, @AnioFin = 2026, @Categoria = N'Categoria inexistente VERIFY-0293';
IF EXISTS (SELECT 1 FROM @Cat WHERE TotalVendido <> 0 OR UnidadesVendidas <> 0)
    THROW 56977, N'Una categoria inexistente no debe tener ventas.', 1;
PRINT N'Verificación 0293 completada.';
