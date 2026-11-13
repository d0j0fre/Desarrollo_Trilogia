SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL OR OBJECT_ID(N'dbo.sp_Reportes_DashboardKpis',N'P') IS NULL OR OBJECT_ID(N'dbo.sp_Reportes_DashboardVentasSerie',N'P') IS NULL THROW 57960,N'Faltan dependencias del tablero gerencial.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0296_tablero_ventas_no_anuladas' AND Status=N'Applied') THROW 57961,N'La migracion ya fue aplicada.',1;
BEGIN TRANSACTION;
GO
-- G2-16: una sola definicion de venta = facturas no anuladas (la del Reporte de ventas).
-- Antes el tablero sumaba solo las facturas en estado Generada y daba otro total para el mismo mes.
CREATE OR ALTER PROCEDURE dbo.sp_Reportes_DashboardKpis
    @Desde DATE = NULL,
    @Hasta DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @Desde IS NULL SET @Desde = CAST(GETDATE() AS DATE);
    IF @Hasta IS NULL SET @Hasta = CAST(GETDATE() AS DATE);

    IF @Desde > @Hasta
    BEGIN
        DECLARE @Tmp DATE = @Desde; SET @Desde = @Hasta; SET @Hasta = @Tmp;
    END;

    DECLARE
        @VentasPeriodo   DECIMAL(18,2),
        @FacturasPeriodo INT,
        @PedidosPeriodo  INT,
        @TicketPromedio  DECIMAL(18,2),
        @StockBajo       INT,
        @Agotados        INT,
        @PedidosEnRuta   INT,
        @CobrosPendientes DECIMAL(18,2);

    SELECT
        @VentasPeriodo   = ISNULL(SUM(f.Total), 0),
        @FacturasPeriodo = COUNT(*)
    FROM dbo.Facturas f
    WHERE f.Estado <> N'Anulada'
      AND f.FechaFactura >= @Desde AND f.FechaFactura < DATEADD(DAY, 1, @Hasta);

    SELECT @PedidosPeriodo = COUNT(*)
    FROM dbo.Pedidos p
    WHERE CAST(p.FechaPedido AS DATE) BETWEEN @Desde AND @Hasta;

    SET @TicketPromedio = CASE WHEN @FacturasPeriodo > 0
                               THEN ROUND(@VentasPeriodo / @FacturasPeriodo, 2)
                               ELSE 0 END;

    SELECT
        @StockBajo = SUM(CASE WHEN EstadoStock = N'Bajo'    THEN 1 ELSE 0 END),
        @Agotados  = SUM(CASE WHEN EstadoStock = N'Agotado' THEN 1 ELSE 0 END)
    FROM dbo.Productos
    WHERE Activo = 1;

    SET @PedidosEnRuta = (
        SELECT COUNT(*) FROM dbo.RutaPedidos rp
        WHERE rp.EstadoEntrega = N'EnRuta'
    );

    SET @CobrosPendientes = (
        SELECT ISNULL(SUM(CASE WHEN saldo.SaldoActual > 0 THEN saldo.SaldoActual ELSE 0 END), 0)
        FROM (
            SELECT cc.UsuarioId,
                   ISNULL((
                       SELECT SUM(CASE cm.TipoMovimiento
                                       WHEN N'Cargo'          THEN  cm.Monto
                                       WHEN N'Abono'          THEN -cm.Monto
                                       WHEN N'AjustePositivo' THEN  cm.Monto
                                       ELSE                       -cm.Monto
                                  END)
                       FROM dbo.ClienteCreditoMovimientos cm
                       WHERE cm.UsuarioId = cc.UsuarioId
                   ), 0) AS SaldoActual
            FROM dbo.ClienteCreditos cc
        ) AS saldo
    );

    SELECT
        @Desde              AS Desde,
        @Hasta              AS Hasta,
        @VentasPeriodo      AS VentasPeriodo,
        @FacturasPeriodo    AS FacturasPeriodo,
        @PedidosPeriodo     AS PedidosPeriodo,
        @TicketPromedio     AS TicketPromedio,
        @StockBajo          AS StockBajo,
        @Agotados           AS ProductosAgotados,
        @PedidosEnRuta      AS PedidosEnRuta,
        @CobrosPendientes   AS CobrosPendientes,
        CAST(CASE WHEN (@FacturasPeriodo > 0 OR @PedidosPeriodo > 0) THEN 1 ELSE 0 END AS BIT) AS HayDatos;
END;
GO
CREATE OR ALTER PROCEDURE dbo.sp_Reportes_DashboardVentasSerie
    @Desde DATE = NULL,
    @Hasta DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @Desde IS NULL SET @Desde = CAST(GETDATE() AS DATE);
    IF @Hasta IS NULL SET @Hasta = CAST(GETDATE() AS DATE);
    IF @Desde > @Hasta
    BEGIN
        DECLARE @Tmp DATE = @Desde; SET @Desde = @Hasta; SET @Hasta = @Tmp;
    END;

    SELECT
        CAST(f.FechaFactura AS DATE) AS Dia,
        SUM(f.Total)                 AS Total,
        COUNT(*)                     AS Facturas
    FROM dbo.Facturas f
    WHERE f.Estado <> N'Anulada'
      AND f.FechaFactura >= @Desde AND f.FechaFactura < DATEADD(DAY, 1, @Hasta)
    GROUP BY CAST(f.FechaFactura AS DATE)
    ORDER BY Dia ASC;
END;
GO
DECLARE @sha NVARCHAR(128)=N'$(MigrationSha256)';
IF LEN(@sha)<>64 OR @sha LIKE N'%[^0-9A-Fa-f]%' THROW 57962,N'SHA-256 invalido.',1;
INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes) VALUES(N'0296_tablero_ventas_no_anuladas',N'0296_tablero_ventas_no_anuladas.sql',UPPER(@sha),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),N'G2-16: el tablero gerencial suma facturas no anuladas, igual que el Reporte de ventas.');
COMMIT TRANSACTION;
GO
