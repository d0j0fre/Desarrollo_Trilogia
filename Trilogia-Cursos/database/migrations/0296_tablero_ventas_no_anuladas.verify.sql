SET NOCOUNT ON;
IF OBJECT_DEFINITION(OBJECT_ID(N'dbo.sp_Reportes_DashboardKpis')) LIKE N'%Estado = N''Generada''%' THROW 57963,N'El tablero todavia filtra por estado Generada.',1;
IF OBJECT_DEFINITION(OBJECT_ID(N'dbo.sp_Reportes_DashboardVentasSerie')) LIKE N'%Estado = N''Generada''%' THROW 57964,N'La serie del tablero todavia filtra por estado Generada.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0296_tablero_ventas_no_anuladas' AND Status=N'Applied') THROW 57965,N'La migracion 0296 no esta registrada.',1;
-- El tablero y el Reporte de ventas deben coincidir para el mismo rango.
DECLARE @d DATE='20000101',@h DATE='20991231',@tablero DECIMAL(18,2),@reporte DECIMAL(18,2);
DECLARE @k TABLE(Desde DATE,Hasta DATE,Ventas DECIMAL(18,2),Facturas INT,Pedidos INT,Ticket DECIMAL(18,2),StockBajo INT,Agotados INT,EnRuta INT,Cobros DECIMAL(18,2),HayDatos BIT);
INSERT @k EXEC dbo.sp_Reportes_DashboardKpis @Desde=@d,@Hasta=@h;
SELECT @tablero=Ventas FROM @k;
SELECT @reporte=ISNULL(SUM(Total),0) FROM dbo.Facturas WHERE Estado<>N'Anulada';
IF @tablero<>@reporte THROW 57966,N'El tablero y las facturas no anuladas no coinciden.',1;
PRINT N'Verificacion 0296 completada.';
