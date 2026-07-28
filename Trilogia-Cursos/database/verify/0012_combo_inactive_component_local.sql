/*
  Prueba LocalDB de componentes inactivos en combos.
  No ejecutar en Azure DEV, producción ni una base compartida. Cada bloque revierte sus datos.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @AdministradorId INT = 1;
DECLARE @ClienteId INT = 2;
DECLARE @StockOriginalProducto1 INT = (SELECT Stock FROM dbo.Productos WHERE ProductoId = 1);
DECLARE @StockOriginalProducto2 INT = (SELECT Stock FROM dbo.Productos WHERE ProductoId = 2);

IF @StockOriginalProducto1 IS NULL OR @StockOriginalProducto2 IS NULL
   OR NOT EXISTS (SELECT 1 FROM dbo.Usuarios WHERE UsuarioId = @AdministradorId AND Activo = 1)
   OR NOT EXISTS (SELECT 1 FROM dbo.Usuarios WHERE UsuarioId = @ClienteId AND Activo = 1)
    THROW 54740, N'La fixture requiere usuarios 1/2 y productos 1/2.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @ComboId INT;
    DECLARE @CatalogoAntes TABLE
    (
        ComboId INT,
        Nombre NVARCHAR(150),
        Descripcion NVARCHAR(500),
        Precio DECIMAL(18,2),
        CantidadProductos INT,
        StockDisponibleCombo INT,
        ComponentesValidos BIT,
        ComponentesResumen NVARCHAR(MAX)
    );
    DECLARE @CatalogoInactivo TABLE
    (
        ComboId INT,
        Nombre NVARCHAR(150),
        Descripcion NVARCHAR(500),
        Precio DECIMAL(18,2),
        CantidadProductos INT,
        StockDisponibleCombo INT,
        ComponentesValidos BIT,
        ComponentesResumen NVARCHAR(MAX)
    );
    DECLARE @AdminInactivo TABLE
    (
        ComboId INT,
        Nombre NVARCHAR(150),
        Descripcion NVARCHAR(500),
        Precio DECIMAL(18,2),
        Activo BIT,
        FechaCreacionUtc DATETIME2(0),
        RegistradoPorNombre NVARCHAR(150),
        CantidadProductos INT,
        StockDisponibleCombo INT,
        EstadoDisponibilidad NVARCHAR(100)
    );

    UPDATE dbo.Productos SET Activo = 1, Stock = 20 WHERE ProductoId IN (1, 2);
    EXEC dbo.sp_Admin_CreateCombo
        @Nombre = N'QA componente inactivo 0012',
        @Descripcion = N'No debe venderse si un componente queda inactivo.',
        @Precio = 1200.00,
        @ComponentesJson = N'[{"productoId":1,"cantidad":2},{"productoId":2,"cantidad":1}]',
        @UsuarioId = @AdministradorId,
        @UsuarioNombre = N'QA Local',
        @NuevoComboId = @ComboId OUTPUT;

    INSERT INTO @CatalogoAntes EXEC dbo.sp_Store_GetComboById @ComboId = @ComboId;
    IF NOT EXISTS (SELECT 1 FROM @CatalogoAntes WHERE StockDisponibleCombo = 10 AND ComponentesValidos = 1)
        THROW 54741, N'El combo activo no quedó disponible antes de inactivar un componente.', 1;

    UPDATE dbo.Productos SET Activo = 0 WHERE ProductoId = 2;
    INSERT INTO @CatalogoInactivo EXEC dbo.sp_Store_GetActiveCombos;
    INSERT INTO @AdminInactivo EXEC dbo.sp_Admin_GetCombos;

    IF EXISTS (SELECT 1 FROM @CatalogoInactivo WHERE ComboId = @ComboId)
       OR EXISTS (SELECT 1 FROM @CatalogoAntes WHERE ComboId = @ComboId AND StockDisponibleCombo <= 0)
       OR NOT EXISTS
          (
              SELECT 1
              FROM @AdminInactivo
              WHERE ComboId = @ComboId
                AND StockDisponibleCombo = 0
                AND EstadoDisponibilidad = N'Componente inactivo o faltante'
          )
        THROW 54742, N'Un componente inactivo dejó el combo visible o disponible.', 1;

    ROLLBACK TRANSACTION;

    BEGIN TRANSACTION;
    DECLARE @ComboCheckoutId INT;
    DECLARE @StockAntesCheckout1 INT;
    DECLARE @StockAntesCheckout2 INT;
    DECLARE @ItemsManipulados NVARCHAR(MAX);
    DECLARE @TokenManipulado UNIQUEIDENTIFIER = NEWID();
    UPDATE dbo.Productos SET Activo = 1, Stock = 20 WHERE ProductoId IN (1, 2);
    EXEC dbo.sp_Admin_CreateCombo
        @Nombre = N'QA checkout componente inactivo 0012',
        @Descripcion = N'La petición manipulada debe fallar sin descontar inventario.',
        @Precio = 1200.00,
        @ComponentesJson = N'[{"productoId":1,"cantidad":2},{"productoId":2,"cantidad":1}]',
        @UsuarioId = @AdministradorId,
        @UsuarioNombre = N'QA Local',
        @NuevoComboId = @ComboCheckoutId OUTPUT;
    UPDATE dbo.Productos SET Activo = 0 WHERE ProductoId = 2;
    SELECT @StockAntesCheckout1 = Stock FROM dbo.Productos WHERE ProductoId = 1;
    SELECT @StockAntesCheckout2 = Stock FROM dbo.Productos WHERE ProductoId = 2;
    SET @ItemsManipulados = CONCAT(N'[{"tipo":"Combo","productoId":null,"comboId":', @ComboCheckoutId, N',"cantidad":1}]');

    BEGIN TRY
        EXEC dbo.sp_Store_CreateOrderWithPromotions
            @UsuarioId = @ClienteId,
            @TipoEntrega = N'Retiro en tienda',
            @IdentificacionCliente = N'QA-COMBO-INACTIVO',
            @ItemsJson = @ItemsManipulados,
            @TokenOperacion = @TokenManipulado;
        THROW 54743, N'El checkout aceptó un combo con componente inactivo.', 1;
    END TRY
    BEGIN CATCH
        IF ERROR_NUMBER() NOT IN (54612, 54613)
            THROW;
    END CATCH;

    -- El procedimiento hace rollback de toda la transacción al rechazar el checkout.
    IF @@TRANCOUNT <> 0 ROLLBACK TRANSACTION;
    IF (SELECT Stock FROM dbo.Productos WHERE ProductoId = 1) <> @StockOriginalProducto1
       OR (SELECT Stock FROM dbo.Productos WHERE ProductoId = 2) <> @StockOriginalProducto2
        THROW 54744, N'El checkout rechazado alteró el inventario.', 1;

    BEGIN TRANSACTION;
    DECLARE @ComboReactivadoId INT;
    DECLARE @CatalogoReactivado TABLE
    (
        ComboId INT,
        Nombre NVARCHAR(150),
        Descripcion NVARCHAR(500),
        Precio DECIMAL(18,2),
        CantidadProductos INT,
        StockDisponibleCombo INT,
        ComponentesValidos BIT,
        ComponentesResumen NVARCHAR(MAX)
    );
    UPDATE dbo.Productos SET Activo = 1, Stock = 20 WHERE ProductoId IN (1, 2);
    EXEC dbo.sp_Admin_CreateCombo
        @Nombre = N'QA componente reactivado 0012',
        @Descripcion = N'El combo vuelve a venderse con ambos componentes activos.',
        @Precio = 1200.00,
        @ComponentesJson = N'[{"productoId":1,"cantidad":2},{"productoId":2,"cantidad":1}]',
        @UsuarioId = @AdministradorId,
        @UsuarioNombre = N'QA Local',
        @NuevoComboId = @ComboReactivadoId OUTPUT;
    INSERT INTO @CatalogoReactivado EXEC dbo.sp_Store_GetComboById @ComboId = @ComboReactivadoId;
    IF NOT EXISTS (SELECT 1 FROM @CatalogoReactivado WHERE StockDisponibleCombo = 10 AND ComponentesValidos = 1)
        THROW 54745, N'El combo no volvió a estar disponible al reactivar el componente.', 1;

    UPDATE dbo.Productos SET Stock = 0 WHERE ProductoId = 2;
    DELETE FROM @CatalogoReactivado;
    INSERT INTO @CatalogoReactivado EXEC dbo.sp_Store_GetActiveCombos;
    IF EXISTS (SELECT 1 FROM @CatalogoReactivado WHERE ComboId = @ComboReactivadoId)
        THROW 54746, N'Un combo con componente sin stock aparece disponible.', 1;

    ROLLBACK TRANSACTION;
    SELECT N'0012 inactive-component local test passed; all writes rolled back.' AS Resultado;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
