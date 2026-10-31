SET NOCOUNT ON;
SET XACT_ABORT ON;

/*  0212 — Búsqueda de inventario por código SKU (BUG-05-03).

    El código SKU que muestra la aplicación es «SKU-» + ProductoId con cinco
    dígitos (por ejemplo SKU-00003), pero sp_Admin_GetProducts sólo filtraba
    por nombre y categoría, así que buscar «SKU-00003» no devolvía nada.

    Esta migración redefine únicamente sp_Admin_GetProducts para que el filtro
    también coincida con el código SKU. Conserva columnas y orden de 0211.
    Requiere 0211.
*/

IF OBJECT_ID(N'dbo.SchemaMigrationHistory', N'U') IS NULL
    THROW 55400, N'Falta el ledger 0001.', 1;

IF EXISTS (SELECT 1 FROM dbo.SchemaMigrationHistory
           WHERE MigrationId = N'0212_product_search_by_sku'
             AND Status = N'Applied')
    THROW 55401, N'0212 ya figura aplicada.', 1;

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrationHistory
               WHERE MigrationId = N'0211_product_expiration_date'
                 AND Status = N'Applied')
    THROW 55402, N'0212 requiere 0211 aplicada.', 1;

BEGIN TRANSACTION;

/* El orden de columnas es el que lee AdminDbService.GetProductsAsync.
   El SKU replica el formato de la vista: "SKU-" + ProductoId.ToString("D5"). */
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
            OR CONCAT(N''SKU-'', FORMAT(ProductoId, ''D5'')) LIKE ''%'' + @Filtro + ''%''
          )
    ORDER BY Activo DESC, Nombre ASC;
END';

IF NOT EXISTS (SELECT 1 FROM sys.sql_modules
               WHERE object_id = OBJECT_ID(N'dbo.sp_Admin_GetProducts')
                 AND definition LIKE N'%SKU-%'
                 AND definition LIKE N'%FechaVencimiento%')
    THROW 55406, N'sp_Admin_GetProducts no quedó con la búsqueda por SKU.', 1;

IF XACT_STATE() <> 1
    THROW 55404, N'La transacción 0212 no está disponible.', 1;

DECLARE @MigrationSha256 NVARCHAR(128) = N'$(MigrationSha256)';
IF LEN(@MigrationSha256) <> 64 OR @MigrationSha256 LIKE N'%[^0-9A-Fa-f]%'
    THROW 55405, N'SHA-256 inválido para 0212.', 1;

INSERT dbo.SchemaMigrationHistory
    (MigrationId, FileName, FileSha256, Status, AppliedBy, EnvironmentName, Notes)
VALUES
    (N'0212_product_search_by_sku',
     N'0212_product_search_by_sku.sql',
     UPPER(@MigrationSha256),
     N'Applied',
     ORIGINAL_LOGIN(),
     DB_NAME(),
     N'BUG-05-03: el buscador de inventario también coincide con el código SKU.');

COMMIT TRANSACTION;
