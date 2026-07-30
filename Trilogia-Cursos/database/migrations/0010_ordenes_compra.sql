SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

/* =========================================================
   CU-102 / Sprint 4 - Compras y Proveedores: Órdenes de compra
   Objetivo:
   - Registrar órdenes de compra a un proveedor.
   - Confirmar recepción (total o parcial) actualizando inventario.
   - Marcar discrepancias cuando lo recibido no coincide con lo ordenado.
   Script seguro: usa IF/CREATE OR ALTER y no elimina datos existentes.
   ========================================================= */

/* 1. Tabla OrdenesCompra (encabezado) */
IF OBJECT_ID('dbo.OrdenesCompra', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.OrdenesCompra
    (
        OrdenCompraId INT IDENTITY(1,1) NOT NULL,
        ProveedorId INT NOT NULL,
        Estado NVARCHAR(20) NOT NULL CONSTRAINT DF_OrdenesCompra_Estado DEFAULT (N'Pendiente'),
        Notas NVARCHAR(300) NULL,
        UsuarioCreacionId INT NULL,
        UsuarioCreacionNombre NVARCHAR(150) NULL,
        FechaCreacion DATETIME2(0) NOT NULL CONSTRAINT DF_OrdenesCompra_FechaCreacion DEFAULT (SYSUTCDATETIME()),
        FechaRecepcion DATETIME2(0) NULL,
        CONSTRAINT PK_OrdenesCompra PRIMARY KEY (OrdenCompraId),
        CONSTRAINT FK_OrdenesCompra_Proveedores FOREIGN KEY (ProveedorId) REFERENCES dbo.Proveedores(ProveedorId),
        CONSTRAINT CK_OrdenesCompra_Estado CHECK (Estado IN (N'Pendiente', N'RecibidaParcial', N'Recibida', N'ConDiscrepancia'))
    );
END;

/* 2. Tabla DetalleOrdenCompra (líneas) */
IF OBJECT_ID('dbo.DetalleOrdenCompra', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.DetalleOrdenCompra
    (
        DetalleOrdenCompraId INT IDENTITY(1,1) NOT NULL,
        OrdenCompraId INT NOT NULL,
        ProductoId INT NOT NULL,
        CantidadOrdenada INT NOT NULL,
        CantidadRecibida INT NOT NULL CONSTRAINT DF_DetalleOrdenCompra_CantidadRecibida DEFAULT (0),
        PrecioUnitario DECIMAL(18,2) NOT NULL,
        CONSTRAINT PK_DetalleOrdenCompra PRIMARY KEY (DetalleOrdenCompraId),
        CONSTRAINT FK_DetalleOrdenCompra_Ordenes FOREIGN KEY (OrdenCompraId) REFERENCES dbo.OrdenesCompra(OrdenCompraId),
        CONSTRAINT FK_DetalleOrdenCompra_Productos FOREIGN KEY (ProductoId) REFERENCES dbo.Productos(ProductoId),
        CONSTRAINT CK_DetalleOrdenCompra_Cantidad CHECK (CantidadOrdenada > 0 AND CantidadRecibida >= 0)
    );
END;

/* 3. Registro en la bitácora de migraciones */
IF OBJECT_ID('dbo.SchemaMigrationHistory', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0010_ordenes_compra')
BEGIN
    INSERT INTO dbo.SchemaMigrationHistory
        (MigrationId, FileName, FileSha256, Status, AppliedBy, EnvironmentName, Notes)
    VALUES
        (N'0010_ordenes_compra', N'0010_ordenes_compra.sql', N'PENDIENTE_CALCULAR_SHA256',
         N'Applied', SUSER_SNAME(), N'DEV', N'CU-102 - Tablas OrdenesCompra/DetalleOrdenCompra + SPs.');
END;

COMMIT TRANSACTION;
GO

/* =========================================================
   Stored Procedures - Órdenes de Compra (CU-102)
   ========================================================= */

-- Crea el encabezado de la orden, en estado Pendiente.
CREATE OR ALTER PROCEDURE dbo.sp_Compras_CrearOrden
    @ProveedorId INT,
    @Notas NVARCHAR(300) = NULL,
    @UsuarioId INT = NULL,
    @UsuarioNombre NVARCHAR(150) = NULL,
    @NuevaOrdenId INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.Proveedores WHERE ProveedorId = @ProveedorId AND Activo = 1)
    BEGIN
        THROW 50101, 'El proveedor indicado no existe o está inactivo.', 1;
    END;

    INSERT INTO dbo.OrdenesCompra (ProveedorId, Notas, UsuarioCreacionId, UsuarioCreacionNombre)
    VALUES (@ProveedorId, @Notas, @UsuarioId, @UsuarioNombre);

    SET @NuevaOrdenId = SCOPE_IDENTITY();
END;
GO

-- Agrega una línea de producto a una orden que aún esté Pendiente.
CREATE OR ALTER PROCEDURE dbo.sp_Compras_AgregarDetalleOrden
    @OrdenCompraId INT,
    @ProductoId INT,
    @Cantidad INT,
    @PrecioUnitario DECIMAL(18,2)
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.OrdenesCompra WHERE OrdenCompraId = @OrdenCompraId AND Estado = N'Pendiente')
    BEGIN
        THROW 50102, 'Solo se pueden agregar productos a una orden en estado Pendiente.', 1;
    END;

    INSERT INTO dbo.DetalleOrdenCompra (OrdenCompraId, ProductoId, CantidadOrdenada, PrecioUnitario)
    VALUES (@OrdenCompraId, @ProductoId, @Cantidad, @PrecioUnitario);
END;
GO

-- Lista órdenes con datos del proveedor y totales, filtrando por estado/proveedor.
CREATE OR ALTER PROCEDURE dbo.sp_Compras_ListarOrdenes
    @Estado NVARCHAR(20) = NULL,
    @ProveedorId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        oc.OrdenCompraId,
        oc.ProveedorId,
        p.Nombre AS ProveedorNombre,
        oc.Estado,
        oc.Notas,
        oc.FechaCreacion,
        oc.FechaRecepcion,
        ISNULL(SUM(d.CantidadOrdenada * d.PrecioUnitario), 0) AS MontoTotal,
        ISNULL(SUM(d.CantidadOrdenada), 0) AS TotalOrdenado,
        ISNULL(SUM(d.CantidadRecibida), 0) AS TotalRecibido
    FROM dbo.OrdenesCompra oc
    INNER JOIN dbo.Proveedores p ON p.ProveedorId = oc.ProveedorId
    LEFT JOIN dbo.DetalleOrdenCompra d ON d.OrdenCompraId = oc.OrdenCompraId
    WHERE (@Estado IS NULL OR oc.Estado = @Estado)
      AND (@ProveedorId IS NULL OR oc.ProveedorId = @ProveedorId)
    GROUP BY oc.OrdenCompraId, oc.ProveedorId, p.Nombre, oc.Estado, oc.Notas, oc.FechaCreacion, oc.FechaRecepcion
    ORDER BY oc.FechaCreacion DESC;
END;
GO

-- Encabezado + líneas de una orden puntual.
CREATE OR ALTER PROCEDURE dbo.sp_Compras_ObtenerOrdenDetalle
    @OrdenCompraId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT oc.OrdenCompraId, oc.ProveedorId, p.Nombre AS ProveedorNombre, oc.Estado,
           oc.Notas, oc.FechaCreacion, oc.FechaRecepcion
    FROM dbo.OrdenesCompra oc
    INNER JOIN dbo.Proveedores p ON p.ProveedorId = oc.ProveedorId
    WHERE oc.OrdenCompraId = @OrdenCompraId;

    SELECT d.DetalleOrdenCompraId, d.OrdenCompraId, d.ProductoId, pr.Nombre AS ProductoNombre,
           d.CantidadOrdenada, d.CantidadRecibida, d.PrecioUnitario
    FROM dbo.DetalleOrdenCompra d
    INNER JOIN dbo.Productos pr ON pr.ProductoId = d.ProductoId
    WHERE d.OrdenCompraId = @OrdenCompraId
    ORDER BY d.DetalleOrdenCompraId;
END;
GO

-- Confirma recepción (total o parcial) de una línea: suma stock de forma atómica
-- y recalcula el estado general de la orden. Si lo recibido no coincide con lo
-- ordenado al cerrar la línea, la orden queda marcada con discrepancia.
CREATE OR ALTER PROCEDURE dbo.sp_Compras_RecibirDetalle
    @DetalleOrdenCompraId INT,
    @CantidadRecibidaAhora INT,
    @UsuarioId INT = NULL,
    @UsuarioNombre NVARCHAR(150) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @CantidadRecibidaAhora <= 0
    BEGIN
        THROW 50103, 'La cantidad recibida debe ser mayor a cero.', 1;
    END;

    BEGIN TRANSACTION;

    DECLARE @OrdenCompraId INT, @ProductoId INT, @CantidadOrdenada INT, @CantidadRecibidaPrevia INT;

    SELECT @OrdenCompraId = OrdenCompraId, @ProductoId = ProductoId,
           @CantidadOrdenada = CantidadOrdenada, @CantidadRecibidaPrevia = CantidadRecibida
    FROM dbo.DetalleOrdenCompra WITH (UPDLOCK, HOLDLOCK)
    WHERE DetalleOrdenCompraId = @DetalleOrdenCompraId;

    IF @OrdenCompraId IS NULL
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 50104, 'La línea de orden de compra indicada no existe.', 1;
    END;

    IF @CantidadRecibidaPrevia + @CantidadRecibidaAhora > @CantidadOrdenada
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 50105, 'La cantidad recibida no puede superar lo ordenado para esta línea.', 1;
    END;

    UPDATE dbo.DetalleOrdenCompra
    SET CantidadRecibida = CantidadRecibida + @CantidadRecibidaAhora
    WHERE DetalleOrdenCompraId = @DetalleOrdenCompraId;

    UPDATE dbo.Productos WITH (UPDLOCK, HOLDLOCK)
    SET Stock = Stock + @CantidadRecibidaAhora
    WHERE ProductoId = @ProductoId;

    -- Recalcular estado general de la orden según todas sus líneas.
    DECLARE @TotalOrdenado INT, @TotalRecibido INT, @LineasCount INT;
    SELECT @TotalOrdenado = SUM(CantidadOrdenada), @TotalRecibido = SUM(CantidadRecibida), @LineasCount = COUNT(1)
    FROM dbo.DetalleOrdenCompra
    WHERE OrdenCompraId = @OrdenCompraId;

    UPDATE dbo.OrdenesCompra
    SET Estado = CASE
            WHEN @TotalRecibido = 0 THEN N'Pendiente'
            WHEN @TotalRecibido < @TotalOrdenado THEN N'RecibidaParcial'
            WHEN @TotalRecibido = @TotalOrdenado THEN N'Recibida'
            ELSE N'ConDiscrepancia'
        END,
        FechaRecepcion = CASE WHEN @TotalRecibido >= @TotalOrdenado THEN SYSUTCDATETIME() ELSE FechaRecepcion END
    WHERE OrdenCompraId = @OrdenCompraId;

    COMMIT TRANSACTION;
END;
GO