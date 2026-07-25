SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.Combos', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Combos (
        ComboId INT IDENTITY(1,1) PRIMARY KEY,
        Nombre NVARCHAR(150) NOT NULL,
        Descripcion NVARCHAR(255) NULL,
        Precio DECIMAL(18,2) NOT NULL,
        Activo BIT NOT NULL DEFAULT 1,
        FechaCreacion DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
        RegistradoPorUsuarioId INT NOT NULL,
        RegistradoPorNombre NVARCHAR(150) NOT NULL,
        CONSTRAINT FK_Combos_Usuario FOREIGN KEY (RegistradoPorUsuarioId) REFERENCES dbo.Usuarios(UsuarioId)
    );
END;

IF OBJECT_ID(N'dbo.ComboDetalle', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ComboDetalle (
        ComboDetalleId INT IDENTITY(1,1) PRIMARY KEY,
        ComboId INT NOT NULL,
        ProductoId INT NOT NULL,
        Cantidad INT NOT NULL,
        CONSTRAINT FK_ComboDetalle_Combo FOREIGN KEY (ComboId) REFERENCES dbo.Combos(ComboId),
        CONSTRAINT FK_ComboDetalle_Producto FOREIGN KEY (ProductoId) REFERENCES dbo.Productos(ProductoId)
    );
END;

COMMIT TRANSACTION;
