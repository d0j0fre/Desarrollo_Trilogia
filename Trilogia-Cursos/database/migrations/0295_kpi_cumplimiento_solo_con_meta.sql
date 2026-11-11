SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL OR OBJECT_ID(N'dbo.MetasVendedor',N'U') IS NULL OR OBJECT_ID(N'dbo.sp_Kpi_CumplimientoGlobal',N'P') IS NULL THROW 57950,N'Faltan dependencias de metas y KPIs.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0295_kpi_cumplimiento_solo_con_meta' AND Status=N'Applied') THROW 57951,N'La migracion ya fue aplicada.',1;
BEGIN TRANSACTION;
GO
-- BUG-21-03: el cumplimiento global compara la venta de los vendedores CON meta contra la meta global.
-- Las ventas de quien no tiene meta salen aparte (VentaSinMeta) y ya no alteran el porcentaje.
-- Venta = facturas no anuladas con vendedor en el pedido (misma definicion que el Reporte de ventas).
CREATE OR ALTER PROCEDURE dbo.sp_Kpi_CumplimientoGlobal
    @Anio INT,
    @Mes  INT
AS
BEGIN
    SET NOCOUNT ON;
    ;WITH Ventas AS (
        SELECT p.VendedorUsuarioId, SUM(f.Total) AS VentasReales, COUNT(DISTINCT f.FacturaId) AS Facturas
        FROM dbo.Facturas f
        INNER JOIN dbo.Pedidos p ON p.PedidoId = f.PedidoId
        WHERE YEAR(f.FechaFactura) = @Anio AND MONTH(f.FechaFactura) = @Mes
          AND f.Estado <> N'Anulada'
          AND p.VendedorUsuarioId IS NOT NULL
        GROUP BY p.VendedorUsuarioId
    ),
    Metas AS (
        SELECT VendedorUsuarioId, MontoMeta FROM dbo.MetasVendedor WHERE Anio = @Anio AND Mes = @Mes
    ),
    Base AS (
        SELECT VendedorUsuarioId FROM Metas
        UNION
        SELECT VendedorUsuarioId FROM Ventas
    )
    SELECT
        b.VendedorUsuarioId,
        u.NombreCompleto AS VendedorNombre,
        ISNULL(m.MontoMeta, 0)      AS MontoMeta,
        ISNULL(v.VentasReales, 0)   AS VentasReales,
        ISNULL(v.Facturas, 0)       AS Facturas,
        CASE WHEN ISNULL(m.MontoMeta, 0) > 0
             THEN CAST(ISNULL(v.VentasReales, 0) / m.MontoMeta * 100 AS DECIMAL(9,2))
             ELSE NULL END          AS PorcentajeCumplimiento,
        CASE
            WHEN ISNULL(m.MontoMeta, 0) = 0 THEN N'SinMeta'
            WHEN ISNULL(v.VentasReales, 0) / m.MontoMeta >= 1.0  THEN N'Cumplida'
            WHEN ISNULL(v.VentasReales, 0) / m.MontoMeta >= 0.7  THEN N'EnRiesgo'
            ELSE N'Incumplida'
        END AS Clasificacion
    FROM Base b
    INNER JOIN dbo.Usuarios u ON u.UsuarioId = b.VendedorUsuarioId
    LEFT JOIN Metas m  ON m.VendedorUsuarioId = b.VendedorUsuarioId
    LEFT JOIN Ventas v ON v.VendedorUsuarioId = b.VendedorUsuarioId
    ORDER BY VentasReales DESC, VendedorNombre;

    -- Resumen global (segunda tabla de resultados)
    SELECT
        (SELECT ISNULL(SUM(MontoMeta), 0) FROM dbo.MetasVendedor WHERE Anio = @Anio AND Mes = @Mes) AS MetaGlobal,
        (SELECT ISNULL(SUM(f.Total), 0)
           FROM dbo.Facturas f INNER JOIN dbo.Pedidos p ON p.PedidoId = f.PedidoId
           WHERE YEAR(f.FechaFactura) = @Anio AND MONTH(f.FechaFactura) = @Mes
             AND f.Estado <> N'Anulada' AND p.VendedorUsuarioId IS NOT NULL
             AND EXISTS (SELECT 1 FROM dbo.MetasVendedor mv
                         WHERE mv.VendedorUsuarioId = p.VendedorUsuarioId AND mv.Anio = @Anio AND mv.Mes = @Mes AND mv.MontoMeta > 0)) AS VentaGlobal,
        (SELECT COUNT(*) FROM dbo.MetasVendedor WHERE Anio = @Anio AND Mes = @Mes) AS VendedoresConMeta,
        (SELECT ISNULL(SUM(f.Total), 0)
           FROM dbo.Facturas f INNER JOIN dbo.Pedidos p ON p.PedidoId = f.PedidoId
           WHERE YEAR(f.FechaFactura) = @Anio AND MONTH(f.FechaFactura) = @Mes
             AND f.Estado <> N'Anulada' AND p.VendedorUsuarioId IS NOT NULL
             AND NOT EXISTS (SELECT 1 FROM dbo.MetasVendedor mv
                             WHERE mv.VendedorUsuarioId = p.VendedorUsuarioId AND mv.Anio = @Anio AND mv.Mes = @Mes AND mv.MontoMeta > 0)) AS VentaSinMeta;
END;
GO
DECLARE @sha NVARCHAR(128)=N'$(MigrationSha256)';
IF LEN(@sha)<>64 OR @sha LIKE N'%[^0-9A-Fa-f]%' THROW 57952,N'SHA-256 invalido.',1;
INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes) VALUES(N'0295_kpi_cumplimiento_solo_con_meta',N'0295_kpi_cumplimiento_solo_con_meta.sql',UPPER(@sha),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),N'BUG-21-03: la venta global del reporte de KPIs solo suma vendedores con meta.');
COMMIT TRANSACTION;
GO
