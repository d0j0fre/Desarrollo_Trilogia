SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

/* Pruebas post instalación, observación 1 (decisión de Esteban, dueño del producto):
   «El total del pedido debe incluir el IVA». Hasta hoy el pedido mostraba y guardaba el total SIN IVA
   (ejemplo: pedido #93, ₡2 970) y la factura sumaba el IVA (subtotal ₡2 970 + IVA ₡386,10 = ₡3 356,10),
   de modo que el cliente veía dos totales distintos.

   DECISIÓN DE DISEÑO (se guarda, no se calcula al leer)
     - dbo.Pedidos.Total pasa a ser el TOTAL CON IVA, el mismo de la factura. Así «Total comprado»,
       Mis pedidos, Administración > Pedidos, el portal, el cobro esperado de la liquidación de rutas y las
       pantallas de la app muestran el mismo número que cobra la factura, sin tocar cada lector.
     - Se agregan dbo.Pedidos.Subtotal (suma de las líneas, con promociones aplicadas) y dbo.Pedidos.Impuesto
       (IVA, ROUND(Subtotal * 0.13, 2): la regla de dbo.sp_Admin_GenerateInvoiceFromOrder, que no tiene
       productos exentos), de modo que Total = Subtotal + Impuesto.
     - La factura NO cambia: sigue calculando subtotal e IVA desde las líneas del pedido, nunca desde
       Pedidos.Total, así que el IVA no se suma dos veces. El cargo a crédito toma la factura (o el total con
       IVA de Venta móvil), el estado de cuenta usa la factura, y los reportes de ventas, las metas, el KPI y
       el tablero gerencial suman Facturas.Total: nada de eso se modifica.
     - Los lectores usan la columna nueva con ISNULL(Subtotal, Total): un INSERT antiguo no se rompe.

   QUÉ HACE ESTA MIGRACIÓN
     1. Agrega Pedidos.Subtotal e Impuesto (NULL permitido).
     2. Guarda en dbo.PedidosTotalAntesIva el total anterior de cada pedido (para el rollback exacto).
     3. Migra los pedidos existentes: si tienen factura, toman su subtotal, IVA y total (el pedido queda
        igual que la factura); si no, subtotal = total anterior, IVA = ROUND(total anterior * 0.13, 2) y
        total = subtotal + IVA. Solo toca filas con Subtotal NULL: no se aplica dos veces.
     4. dbo.sp_Store_CreateOrderWithPromotions (checkout de la Tienda) guarda Subtotal, Impuesto y Total.
     5. dbo.sp_Store_GetCheckoutResult y dbo.sp_Admin_GetOrderHeader devuelven además Subtotal e Impuesto
        (columnas nuevas al FINAL del primer conjunto de resultados: la web lee ese procedimiento por posición).
   Venta móvil se corrige en 0332 (misma regla). Rollback en 0330_pedido_total_con_iva.rollback.md. */
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL THROW 59300,N'Falta el ledger 0001.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0330_pedido_total_con_iva' AND Status=N'Applied')
    THROW 59301,N'0330 ya figura aplicada.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0325_facturar_exige_identificacion' AND Status=N'Applied')
    THROW 59302,N'0330 parte de las definiciones vigentes hasta 0325. Aplicar 0325 primero.',1;
IF OBJECT_ID(N'dbo.sp_Store_CreateOrderWithPromotions',N'P') IS NULL OR OBJECT_ID(N'dbo.sp_Admin_GetOrderHeader',N'P') IS NULL
    THROW 59303,N'Faltan los procedimientos de pedidos de 0012 y 0200.',1;

BEGIN TRANSACTION;
GO

IF COL_LENGTH(N'dbo.Pedidos',N'Subtotal') IS NULL ALTER TABLE dbo.Pedidos ADD Subtotal DECIMAL(18,2) NULL;
IF COL_LENGTH(N'dbo.Pedidos',N'Impuesto') IS NULL ALTER TABLE dbo.Pedidos ADD Impuesto DECIMAL(18,2) NULL;
IF OBJECT_ID(N'dbo.PedidosTotalAntesIva',N'U') IS NULL
    CREATE TABLE dbo.PedidosTotalAntesIva
    (
        PedidoId INT NOT NULL CONSTRAINT PK_PedidosTotalAntesIva PRIMARY KEY,
        TotalAnterior DECIMAL(18,2) NULL,
        Origen NVARCHAR(30) NOT NULL,
        FechaMigracion DATETIME2(0) NOT NULL CONSTRAINT DF_PedidosTotalAntesIva_Fecha DEFAULT (SYSDATETIME())
    );
GO

/* Migración de los pedidos existentes (solo filas sin Subtotal: no se repite). */
INSERT dbo.PedidosTotalAntesIva (PedidoId, TotalAnterior, Origen)
SELECT p.PedidoId, p.Total,
       CASE WHEN f.FacturaId IS NOT NULL THEN N'Factura' ELSE N'Total x 1,13' END
FROM dbo.Pedidos p
OUTER APPLY (SELECT TOP (1) fa.FacturaId FROM dbo.Facturas fa WHERE fa.PedidoId = p.PedidoId ORDER BY fa.FacturaId) f
WHERE p.Subtotal IS NULL
  AND NOT EXISTS (SELECT 1 FROM dbo.PedidosTotalAntesIva b WHERE b.PedidoId = p.PedidoId);

UPDATE p
SET p.Subtotal = COALESCE(f.Subtotal, ISNULL(p.Total, 0)),
    p.Impuesto = COALESCE(f.Impuesto, ROUND(ISNULL(p.Total, 0) * 0.13, 2)),
    p.Total    = COALESCE(f.Total, ISNULL(p.Total, 0) + ROUND(ISNULL(p.Total, 0) * 0.13, 2))
FROM dbo.Pedidos p
OUTER APPLY (SELECT TOP (1) fa.Subtotal, fa.Impuesto, fa.Total
             FROM dbo.Facturas fa WHERE fa.PedidoId = p.PedidoId ORDER BY fa.FacturaId) f
WHERE p.Subtotal IS NULL;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Store_CreateOrderWithPromotions
    @UsuarioId INT,
    @TipoEntrega NVARCHAR(100),
    @DireccionEntrega NVARCHAR(500) = NULL,
    @Observaciones NVARCHAR(500) = NULL,
    @IdentificacionCliente NVARCHAR(100) = NULL,
    @ItemsJson NVARCHAR(MAX),
    @MetodoPago NVARCHAR(40) = N'Efectivo contra entrega',
    @ReferenciaPago NVARCHAR(80) = NULL,
    @TokenOperacion UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    SET @TipoEntrega = NULLIF(LTRIM(RTRIM(@TipoEntrega)), N'');
    SET @DireccionEntrega = NULLIF(LTRIM(RTRIM(@DireccionEntrega)), N'');
    SET @Observaciones = NULLIF(LTRIM(RTRIM(@Observaciones)), N'');
    SET @IdentificacionCliente = NULLIF(LTRIM(RTRIM(@IdentificacionCliente)), N'');
    SET @MetodoPago = COALESCE(NULLIF(LTRIM(RTRIM(@MetodoPago)), N''), N'Efectivo contra entrega');
    SET @ReferenciaPago = NULLIF(LTRIM(RTRIM(@ReferenciaPago)), N'');

    IF @UsuarioId <= 0 OR @TokenOperacion IS NULL
        THROW 54601, N'El usuario o el identificador de checkout no es válido.', 1;
    IF @TipoEntrega IS NULL
        THROW 54602, N'El tipo de entrega es obligatorio.', 1;
    IF @MetodoPago NOT IN
       (N'Efectivo contra entrega', N'SINPE Móvil simulado', N'Tarjeta demo', N'Transferencia simulada')
        THROW 54603, N'El método de pago no es válido.', 1;
    IF ISJSON(@ItemsJson) <> 1
        THROW 54604, N'El carrito no tiene un formato válido.', 1;

    DECLARE @Items TABLE
    (
        Tipo NVARCHAR(20) NOT NULL,
        ItemId INT NOT NULL,
        ProductoId INT NULL,
        ComboId INT NULL,
        Cantidad INT NOT NULL,
        PRIMARY KEY (Tipo, ItemId)
    );

    IF EXISTS
    (
        SELECT 1
        FROM OPENJSON(@ItemsJson)
        WITH
        (
            Tipo NVARCHAR(20) N'$.tipo',
            ProductoId INT N'$.productoId',
            ComboId INT N'$.comboId',
            Cantidad INT N'$.cantidad'
        ) source
        WHERE source.Tipo IS NULL OR source.Tipo NOT IN (N'Producto', N'Combo')
           OR source.Cantidad IS NULL OR source.Cantidad NOT BETWEEN 1 AND 999
           OR (source.Tipo = N'Producto' AND (source.ProductoId IS NULL OR source.ProductoId <= 0 OR source.ComboId IS NOT NULL))
           OR (source.Tipo = N'Combo' AND (source.ComboId IS NULL OR source.ComboId <= 0 OR source.ProductoId IS NOT NULL))
    )
        THROW 54605, N'El carrito contiene una línea no válida.', 1;

    INSERT INTO @Items (Tipo, ItemId, ProductoId, ComboId, Cantidad)
    SELECT source.Tipo,
           IIF(source.Tipo = N'Producto', source.ProductoId, source.ComboId),
           source.ProductoId,
           source.ComboId,
           SUM(source.Cantidad)
    FROM OPENJSON(@ItemsJson)
    WITH
    (
        Tipo NVARCHAR(20) N'$.tipo',
        ProductoId INT N'$.productoId',
        ComboId INT N'$.comboId',
        Cantidad INT N'$.cantidad'
    ) source
    GROUP BY source.Tipo, source.ProductoId, source.ComboId;

    IF NOT EXISTS (SELECT 1 FROM @Items)
        THROW 54606, N'El carrito está vacío.', 1;
    IF EXISTS (SELECT 1 FROM @Items WHERE Cantidad NOT BETWEEN 1 AND 999)
        THROW 54607, N'La cantidad acumulada de una línea no es válida.', 1;

    DECLARE @CanonicalItems NVARCHAR(MAX);
    DECLARE @SolicitudHash BINARY(32);
    SELECT @CanonicalItems =
    (
        SELECT Tipo AS tipo,
               ProductoId AS productoId,
               ComboId AS comboId,
               Cantidad AS cantidad
        FROM @Items
        ORDER BY Tipo, ProductoId, ComboId
        FOR JSON PATH
    );
    SET @SolicitudHash = HASHBYTES
    (
        N'SHA2_256',
        CONCAT(@UsuarioId, N'|', @TipoEntrega, N'|', COALESCE(@DireccionEntrega, N''), N'|',
               COALESCE(@Observaciones, N''), N'|', COALESCE(@IdentificacionCliente, N''), N'|',
               @MetodoPago, N'|', COALESCE(@ReferenciaPago, N''), N'|', @CanonicalItems)
    );

    DECLARE @PedidoId INT;
    DECLARE @ExistingPedidoId INT;
    DECLARE @ExistingHash BINARY(32);
    DECLARE @NombreUsuario NVARCHAR(150);
    DECLARE @Segmento NVARCHAR(20);
    DECLARE @LockResult INT;
    DECLARE @LockResource NVARCHAR(255) = CONCAT(N'checkout:', @UsuarioId, N':', CONVERT(NVARCHAR(36), @TokenOperacion));

    BEGIN TRY
        BEGIN TRANSACTION;

        EXEC @LockResult = sys.sp_getapplock
            @Resource = @LockResource,
            @LockMode = N'Exclusive',
            @LockOwner = N'Transaction',
            @LockTimeout = 15000;
        IF @LockResult < 0
            THROW 54608, N'No fue posible reservar el intento de checkout. Intente nuevamente.', 1;

        SELECT @ExistingPedidoId = operation.PedidoId,
               @ExistingHash = operation.SolicitudHash
        FROM dbo.CheckoutOperaciones operation WITH (UPDLOCK, HOLDLOCK)
        WHERE operation.UsuarioId = @UsuarioId
          AND operation.TokenOperacion = @TokenOperacion;

        IF @ExistingPedidoId IS NOT NULL
        BEGIN
            IF @ExistingHash <> @SolicitudHash
                THROW 54609, N'El identificador de checkout ya fue utilizado con otra solicitud.', 1;

            COMMIT TRANSACTION;
            EXEC dbo.sp_Store_GetCheckoutResult @PedidoId = @ExistingPedidoId, @UsuarioId = @UsuarioId;
            RETURN;
        END;

        SELECT @NombreUsuario = userAccount.NombreCompleto,
               @Segmento = ISNULL(NULLIF(userAccount.SegmentoCliente, N''), N'Minorista')
        FROM dbo.Usuarios userAccount WITH (UPDLOCK, HOLDLOCK)
        WHERE userAccount.UsuarioId = @UsuarioId AND userAccount.Activo = 1;
        IF @NombreUsuario IS NULL
            THROW 54610, N'El usuario del pedido no está disponible.', 1;

        DECLARE @CartCombos TABLE
        (
            ComboId INT NOT NULL PRIMARY KEY,
            Cantidad INT NOT NULL,
            Nombre NVARCHAR(150) NOT NULL,
            Descripcion NVARCHAR(500) NULL,
            Precio DECIMAL(18,2) NOT NULL
        );
        INSERT INTO @CartCombos (ComboId, Cantidad, Nombre, Descripcion, Precio)
        SELECT combo.ComboId, item.Cantidad, combo.Nombre, combo.Descripcion, combo.Precio
        FROM @Items item
        INNER JOIN dbo.Combos combo WITH (UPDLOCK, HOLDLOCK)
            ON combo.ComboId = item.ComboId AND combo.Activo = 1
        WHERE item.Tipo = N'Combo';
        IF (SELECT COUNT(*) FROM @CartCombos) <> (SELECT COUNT(*) FROM @Items WHERE Tipo = N'Combo')
            THROW 54611, N'Uno o más combos no están disponibles.', 1;
        IF EXISTS
        (
            SELECT 1
            FROM @CartCombos cart
            WHERE NOT EXISTS (SELECT 1 FROM dbo.ComboDetalle detail WHERE detail.ComboId = cart.ComboId)
        )
            THROW 54612, N'Uno o más combos no tienen componentes.', 1;

        IF EXISTS
        (
            SELECT 1
            FROM @CartCombos cart
            CROSS APPLY
            (
                SELECT COUNT(detail.ComboDetalleId) AS CantidadComponentes,
                       SUM(CASE WHEN product.ProductoId IS NOT NULL
                                     AND product.Activo = 1
                                     AND detail.Cantidad > 0 THEN 1 ELSE 0 END) AS CantidadComponentesValidos
                FROM dbo.ComboDetalle detail
                LEFT JOIN dbo.Productos product ON product.ProductoId = detail.ProductoId
                WHERE detail.ComboId = cart.ComboId
            ) availability
            WHERE availability.CantidadComponentes = 0
               OR availability.CantidadComponentesValidos <> availability.CantidadComponentes
        )
            THROW 54612, N'Uno o más combos tienen componentes inactivos o inválidos.', 1;

        DECLARE @Candidates TABLE
        (
            PromocionId INT NOT NULL,
            ProductoId INT NOT NULL,
            Tipo NVARCHAR(25) NOT NULL,
            PorcentajeDescuento DECIMAL(5,2) NULL,
            ProductoRegaloId INT NULL,
            CantidadRegalo INT NULL,
            CantidadMinima INT NOT NULL,
            Prioridad INT NOT NULL,
            CantidadComprada INT NOT NULL,
            PRIMARY KEY (PromocionId, ProductoId)
        );
        ;WITH Ranked AS
        (
            SELECT promotion.PromocionId,
                   promotion.ProductoId,
                   promotion.Tipo,
                   promotion.PorcentajeDescuento,
                   promotion.ProductoRegaloId,
                   promotion.CantidadRegalo,
                   promotion.CantidadMinima,
                   promotion.Prioridad,
                   item.Cantidad AS CantidadComprada,
                   ROW_NUMBER() OVER
                   (
                       PARTITION BY promotion.ProductoId
                       ORDER BY promotion.Prioridad DESC, promotion.PromocionId
                   ) AS Position
            FROM @Items item
            INNER JOIN dbo.Promociones promotion WITH (UPDLOCK, HOLDLOCK)
                ON promotion.ProductoId = item.ProductoId
            WHERE item.Tipo = N'Producto'
              AND item.Cantidad >= promotion.CantidadMinima
              AND promotion.Estado = N'Activa'
              AND CONVERT(DATE, SYSDATETIME()) BETWEEN promotion.FechaInicio AND promotion.FechaFin
              AND promotion.SegmentoCliente IN (N'Todos', @Segmento)
        )
        INSERT INTO @Candidates
            (PromocionId, ProductoId, Tipo, PorcentajeDescuento, ProductoRegaloId,
             CantidadRegalo, CantidadMinima, Prioridad, CantidadComprada)
        SELECT PromocionId, ProductoId, Tipo, PorcentajeDescuento, ProductoRegaloId,
               CantidadRegalo, CantidadMinima, Prioridad, CantidadComprada
        FROM Ranked
        WHERE Position = 1;

        DECLARE @Requirements TABLE
        (
            ProductoId INT NOT NULL PRIMARY KEY,
            CantidadCarrito BIGINT NOT NULL,
            CantidadRegalo BIGINT NOT NULL DEFAULT (0)
        );
        INSERT INTO @Requirements (ProductoId, CantidadCarrito)
        SELECT source.ProductoId, SUM(CONVERT(BIGINT, source.Cantidad))
        FROM
        (
            SELECT item.ProductoId, item.Cantidad
            FROM @Items item
            WHERE item.Tipo = N'Producto'
            UNION ALL
            SELECT detail.ProductoId,
                   CONVERT(BIGINT, detail.Cantidad) * cart.Cantidad
            FROM @CartCombos cart
            INNER JOIN dbo.ComboDetalle detail ON detail.ComboId = cart.ComboId
        ) source
        GROUP BY source.ProductoId;

        INSERT INTO @Requirements (ProductoId, CantidadCarrito)
        SELECT DISTINCT candidate.ProductoRegaloId, 0
        FROM @Candidates candidate
        WHERE candidate.Tipo = N'RegaliaPorVolumen'
          AND candidate.ProductoRegaloId IS NOT NULL
          AND NOT EXISTS
              (SELECT 1 FROM @Requirements requirement WHERE requirement.ProductoId = candidate.ProductoRegaloId);

        DECLARE @LockedProducts TABLE
        (
            ProductoId INT NOT NULL PRIMARY KEY,
            Nombre NVARCHAR(150) NOT NULL,
            Categoria NVARCHAR(100) NOT NULL,
            Descripcion NVARCHAR(255) NULL,
            Precio DECIMAL(18,2) NOT NULL,
            Stock INT NOT NULL,
            ImagenUrl NVARCHAR(300) NULL,
            Activo BIT NOT NULL
        );
        DECLARE @LockProductId INT;
        DECLARE @LockName NVARCHAR(150);
        DECLARE @LockCategory NVARCHAR(100);
        DECLARE @LockDescription NVARCHAR(255);
        DECLARE @LockPrice DECIMAL(18,2);
        DECLARE @LockStock INT;
        DECLARE @LockImage NVARCHAR(300);
        DECLARE @LockActive BIT;
        DECLARE inventory_cursor CURSOR LOCAL FAST_FORWARD FOR
            SELECT ProductoId FROM @Requirements ORDER BY ProductoId;
        OPEN inventory_cursor;
        FETCH NEXT FROM inventory_cursor INTO @LockProductId;
        WHILE @@FETCH_STATUS = 0
        BEGIN
            SELECT @LockName = NULL, @LockCategory = NULL, @LockDescription = NULL,
                   @LockPrice = NULL, @LockStock = NULL, @LockImage = NULL, @LockActive = NULL;
            SELECT @LockName = product.Nombre,
                   @LockCategory = product.Categoria,
                   @LockDescription = product.Descripcion,
                   @LockPrice = product.Precio,
                   @LockStock = product.Stock,
                   @LockImage = product.ImagenUrl,
                   @LockActive = product.Activo
            FROM dbo.Productos product WITH (UPDLOCK, HOLDLOCK)
            WHERE product.ProductoId = @LockProductId;
            IF @LockName IS NOT NULL
            BEGIN
                INSERT INTO @LockedProducts
                    (ProductoId, Nombre, Categoria, Descripcion, Precio, Stock, ImagenUrl, Activo)
                VALUES
                    (@LockProductId, @LockName, @LockCategory, @LockDescription,
                     @LockPrice, @LockStock, @LockImage, @LockActive);
            END;
            FETCH NEXT FROM inventory_cursor INTO @LockProductId;
        END;
        CLOSE inventory_cursor;
        DEALLOCATE inventory_cursor;

        IF EXISTS
        (
            SELECT 1
            FROM @Requirements requirement
            LEFT JOIN @LockedProducts product ON product.ProductoId = requirement.ProductoId
            WHERE requirement.CantidadCarrito > 0
              AND (product.ProductoId IS NULL OR product.Activo = 0)
        )
            THROW 54613, N'Uno o más productos o componentes no están disponibles.', 1;

        DECLARE @CartProducts TABLE
        (
            ProductoId INT NOT NULL PRIMARY KEY,
            Cantidad INT NOT NULL,
            Nombre NVARCHAR(150) NOT NULL,
            Categoria NVARCHAR(100) NOT NULL,
            Descripcion NVARCHAR(255) NULL,
            Precio DECIMAL(18,2) NOT NULL,
            ImagenUrl NVARCHAR(300) NULL
        );
        INSERT INTO @CartProducts
            (ProductoId, Cantidad, Nombre, Categoria, Descripcion, Precio, ImagenUrl)
        SELECT item.ProductoId, item.Cantidad, product.Nombre, product.Categoria,
               product.Descripcion, product.Precio, product.ImagenUrl
        FROM @Items item
        INNER JOIN @LockedProducts product ON product.ProductoId = item.ProductoId AND product.Activo = 1
        WHERE item.Tipo = N'Producto';
        IF (SELECT COUNT(*) FROM @CartProducts) <> (SELECT COUNT(*) FROM @Items WHERE Tipo = N'Producto')
            THROW 54614, N'Uno o más productos no están disponibles.', 1;

        DECLARE @Applications TABLE
        (
            PromocionId INT NOT NULL,
            ProductoId INT NOT NULL,
            TipoBeneficio NVARCHAR(20) NOT NULL,
            MontoDescontado DECIMAL(18,2) NULL,
            UnidadesRegalo INT NULL,
            ProductoRegaloId INT NULL,
            Prioridad INT NOT NULL,
            PRIMARY KEY (PromocionId, ProductoId)
        );
        INSERT INTO @Applications
            (PromocionId, ProductoId, TipoBeneficio, MontoDescontado,
             UnidadesRegalo, ProductoRegaloId, Prioridad)
        SELECT candidate.PromocionId,
               candidate.ProductoId,
               IIF(candidate.Tipo = N'DescuentoPorcentual', N'Descuento', N'Regalia'),
               CASE WHEN candidate.Tipo = N'DescuentoPorcentual'
                    THEN ROUND(product.Cantidad * product.Precio * candidate.PorcentajeDescuento / 100.0, 2)
               END,
               CASE WHEN candidate.Tipo = N'RegaliaPorVolumen'
                    THEN (candidate.CantidadComprada / candidate.CantidadMinima) * candidate.CantidadRegalo
               END,
               candidate.ProductoRegaloId,
               candidate.Prioridad
        FROM @Candidates candidate
        INNER JOIN @CartProducts product ON product.ProductoId = candidate.ProductoId
        LEFT JOIN @LockedProducts gift
            ON gift.ProductoId = candidate.ProductoRegaloId AND gift.Activo = 1
        WHERE (candidate.Tipo = N'DescuentoPorcentual' AND candidate.PorcentajeDescuento > 0)
           OR (candidate.Tipo = N'RegaliaPorVolumen' AND gift.ProductoId IS NOT NULL
               AND candidate.CantidadRegalo > 0);

        ;WITH Allocation AS
        (
            SELECT application.PromocionId,
                   application.ProductoId,
                   CASE
                       WHEN locked.Stock - requirement.CantidadCarrito
                            - ISNULL
                              (
                                  SUM(application.UnidadesRegalo) OVER
                                  (
                                      PARTITION BY application.ProductoRegaloId
                                      ORDER BY application.Prioridad DESC, application.PromocionId, application.ProductoId
                                      ROWS BETWEEN UNBOUNDED PRECEDING AND 1 PRECEDING
                                  ), 0
                              ) <= 0 THEN 0
                       WHEN application.UnidadesRegalo <= locked.Stock - requirement.CantidadCarrito
                            - ISNULL
                              (
                                  SUM(application.UnidadesRegalo) OVER
                                  (
                                      PARTITION BY application.ProductoRegaloId
                                      ORDER BY application.Prioridad DESC, application.PromocionId, application.ProductoId
                                      ROWS BETWEEN UNBOUNDED PRECEDING AND 1 PRECEDING
                                  ), 0
                              ) THEN application.UnidadesRegalo
                       ELSE CONVERT(INT, locked.Stock - requirement.CantidadCarrito
                            - ISNULL
                              (
                                  SUM(application.UnidadesRegalo) OVER
                                  (
                                      PARTITION BY application.ProductoRegaloId
                                      ORDER BY application.Prioridad DESC, application.PromocionId, application.ProductoId
                                      ROWS BETWEEN UNBOUNDED PRECEDING AND 1 PRECEDING
                                  ), 0
                              ))
                   END AS Permitidas
            FROM @Applications application
            INNER JOIN @Requirements requirement ON requirement.ProductoId = application.ProductoRegaloId
            INNER JOIN @LockedProducts locked ON locked.ProductoId = application.ProductoRegaloId
            WHERE application.TipoBeneficio = N'Regalia'
        )
        UPDATE application
        SET UnidadesRegalo = allocation.Permitidas
        FROM @Applications application
        INNER JOIN Allocation allocation
            ON allocation.PromocionId = application.PromocionId
           AND allocation.ProductoId = application.ProductoId;
        DELETE FROM @Applications WHERE TipoBeneficio = N'Regalia' AND ISNULL(UnidadesRegalo, 0) <= 0;

        UPDATE requirement
        SET CantidadRegalo = gifts.Total
        FROM @Requirements requirement
        INNER JOIN
        (
            SELECT ProductoRegaloId, SUM(CONVERT(BIGINT, UnidadesRegalo)) AS Total
            FROM @Applications
            WHERE TipoBeneficio = N'Regalia'
            GROUP BY ProductoRegaloId
        ) gifts ON gifts.ProductoRegaloId = requirement.ProductoId;

        IF EXISTS
        (
            SELECT 1
            FROM @Requirements requirement
            INNER JOIN @LockedProducts product ON product.ProductoId = requirement.ProductoId
            WHERE requirement.CantidadCarrito + requirement.CantidadRegalo > product.Stock
               OR requirement.CantidadCarrito + requirement.CantidadRegalo > 2147483647
        )
            THROW 54615, N'No hay stock suficiente para completar el pedido.', 1;

        INSERT INTO dbo.Pedidos
        (
            UsuarioId, FechaPedido, Estado, TipoEntrega, DireccionEntrega, Total,
            Observaciones, IdentificacionCliente, MetodoPago, EstadoPago,
            ReferenciaPago, FechaPago, InventarioDescontado, FechaActualizacion
        )
        VALUES
        (
            @UsuarioId, SYSDATETIME(), N'Pendiente', @TipoEntrega, LEFT(@DireccionEntrega, 255), 0,
            LEFT(@Observaciones, 255), @IdentificacionCliente, @MetodoPago, N'Confirmado simulado',
            @ReferenciaPago, SYSDATETIME(), 0, SYSDATETIME()
        );
        SET @PedidoId = CONVERT(INT, SCOPE_IDENTITY());

        INSERT INTO dbo.PedidoDetalle
            (PedidoId, ProductoId, Cantidad, PrecioUnitario, ProductoNombreSnapshot, EsRegalo)
        SELECT @PedidoId, ProductoId, Cantidad, Precio, Nombre, 0
        FROM @CartProducts;

        DECLARE @CreatedCombos TABLE (PedidoComboId INT NOT NULL, ComboId INT NOT NULL PRIMARY KEY);
        INSERT INTO dbo.PedidoCombos
            (PedidoId, ComboId, ComboNombreSnapshot, ComboDescripcionSnapshot, Cantidad, PrecioUnitario)
        OUTPUT inserted.PedidoComboId, inserted.ComboId INTO @CreatedCombos (PedidoComboId, ComboId)
        SELECT @PedidoId, ComboId, Nombre, Descripcion, Cantidad, Precio
        FROM @CartCombos;
        INSERT INTO dbo.PedidoComboDetalle
            (PedidoComboId, ProductoId, ProductoNombreSnapshot, CantidadPorCombo, CantidadTotal)
        SELECT created.PedidoComboId,
               detail.ProductoId,
               product.Nombre,
               detail.Cantidad,
               detail.Cantidad * cart.Cantidad
        FROM @CreatedCombos created
        INNER JOIN @CartCombos cart ON cart.ComboId = created.ComboId
        INNER JOIN dbo.ComboDetalle detail ON detail.ComboId = created.ComboId
        INNER JOIN @LockedProducts product ON product.ProductoId = detail.ProductoId;

        INSERT INTO dbo.PedidoDetalle
            (PedidoId, ProductoId, Cantidad, PrecioUnitario, ProductoNombreSnapshot, EsRegalo)
        SELECT @PedidoId,
               application.ProductoRegaloId,
               SUM(application.UnidadesRegalo),
               0,
               product.Nombre,
               1
        FROM @Applications application
        INNER JOIN @LockedProducts product ON product.ProductoId = application.ProductoRegaloId
        WHERE application.TipoBeneficio = N'Regalia'
        GROUP BY application.ProductoRegaloId, product.Nombre;

        UPDATE detail
        SET detail.PrecioUnitario = CONVERT
            (
                DECIMAL(18,2),
                CASE WHEN detail.Cantidad > 0
                     THEN (detail.PrecioUnitario * detail.Cantidad - discount.Total) / detail.Cantidad
                     ELSE detail.PrecioUnitario END
            )
        FROM dbo.PedidoDetalle detail
        INNER JOIN
        (
            SELECT ProductoId, SUM(MontoDescontado) AS Total
            FROM @Applications
            WHERE TipoBeneficio = N'Descuento'
            GROUP BY ProductoId
        ) discount ON discount.ProductoId = detail.ProductoId
        WHERE detail.PedidoId = @PedidoId
          AND detail.EsRegalo = 0
          AND detail.PedidoComboId IS NULL;

        UPDATE product
        SET product.Stock = product.Stock - CONVERT(INT, requirement.CantidadCarrito + requirement.CantidadRegalo)
        FROM dbo.Productos product
        INNER JOIN @Requirements requirement ON requirement.ProductoId = product.ProductoId
        WHERE requirement.CantidadCarrito + requirement.CantidadRegalo > 0
          AND product.Stock >= requirement.CantidadCarrito + requirement.CantidadRegalo;
        IF @@ROWCOUNT <> (SELECT COUNT(*) FROM @Requirements WHERE CantidadCarrito + CantidadRegalo > 0)
            THROW 54616, N'El inventario cambió durante el checkout. Intente nuevamente.', 1;

        INSERT INTO dbo.MovimientosInventario
            (ProductoId, ProductoNombre, TipoMovimiento, Cantidad, StockAnterior, StockNuevo,
             Motivo, UsuarioId, UsuarioNombre, FechaMovimiento)
        SELECT requirement.ProductoId,
               product.Nombre,
               N'Salida',
               CONVERT(INT, requirement.CantidadCarrito + requirement.CantidadRegalo),
               product.Stock,
               product.Stock - CONVERT(INT, requirement.CantidadCarrito + requirement.CantidadRegalo),
               CONCAT(N'Pedido #', @PedidoId, N' (productos, combos y regalías)'),
               @UsuarioId,
               @NombreUsuario,
               SYSDATETIME()
        FROM @Requirements requirement
        INNER JOIN @LockedProducts product ON product.ProductoId = requirement.ProductoId
        WHERE requirement.CantidadCarrito + requirement.CantidadRegalo > 0;

        INSERT INTO dbo.PromocionAplicaciones
            (PromocionId, PedidoId, ProductoId, TipoBeneficio, MontoDescontado, UnidadesRegalo)
        SELECT PromocionId, @PedidoId, ProductoId, TipoBeneficio, MontoDescontado, UnidadesRegalo
        FROM @Applications;

        /* 0330: el pedido guarda el subtotal (lo que suman sus líneas, con las promociones ya aplicadas),
           el IVA (13 %, la misma regla y redondeo de la factura) y el total con IVA, que es el que
           ve el cliente y el que cobra la factura. */
        UPDATE orders
        SET orders.Subtotal = amounts.Subtotal,
            orders.Impuesto = ROUND(amounts.Subtotal * 0.13, 2),
            orders.Total = amounts.Subtotal + ROUND(amounts.Subtotal * 0.13, 2),
            orders.InventarioDescontado = 1,
            orders.FechaActualizacion = SYSDATETIME()
        FROM dbo.Pedidos orders
        CROSS APPLY
        (
            SELECT ISNULL((SELECT SUM(detail.Cantidad * detail.PrecioUnitario)
                           FROM dbo.PedidoDetalle detail
                           WHERE detail.PedidoId = @PedidoId
                             AND detail.PedidoComboId IS NULL), 0)
                   + ISNULL((SELECT SUM(combo.Cantidad * combo.PrecioUnitario)
                             FROM dbo.PedidoCombos combo WHERE combo.PedidoId = @PedidoId), 0) AS Subtotal
        ) amounts
        WHERE orders.PedidoId = @PedidoId;

        INSERT INTO dbo.CheckoutOperaciones (UsuarioId, TokenOperacion, SolicitudHash, PedidoId)
        VALUES (@UsuarioId, @TokenOperacion, @SolicitudHash, @PedidoId);
        INSERT INTO dbo.InventarioOperacionAuditoria
            (Modulo, EntidadId, Accion, UsuarioId, UsuarioNombre, Detalle)
        VALUES
            (N'Checkout', @PedidoId, N'Crear pedido', @UsuarioId, @NombreUsuario,
             CONCAT(N'Pedido creado con token idempotente ', CONVERT(NVARCHAR(36), @TokenOperacion), N'.'));

        COMMIT TRANSACTION;
        EXEC dbo.sp_Store_GetCheckoutResult @PedidoId = @PedidoId, @UsuarioId = @UsuarioId;
    END TRY
    BEGIN CATCH
        IF CURSOR_STATUS(N'local', N'inventory_cursor') >= 0 CLOSE inventory_cursor;
        IF CURSOR_STATUS(N'local', N'inventory_cursor') > -3 DEALLOCATE inventory_cursor;
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Store_GetCheckoutResult
    @PedidoId INT,
    @UsuarioId INT
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS
    (
        SELECT 1
        FROM dbo.Pedidos
        WHERE PedidoId = @PedidoId AND UsuarioId = @UsuarioId
    )
        THROW 54600, N'El pedido no pertenece al usuario indicado.', 1;

    SELECT PedidoId,
           ISNULL(Subtotal, Total) AS Subtotal,   -- 0330
           ISNULL(Impuesto, 0) AS Impuesto,
           Total
    FROM dbo.Pedidos
    WHERE PedidoId = @PedidoId AND UsuarioId = @UsuarioId;

    SELECT result.TipoItem,
           result.ProductoId,
           result.ComboId,
           result.Nombre,
           result.Categoria,
           result.Descripcion,
           result.PrecioUnitario,
           result.Cantidad,
           result.ImagenUrl,
           result.MontoDescuento
    FROM
    (
        SELECT N'Producto' AS TipoItem,
               detail.ProductoId,
               CONVERT(INT, NULL) AS ComboId,
               COALESCE(detail.ProductoNombreSnapshot, product.Nombre) AS Nombre,
               product.Categoria,
               product.Descripcion,
               detail.PrecioUnitario,
               detail.Cantidad,
               product.ImagenUrl,
               CONVERT(DECIMAL(18,2), ISNULL
               (
                   (
                       SELECT SUM(application.MontoDescontado)
                       FROM dbo.PromocionAplicaciones application
                       WHERE application.PedidoId = detail.PedidoId
                         AND application.ProductoId = detail.ProductoId
                         AND application.TipoBeneficio = N'Descuento'
                   ), 0
               )) AS MontoDescuento,
               1 AS SortOrder,
               detail.PedidoDetalleId AS SortId
        FROM dbo.PedidoDetalle detail
        INNER JOIN dbo.Productos product ON product.ProductoId = detail.ProductoId
        WHERE detail.PedidoId = @PedidoId
          AND detail.EsRegalo = 0
          AND detail.PedidoComboId IS NULL

        UNION ALL

        SELECT N'Combo',
               CONVERT(INT, NULL),
               combo.ComboId,
               combo.ComboNombreSnapshot,
               N'Combo',
               combo.ComboDescripcionSnapshot,
               combo.PrecioUnitario,
               combo.Cantidad,
               CONVERT(NVARCHAR(300), NULL),
               CONVERT(DECIMAL(18,2), 0),
               2,
               combo.PedidoComboId
        FROM dbo.PedidoCombos combo
        WHERE combo.PedidoId = @PedidoId
    ) result
    ORDER BY result.SortOrder, result.SortId;

    SELECT gift.ProductoId,
           product.Nombre,
           SUM(gift.Cantidad) AS Cantidad,
           MAX(appliedPromotion.Nombre) AS PromocionNombre
    FROM dbo.PedidoDetalle gift
    INNER JOIN dbo.Productos product ON product.ProductoId = gift.ProductoId
    OUTER APPLY
    (
        SELECT TOP (1) promotion.Nombre
        FROM dbo.PromocionAplicaciones application
        INNER JOIN dbo.Promociones promotion ON promotion.PromocionId = application.PromocionId
        WHERE application.PedidoId = gift.PedidoId
          AND application.TipoBeneficio = N'Regalia'
          AND promotion.ProductoRegaloId = gift.ProductoId
        ORDER BY promotion.Prioridad DESC, promotion.PromocionId
    ) appliedPromotion
    WHERE gift.PedidoId = @PedidoId
      AND gift.EsRegalo = 1
      AND gift.PedidoComboId IS NULL
    GROUP BY gift.ProductoId, product.Nombre
    ORDER BY product.Nombre, gift.ProductoId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Admin_GetOrderHeader
    @PedidoId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        p.PedidoId,
        p.UsuarioId,
        u.NombreCompleto,
        u.Correo,
        p.FechaPedido,
        p.Estado,
        p.TipoEntrega,
        p.DireccionEntrega,
        p.Observaciones,
        p.Total,
        ISNULL(p.MetodoPago, N'') AS MetodoPago,
        ISNULL(p.EstadoPago, N'') AS EstadoPago,
        p.ReferenciaPago,
        p.FechaPago,
        p.MotivoRechazo,
        ISNULL(p.Subtotal, p.Total) AS Subtotal,   -- 0330: al final, el lector de la web lee por posición
        ISNULL(p.Impuesto, 0) AS Impuesto
    FROM dbo.Pedidos p
    INNER JOIN dbo.Usuarios u ON u.UsuarioId = p.UsuarioId
    WHERE p.PedidoId = @PedidoId;
END
GO

IF XACT_STATE()<>1 THROW 59304,N'La transacción 0330 no está disponible.',1;
DECLARE @MigrationSha256 NVARCHAR(128)=N'$(MigrationSha256)';
IF LEN(@MigrationSha256)<>64 OR @MigrationSha256 LIKE N'%[^0-9A-Fa-f]%' THROW 59305,N'SHA-256 inválido para 0330.',1;
INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes)
VALUES(N'0330_pedido_total_con_iva',N'0330_pedido_total_con_iva.sql',UPPER(@MigrationSha256),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),
       N'Pruebas post instalación obs. 1: el pedido guarda Subtotal, Impuesto (IVA 13 %) y Total con IVA, igual que la factura. Migra los pedidos existentes (respaldo en PedidosTotalAntesIva). Checkout de la Tienda, sp_Store_GetCheckoutResult y sp_Admin_GetOrderHeader.');
COMMIT TRANSACTION;
GO
