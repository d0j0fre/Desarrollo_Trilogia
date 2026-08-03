SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

/* =========================================================
   CU-104 / Sprint 4 - Compras y Proveedores: Histórico de precios
   Objetivo:
   - Consultar el histórico de precios pagados por producto y proveedor.
   - Obtener el último precio pagado por producto, para alertar
     variaciones al registrar una nueva orden de compra.
   No requiere tablas nuevas: los precios ya quedan guardados en
   DetalleOrdenCompra cada vez que se registra una orden (CU-102).
   ========================================================= */

-- Histórico completo de precios pagados a proveedores por un producto puntual.
CREATE OR ALTER PROCEDURE dbo.sp_Compras_HistoricoPrecios
    @ProductoId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        oc.OrdenCompraId,
        oc.ProveedorId,
        p.Nombre AS ProveedorNombre,
        d.PrecioUnitario,
        oc.Estado,
        oc.FechaCreacion
    FROM dbo.DetalleOrdenCompra d
    INNER JOIN dbo.OrdenesCompra oc ON oc.OrdenCompraId = d.OrdenCompraId
    INNER JOIN dbo.Proveedores p ON p.ProveedorId = oc.ProveedorId
    WHERE d.ProductoId = @ProductoId
    ORDER BY oc.FechaCreacion DESC;
END;
GO

-- Último precio pagado por cada producto (el más reciente entre todos los proveedores),
-- para mostrarlo como referencia y detectar variaciones al crear una orden nueva.
CREATE OR ALTER PROCEDURE dbo.sp_Compras_UltimosPrecios
AS
BEGIN
    SET NOCOUNT ON;

    ;WITH Ultimos AS
    (
        SELECT
            d.ProductoId,
            d.PrecioUnitario,
            oc.ProveedorId,
            p.Nombre AS ProveedorNombre,
            oc.FechaCreacion,
            ROW_NUMBER() OVER (PARTITION BY d.ProductoId ORDER BY oc.FechaCreacion DESC) AS rn
        FROM dbo.DetalleOrdenCompra d
        INNER JOIN dbo.OrdenesCompra oc ON oc.OrdenCompraId = d.OrdenCompraId
        INNER JOIN dbo.Proveedores p ON p.ProveedorId = oc.ProveedorId
    )
    SELECT ProductoId, PrecioUnitario AS UltimoPrecioPagado, ProveedorNombre AS UltimoProveedor, FechaCreacion AS UltimaFecha
    FROM Ultimos
    WHERE rn = 1;
END;
GO

/* Registro en la bitácora de migraciones */
IF OBJECT_ID('dbo.SchemaMigrationHistory', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0012_compras_historico_precios')
BEGIN
    INSERT INTO dbo.SchemaMigrationHistory
        (MigrationId, FileName, FileSha256, Status, AppliedBy, EnvironmentName, Notes)
    VALUES
        (N'0012_compras_historico_precios', N'0012_compras_historico_precios.sql', N'PENDIENTE_CALCULAR_SHA256',
         N'Applied', SUSER_SNAME(), N'DEV', N'CU-104 - SPs de histórico y último precio pagado por producto.');
END;
GO