SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

/* BUG-24-02 / G1-13: la pantalla Sugerencias de compra (Finanzas > Ordenes de compra)
   mostraba promedio mensual 0.0 y cantidad 1 en productos que si tienen ventas, porque
   solo recibia de sp_Admin_GetPurchaseSuggestions los productos con cantidad sugerida
   mayor que cero y rellenaba el resto de la lista con ceros.

   Se agrega @IncluirTodos (por defecto 0: devuelve lo mismo que antes). Con 1 devuelve
   todos los productos activos con el MISMO calculo de demanda (promedio, cobertura y
   cantidad sugerida), de modo que Sugerencias de compra e Inteligencia de inventario
   usan una sola formula y ningun producto aparece con un promedio inventado.

   Reemplaza solo ese procedimiento (definicion de la 0012). No cambia datos.
   Rollback en 0290_sugerencias_compra_promedio_real.rollback.md. */
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL THROW 56900,N'Falta el ledger 0001.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0290_sugerencias_compra_promedio_real' AND Status=N'Applied')
    THROW 56901,N'0290 ya figura aplicada.',1;
IF OBJECT_ID(N'dbo.sp_Admin_GetPurchaseSuggestions',N'P') IS NULL THROW 56902,N'Falta sp_Admin_GetPurchaseSuggestions de la 0012.',1;

BEGIN TRANSACTION;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Admin_GetPurchaseSuggestions
    @MesesRecientes INT = 3,
    @MesesCobertura INT = 2,
    @IncluirTodos BIT = 0
AS
BEGIN
    SET NOCOUNT ON;
    IF @MesesRecientes NOT BETWEEN 1 AND 12 OR @MesesCobertura NOT BETWEEN 1 AND 12
        THROW 54540, N'La ventana o cobertura no es válida.', 1;

    ;WITH Sales AS
    (
        SELECT detail.ProductoId, detail.Cantidad, orders.FechaPedido
        FROM dbo.PedidoDetalle detail
        INNER JOIN dbo.Pedidos orders ON orders.PedidoId = detail.PedidoId
        WHERE orders.Estado NOT IN (N'Cancelado', N'Rechazado')
          AND detail.PedidoComboId IS NULL
        UNION ALL
        SELECT component.ProductoId, component.CantidadTotal, orders.FechaPedido
        FROM dbo.PedidoComboDetalle component
        INNER JOIN dbo.PedidoCombos combo ON combo.PedidoComboId = component.PedidoComboId
        INNER JOIN dbo.Pedidos orders ON orders.PedidoId = combo.PedidoId
        WHERE orders.Estado NOT IN (N'Cancelado', N'Rechazado')
    ), WindowSales AS
    (
        SELECT ProductoId, SUM(Cantidad) AS Units
        FROM Sales
        WHERE FechaPedido >= DATEADD(MONTH, -@MesesRecientes, CONVERT(DATE, SYSDATETIME()))
        GROUP BY ProductoId
    ), Calculation AS
    (
        SELECT product.ProductoId,
               product.Nombre,
               product.Stock AS StockActual,
               ISNULL(product.StockMinimo, 0) AS StockMinimo,
               CONVERT(INT, ISNULL(sales.Units, 0)) AS UnidadesVendidasVentana,
               CONVERT(DECIMAL(18,2), ISNULL(sales.Units, 0) / CONVERT(DECIMAL(18,4), @MesesRecientes)) AS PromedioVentaMensual,
               @MesesCobertura AS MesesCobertura,
               CONVERT(INT, CASE
                   WHEN CEILING(ISNULL(sales.Units, 0) / CONVERT(DECIMAL(18,4), @MesesRecientes) * @MesesCobertura)
                        + ISNULL(product.StockMinimo, 0) - product.Stock > 0
                   THEN CEILING(ISNULL(sales.Units, 0) / CONVERT(DECIMAL(18,4), @MesesRecientes) * @MesesCobertura)
                        + ISNULL(product.StockMinimo, 0) - product.Stock
                   ELSE 0 END) AS CantidadSugerida,
               CONVERT(BIT, IIF(sales.Units IS NULL, 1, 0)) AS DatosInsuficientes
        FROM dbo.Productos product
        LEFT JOIN WindowSales sales ON sales.ProductoId = product.ProductoId
        WHERE product.Activo = 1
    )
    SELECT ProductoId, Nombre, StockActual, StockMinimo, UnidadesVendidasVentana,
           PromedioVentaMensual, MesesCobertura, CantidadSugerida, DatosInsuficientes
    FROM Calculation
    WHERE @IncluirTodos = 1 OR CantidadSugerida > 0
    ORDER BY CantidadSugerida DESC, Nombre;
END;
GO

IF XACT_STATE()<>1 THROW 56903,N'La transacción 0290 no está disponible.',1;
DECLARE @MigrationSha256 NVARCHAR(128)=N'$(MigrationSha256)';
IF LEN(@MigrationSha256)<>64 OR @MigrationSha256 LIKE N'%[^0-9A-Fa-f]%' THROW 56904,N'SHA-256 inválido para 0290.',1;
INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes)
VALUES(N'0290_sugerencias_compra_promedio_real',N'0290_sugerencias_compra_promedio_real.sql',UPPER(@MigrationSha256),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),
       N'BUG-24-02/G1-13: sp_Admin_GetPurchaseSuggestions acepta @IncluirTodos (0 = igual que antes). Solo lectura; no cambia datos.');
COMMIT TRANSACTION;
GO
