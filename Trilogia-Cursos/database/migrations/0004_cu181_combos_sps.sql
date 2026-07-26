CREATE OR ALTER PROCEDURE dbo.sp_Admin_CreateCombo
    @Nombre NVARCHAR(150),
    @Descripcion NVARCHAR(255) = NULL,
    @Precio DECIMAL(18,2),
    @RegistradoPorUsuarioId INT,
    @RegistradoPorNombre NVARCHAR(150),
    @NuevoComboId INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.Combos (Nombre, Descripcion, Precio, RegistradoPorUsuarioId, RegistradoPorNombre)
    VALUES (@Nombre, @Descripcion, @Precio, @RegistradoPorUsuarioId, @RegistradoPorNombre);
    SET @NuevoComboId = CAST(SCOPE_IDENTITY() AS INT);
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Admin_AddComboDetail
    @ComboId INT,
    @ProductoId INT,
    @Cantidad INT
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.ComboDetalle (ComboId, ProductoId, Cantidad)
    VALUES (@ComboId, @ProductoId, @Cantidad);
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Admin_GetCombos
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        c.ComboId, c.Nombre, c.Descripcion, c.Precio, c.Activo, c.FechaCreacion, c.RegistradoPorNombre,
        (SELECT COUNT(*) FROM dbo.ComboDetalle cd WHERE cd.ComboId = c.ComboId) AS CantidadProductos,
        (SELECT MIN(p.Stock / cd.Cantidad)
         FROM dbo.ComboDetalle cd
         INNER JOIN dbo.Productos p ON p.ProductoId = cd.ProductoId
         WHERE cd.ComboId = c.ComboId) AS StockDisponibleCombo
    FROM dbo.Combos c
    ORDER BY c.FechaCreacion DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Admin_GetComboDetail
    @ComboId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT c.ComboId, c.Nombre, c.Descripcion, c.Precio, c.Activo, c.RegistradoPorNombre, c.FechaCreacion
    FROM dbo.Combos c WHERE c.ComboId = @ComboId;

    SELECT cd.ComboDetalleId, cd.ProductoId, p.Nombre AS ProductoNombre, cd.Cantidad, p.Stock AS StockDisponible
    FROM dbo.ComboDetalle cd
    INNER JOIN dbo.Productos p ON p.ProductoId = cd.ProductoId
    WHERE cd.ComboId = @ComboId
    ORDER BY cd.ComboDetalleId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Admin_ToggleComboStatus
    @ComboId INT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Combos SET Activo = CASE WHEN Activo = 1 THEN 0 ELSE 1 END WHERE ComboId = @ComboId;
END;
GO
