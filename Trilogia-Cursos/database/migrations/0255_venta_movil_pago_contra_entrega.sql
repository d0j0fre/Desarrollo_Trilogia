SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

/* G2-39 / G3-19: Venta movil guardaba «Crédito vendedor» como metodo de pago de
   los pedidos contra entrega. Liquidacion de cobros solo cuenta como efectivo
   esperado los pedidos cuyo metodo contiene «Efectivo», asi que las rutas de
   venta movil daban 0 en efectivo; y el detalle del pedido lo mostraba como si
   fuera un pedido a credito.

   Se reemplaza dbo.sp_Seller_CreateOrder copiando su definicion vigente (0230)
   y cambiando solo dos cosas:
   - sin @MetodoPago, el pedido se guarda como «Efectivo contra entrega», el
     mismo nombre que ya usa la Tienda (sp_Store_CreateOrderWithPromotions);
   - @MetodoPago acepta ese valor ademas de «Crédito».
   No cambia los pedidos ya registrados: los historicos siguen con «Crédito
   vendedor» y la pantalla los muestra como «Pago contra entrega».

   Rollback en 0255_venta_movil_pago_contra_entrega.rollback.md. */
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL THROW 56550,N'Falta el ledger 0001.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0255_venta_movil_pago_contra_entrega' AND Status=N'Applied')
    THROW 56551,N'0255 ya figura aplicada.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0230_cliente_estado_deuda' AND Status=N'Applied')
    THROW 56552,N'0255 parte de la versión de sp_Seller_CreateOrder de la 0230. Aplicar 0230 primero.',1;

BEGIN TRANSACTION;
GO


/* ─────────────────────────────────────────────────────────────────────────
   5. Venta movil con pago a credito. Copia de la version de cu098 con
      @MetodoPago y el cargo antes del COMMIT (lineas marcadas con 0230).
   ───────────────────────────────────────────────────────────────────────── */
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

    SELECT @Total = SUM(Precio * Cantidad) FROM @Items;

    /* ── Regla de retención ── */
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

    SELECT @ClienteNombre = NombreCompleto, @ClienteCorreo = Correo
    FROM   dbo.Usuarios
    WHERE  UsuarioId = @ClienteUsuarioId;

    /* ── Insertar pedido ── */
    INSERT INTO dbo.Pedidos (
        UsuarioId, FechaPedido, Estado, TipoEntrega, DireccionEntrega,
        Total, Observaciones, IdentificacionCliente,
        VendedorUsuarioId, VendedorNombre, CanalPedido, FechaActualizacion,
        PedidoOfflineGuid, MetodoPago, EstadoPago, InventarioDescontado
    )
    VALUES (
        @ClienteUsuarioId, SYSDATETIME(), @Estado,
        @TipoEntrega, LEFT(ISNULL(@DireccionEntrega, N''), 255),
        @Total, LEFT(ISNULL(@Observaciones, N''), 255), @IdentificacionCliente,
        @VendedorUsuarioId, @VendedorNombre, @CanalFinal, SYSDATETIME(),
        @PedidoOfflineGuid, ISNULL(@MetodoPago, N'Efectivo contra entrega'), N'Pendiente', @DescuentaInv  -- 0230, 0255
    );

    SET @PedidoId = CAST(SCOPE_IDENTITY() AS INT);

    INSERT INTO dbo.PedidoDetalle (PedidoId, ProductoId, Cantidad, PrecioUnitario)
    SELECT @PedidoId, ProductoId, Cantidad, Precio
    FROM   @Items;

    /* 0230: montos de la factura (CU-091), calculados una sola vez antes de la
       regla de retención: los usa la auto-factura y, en un pedido retenido, el
       cargo a crédito (mismo IVA y redondeo). */
    SELECT @Subtotal = SUM(Precio * Cantidad) FROM @Items;
    SET @Impuesto    = ROUND(@Subtotal * 0.13, 2);
    SET @TotalFactura = @Subtotal + @Impuesto;

    /* ── Solo para pedidos NO retenidos: inventario + CU-091 auto-factura ── */
    IF @DescuentaInv = 1
    BEGIN
        UPDATE p
        SET    p.Stock = p.Stock - i.Cantidad
        FROM   dbo.Productos p
        INNER JOIN @Items i ON i.ProductoId = p.ProductoId
        WHERE  p.Stock >= i.Cantidad;

        IF @@ROWCOUNT <> (SELECT COUNT(*) FROM @Items)
        BEGIN
            ROLLBACK TRANSACTION;
            THROW 50716, 'Concurrencia de stock detectada. Intente nuevamente.', 1;
        END;

        UPDATE i
        SET StockNuevo = p.Stock
        FROM @Items i
        INNER JOIN dbo.Productos p ON p.ProductoId = i.ProductoId;

        INSERT INTO dbo.MovimientosInventario (
            ProductoId, ProductoNombre, TipoMovimiento, Cantidad,
            StockAnterior, StockNuevo, Motivo, UsuarioId, UsuarioNombre, FechaMovimiento
        )
        SELECT
            ProductoId, ProductoNombre, N'Salida', Cantidad,
            StockAnterior, StockNuevo,
            CONCAT(N'Pedido móvil #', @PedidoId, N' - vendedor: ', @VendedorNombre),
            @VendedorUsuarioId, @VendedorNombre, SYSDATETIME()
        FROM @Items;

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
        SELECT @FacturaId, ProductoId, ProductoNombre, Cantidad, Precio
        FROM   @Items;
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

IF XACT_STATE()<>1 THROW 56553,N'La transacción 0255 no está disponible.',1;
DECLARE @MigrationSha256 NVARCHAR(128)=N'$(MigrationSha256)';
IF LEN(@MigrationSha256)<>64 OR @MigrationSha256 LIKE N'%[^0-9A-Fa-f]%' THROW 56554,N'SHA-256 inválido para 0255.',1;
INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes)
VALUES(N'0255_venta_movil_pago_contra_entrega',N'0255_venta_movil_pago_contra_entrega.sql',UPPER(@MigrationSha256),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),
       N'G2-39: Venta móvil guarda «Efectivo contra entrega» en vez de «Crédito vendedor»; no cambia pedidos registrados.');
COMMIT TRANSACTION;
GO
