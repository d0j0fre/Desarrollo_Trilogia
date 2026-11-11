SET NOCOUNT ON;

/* Verificacion de 0290 (solo lectura). */
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0290_sugerencias_compra_promedio_real' AND Status = N'Applied')
    THROW 56910, N'0290 no figura aplicada en el ledger.', 1;
IF NOT EXISTS (SELECT 1 FROM sys.parameters WHERE object_id = OBJECT_ID(N'dbo.sp_Admin_GetPurchaseSuggestions') AND name = N'@IncluirTodos')
    THROW 56911, N'sp_Admin_GetPurchaseSuggestions no tiene @IncluirTodos.', 1;

DECLARE @Sugeridos TABLE (ProductoId INT, Nombre NVARCHAR(150), StockActual INT, StockMinimo INT, UnidadesVendidasVentana INT,
    PromedioVentaMensual DECIMAL(18,2), MesesCobertura INT, CantidadSugerida INT, DatosInsuficientes BIT);
DECLARE @Todos TABLE (ProductoId INT, Nombre NVARCHAR(150), StockActual INT, StockMinimo INT, UnidadesVendidasVentana INT,
    PromedioVentaMensual DECIMAL(18,2), MesesCobertura INT, CantidadSugerida INT, DatosInsuficientes BIT);
INSERT @Sugeridos EXEC dbo.sp_Admin_GetPurchaseSuggestions @MesesRecientes = 3, @MesesCobertura = 2;
INSERT @Todos EXEC dbo.sp_Admin_GetPurchaseSuggestions @MesesRecientes = 3, @MesesCobertura = 2, @IncluirTodos = 1;

IF (SELECT COUNT(*) FROM @Todos) <> (SELECT COUNT(*) FROM dbo.Productos WHERE Activo = 1)
    THROW 56912, N'Con @IncluirTodos = 1 deben salir todos los productos activos.', 1;
IF EXISTS (SELECT 1 FROM @Sugeridos WHERE CantidadSugerida <= 0)
    THROW 56913, N'Por defecto solo deben salir productos con cantidad sugerida.', 1;
-- Mismo producto, mismo calculo: el promedio y la cantidad coinciden con y sin @IncluirTodos.
IF EXISTS (SELECT 1 FROM @Sugeridos s INNER JOIN @Todos t ON t.ProductoId = s.ProductoId
           WHERE t.PromedioVentaMensual <> s.PromedioVentaMensual OR t.CantidadSugerida <> s.CantidadSugerida)
    THROW 56914, N'El calculo difiere entre los dos modos.', 1;
PRINT N'Verificación 0290 completada.';
