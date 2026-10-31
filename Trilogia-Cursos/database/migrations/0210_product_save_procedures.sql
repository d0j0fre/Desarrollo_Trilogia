SET NOCOUNT ON;
SET XACT_ABORT ON;

/*  0210 — Procedimientos de productos alineados con la aplicación (BUG-05-01).

    Problema corregido
    ------------------
    AdminDbService.CreateProductAsync y UpdateProductAsync envían nueve
    parámetros (incluidos @StockMinimo, @ImagenUrl y @EsDestacado), pero la base
    conserva las definiciones históricas de sp_admin_modulo.sql, que sólo
    aceptan seis. SQL Server responde «has too many arguments specified», la
    excepción llega al manejador global, éste re-ejecuta /Home/Error con el
    mismo verbo POST y la acción Error (sólo GET) contesta HTTP 405: el producto
    no se crea ni se edita. La definición histórica de creación tampoco
    llenaba CategoriaId (NOT NULL con llave foránea), así que el INSERT fallaba
    aun con los parámetros correctos; ahora se resuelve por nombre.

    Además, sp_Admin_GetProductById y sp_Admin_GetProducts no devuelven
    ImagenUrl, EsDestacado ni StockMinimo, de modo que el formulario de edición
    siempre mostraba stock mínimo 5 y el listado no reflejaba el valor guardado.

    Esta migración deja las cuatro definiciones con el contrato que usa la
    aplicación. Se usa CREATE OR ALTER para conservar object_id y GRANT EXECUTE.
    No modifica tablas ni filas.
*/

IF OBJECT_ID(N'dbo.SchemaMigrationHistory', N'U') IS NULL
    THROW 55400, N'Falta el ledger 0001.', 1;

IF EXISTS (SELECT 1 FROM dbo.SchemaMigrationHistory
           WHERE MigrationId = N'0210_product_save_procedures'
             AND Status = N'Applied')
    THROW 55401, N'0210 ya figura aplicada.', 1;

IF OBJECT_ID(N'dbo.Productos', N'U') IS NULL
    THROW 55402, N'Falta la tabla dbo.Productos.', 1;

IF COL_LENGTH(N'dbo.Productos', N'StockMinimo') IS NULL
   OR COL_LENGTH(N'dbo.Productos', N'ImagenUrl') IS NULL
   OR COL_LENGTH(N'dbo.Productos', N'EsDestacado') IS NULL
   OR COL_LENGTH(N'dbo.Productos', N'EstadoStock') IS NULL
   OR COL_LENGTH(N'dbo.Productos', N'FechaCreacion') IS NULL
   OR COL_LENGTH(N'dbo.Productos', N'CategoriaId') IS NULL
   OR OBJECT_ID(N'dbo.Categorias', N'U') IS NULL
    THROW 55403, N'dbo.Productos no tiene StockMinimo, ImagenUrl, EsDestacado, EstadoStock, FechaCreacion o CategoriaId, o falta dbo.Categorias.', 1;

BEGIN TRANSACTION;

EXEC sys.sp_executesql N'
CREATE OR ALTER PROCEDURE dbo.sp_Admin_CreateProduct
    @Nombre NVARCHAR(150),
    @Categoria NVARCHAR(100),
    @Descripcion NVARCHAR(255) = NULL,
    @Precio DECIMAL(18,2),
    @Stock INT,
    @StockMinimo INT = 5,
    @Activo BIT,
    @ImagenUrl NVARCHAR(255) = NULL,
    @EsDestacado BIT = 0
AS
BEGIN
    SET NOCOUNT ON;

    IF @Stock < 0
        THROW 51001, N''El stock no puede ser negativo.'', 1;

    IF @StockMinimo < 0
        THROW 51002, N''El stock mínimo no puede ser negativo.'', 1;

    /* CategoriaId es NOT NULL con llave foránea: se resuelve por nombre. */
    DECLARE @CategoriaId INT =
        (SELECT TOP 1 CategoriaId FROM dbo.Categorias WHERE Nombre = LTRIM(RTRIM(@Categoria)));

    IF @CategoriaId IS NULL
        THROW 51006, N''La categoría indicada no existe.'', 1;

    INSERT INTO dbo.Productos
        (Nombre, Categoria, CategoriaId, Descripcion, Precio, Stock, StockMinimo, Activo, ImagenUrl, EsDestacado)
    VALUES
        (LTRIM(RTRIM(@Nombre)),
         LTRIM(RTRIM(@Categoria)),
         @CategoriaId,
         NULLIF(LTRIM(RTRIM(@Descripcion)), N''''),
         @Precio,
         @Stock,
         ISNULL(@StockMinimo, 5),
         @Activo,
         NULLIF(LTRIM(RTRIM(@ImagenUrl)), N''''),
         ISNULL(@EsDestacado, 0));

    SELECT CAST(SCOPE_IDENTITY() AS INT) AS ProductoId;
END';

EXEC sys.sp_executesql N'
CREATE OR ALTER PROCEDURE dbo.sp_Admin_UpdateProduct
    @ProductoId INT,
    @Nombre NVARCHAR(150),
    @Categoria NVARCHAR(100),
    @Descripcion NVARCHAR(255) = NULL,
    @Precio DECIMAL(18,2),
    @Stock INT,
    @StockMinimo INT = 5,
    @Activo BIT,
    @ImagenUrl NVARCHAR(255) = NULL,
    @EsDestacado BIT = 0
AS
BEGIN
    SET NOCOUNT ON;

    IF @Stock < 0
        THROW 51003, N''El stock no puede ser negativo.'', 1;

    IF @StockMinimo < 0
        THROW 51004, N''El stock mínimo no puede ser negativo.'', 1;

    UPDATE dbo.Productos
    SET Nombre = LTRIM(RTRIM(@Nombre)),
        Categoria = LTRIM(RTRIM(@Categoria)),
        Descripcion = NULLIF(LTRIM(RTRIM(@Descripcion)), N''''),
        Precio = @Precio,
        Stock = @Stock,
        StockMinimo = ISNULL(@StockMinimo, 5),
        Activo = @Activo,
        ImagenUrl = NULLIF(LTRIM(RTRIM(@ImagenUrl)), N''''),
        EsDestacado = ISNULL(@EsDestacado, 0)
    WHERE ProductoId = @ProductoId;
END';

/* El orden de columnas es el que lee AdminDbService.GetProductByIdAsync. */
EXEC sys.sp_executesql N'
CREATE OR ALTER PROCEDURE dbo.sp_Admin_GetProductById
    @ProductoId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        ProductoId,
        Nombre,
        Categoria,
        Descripcion,
        Precio,
        Stock,
        Activo,
        ISNULL(ImagenUrl, N'''') AS ImagenUrl,
        ISNULL(EsDestacado, 0) AS EsDestacado,
        ISNULL(StockMinimo, 5) AS StockMinimo
    FROM dbo.Productos
    WHERE ProductoId = @ProductoId;
END';

/* El orden de columnas es el que lee AdminDbService.GetProductsAsync. */
EXEC sys.sp_executesql N'
CREATE OR ALTER PROCEDURE dbo.sp_Admin_GetProducts
    @Filtro NVARCHAR(200) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        ProductoId,
        Nombre,
        Categoria,
        Descripcion,
        Precio,
        Stock,
        EstadoStock,
        Activo,
        FechaCreacion,
        ISNULL(ImagenUrl, N'''') AS ImagenUrl,
        ISNULL(EsDestacado, 0) AS EsDestacado,
        ISNULL(StockMinimo, 5) AS StockMinimo
    FROM dbo.Productos
    WHERE (
            @Filtro IS NULL
            OR Nombre LIKE ''%'' + @Filtro + ''%''
            OR Categoria LIKE ''%'' + @Filtro + ''%''
          )
    ORDER BY Activo DESC, Nombre ASC;
END';

/* Validación posterior: la migración se revierte si alguna firma no quedó
   con la forma esperada. */
IF (SELECT COUNT(*) FROM sys.parameters
    WHERE object_id = OBJECT_ID(N'dbo.sp_Admin_CreateProduct')
      AND name IN (N'@Nombre', N'@Categoria', N'@Descripcion', N'@Precio', N'@Stock',
                   N'@StockMinimo', N'@Activo', N'@ImagenUrl', N'@EsDestacado')) <> 9
    THROW 55406, N'sp_Admin_CreateProduct no quedó con los nueve parámetros esperados.', 1;

IF (SELECT COUNT(*) FROM sys.parameters
    WHERE object_id = OBJECT_ID(N'dbo.sp_Admin_UpdateProduct')
      AND name IN (N'@ProductoId', N'@Nombre', N'@Categoria', N'@Descripcion', N'@Precio', N'@Stock',
                   N'@StockMinimo', N'@Activo', N'@ImagenUrl', N'@EsDestacado')) <> 10
    THROW 55406, N'sp_Admin_UpdateProduct no quedó con los diez parámetros esperados.', 1;

IF NOT EXISTS (SELECT 1 FROM sys.sql_modules
               WHERE object_id = OBJECT_ID(N'dbo.sp_Admin_GetProductById')
                 AND definition LIKE N'%StockMinimo%')
   OR NOT EXISTS (SELECT 1 FROM sys.sql_modules
                  WHERE object_id = OBJECT_ID(N'dbo.sp_Admin_GetProducts')
                    AND definition LIKE N'%StockMinimo%')
    THROW 55406, N'Las consultas de productos no devuelven StockMinimo.', 1;

IF XACT_STATE() <> 1
    THROW 55404, N'La transacción 0210 no está disponible.', 1;

DECLARE @MigrationSha256 NVARCHAR(128) = N'$(MigrationSha256)';
IF LEN(@MigrationSha256) <> 64 OR @MigrationSha256 LIKE N'%[^0-9A-Fa-f]%'
    THROW 55405, N'SHA-256 inválido para 0210.', 1;

INSERT dbo.SchemaMigrationHistory
    (MigrationId, FileName, FileSha256, Status, AppliedBy, EnvironmentName, Notes)
VALUES
    (N'0210_product_save_procedures',
     N'0210_product_save_procedures.sql',
     UPPER(@MigrationSha256),
     N'Applied',
     ORIGINAL_LOGIN(),
     DB_NAME(),
     N'BUG-05-01: procedimientos de crear, editar y consultar productos con StockMinimo, ImagenUrl y EsDestacado.');

COMMIT TRANSACTION;
