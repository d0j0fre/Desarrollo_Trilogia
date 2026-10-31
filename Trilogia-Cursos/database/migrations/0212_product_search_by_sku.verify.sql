SET NOCOUNT ON;

/*  Verificación de 0212. Sólo lectura: ejecuta la consulta con el SKU del
    primer producto y comprueba que lo devuelve. */

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrationHistory
               WHERE MigrationId = N'0212_product_search_by_sku'
                 AND Status = N'Applied'
                 AND LEN(FileSha256) = 64)
    THROW 55410, N'0212 no figura aplicada correctamente.', 1;

IF NOT EXISTS (SELECT 1 FROM sys.sql_modules
               WHERE object_id = OBJECT_ID(N'dbo.sp_Admin_GetProducts')
                 AND definition LIKE N'%SKU-%')
    THROW 55411, N'sp_Admin_GetProducts no filtra por SKU.', 1;

DECLARE @ProductoId INT = (SELECT MIN(ProductoId) FROM dbo.Productos);
IF @ProductoId IS NOT NULL
BEGIN
    DECLARE @Sku NVARCHAR(20) = CONCAT(N'SKU-', FORMAT(@ProductoId, 'D5'));
    DECLARE @Resultado TABLE
    (
        ProductoId INT, Nombre NVARCHAR(150), Categoria NVARCHAR(100), Descripcion NVARCHAR(255),
        Precio DECIMAL(18,2), Stock INT, EstadoStock VARCHAR(20), Activo BIT, FechaCreacion DATETIME2,
        ImagenUrl NVARCHAR(300), EsDestacado BIT, StockMinimo INT, FechaVencimiento DATE
    );
    INSERT @Resultado EXEC dbo.sp_Admin_GetProducts @Filtro = @Sku;
    IF NOT EXISTS (SELECT 1 FROM @Resultado WHERE ProductoId = @ProductoId)
        THROW 55412, N'La búsqueda por SKU no devuelve el producto esperado.', 1;
END;

SELECT ledger.MigrationId, ledger.Status, ledger.AppliedAtUtc
FROM dbo.SchemaMigrationHistory ledger
WHERE ledger.MigrationId = N'0212_product_search_by_sku';
