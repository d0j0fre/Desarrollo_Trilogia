SET NOCOUNT ON;
SET XACT_ABORT ON;

/*  0211 — Fecha de vencimiento de productos (BUG-05-02).

    Los productos no tenían fecha de vencimiento, así que el inventario no
    podía advertir de productos próximos a vencer (CU-052-04, CU-053-05,
    CU-054-05, CU-055-05).

    Cambios
    -------
    - dbo.Productos.FechaVencimiento DATE NULL (opcional: no todo producto vence).
    - sp_Admin_CreateProduct y sp_Admin_UpdateProduct aceptan @FechaVencimiento
      (opcional, por defecto NULL).
    - sp_Admin_GetProductById y sp_Admin_GetProducts devuelven FechaVencimiento
      como última columna (AdminDbService la lee sólo si existe).

    Requiere 0210. Es aditiva: no modifica filas existentes.
*/

IF OBJECT_ID(N'dbo.SchemaMigrationHistory', N'U') IS NULL
    THROW 55400, N'Falta el ledger 0001.', 1;

IF EXISTS (SELECT 1 FROM dbo.SchemaMigrationHistory
           WHERE MigrationId = N'0211_product_expiration_date'
             AND Status = N'Applied')
    THROW 55401, N'0211 ya figura aplicada.', 1;

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrationHistory
               WHERE MigrationId = N'0210_product_save_procedures'
                 AND Status = N'Applied')
    THROW 55402, N'0211 requiere 0210 aplicada.', 1;

BEGIN TRANSACTION;

IF COL_LENGTH(N'dbo.Productos', N'FechaVencimiento') IS NULL
    EXEC sys.sp_executesql N'ALTER TABLE dbo.Productos ADD FechaVencimiento DATE NULL;';

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
    @EsDestacado BIT = 0,
    @FechaVencimiento DATE = NULL
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
        (Nombre, Categoria, CategoriaId, Descripcion, Precio, Stock, StockMinimo, Activo, ImagenUrl, EsDestacado, FechaVencimiento)
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
         ISNULL(@EsDestacado, 0),
         @FechaVencimiento);

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
    @EsDestacado BIT = 0,
    @FechaVencimiento DATE = NULL
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
        EsDestacado = ISNULL(@EsDestacado, 0),
        FechaVencimiento = @FechaVencimiento
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
        ISNULL(StockMinimo, 5) AS StockMinimo,
        FechaVencimiento
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
        ISNULL(StockMinimo, 5) AS StockMinimo,
        FechaVencimiento
    FROM dbo.Productos
    WHERE (
            @Filtro IS NULL
            OR Nombre LIKE ''%'' + @Filtro + ''%''
            OR Categoria LIKE ''%'' + @Filtro + ''%''
          )
    ORDER BY Activo DESC, Nombre ASC;
END';

/* Validación posterior. */
IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'dbo.Productos')
                 AND name = N'FechaVencimiento'
                 AND TYPE_NAME(user_type_id) = N'date'
                 AND is_nullable = 1)
    THROW 55406, N'dbo.Productos.FechaVencimiento no quedó como DATE NULL.', 1;

IF NOT EXISTS (SELECT 1 FROM sys.parameters WHERE object_id = OBJECT_ID(N'dbo.sp_Admin_CreateProduct') AND name = N'@FechaVencimiento')
   OR NOT EXISTS (SELECT 1 FROM sys.parameters WHERE object_id = OBJECT_ID(N'dbo.sp_Admin_UpdateProduct') AND name = N'@FechaVencimiento')
    THROW 55406, N'Crear o editar producto no acepta @FechaVencimiento.', 1;

IF NOT EXISTS (SELECT 1 FROM sys.sql_modules WHERE object_id = OBJECT_ID(N'dbo.sp_Admin_GetProductById') AND definition LIKE N'%FechaVencimiento%')
   OR NOT EXISTS (SELECT 1 FROM sys.sql_modules WHERE object_id = OBJECT_ID(N'dbo.sp_Admin_GetProducts') AND definition LIKE N'%FechaVencimiento%')
    THROW 55406, N'Las consultas de productos no devuelven FechaVencimiento.', 1;

IF XACT_STATE() <> 1
    THROW 55404, N'La transacción 0211 no está disponible.', 1;

DECLARE @MigrationSha256 NVARCHAR(128) = N'$(MigrationSha256)';
IF LEN(@MigrationSha256) <> 64 OR @MigrationSha256 LIKE N'%[^0-9A-Fa-f]%'
    THROW 55405, N'SHA-256 inválido para 0211.', 1;

INSERT dbo.SchemaMigrationHistory
    (MigrationId, FileName, FileSha256, Status, AppliedBy, EnvironmentName, Notes)
VALUES
    (N'0211_product_expiration_date',
     N'0211_product_expiration_date.sql',
     UPPER(@MigrationSha256),
     N'Applied',
     ORIGINAL_LOGIN(),
     DB_NAME(),
     N'BUG-05-02: fecha de vencimiento opcional en productos y en sus procedimientos de administración.');

COMMIT TRANSACTION;
