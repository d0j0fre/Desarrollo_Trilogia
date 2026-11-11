SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL OR OBJECT_ID(N'dbo.sp_Reportes_DashboardKpis',N'P') IS NULL OR OBJECT_ID(N'dbo.RutaPedidos',N'U') IS NULL OR OBJECT_ID(N'dbo.ClienteCreditoMovimientos',N'U') IS NULL THROW 57970,N'Faltan dependencias del tablero gerencial.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0297_tablero_detalle_indicadores' AND Status=N'Applied') THROW 57971,N'La migracion ya fue aplicada.',1;
BEGIN TRANSACTION;
GO
-- BUG-13-02: detalle (datos de los graficos) de cada indicador del tablero gerencial.
-- Devuelve un solo conjunto: Grupo, Etiqueta, Valor, Detalle. Los titulos se arman en la aplicacion.
CREATE OR ALTER PROCEDURE dbo.sp_Reportes_DashboardDetalle
    @Indicador NVARCHAR(20),
    @Desde DATE = NULL,
    @Hasta DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF @Indicador NOT IN (N'ventas', N'stock', N'ruta', N'cobros') THROW 57972, N'Indicador invalido.', 1;
    IF @Desde IS NULL SET @Desde = CAST(GETDATE() AS DATE);
    IF @Hasta IS NULL SET @Hasta = CAST(GETDATE() AS DATE);
    IF @Desde > @Hasta
    BEGIN
        DECLARE @Tmp DATE = @Desde; SET @Desde = @Hasta; SET @Hasta = @Tmp;
    END;

    CREATE TABLE #R (Grupo NVARCHAR(30) NOT NULL, Etiqueta NVARCHAR(150) NOT NULL, Valor DECIMAL(18,2) NOT NULL, Detalle NVARCHAR(200) NULL);

    IF @Indicador = N'ventas'
    BEGIN
        -- Misma definicion de venta del Reporte de ventas: facturas no anuladas del rango.
        INSERT #R
        SELECT TOP (10) N'categoria', ISNULL(NULLIF(LTRIM(RTRIM(p.Categoria)), N''), N'Sin categoria'),
               SUM(d.Subtotal), CONCAT(SUM(d.Cantidad), N' unidad(es)')
        FROM dbo.Facturas f
        INNER JOIN dbo.FacturaDetalle d ON d.FacturaId = f.FacturaId
        LEFT JOIN dbo.Productos p ON p.ProductoId = d.ProductoId
        WHERE f.Estado <> N'Anulada' AND f.FechaFactura >= @Desde AND f.FechaFactura < DATEADD(DAY, 1, @Hasta)
        GROUP BY ISNULL(NULLIF(LTRIM(RTRIM(p.Categoria)), N''), N'Sin categoria')
        ORDER BY SUM(d.Subtotal) DESC;

        INSERT #R
        SELECT TOP (10) N'producto', d.ProductoNombre, SUM(d.Subtotal), CONCAT(SUM(d.Cantidad), N' unidad(es)')
        FROM dbo.Facturas f
        INNER JOIN dbo.FacturaDetalle d ON d.FacturaId = f.FacturaId
        WHERE f.Estado <> N'Anulada' AND f.FechaFactura >= @Desde AND f.FechaFactura < DATEADD(DAY, 1, @Hasta)
        GROUP BY d.ProductoNombre
        ORDER BY SUM(d.Subtotal) DESC;

        INSERT #R
        SELECT TOP (10) N'vendedor', ISNULL(NULLIF(LTRIM(RTRIM(o.VendedorNombre)), N''), N'Sin vendedor'),
               SUM(f.Total), CONCAT(COUNT(*), N' factura(s)')
        FROM dbo.Facturas f
        LEFT JOIN dbo.Pedidos o ON o.PedidoId = f.PedidoId
        WHERE f.Estado <> N'Anulada' AND f.FechaFactura >= @Desde AND f.FechaFactura < DATEADD(DAY, 1, @Hasta)
        GROUP BY ISNULL(NULLIF(LTRIM(RTRIM(o.VendedorNombre)), N''), N'Sin vendedor')
        ORDER BY SUM(f.Total) DESC;
    END
    ELSE IF @Indicador = N'stock'
    BEGIN
        INSERT #R
        SELECT TOP (10) N'categoria', ISNULL(NULLIF(LTRIM(RTRIM(Categoria)), N''), N'Sin categoria'), COUNT(*), NULL
        FROM dbo.Productos
        WHERE Activo = 1 AND EstadoStock IN (N'Bajo', N'Agotado')
        GROUP BY ISNULL(NULLIF(LTRIM(RTRIM(Categoria)), N''), N'Sin categoria')
        ORDER BY COUNT(*) DESC;

        INSERT #R
        SELECT TOP (10) N'producto', Nombre, Stock, CONCAT(N'Minimo ', StockMinimo, N' - ', EstadoStock)
        FROM dbo.Productos
        WHERE Activo = 1 AND EstadoStock IN (N'Bajo', N'Agotado')
        ORDER BY Stock - StockMinimo, Nombre;
    END
    ELSE IF @Indicador = N'ruta'
    BEGIN
        -- Estado actual de las entregas (igual que la tarjeta): lo que esta en reparto ahora.
        INSERT #R
        SELECT TOP (10) N'ruta', CONCAT(r.Codigo, N' - ', r.Zona), COUNT(*), r.Estado
        FROM dbo.RutaPedidos rp
        INNER JOIN dbo.Rutas r ON r.RutaId = rp.RutaId
        WHERE rp.EstadoEntrega = N'EnRuta'
        GROUP BY r.Codigo, r.Zona, r.Estado
        ORDER BY COUNT(*) DESC, r.Codigo;

        INSERT #R
        SELECT N'estado', rp.EstadoEntrega, COUNT(*), NULL
        FROM dbo.RutaPedidos rp
        INNER JOIN dbo.Rutas r ON r.RutaId = rp.RutaId
        WHERE r.Liquidada = 0
        GROUP BY rp.EstadoEntrega;
    END
    ELSE
    BEGIN
        -- Saldos deudores actuales por cliente y movimientos del periodo por tipo.
        ;WITH saldo AS (
            SELECT cc.UsuarioId,
                   ISNULL((SELECT SUM(CASE cm.TipoMovimiento
                                           WHEN N'Cargo' THEN cm.Monto WHEN N'Abono' THEN -cm.Monto
                                           WHEN N'AjustePositivo' THEN cm.Monto ELSE -cm.Monto END)
                           FROM dbo.ClienteCreditoMovimientos cm WHERE cm.UsuarioId = cc.UsuarioId), 0) AS Saldo
            FROM dbo.ClienteCreditos cc
        )
        INSERT #R
        SELECT TOP (10) N'cliente', u.NombreCompleto, s.Saldo, u.Correo
        FROM saldo s INNER JOIN dbo.Usuarios u ON u.UsuarioId = s.UsuarioId
        WHERE s.Saldo > 0
        ORDER BY s.Saldo DESC;

        INSERT #R
        SELECT N'movimiento', cm.TipoMovimiento, SUM(cm.Monto), CONCAT(COUNT(*), N' movimiento(s)')
        FROM dbo.ClienteCreditoMovimientos cm
        WHERE cm.FechaMovimiento >= @Desde AND cm.FechaMovimiento < DATEADD(DAY, 1, @Hasta)
        GROUP BY cm.TipoMovimiento;
    END;

    SELECT Grupo, Etiqueta, Valor, Detalle FROM #R ORDER BY Grupo, Valor DESC, Etiqueta;
END;
GO
DECLARE @sha NVARCHAR(128)=N'$(MigrationSha256)';
IF LEN(@sha)<>64 OR @sha LIKE N'%[^0-9A-Fa-f]%' THROW 57973,N'SHA-256 invalido.',1;
INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes) VALUES(N'0297_tablero_detalle_indicadores',N'0297_tablero_detalle_indicadores.sql',UPPER(@sha),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),N'BUG-13-02: graficos detallados de cada indicador del tablero gerencial.');
COMMIT TRANSACTION;
GO
