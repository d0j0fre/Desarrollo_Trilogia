-- CU-182 — Reemplaza la lógica de lectura+cálculo+escritura en C# por un SP atómico
-- con bloqueo de fila (UPDLOCK/HOLDLOCK), evitando condiciones de carrera entre
-- transformaciones simultáneas sobre el mismo producto.
CREATE OR ALTER PROCEDURE dbo.sp_Admin_TransformStock
    @ProductoOrigenId INT,
    @CantidadOrigen INT,
    @ProductoDestinoId INT,
    @CantidadDestino INT,
    @Motivo NVARCHAR(300) = NULL,
    @UsuarioId INT,
    @UsuarioNombre NVARCHAR(150)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @ProductoOrigenId = @ProductoDestinoId
    BEGIN
        RAISERROR('El producto de origen y destino deben ser diferentes.', 16, 1);
        RETURN;
    END

    IF @CantidadOrigen <= 0 OR @CantidadDestino <= 0
    BEGIN
        RAISERROR('Las cantidades deben ser mayores que cero.', 16, 1);
        RETURN;
    END

    BEGIN TRANSACTION;

    DECLARE @OrigenNombre NVARCHAR(150), @OrigenStockAnterior INT;
    DECLARE @DestinoNombre NVARCHAR(150), @DestinoStockAnterior INT;

    -- UPDLOCK+HOLDLOCK bloquea la fila del origen hasta el COMMIT: si otra
    -- transformación intenta tocar el mismo producto, espera en fila (no lee stock obsoleto).
    SELECT @OrigenNombre = Nombre, @OrigenStockAnterior = Stock
    FROM dbo.Productos WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
    WHERE ProductoId = @ProductoOrigenId AND Activo = 1;

    IF @OrigenNombre IS NULL
    BEGIN
        ROLLBACK TRANSACTION;
        RAISERROR('El producto de origen no existe o está inactivo.', 16, 1);
        RETURN;
    END

    IF @OrigenStockAnterior < @CantidadOrigen
    BEGIN
        ROLLBACK TRANSACTION;
        RAISERROR('No hay suficiente stock del producto de origen para transformar.', 16, 1);
        RETURN;
    END

    SELECT @DestinoNombre = Nombre, @DestinoStockAnterior = Stock
    FROM dbo.Productos WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
    WHERE ProductoId = @ProductoDestinoId AND Activo = 1;

    IF @DestinoNombre IS NULL
    BEGIN
        ROLLBACK TRANSACTION;
        RAISERROR('El producto de destino no existe o está inactivo.', 16, 1);
        RETURN;
    END

    DECLARE @OrigenStockNuevo INT = @OrigenStockAnterior - @CantidadOrigen;
    DECLARE @DestinoStockNuevo INT = @DestinoStockAnterior + @CantidadDestino;
    DECLARE @MotivoBase NVARCHAR(300) = ISNULL(NULLIF(LTRIM(RTRIM(@Motivo)), ''), 'Transformación de producto');

    UPDATE dbo.Productos SET Stock = @OrigenStockNuevo WHERE ProductoId = @ProductoOrigenId;
    UPDATE dbo.Productos SET Stock = @DestinoStockNuevo WHERE ProductoId = @ProductoDestinoId;

    INSERT INTO dbo.MovimientosInventario
        (ProductoId, ProductoNombre, TipoMovimiento, Cantidad, StockAnterior, StockNuevo, Motivo, UsuarioId, UsuarioNombre, FechaMovimiento)
    VALUES
        (@ProductoOrigenId, @OrigenNombre, 'TransformacionSalida', @CantidadOrigen, @OrigenStockAnterior, @OrigenStockNuevo,
         @MotivoBase + N' (hacia ' + @DestinoNombre + N')', @UsuarioId, @UsuarioNombre, SYSDATETIME());

    INSERT INTO dbo.MovimientosInventario
        (ProductoId, ProductoNombre, TipoMovimiento, Cantidad, StockAnterior, StockNuevo, Motivo, UsuarioId, UsuarioNombre, FechaMovimiento)
    VALUES
        (@ProductoDestinoId, @DestinoNombre, 'TransformacionEntrada', @CantidadDestino, @DestinoStockAnterior, @DestinoStockNuevo,
         @MotivoBase + N' (desde ' + @OrigenNombre + N')', @UsuarioId, @UsuarioNombre, SYSDATETIME());

    COMMIT TRANSACTION;
END;
GO
