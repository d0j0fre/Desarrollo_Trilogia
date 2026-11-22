SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

/* Pruebas post instalación, observaciones 1 y 3 (decisiones de Esteban, dueño del producto), parte de
   Venta móvil (sitio web del vendedor y app Flutter, mismo procedimiento):

     1. El pedido guarda y muestra el TOTAL CON IVA (ver 0330): Subtotal = suma de las líneas, Impuesto =
        ROUND(Subtotal * 0.13, 2), Total = Subtotal + Impuesto. Es la misma cifra que la factura automática
        y que el cargo a crédito (@MontoFactura) que ya calculaba este procedimiento.
     2. Venta móvil aplica las promociones activas como la Tienda: descuento porcentual (precio unitario
        con el descuento repartido, como el checkout) y regalía por volumen (línea a precio 0 con
        EsRegalo = 1). Registra dbo.PromocionAplicaciones, así que el límite de unidades de 0214 y el
        reporte de promociones funcionan igual. La regla está en dbo.fn_Promociones_Aplicar y
        dbo.fn_Venta_Lineas (0331).

   Se reemplazan, copiando su definición vigente (0325) y cambiando solo lo indicado con «0332»:

     - dbo.sp_Seller_CreateOrder: precios por dbo.fn_Venta_Lineas; guarda Subtotal, Impuesto y Total con IVA;
       descuenta del inventario las unidades compradas MÁS las regaladas; la factura automática lleva las
       líneas del pedido (regalías a precio 0, como «Generar factura»). La retención se compara contra el
       TOTAL con IVA (@UmbralRetencion sigue en ₡50 000): el mensaje de la pantalla dice «el total del pedido
       supera el umbral», y ahora el total que ve el vendedor es el que se compara. El resultado conserva las
       cuatro columnas de siempre (PedidoId, Estado, FacturaId, NumeroFactura): hay scripts que lo capturan con INSERT … EXEC.
     - dbo.sp_Manager_ApproveOrder: al aprobar un pedido retenido descuenta las unidades compradas y
       regaladas y factura las líneas del pedido tal cual (antes agrupaba por producto con MAX(precio), lo que
       cobraría las regalías de un producto que también se compra).
     - dbo.sp_Seller_CotizarPedido (nuevo): lo que ve el vendedor ANTES de confirmar (líneas con descuento y
       regalías, subtotal, IVA, total y si quedará retenido). Solo lectura; usa las mismas funciones que el
       registro, de modo que lo cotizado es lo que se guarda.

   Sin cambios de tablas ni de datos. Requiere 0325, 0330 y 0331.
   Rollback en 0332_venta_movil_promociones_iva.rollback.md. */
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL THROW 59320,N'Falta el ledger 0001.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0332_venta_movil_promociones_iva' AND Status=N'Applied')
    THROW 59321,N'0332 ya figura aplicada.',1;
IF (SELECT COUNT(*) FROM dbo.SchemaMigrationHistory
    WHERE MigrationId IN (N'0325_facturar_exige_identificacion',N'0330_pedido_total_con_iva',N'0331_promociones_calculo_compartido')
      AND Status=N'Applied') <> 3
    THROW 59322,N'0332 requiere 0325, 0330 y 0331 aplicadas.',1;

BEGIN TRANSACTION;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Seller_CreateOrder
    @ClienteUsuarioId      INT,
    @VendedorUsuarioId     INT,
    @VendedorNombre        NVARCHAR(150),
    @TipoEntrega           NVARCHAR(100),
    @DireccionEntrega      NVARCHAR(500)   = NULL,
    @Observaciones         NVARCHAR(500)   = NULL,
    @IdentificacionCliente NVARCHAR(100)   = NULL,
    @ItemsJson             NVARCHAR(MAX),
    @PedidoOfflineGuid     UNIQUEIDENTIFIER = NULL,
    @CanalPedido           NVARCHAR(50)    = NULL,
    @UmbralRetencion       DECIMAL(18,2)   = 50000.00,
    @MetodoPago            NVARCHAR(40)    = NULL   -- 0230: N'Crédito' carga el pedido al crédito del cliente
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE
        @PedidoId        INT,
        @Total           DECIMAL(18,2),
        @Estado          NVARCHAR(30),
        @DescuentaInv    BIT           = 0,
        @FacturaId       INT           = NULL,
        @NumeroFactura   NVARCHAR(30)  = NULL,
        @ClienteNombre   NVARCHAR(150),
        @ClienteCorreo   NVARCHAR(150),
        @Subtotal        DECIMAL(18,2),
        @Impuesto        DECIMAL(18,2),
        @TotalFactura    DECIMAL(18,2),
        @CanalFinal      NVARCHAR(50)  = ISNULL(NULLIF(LTRIM(RTRIM(@CanalPedido)), N''), N'Venta móvil');

    /* 0255 (G2-39): sin método, el pedido es pago contra entrega en efectivo, con el
       mismo nombre que usa la Tienda; antes se guardaba «Crédito vendedor». */
    SET @MetodoPago = NULLIF(LTRIM(RTRIM(@MetodoPago)), N'');
    IF @MetodoPago IS NOT NULL AND @MetodoPago NOT IN (N'Crédito', N'Efectivo contra entrega')
        THROW 50717, 'El método de pago no es válido.', 1;

    /* ── Idempotencia offline: si el GUID ya existe, devolver el resultado existente ── */
    IF @PedidoOfflineGuid IS NOT NULL
    BEGIN
        DECLARE
            @ExistingFacturaId     INT           = NULL,
            @ExistingNumeroFactura NVARCHAR(30)  = NULL,
            @ExistingEstado        NVARCHAR(30)  = NULL;

        SELECT @PedidoId = p.PedidoId, @ExistingEstado = p.Estado
        FROM dbo.Pedidos p
        WHERE p.PedidoOfflineGuid = @PedidoOfflineGuid;

        IF @PedidoId IS NOT NULL
        BEGIN
            SELECT @ExistingFacturaId = FacturaId, @ExistingNumeroFactura = NumeroFactura
            FROM dbo.Facturas
            WHERE PedidoId = @PedidoId;

            SELECT
                @PedidoId                              AS PedidoId,
                @ExistingEstado                        AS Estado,
                ISNULL(@ExistingFacturaId, 0)          AS FacturaId,
                ISNULL(@ExistingNumeroFactura, N'')    AS NumeroFactura;
            RETURN;
        END;
    END;

    /* ── Validaciones ── */
    IF NOT EXISTS (
        SELECT 1
        FROM   dbo.Usuarios u
        INNER JOIN dbo.Perfiles p ON p.PerfilId = u.PerfilId
        WHERE  u.UsuarioId = @ClienteUsuarioId
          AND  u.Activo    = 1
          AND  p.Nombre    = N'Cliente'
    )
        THROW 50710, 'El cliente seleccionado no existe o está inactivo.', 1;

    IF NOT EXISTS (
        SELECT 1 FROM dbo.Usuarios
        WHERE UsuarioId = @VendedorUsuarioId AND Activo = 1
    )
        THROW 50711, 'El vendedor que registra el pedido no es válido.', 1;

    IF ISNULL(@ItemsJson, N'') = N''
        THROW 50712, 'Debe seleccionar al menos un producto.', 1;

    /* ── Parseo del carrito ── */
    DECLARE @Items TABLE (
        ProductoId     INT             NOT NULL,
        Cantidad       INT             NOT NULL,
        Precio         DECIMAL(18,2)   NULL,
        StockAnterior  INT             NULL,
        StockNuevo     INT             NULL,
        ProductoNombre NVARCHAR(150)   NULL
    );

    INSERT INTO @Items (ProductoId, Cantidad)
    SELECT ProductoId, SUM(Cantidad)
    FROM OPENJSON(@ItemsJson)
    WITH (
        ProductoId INT '$.productoId',
        Cantidad   INT '$.cantidad'
    )
    WHERE ISNULL(Cantidad, 0) > 0
    GROUP BY ProductoId;

    IF NOT EXISTS (SELECT 1 FROM @Items)
        THROW 50713, 'Debe indicar cantidades mayores a cero.', 1;

    /* ── Inicio de transacción ── */
    BEGIN TRANSACTION;

    UPDATE i
    SET    Precio         = p.Precio,
           StockAnterior  = p.Stock,
           ProductoNombre = p.Nombre
    FROM   @Items i
    INNER JOIN dbo.Productos p WITH (UPDLOCK, HOLDLOCK)
           ON p.ProductoId = i.ProductoId
    WHERE  p.Activo = 1;

    IF EXISTS (SELECT 1 FROM @Items WHERE Precio IS NULL OR StockAnterior IS NULL)
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 50714, 'Uno o más productos no están disponibles.', 1;
    END;

    IF EXISTS (SELECT 1 FROM @Items WHERE Cantidad > StockAnterior)
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 50715, 'No hay stock suficiente para uno o más productos.', 1;
    END;

    /* 0332 · Promociones (misma regla que la Tienda, ver 0331). Se bloquean las promociones de los productos
       del pedido y los productos que regalan, como hace el checkout de la Tienda, para que el cálculo no
       cambie hasta confirmar. */
    DECLARE @Bloqueo INT;
    SELECT @Bloqueo = COUNT(*)
    FROM dbo.Promociones promo WITH (UPDLOCK, HOLDLOCK)
    INNER JOIN @Items i ON i.ProductoId = promo.ProductoId;
    SELECT @Bloqueo = COUNT(*)
    FROM dbo.Productos gift WITH (UPDLOCK, HOLDLOCK)
    WHERE gift.ProductoId IN (SELECT promo.ProductoRegaloId
                              FROM dbo.Promociones promo
                              INNER JOIN @Items i ON i.ProductoId = promo.ProductoId
                              WHERE promo.Estado = N'Activa' AND promo.ProductoRegaloId IS NOT NULL);

    DECLARE @Lineas TABLE (
        Orden          INT            NOT NULL,
        ProductoId     INT            NOT NULL,
        ProductoNombre NVARCHAR(150)  NOT NULL,
        Cantidad       INT            NOT NULL,
        PrecioLista    DECIMAL(18,2)  NOT NULL,
        MontoDescuento DECIMAL(18,2)  NOT NULL,
        PrecioUnitario DECIMAL(18,2)  NOT NULL,
        EsRegalo       BIT            NOT NULL,
        PromocionId    INT            NULL,
        PromocionNombre NVARCHAR(150) NULL
    );
    INSERT INTO @Lineas (Orden, ProductoId, ProductoNombre, Cantidad, PrecioLista, MontoDescuento, PrecioUnitario,
                         EsRegalo, PromocionId, PromocionNombre)
    SELECT Orden, ProductoId, ProductoNombre, Cantidad, PrecioLista, MontoDescuento, PrecioUnitario,
           EsRegalo, PromocionId, PromocionNombre
    FROM dbo.fn_Venta_Lineas(@ClienteUsuarioId, @ItemsJson);

    DECLARE @Aplicaciones TABLE (
        PromocionId     INT           NOT NULL,
        ProductoId      INT           NOT NULL,
        TipoBeneficio   NVARCHAR(20)  NOT NULL,
        MontoDescontado DECIMAL(18,2) NULL,
        UnidadesRegalo  INT           NULL
    );
    INSERT INTO @Aplicaciones (PromocionId, ProductoId, TipoBeneficio, MontoDescontado, UnidadesRegalo)
    SELECT PromocionId, ProductoId, TipoBeneficio, MontoDescontado, UnidadesRegalo
    FROM dbo.fn_Promociones_Aplicar(@ClienteUsuarioId, @ItemsJson);

    /* Inventario que sale: unidades compradas + unidades regaladas, por producto. */
    DECLARE @Salida TABLE (
        ProductoId     INT           NOT NULL PRIMARY KEY,
        ProductoNombre NVARCHAR(150) NOT NULL,
        Cantidad       INT           NOT NULL,
        StockAnterior  INT           NULL,
        StockNuevo     INT           NULL
    );
    INSERT INTO @Salida (ProductoId, ProductoNombre, Cantidad, StockAnterior)
    SELECT l.ProductoId, MAX(l.ProductoNombre), SUM(l.Cantidad), MAX(p.Stock)
    FROM @Lineas l
    INNER JOIN dbo.Productos p ON p.ProductoId = l.ProductoId
    GROUP BY l.ProductoId;

    IF EXISTS (SELECT 1 FROM @Salida WHERE Cantidad > StockAnterior)
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 50715, 'No hay stock suficiente para uno o más productos.', 1;
    END;

    /* 0332 · Montos: subtotal de las líneas (con promociones), IVA 13 % (regla de la factura) y total con IVA. */
    SELECT @Subtotal = ISNULL(SUM(Cantidad * PrecioUnitario), 0) FROM @Lineas;
    SET @Impuesto     = ROUND(@Subtotal * 0.13, 2);
    SET @TotalFactura = @Subtotal + @Impuesto;
    SET @Total        = @TotalFactura;

    /* ── Regla de retención (se compara el total con IVA, el que ve el vendedor) ── */
    IF @Total > @UmbralRetencion
    BEGIN
        SET @Estado      = N'Retenido';
        SET @DescuentaInv = 0;
    END
    ELSE
    BEGIN
        SET @Estado      = N'Pendiente';
        SET @DescuentaInv = 1;
    END;

    /* POST-qa2-02 (CU-091-03): un pedido que se factura en este momento necesita la identificación del cliente.
       Un pedido retenido no se factura aqui: se revisa al aprobarlo (sp_Manager_ApproveOrder). */
    IF @DescuentaInv = 1 AND NULLIF(LTRIM(RTRIM(@IdentificacionCliente)), N'') IS NULL
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 50718, 'Falta la identificación del cliente para facturar.', 1;
    END;

    SELECT @ClienteNombre = NombreCompleto, @ClienteCorreo = Correo
    FROM   dbo.Usuarios
    WHERE  UsuarioId = @ClienteUsuarioId;

    /* ── Insertar pedido ── */
    INSERT INTO dbo.Pedidos (
        UsuarioId, FechaPedido, Estado, TipoEntrega, DireccionEntrega,
        Subtotal, Impuesto, Total, Observaciones, IdentificacionCliente,
        VendedorUsuarioId, VendedorNombre, CanalPedido, FechaActualizacion,
        PedidoOfflineGuid, MetodoPago, EstadoPago, InventarioDescontado
    )
    VALUES (
        @ClienteUsuarioId, SYSDATETIME(), @Estado,
        @TipoEntrega, LEFT(ISNULL(@DireccionEntrega, N''), 255),
        @Subtotal, @Impuesto, @Total, LEFT(ISNULL(@Observaciones, N''), 255), @IdentificacionCliente,   -- 0332
        @VendedorUsuarioId, @VendedorNombre, @CanalFinal, SYSDATETIME(),
        @PedidoOfflineGuid, ISNULL(@MetodoPago, N'Efectivo contra entrega'), N'Pendiente', @DescuentaInv  -- 0230, 0255
    );

    SET @PedidoId = CAST(SCOPE_IDENTITY() AS INT);

    INSERT INTO dbo.PedidoDetalle (PedidoId, ProductoId, Cantidad, PrecioUnitario, ProductoNombreSnapshot, EsRegalo)
    SELECT @PedidoId, ProductoId, Cantidad, PrecioUnitario, ProductoNombre, EsRegalo   -- 0332: con descuento y regalías
    FROM   @Lineas
    ORDER BY Orden, ProductoId;

    /* 0332 · Promociones aplicadas (alimenta el límite de unidades de 0214 y el reporte de promociones). */
    INSERT INTO dbo.PromocionAplicaciones (PromocionId, PedidoId, ProductoId, TipoBeneficio, MontoDescontado, UnidadesRegalo)
    SELECT PromocionId, @PedidoId, ProductoId, TipoBeneficio, MontoDescontado, UnidadesRegalo
    FROM   @Aplicaciones;

    /* ── Solo para pedidos NO retenidos: inventario + CU-091 auto-factura ── */
    IF @DescuentaInv = 1
    BEGIN
        UPDATE p
        SET    p.Stock = p.Stock - s.Cantidad
        FROM   dbo.Productos p
        INNER JOIN @Salida s ON s.ProductoId = p.ProductoId
        WHERE  p.Stock >= s.Cantidad;

        IF @@ROWCOUNT <> (SELECT COUNT(*) FROM @Salida)
        BEGIN
            ROLLBACK TRANSACTION;
            THROW 50716, 'Concurrencia de stock detectada. Intente nuevamente.', 1;
        END;

        UPDATE s
        SET StockNuevo = p.Stock
        FROM @Salida s
        INNER JOIN dbo.Productos p ON p.ProductoId = s.ProductoId;

        INSERT INTO dbo.MovimientosInventario (
            ProductoId, ProductoNombre, TipoMovimiento, Cantidad,
            StockAnterior, StockNuevo, Motivo, UsuarioId, UsuarioNombre, FechaMovimiento
        )
        SELECT
            ProductoId, ProductoNombre, N'Salida', Cantidad,
            StockAnterior, StockNuevo,
            CONCAT(N'Pedido móvil #', @PedidoId, N' - vendedor: ', @VendedorNombre),
            @VendedorUsuarioId, @VendedorNombre, SYSDATETIME()
        FROM @Salida;

        /* CU-091: auto-factura */
        SET @NumeroFactura = CONCAT(
            N'FAC-',
            FORMAT(SYSDATETIME(), 'yyyyMMddHHmmss'),
            N'-',
            RIGHT(CONCAT(N'0000', CAST(@PedidoId AS NVARCHAR(4))), 4)
        );

        INSERT INTO dbo.Facturas (
            PedidoId, NumeroFactura, UsuarioId,
            ClienteNombre, ClienteCorreo,
            Subtotal, Impuesto, Total, Estado
        )
        VALUES (
            @PedidoId, @NumeroFactura, @ClienteUsuarioId,
            @ClienteNombre, @ClienteCorreo,
            @Subtotal, @Impuesto, @TotalFactura, N'Generada'
        );

        SET @FacturaId = CAST(SCOPE_IDENTITY() AS INT);

        INSERT INTO dbo.FacturaDetalle (
            FacturaId, ProductoId, ProductoNombre, Cantidad, PrecioUnitario
        )
        SELECT @FacturaId, ProductoId, ProductoNombre, Cantidad, PrecioUnitario   -- 0332: las líneas del pedido
        FROM   @Lineas
        ORDER BY Orden, ProductoId;
    END;

    /* 0230: el pedido a crédito valida el crédito y registra el cargo por el
       total facturado; si no procede, el THROW revierte el pedido completo
       (XACT_ABORT). */
    IF @MetodoPago = N'Crédito'
        EXEC dbo.sp_Credito_CargarPedido
            @PedidoId = @PedidoId,
            @RegistradoPorUsuarioId = @VendedorUsuarioId,
            @RegistradoPorNombre = @VendedorNombre,
            @MontoFactura = @TotalFactura;

    COMMIT TRANSACTION;

    SELECT
        @PedidoId                      AS PedidoId,
        @Estado                        AS Estado,
        ISNULL(@FacturaId, 0)          AS FacturaId,
        ISNULL(@NumeroFactura, N'')    AS NumeroFactura;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Manager_ApproveOrder
    @PedidoId      INT,
    @UsuarioId     INT,
    @UsuarioNombre NVARCHAR(150)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE
        @EstadoActual  NVARCHAR(30),
        @ClienteId     INT,
        @ClienteNombre NVARCHAR(150),
        @ClienteCorreo NVARCHAR(150),
        @FacturaId     INT,
        @NumeroFactura NVARCHAR(30),
        @Subtotal      DECIMAL(18,2),
        @Impuesto      DECIMAL(18,2),
        @TotalFactura  DECIMAL(18,2),
        @Identificacion NVARCHAR(100);

    IF @PedidoId IS NULL OR @PedidoId <= 0
        THROW 50730, 'El pedido indicado no es válido.', 1;

    /* 0332: inventario por producto (unidades compradas + regaladas) y, aparte, las líneas del pedido
       tal cual para facturar (una regalía a precio 0 no se cobra aunque el producto también se compre). */
    DECLARE @Items TABLE (
        ProductoId     INT             NOT NULL PRIMARY KEY,
        ProductoNombre NVARCHAR(150)   NOT NULL,
        Cantidad       INT             NOT NULL,
        StockAnterior  INT             NOT NULL,
        StockNuevo     INT             NULL
    );

    BEGIN TRANSACTION;

    SELECT
        @EstadoActual  = p.Estado,
        @ClienteId     = p.UsuarioId,
        @ClienteNombre = u.NombreCompleto,
        @ClienteCorreo = u.Correo,
        @Identificacion = p.IdentificacionCliente
    FROM dbo.Pedidos p WITH (UPDLOCK, HOLDLOCK)
    INNER JOIN dbo.Usuarios u ON u.UsuarioId = p.UsuarioId
    WHERE p.PedidoId = @PedidoId;

    IF @EstadoActual IS NULL
    BEGIN ROLLBACK TRANSACTION; THROW 50731, 'No se encontró el pedido solicitado.', 1; END;

    IF @EstadoActual <> N'Retenido'
    BEGIN ROLLBACK TRANSACTION; THROW 50732, 'Solo se pueden aprobar pedidos en estado Retenido.', 1; END;

    IF EXISTS (SELECT 1 FROM dbo.Facturas WITH (UPDLOCK, HOLDLOCK) WHERE PedidoId = @PedidoId)
    BEGIN ROLLBACK TRANSACTION; THROW 50733, 'El pedido ya tiene una factura asociada.', 1; END;

    IF NULLIF(LTRIM(RTRIM(@Identificacion)), N'') IS NULL
    BEGIN ROLLBACK TRANSACTION; THROW 50737, 'Falta la identificación del cliente para facturar.', 1; END;

    INSERT INTO @Items (ProductoId, ProductoNombre, Cantidad, StockAnterior)
    SELECT d.ProductoId, pr.Nombre, SUM(d.Cantidad), pr.Stock
    FROM dbo.PedidoDetalle d
    INNER JOIN dbo.Productos pr WITH (UPDLOCK, HOLDLOCK) ON pr.ProductoId = d.ProductoId
    WHERE d.PedidoId = @PedidoId
    GROUP BY d.ProductoId, pr.Nombre, pr.Stock;

    IF NOT EXISTS (SELECT 1 FROM @Items)
    BEGIN ROLLBACK TRANSACTION; THROW 50734, 'El pedido no tiene líneas para procesar.', 1; END;

    IF EXISTS (SELECT 1 FROM @Items WHERE StockAnterior < Cantidad)
    BEGIN ROLLBACK TRANSACTION; THROW 50735, 'Stock insuficiente para uno o más productos al momento de aprobar.', 1; END;

    UPDATE pr
    SET    pr.Stock = pr.Stock - i.Cantidad
    FROM   dbo.Productos pr
    INNER JOIN @Items i ON i.ProductoId = pr.ProductoId
    WHERE  pr.Stock >= i.Cantidad;

    IF @@ROWCOUNT <> (SELECT COUNT(*) FROM @Items)
    BEGIN ROLLBACK TRANSACTION; THROW 50736, 'Error de concurrencia de stock. Intente nuevamente.', 1; END;

    UPDATE i SET StockNuevo = pr.Stock
    FROM @Items i INNER JOIN dbo.Productos pr ON pr.ProductoId = i.ProductoId;

    INSERT INTO dbo.MovimientosInventario (
        ProductoId, ProductoNombre, TipoMovimiento, Cantidad,
        StockAnterior, StockNuevo, Motivo, UsuarioId, UsuarioNombre, FechaMovimiento
    )
    SELECT ProductoId, ProductoNombre, N'Salida', Cantidad, StockAnterior, StockNuevo,
           CONCAT(N'Pedido #', @PedidoId, N' liberado por ', @UsuarioNombre),
           @UsuarioId, @UsuarioNombre, SYSDATETIME()
    FROM @Items;

    SET @NumeroFactura = CONCAT(
        N'FAC-',
        FORMAT(SYSDATETIME(), 'yyyyMMddHHmmss'),
        N'-',
        RIGHT(CONCAT(N'0000', CAST(@PedidoId AS NVARCHAR(4))), 4)
    );

    SELECT @Subtotal = ISNULL(SUM(d.PrecioUnitario * d.Cantidad), 0)
    FROM dbo.PedidoDetalle d
    WHERE d.PedidoId = @PedidoId AND d.PedidoComboId IS NULL;
    SET @Impuesto    = ROUND(@Subtotal * 0.13, 2);
    SET @TotalFactura = @Subtotal + @Impuesto;

    INSERT INTO dbo.Facturas (
        PedidoId, NumeroFactura, UsuarioId, ClienteNombre, ClienteCorreo,
        Subtotal, Impuesto, Total, Estado
    )
    VALUES (
        @PedidoId, @NumeroFactura, @ClienteId, @ClienteNombre, @ClienteCorreo,
        @Subtotal, @Impuesto, @TotalFactura, N'Generada'
    );

    SET @FacturaId = CAST(SCOPE_IDENTITY() AS INT);

    INSERT INTO dbo.FacturaDetalle (FacturaId, ProductoId, ProductoNombre, Cantidad, PrecioUnitario)
    SELECT @FacturaId, d.ProductoId, COALESCE(d.ProductoNombreSnapshot, pr.Nombre), d.Cantidad, d.PrecioUnitario
    FROM   dbo.PedidoDetalle d
    INNER JOIN dbo.Productos pr ON pr.ProductoId = d.ProductoId
    WHERE  d.PedidoId = @PedidoId AND d.PedidoComboId IS NULL
    ORDER BY d.PedidoDetalleId;

    UPDATE dbo.Pedidos
    SET Estado               = N'Liberado',
        FechaActualizacion   = SYSDATETIME(),
        InventarioDescontado = 1
    WHERE PedidoId = @PedidoId;

    COMMIT TRANSACTION;

    SELECT
        @PedidoId      AS PedidoId,
        N'Liberado'    AS Estado,
        @FacturaId     AS FacturaId,
        @NumeroFactura AS NumeroFactura;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Seller_CotizarPedido
    @ClienteUsuarioId INT = NULL,
    @ItemsJson        NVARCHAR(MAX),
    @UmbralRetencion  DECIMAL(18,2) = 50000.00
AS
BEGIN
    SET NOCOUNT ON;

    IF ISJSON(@ItemsJson) <> 1
        THROW 50712, 'Debe seleccionar al menos un producto.', 1;

    DECLARE @Lineas TABLE (
        Orden           INT            NOT NULL,
        ProductoId      INT            NOT NULL,
        ProductoNombre  NVARCHAR(150)  NOT NULL,
        Cantidad        INT            NOT NULL,
        PrecioLista     DECIMAL(18,2)  NOT NULL,
        MontoDescuento  DECIMAL(18,2)  NOT NULL,
        PrecioUnitario  DECIMAL(18,2)  NOT NULL,
        EsRegalo        BIT            NOT NULL,
        PromocionId     INT            NULL,
        PromocionNombre NVARCHAR(150)  NULL
    );
    INSERT INTO @Lineas
    SELECT Orden, ProductoId, ProductoNombre, Cantidad, PrecioLista, MontoDescuento, PrecioUnitario,
           EsRegalo, PromocionId, PromocionNombre
    FROM dbo.fn_Venta_Lineas(@ClienteUsuarioId, @ItemsJson);

    DECLARE @Subtotal DECIMAL(18,2) = (SELECT ISNULL(SUM(Cantidad * PrecioUnitario), 0) FROM @Lineas);
    DECLARE @Impuesto DECIMAL(18,2) = ROUND(@Subtotal * 0.13, 2);

    SELECT l.Orden, l.ProductoId, l.ProductoNombre, l.Cantidad, l.PrecioLista, l.MontoDescuento, l.PrecioUnitario,
           CONVERT(DECIMAL(18,2), l.Cantidad * l.PrecioUnitario) AS SubtotalLinea,
           l.EsRegalo, l.PromocionNombre
    FROM @Lineas l
    ORDER BY l.Orden, l.ProductoId;

    SELECT CONVERT(DECIMAL(18,2), ISNULL((SELECT SUM(Cantidad * PrecioLista) FROM @Lineas WHERE EsRegalo = 0), 0)) AS SubtotalLista,
           CONVERT(DECIMAL(18,2), ISNULL((SELECT SUM(MontoDescuento) FROM @Lineas WHERE EsRegalo = 0), 0)) AS Descuento,
           @Subtotal AS Subtotal,
           @Impuesto AS Impuesto,
           @Subtotal + @Impuesto AS Total,
           @UmbralRetencion AS UmbralRetencion,
           CONVERT(BIT, CASE WHEN @Subtotal + @Impuesto > @UmbralRetencion THEN 1 ELSE 0 END) AS Retenido;
END;
GO

IF XACT_STATE()<>1 THROW 59323,N'La transacción 0332 no está disponible.',1;
DECLARE @MigrationSha256 NVARCHAR(128)=N'$(MigrationSha256)';
IF LEN(@MigrationSha256)<>64 OR @MigrationSha256 LIKE N'%[^0-9A-Fa-f]%' THROW 59324,N'SHA-256 inválido para 0332.',1;
INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes)
VALUES(N'0332_venta_movil_promociones_iva',N'0332_venta_movil_promociones_iva.sql',UPPER(@MigrationSha256),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),
       N'Pruebas post instalación obs. 1 y 3: Venta móvil aplica las promociones activas (como la Tienda) y guarda Subtotal, IVA y Total con IVA; retención sobre el total con IVA; sp_Manager_ApproveOrder gift-aware; sp_Seller_CotizarPedido (cotización antes de confirmar).');
COMMIT TRANSACTION;
GO
