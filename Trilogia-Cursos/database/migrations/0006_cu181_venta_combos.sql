-- CU-181 — Habilita la venta de combos: nueva tabla de líneas de combo por pedido,
-- columna de trazabilidad en PedidoDetalle, catálogo de combos activos para la tienda,
-- y extensión de sp_Store_CreateOrder para descontar sus componentes de forma atómica.

IF OBJECT_ID(N'dbo.PedidoCombos', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PedidoCombos (
        PedidoComboId INT IDENTITY(1,1) PRIMARY KEY,
        PedidoId INT NOT NULL,
        ComboId INT NOT NULL,
        ComboNombre NVARCHAR(150) NOT NULL,
        PrecioUnitario DECIMAL(18,2) NOT NULL,
        Cantidad INT NOT NULL,
        CONSTRAINT FK_PedidoCombos_Pedido FOREIGN KEY (PedidoId) REFERENCES dbo.Pedidos(PedidoId),
        CONSTRAINT FK_PedidoCombos_Combo FOREIGN KEY (ComboId) REFERENCES dbo.Combos(ComboId)
    );
END
GO

IF COL_LENGTH('dbo.PedidoDetalle', 'PedidoComboId') IS NULL
BEGIN
    ALTER TABLE dbo.PedidoDetalle ADD PedidoComboId INT NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_PedidoDetalle_PedidoCombo')
BEGIN
    ALTER TABLE dbo.PedidoDetalle ADD CONSTRAINT FK_PedidoDetalle_PedidoCombo
        FOREIGN KEY (PedidoComboId) REFERENCES dbo.PedidoCombos(PedidoComboId);
END
GO

-- Catálogo de combos activos, con disponibilidad calculada, para mostrar en la tienda.
CREATE OR ALTER PROCEDURE dbo.sp_Store_GetActiveCombos
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        c.ComboId, c.Nombre, c.Descripcion, c.Precio,
        (SELECT MIN(p.Stock / cd.Cantidad)
         FROM dbo.ComboDetalle cd
         INNER JOIN dbo.Productos p ON p.ProductoId = cd.ProductoId
         WHERE cd.ComboId = c.ComboId) AS StockDisponibleCombo
    FROM dbo.Combos c
    WHERE c.Activo = 1
    ORDER BY c.Nombre;
END;
GO

-- Detalle de un combo puntual (para la tarjeta "Agregar al carrito" en la tienda).
CREATE OR ALTER PROCEDURE dbo.sp_Store_GetComboForCart
    @ComboId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        c.ComboId, c.Nombre, c.Descripcion, c.Precio, c.Activo,
        (SELECT MIN(p.Stock / cd.Cantidad)
         FROM dbo.ComboDetalle cd
         INNER JOIN dbo.Productos p ON p.ProductoId = cd.ProductoId
         WHERE cd.ComboId = c.ComboId) AS StockDisponibleCombo
    FROM dbo.Combos c
    WHERE c.ComboId = @ComboId AND c.Activo = 1;
END;
GO

-- CU-181 — sp_Store_CreateOrder ahora acepta un segundo array opcional (@CombosJson)
-- con {comboId, cantidad}. Cada combo se expande a sus productos componentes y se
-- descuenta con el MISMO patrón atómico (UPDLOCK/HOLDLOCK) que ya usan los productos
-- sueltos. El precio del pedido usa el precio propio del combo, no la suma de sus partes.
CREATE OR ALTER PROCEDURE dbo.sp_Store_CreateOrder
    @UsuarioId INT,
    @TipoEntrega NVARCHAR(100),
    @DireccionEntrega NVARCHAR(500) = NULL,
    @Observaciones NVARCHAR(500) = NULL,
    @IdentificacionCliente NVARCHAR(100) = NULL,
    @ItemsJson NVARCHAR(MAX) = NULL,
    @CombosJson NVARCHAR(MAX) = NULL,
    @MetodoPago NVARCHAR(40) = N'Efectivo contra entrega',
    @ReferenciaPago NVARCHAR(80) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE
        @PedidoId INT,
        @Total DECIMAL(18,2) = 0,
        @UsuarioNombre NVARCHAR(150),
        @FechaPago DATETIME2 = SYSDATETIME();

    SET @MetodoPago = NULLIF(LTRIM(RTRIM(@MetodoPago)), N'');
    SET @ReferenciaPago = NULLIF(LTRIM(RTRIM(@ReferenciaPago)), N'');
    IF @MetodoPago IS NULL SET @MetodoPago = N'Efectivo contra entrega';

    IF @MetodoPago NOT IN
    (
        N'Efectivo contra entrega', N'SINPE Móvil simulado', N'Tarjeta demo', N'Transferencia simulada'
    )
    BEGIN
        THROW 51101, 'El metodo de pago indicado no es valido.', 1;
    END;

    -- Productos sueltos (comportamiento original, sin cambios).
    DECLARE @Items TABLE
    (
        ProductoId INT NOT NULL PRIMARY KEY,
        Cantidad INT NOT NULL,
        Precio DECIMAL(18,2) NULL,
        StockAnterior INT NULL,
        StockNuevo INT NULL,
        ProductoNombre NVARCHAR(150) NULL
    );

    IF @ItemsJson IS NOT NULL AND LTRIM(RTRIM(@ItemsJson)) <> N''
    BEGIN
        INSERT INTO @Items (ProductoId, Cantidad)
        SELECT ProductoId, SUM(Cantidad)
        FROM OPENJSON(@ItemsJson) WITH (ProductoId INT '$.productoId', Cantidad INT '$.cantidad')
        WHERE ProductoId IS NOT NULL AND Cantidad IS NOT NULL AND Cantidad > 0
        GROUP BY ProductoId;
    END;

    -- Líneas de combo (una fila por ComboId distinto en el carrito).
    DECLARE @ComboLines TABLE
    (
        ComboId INT NOT NULL PRIMARY KEY,
        Cantidad INT NOT NULL,
        Nombre NVARCHAR(150) NULL,
        Precio DECIMAL(18,2) NULL
    );

    IF @CombosJson IS NOT NULL AND LTRIM(RTRIM(@CombosJson)) <> N''
    BEGIN
        INSERT INTO @ComboLines (ComboId, Cantidad)
        SELECT ComboId, SUM(Cantidad)
        FROM OPENJSON(@CombosJson) WITH (ComboId INT '$.comboId', Cantidad INT '$.cantidad')
        WHERE ComboId IS NOT NULL AND Cantidad IS NOT NULL AND Cantidad > 0
        GROUP BY ComboId;
    END;

    IF NOT EXISTS (SELECT 1 FROM @Items) AND NOT EXISTS (SELECT 1 FROM @ComboLines)
    BEGIN
        THROW 51103, 'El carrito esta vacio.', 1;
    END;

    -- Componentes resultantes de expandir cada combo (se descuentan aparte de los productos sueltos
    -- para poder etiquetar cada línea con el PedidoComboId que la originó).
    DECLARE @ComboItems TABLE
    (
        RowId INT IDENTITY(1,1) PRIMARY KEY,
        ComboId INT NOT NULL,
        ProductoId INT NOT NULL,
        Cantidad INT NOT NULL,
        Precio DECIMAL(18,2) NULL,
        StockAnterior INT NULL,
        StockNuevo INT NULL,
        ProductoNombre NVARCHAR(150) NULL,
        PedidoComboId INT NULL
    );

    BEGIN TRANSACTION;

    SELECT @UsuarioNombre = NombreCompleto
    FROM dbo.Usuarios WITH (UPDLOCK, HOLDLOCK)
    WHERE UsuarioId = @UsuarioId;

    IF @UsuarioNombre IS NULL
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 51104, 'El usuario del pedido no es valido.', 1;
    END;

    -- Validar y traer precio/nombre de cada combo.
    IF EXISTS (SELECT 1 FROM @ComboLines)
    BEGIN
        UPDATE cl
        SET Nombre = c.Nombre, Precio = c.Precio
        FROM @ComboLines cl
        INNER JOIN dbo.Combos c ON c.ComboId = cl.ComboId
        WHERE c.Activo = 1;

        IF EXISTS (SELECT 1 FROM @ComboLines WHERE Nombre IS NULL)
        BEGIN
            ROLLBACK TRANSACTION;
            THROW 51110, 'Uno o mas combos ya no estan disponibles.', 1;
        END;

        -- Expandir cada combo en sus productos componentes (cantidad del componente x cantidad de combos comprados).
        INSERT INTO @ComboItems (ComboId, ProductoId, Cantidad)
        SELECT cl.ComboId, cd.ProductoId, cd.Cantidad * cl.Cantidad
        FROM @ComboLines cl
        INNER JOIN dbo.ComboDetalle cd ON cd.ComboId = cl.ComboId;

        -- Mismo patrón de bloqueo que los productos sueltos: bloquea la fila hasta el COMMIT.
        UPDATE ci
        SET Precio = p.Precio, StockAnterior = p.Stock, ProductoNombre = p.Nombre
        FROM @ComboItems ci
        INNER JOIN dbo.Productos p WITH (UPDLOCK, HOLDLOCK) ON p.ProductoId = ci.ProductoId
        WHERE p.Activo = 1;

        IF EXISTS (SELECT 1 FROM @ComboItems WHERE StockAnterior IS NULL)
        BEGIN
            ROLLBACK TRANSACTION;
            THROW 51111, 'Uno o mas productos de un combo ya no estan disponibles.', 1;
        END;

        -- Si el mismo producto aparece repetido entre varios combos, se valida el total combinado.
        IF EXISTS (
            SELECT ProductoId FROM @ComboItems
            GROUP BY ProductoId, StockAnterior
            HAVING SUM(Cantidad) > MIN(StockAnterior)
        )
        BEGIN
            ROLLBACK TRANSACTION;
            THROW 51112, 'No hay stock suficiente para completar uno o mas combos.', 1;
        END;
    END;

    IF EXISTS (SELECT 1 FROM @Items)
    BEGIN
        UPDATE i
        SET Precio = p.Precio, StockAnterior = p.Stock, ProductoNombre = p.Nombre
        FROM @Items i
        INNER JOIN dbo.Productos p WITH (UPDLOCK, HOLDLOCK) ON p.ProductoId = i.ProductoId
        WHERE p.Activo = 1;

        IF EXISTS (SELECT 1 FROM @Items WHERE Precio IS NULL OR StockAnterior IS NULL OR ProductoNombre IS NULL)
        BEGIN
            ROLLBACK TRANSACTION;
            THROW 51105, 'Uno o mas productos no estan disponibles.', 1;
        END;

        IF EXISTS (SELECT 1 FROM @Items WHERE StockAnterior < Cantidad)
        BEGIN
            ROLLBACK TRANSACTION;
            THROW 51106, 'No hay stock suficiente para uno o mas productos.', 1;
        END;
    END;

    SELECT @Total = ISNULL((SELECT SUM(Precio * Cantidad) FROM @Items), 0)
                  + ISNULL((SELECT SUM(Precio * Cantidad) FROM @ComboLines), 0);

    INSERT INTO dbo.Pedidos
        (UsuarioId, FechaPedido, Estado, TipoEntrega, DireccionEntrega, Total, Observaciones,
         IdentificacionCliente, MetodoPago, EstadoPago, ReferenciaPago, FechaPago, InventarioDescontado)
    VALUES
        (@UsuarioId, SYSDATETIME(), N'Pendiente', @TipoEntrega, @DireccionEntrega, @Total, @Observaciones,
         @IdentificacionCliente, @MetodoPago, N'Confirmado simulado', @ReferenciaPago, @FechaPago, 0);

    SET @PedidoId = CAST(SCOPE_IDENTITY() AS INT);

    -- Registrar cada combo comprado y capturar su PedidoComboId recién generado.
    IF EXISTS (SELECT 1 FROM @ComboLines)
    BEGIN
        DECLARE @InsertedCombos TABLE (PedidoComboId INT, ComboId INT);

        INSERT INTO dbo.PedidoCombos (PedidoId, ComboId, ComboNombre, PrecioUnitario, Cantidad)
        OUTPUT inserted.PedidoComboId, inserted.ComboId INTO @InsertedCombos (PedidoComboId, ComboId)
        SELECT @PedidoId, ComboId, Nombre, Precio, Cantidad
        FROM @ComboLines;

        UPDATE ci
        SET ci.PedidoComboId = ic.PedidoComboId
        FROM @ComboItems ci
        INNER JOIN @InsertedCombos ic ON ic.ComboId = ci.ComboId;
    END;

    -- Descuento de stock: primero los componentes de combo, luego los productos sueltos
    -- (si un mismo producto aparece en ambos, el segundo paso ya ve el stock actualizado por el primero).
    IF EXISTS (SELECT 1 FROM @ComboItems)
    BEGIN
        UPDATE p
        SET p.Stock = p.Stock - agg.Cantidad
        FROM dbo.Productos p
        INNER JOIN (SELECT ProductoId, SUM(Cantidad) AS Cantidad FROM @ComboItems GROUP BY ProductoId) agg
            ON agg.ProductoId = p.ProductoId
        WHERE p.Stock >= agg.Cantidad;

        IF @@ROWCOUNT <> (SELECT COUNT(DISTINCT ProductoId) FROM @ComboItems)
        BEGIN
            ROLLBACK TRANSACTION;
            THROW 51113, 'No hay stock suficiente para completar uno o mas combos.', 1;
        END;

        UPDATE ci SET StockNuevo = p.Stock FROM @ComboItems ci INNER JOIN dbo.Productos p ON p.ProductoId = ci.ProductoId;

        INSERT INTO dbo.PedidoDetalle (PedidoId, ProductoId, Cantidad, PrecioUnitario, PedidoComboId)
        SELECT @PedidoId, ProductoId, Cantidad, Precio, PedidoComboId FROM @ComboItems;

        INSERT INTO dbo.MovimientosInventario
            (ProductoId, ProductoNombre, TipoMovimiento, Cantidad, StockAnterior, StockNuevo, Motivo, UsuarioId, UsuarioNombre, FechaMovimiento)
        SELECT ProductoId, ProductoNombre, N'Salida', Cantidad, StockAnterior, StockNuevo,
               CONCAT(N'Pedido #', @PedidoId, N' - combo'), @UsuarioId, @UsuarioNombre, SYSDATETIME()
        FROM @ComboItems;
    END;

    IF EXISTS (SELECT 1 FROM @Items)
    BEGIN
        INSERT INTO dbo.PedidoDetalle (PedidoId, ProductoId, Cantidad, PrecioUnitario)
        SELECT @PedidoId, ProductoId, Cantidad, Precio FROM @Items;

        UPDATE p
        SET p.Stock = p.Stock - i.Cantidad
        FROM dbo.Productos p
        INNER JOIN @Items i ON i.ProductoId = p.ProductoId
        WHERE p.Stock >= i.Cantidad;

        IF @@ROWCOUNT <> (SELECT COUNT(*) FROM @Items)
        BEGIN
            ROLLBACK TRANSACTION;
            THROW 51107, 'No hay stock suficiente para completar el pedido.', 1;
        END;

        UPDATE i SET StockNuevo = p.Stock FROM @Items i INNER JOIN dbo.Productos p ON p.ProductoId = i.ProductoId;

        INSERT INTO dbo.MovimientosInventario
            (ProductoId, ProductoNombre, TipoMovimiento, Cantidad, StockAnterior, StockNuevo, Motivo, UsuarioId, UsuarioNombre, FechaMovimiento)
        SELECT ProductoId, ProductoNombre, N'Salida', Cantidad, StockAnterior, StockNuevo,
               CONCAT(N'Pedido #', @PedidoId, N' - pago simulado'), @UsuarioId, @UsuarioNombre, SYSDATETIME()
        FROM @Items;
    END;

    UPDATE dbo.Pedidos SET InventarioDescontado = 1 WHERE PedidoId = @PedidoId;

    COMMIT TRANSACTION;

    SELECT @PedidoId;
END;
GO
