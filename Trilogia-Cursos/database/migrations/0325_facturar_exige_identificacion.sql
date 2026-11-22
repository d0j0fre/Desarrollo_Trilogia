SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

/* POST-qa2-02 · CU-091-03: el sistema factura un pedido de un cliente sin identificación.

   Decisión del gerente: la identificación se exige solo al FACTURAR. Registrar pedidos sin facturar
   sigue igual. Se reemplazan, copiando su definición vigente y agregando solo la validación:

     - dbo.sp_Admin_GenerateInvoiceFromOrder (0012): «Generar factura» del detalle del pedido.
       Error 50906.
     - dbo.sp_Seller_CreateOrder (0255): la factura automática de Venta móvil (sitio y app) cuando el
       pedido no queda retenido. Error 50718; el pedido completo se revierte.
     - dbo.sp_Manager_ApproveOrder (cu098): «Aprobar y facturar» de un pedido retenido. Error 50737.

   Mensaje: «Falta la identificación del cliente para facturar.» La identificación es
   dbo.Pedidos.IdentificacionCliente (la Tienda ya la exige al pagar). Las facturas existentes no se tocan.
   Rollback en 0325_facturar_exige_identificacion.rollback.md. */
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL THROW 58470,N'Falta el ledger 0001.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0325_facturar_exige_identificacion' AND Status=N'Applied')
    THROW 58471,N'0325 ya figura aplicada.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0255_venta_movil_pago_contra_entrega' AND Status=N'Applied')
    THROW 58472,N'0325 parte de la versión de sp_Seller_CreateOrder de la 0255. Aplicar 0255 primero.',1;

BEGIN TRANSACTION;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Admin_GenerateInvoiceFromOrder
    @PedidoId INT,
    @UsuarioId INT = NULL,
    @UsuarioNombre NVARCHAR(150) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF @PedidoId <= 0 THROW 50901, N'El pedido indicado no es válido.', 1;

    DECLARE @PedidoUsuarioId INT;
    DECLARE @Estado NVARCHAR(30);
    DECLARE @ClienteNombre NVARCHAR(150);
    DECLARE @ClienteCorreo NVARCHAR(150);
    DECLARE @Subtotal DECIMAL(18,2);
    DECLARE @Impuesto DECIMAL(18,2);
    DECLARE @Total DECIMAL(18,2);
    DECLARE @FacturaId INT;
    DECLARE @NumeroFactura NVARCHAR(30);
    DECLARE @Identificacion NVARCHAR(100);
    BEGIN TRY
        BEGIN TRANSACTION;
        SELECT @PedidoUsuarioId = orders.UsuarioId,
               @Estado = orders.Estado,
               @ClienteNombre = userAccount.NombreCompleto,
               @ClienteCorreo = userAccount.Correo,
               @Identificacion = orders.IdentificacionCliente
        FROM dbo.Pedidos orders WITH (UPDLOCK, HOLDLOCK)
        INNER JOIN dbo.Usuarios userAccount ON userAccount.UsuarioId = orders.UsuarioId
        WHERE orders.PedidoId = @PedidoId;
        IF @PedidoUsuarioId IS NULL THROW 50902, N'No se encontró el pedido solicitado.', 1;
        IF @Estado NOT IN (N'Pendiente', N'Aprobado', N'EnProceso', N'Entregado', N'Liberado')
            THROW 50903, N'El estado del pedido no permite facturación.', 1;
        IF EXISTS (SELECT 1 FROM dbo.Facturas WITH (UPDLOCK, HOLDLOCK) WHERE PedidoId = @PedidoId)
            THROW 50904, N'El pedido ya tiene una factura asociada.', 1;
        IF NULLIF(LTRIM(RTRIM(@Identificacion)), N'') IS NULL
            THROW 50906, N'Falta la identificación del cliente para facturar.', 1;
        IF NOT EXISTS
           (
               SELECT 1
               FROM dbo.PedidoDetalle
               WHERE PedidoId = @PedidoId
                 AND PedidoComboId IS NULL
           )
           AND NOT EXISTS (SELECT 1 FROM dbo.PedidoCombos WHERE PedidoId = @PedidoId)
            THROW 50905, N'El pedido no tiene líneas para facturar.', 1;

        SELECT @Subtotal =
            ISNULL((SELECT SUM(detail.Cantidad * detail.PrecioUnitario)
                    FROM dbo.PedidoDetalle detail
                    WHERE detail.PedidoId = @PedidoId
                      AND detail.PedidoComboId IS NULL), 0)
            + ISNULL((SELECT SUM(combo.Cantidad * combo.PrecioUnitario)
                      FROM dbo.PedidoCombos combo WHERE combo.PedidoId = @PedidoId), 0);
        SET @Impuesto = ROUND(@Subtotal * 0.13, 2);
        SET @Total = @Subtotal + @Impuesto;
        SET @NumeroFactura = CONCAT
        (
            N'FAC-', FORMAT(SYSDATETIME(), N'yyyyMMddHHmmss'), N'-',
            RIGHT(CONCAT(N'0000', CONVERT(NVARCHAR(10), @PedidoId)), 4)
        );

        INSERT INTO dbo.Facturas
            (PedidoId, NumeroFactura, UsuarioId, ClienteNombre, ClienteCorreo,
             Subtotal, Impuesto, Total, Estado)
        VALUES
            (@PedidoId, @NumeroFactura, @PedidoUsuarioId, @ClienteNombre, @ClienteCorreo,
             @Subtotal, @Impuesto, @Total, N'Generada');
        SET @FacturaId = CONVERT(INT, SCOPE_IDENTITY());

        INSERT INTO dbo.FacturaDetalle
            (FacturaId, ProductoId, ProductoNombre, Cantidad, PrecioUnitario)
        SELECT @FacturaId,
               detail.ProductoId,
               COALESCE(detail.ProductoNombreSnapshot, product.Nombre),
               detail.Cantidad,
               detail.PrecioUnitario
        FROM dbo.PedidoDetalle detail
        INNER JOIN dbo.Productos product ON product.ProductoId = detail.ProductoId
        WHERE detail.PedidoId = @PedidoId
          AND detail.PedidoComboId IS NULL;
        INSERT INTO dbo.FacturaCombos
            (FacturaId, PedidoComboId, ComboNombreSnapshot, Cantidad, PrecioUnitario)
        SELECT @FacturaId, combo.PedidoComboId, combo.ComboNombreSnapshot, combo.Cantidad, combo.PrecioUnitario
        FROM dbo.PedidoCombos combo
        WHERE combo.PedidoId = @PedidoId;

        UPDATE dbo.Pedidos SET Estado = N'Entregado', FechaActualizacion = SYSDATETIME()
        WHERE PedidoId = @PedidoId;
        COMMIT TRANSACTION;

        SELECT @FacturaId AS FacturaId,
               @PedidoId AS PedidoId,
               @NumeroFactura AS NumeroFactura,
               N'Entregado' AS EstadoPedido;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;

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

    DECLARE @Items TABLE (
        ProductoId     INT             NOT NULL PRIMARY KEY,
        ProductoNombre NVARCHAR(150)   NOT NULL,
        Cantidad       INT             NOT NULL,
        PrecioUnitario DECIMAL(18,2)   NOT NULL,
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

    INSERT INTO @Items (ProductoId, ProductoNombre, Cantidad, PrecioUnitario, StockAnterior)
    SELECT d.ProductoId, pr.Nombre, SUM(d.Cantidad), MAX(d.PrecioUnitario), pr.Stock
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

    SELECT @Subtotal = SUM(PrecioUnitario * Cantidad) FROM @Items;
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
    SELECT @FacturaId, ProductoId, ProductoNombre, Cantidad, PrecioUnitario
    FROM   @Items;

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

IF XACT_STATE()<>1 THROW 58473,N'La transacción 0325 no está disponible.',1;
DECLARE @MigrationSha256 NVARCHAR(128)=N'$(MigrationSha256)';
IF LEN(@MigrationSha256)<>64 OR @MigrationSha256 LIKE N'%[^0-9A-Fa-f]%' THROW 58474,N'SHA-256 inválido para 0325.',1;
INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes)
VALUES(N'0325_facturar_exige_identificacion',N'0325_facturar_exige_identificacion.sql',UPPER(@MigrationSha256),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),
       N'POST-qa2-02 (CU-091-03): sp_Admin_GenerateInvoiceFromOrder, sp_Seller_CreateOrder y sp_Manager_ApproveOrder rechazan facturar un pedido sin identificación del cliente (50906, 50718, 50737).');
COMMIT TRANSACTION;
GO
