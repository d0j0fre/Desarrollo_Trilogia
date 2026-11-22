SET NOCOUNT ON;

/* Verificacion de 0355. Solo lectura. */
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0355_tablero_stock_minimo' AND Status = N'Applied')
    THROW 58960, N'0355 no figura aplicada en el ledger.', 1;
IF EXISTS (SELECT 1 FROM (VALUES (N'sp_Reportes_DashboardKpis'),(N'sp_Asistente_Metricas'),(N'sp_Reportes_DashboardDetalle')) o(n)
           WHERE OBJECT_ID(N'dbo.' + o.n) IS NULL OR OBJECT_DEFINITION(OBJECT_ID(N'dbo.' + o.n)) LIKE N'%EstadoStock%')
    THROW 58961, N'Un procedimiento del tablero todavia usa EstadoStock (tope fijo de 5).', 1;

-- El tablero cuenta lo mismo que Inventario: agotado = stock 0; bajo = stock > 0 y hasta el minimo del producto.
DECLARE @d DATE='20000101',@h DATE='20991231',@bajo INT,@ago INT,@bajoReal INT,@agoReal INT;
DECLARE @k TABLE(Desde DATE,Hasta DATE,Ventas DECIMAL(18,2),Facturas INT,Pedidos INT,Ticket DECIMAL(18,2),StockBajo INT,Agotados INT,EnRuta INT,Cobros DECIMAL(18,2),HayDatos BIT);
INSERT @k EXEC dbo.sp_Reportes_DashboardKpis @Desde=@d,@Hasta=@h;
SELECT @bajo=StockBajo, @ago=Agotados FROM @k;
SELECT @bajoReal=SUM(CASE WHEN Stock > 0 AND Stock <= StockMinimo THEN 1 ELSE 0 END),
       @agoReal=SUM(CASE WHEN Stock <= 0 THEN 1 ELSE 0 END)
FROM dbo.Productos WHERE Activo = 1;
IF ISNULL(@bajo,0)<>ISNULL(@bajoReal,0) OR ISNULL(@ago,0)<>ISNULL(@agoReal,0)
    THROW 58962, N'El tablero no cuenta el stock bajo o agotado con el minimo de cada producto.', 1;
PRINT N'Verificacion 0355 completada.';
