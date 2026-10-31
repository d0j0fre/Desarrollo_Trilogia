SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

/* BUG-06-02 · CU-062-01, CU-062-02 (y CU-062-03, CU-062-06): el credito del
   cliente no se usaba en los pedidos. Solo existian movimientos manuales
   (Cargo, Abono, Ajustes); ningun pedido bajaba el credito disponible ni se
   validaban pagos vencidos.

   Regla de credito (la misma para todo el sistema)
   ------------------------------------------------
   - Deuda actual = cargos (Cargo, AjustePositivo) - pagos (Abono,
     AjusteNegativo), nunca negativa. Es la formula que ya usaban Creditos y
     Cuentas por cobrar.
   - Cada cargo vence PlazoCreditoDias dias despues de su FechaMovimiento
     (columna nueva dbo.ClienteCreditos.PlazoCreditoDias, 30 por defecto,
     0 a 365; 0 = el cargo vence de inmediato).
   - Los pagos cubren primero los cargos mas antiguos (FIFO).
   - Deuda vencida = lo que queda sin pagar de los cargos ya vencidos. El
     cliente "tiene pagos vencidos" si la deuda vencida es mayor que cero.
   - El cargo de un pedido a credito anulado (pedido Cancelado o Rechazado) y
     su reverso se excluyen del calculo FIFO, para que el reverso no "pague"
     otro cargo mas antiguo.

   Un pedido a credito se permite solo si el cliente tiene credito activo, no
   bloqueado, con limite mayor que cero, SIN deuda vencida y si el monto a
   pagar cabe en el credito disponible (limite - deuda actual). Al crearse
   registra un Cargo por el TOTAL FACTURADO (con IVA), con PedidoId y la
   referencia "Pedido #N"; si el pedido se cancela o se rechaza, se registra
   un AjusteNegativo por el mismo monto.

   Cambios
   -------
   1. dbo.ClienteCreditos.PlazoCreditoDias y dbo.ClienteCreditoMovimientos.PedidoId.
   2. dbo.fn_Cliente_EstadoDeuda(@UsuarioId): funcion en linea reutilizable
      (la usa tambien el bloqueo de comodatos, BUG-16-02) y
      dbo.sp_Cliente_EstadoDeuda para leerla desde la aplicacion.
   3. sp_Admin_UpdateClientCreditSettings acepta @PlazoCreditoDias (opcional);
      sp_Admin_GetClientCreditDetail agrega al final PlazoCreditoDias,
      DeudaVencida y VencimientoMasAntiguo.
   4. dbo.sp_Credito_CargarPedido: valida y registra el cargo de un pedido.
      Debe ejecutarse dentro de la transaccion que crea el pedido.
   5. sp_Seller_CreateOrder (Venta movil) acepta @MetodoPago = N'Crédito'.
      Sin el parametro se comporta igual que antes (la API movil y la
      sincronizacion offline no cambian).
   6. TR_Pedidos_AnularCargoCredito (AFTER UPDATE en dbo.Pedidos): reversa el
      cargo cuando un pedido a credito pasa a Cancelado o Rechazado, por
      cualquier camino (cliente, administracion o gerente).
   7. TR_Facturas_SincronizarCargoCredito (AFTER INSERT en dbo.Facturas): si
      un pedido retenido se factura despues, su cargo queda igual al total
      de la factura.

   Errores nuevos: 52016 (plazo invalido), 52020-52024 (pedido a credito).
   Aditiva salvo los procedimientos redefinidos. Rollback en
   0230_cliente_estado_deuda.rollback.md. */
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL THROW 56300,N'Falta el ledger 0001.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0230_cliente_estado_deuda' AND Status=N'Applied')
    THROW 56301,N'0230 ya figura aplicada.',1;
IF OBJECT_ID(N'dbo.ClienteCreditos',N'U') IS NULL OR OBJECT_ID(N'dbo.ClienteCreditoMovimientos',N'U') IS NULL
    THROW 56302,N'Faltan las tablas de credito (cu062).',1;
IF COL_LENGTH(N'dbo.Pedidos',N'MetodoPago') IS NULL OR COL_LENGTH(N'dbo.Pedidos',N'MotivoRechazo') IS NULL
    THROW 56303,N'Faltan las columnas de pago y retencion de dbo.Pedidos (cu097/cu098).',1;
IF OBJECT_ID(N'dbo.sp_Seller_CreateOrder',N'P') IS NULL
    THROW 56304,N'Falta sp_Seller_CreateOrder (cu098).',1;

BEGIN TRANSACTION;
GO

/* ─────────────────────────────────────────────────────────────────────────
   1. Columnas.
   ───────────────────────────────────────────────────────────────────────── */
IF COL_LENGTH(N'dbo.ClienteCreditos', N'PlazoCreditoDias') IS NULL
    EXEC sys.sp_executesql N'ALTER TABLE dbo.ClienteCreditos ADD PlazoCreditoDias INT NOT NULL
        CONSTRAINT DF_ClienteCreditos_PlazoCreditoDias DEFAULT (30)
        CONSTRAINT CK_ClienteCreditos_PlazoCreditoDias CHECK (PlazoCreditoDias BETWEEN 0 AND 365);';

IF COL_LENGTH(N'dbo.ClienteCreditoMovimientos', N'PedidoId') IS NULL
    EXEC sys.sp_executesql N'ALTER TABLE dbo.ClienteCreditoMovimientos ADD PedidoId INT NULL;';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ClienteCreditoMovimientos_PedidoId'
               AND object_id = OBJECT_ID(N'dbo.ClienteCreditoMovimientos'))
    CREATE INDEX IX_ClienteCreditoMovimientos_PedidoId
        ON dbo.ClienteCreditoMovimientos (PedidoId, TipoMovimiento)
        WHERE PedidoId IS NOT NULL;
GO

/* ─────────────────────────────────────────────────────────────────────────
   2. Estado de la deuda de un cliente.
   ───────────────────────────────────────────────────────────────────────── */
CREATE OR ALTER FUNCTION dbo.fn_Cliente_EstadoDeuda (@UsuarioId INT)
RETURNS TABLE
AS
RETURN
WITH Anulados AS
(
    -- Pedidos a credito ya reversados: su cargo y su reverso no entran al FIFO.
    SELECT DISTINCT m.PedidoId
    FROM dbo.ClienteCreditoMovimientos m
    WHERE m.UsuarioId = @UsuarioId
      AND m.PedidoId IS NOT NULL
      AND m.TipoMovimiento = N'AjusteNegativo'
),
Movimientos AS
(
    SELECT m.CreditoMovimientoId, m.TipoMovimiento, m.Monto, m.FechaMovimiento
    FROM dbo.ClienteCreditoMovimientos m
    WHERE m.UsuarioId = @UsuarioId
      AND (m.PedidoId IS NULL OR m.PedidoId NOT IN (SELECT a.PedidoId FROM Anulados a))
),
Cargos AS
(
    SELECT c.Monto,
           c.FechaMovimiento,
           SUM(c.Monto) OVER (ORDER BY c.FechaMovimiento, c.CreditoMovimientoId
                              ROWS UNBOUNDED PRECEDING) AS Acumulado
    FROM Movimientos c
    WHERE c.TipoMovimiento IN (N'Cargo', N'AjustePositivo')
),
Pagos AS
(
    SELECT ISNULL(SUM(p.Monto), 0) AS Total
    FROM Movimientos p
    WHERE p.TipoMovimiento IN (N'Abono', N'AjusteNegativo')
),
Configuracion AS
(
    SELECT u.UsuarioId,
           CAST(ISNULL(cc.LimiteCredito, 0) AS DECIMAL(18,2)) AS LimiteCredito,
           ISNULL(cc.PlazoCreditoDias, 30) AS PlazoCreditoDias
    FROM dbo.Usuarios u
    LEFT JOIN dbo.ClienteCreditos cc ON cc.UsuarioId = u.UsuarioId
    WHERE u.UsuarioId = @UsuarioId
),
Pendientes AS
(
    -- Parte de cada cargo que los pagos (aplicados del mas antiguo al mas nuevo) no cubren.
    SELECT DATEADD(DAY, cfg.PlazoCreditoDias, c.FechaMovimiento) AS Vencimiento,
           CASE WHEN p.Total >= c.Acumulado THEN 0
                WHEN p.Total <= c.Acumulado - c.Monto THEN c.Monto
                ELSE c.Acumulado - p.Total END AS Pendiente
    FROM Cargos c
    CROSS JOIN Pagos p
    CROSS JOIN Configuracion cfg
),
Vencidos AS
(
    SELECT SUM(Pendiente) AS DeudaVencida, MIN(Vencimiento) AS VencimientoMasAntiguo
    FROM Pendientes
    WHERE Pendiente > 0 AND Vencimiento <= SYSDATETIME()
),
Totales AS
(
    -- Misma formula que Creditos y Cuentas por cobrar (incluye los pedidos anulados, que netean a cero).
    SELECT SUM(CASE WHEN m.TipoMovimiento IN (N'Cargo', N'AjustePositivo') THEN m.Monto ELSE -m.Monto END) AS Neto
    FROM dbo.ClienteCreditoMovimientos m
    WHERE m.UsuarioId = @UsuarioId
)
SELECT cfg.UsuarioId,
       cfg.LimiteCredito,
       cfg.PlazoCreditoDias,
       CAST(CASE WHEN ISNULL(t.Neto, 0) < 0 THEN 0 ELSE ISNULL(t.Neto, 0) END AS DECIMAL(18,2)) AS DeudaActual,
       CAST(ISNULL(v.DeudaVencida, 0) AS DECIMAL(18,2)) AS DeudaVencida,
       CAST(CASE WHEN ISNULL(v.DeudaVencida, 0) > 0 THEN 1 ELSE 0 END AS BIT) AS TieneDeudaVencida,
       v.VencimientoMasAntiguo,
       CASE WHEN v.VencimientoMasAntiguo IS NULL THEN 0
            ELSE DATEDIFF(DAY, v.VencimientoMasAntiguo, SYSDATETIME()) END AS DiasAtraso
FROM Configuracion cfg
CROSS JOIN Totales t
CROSS JOIN Vencidos v;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Cliente_EstadoDeuda
    @UsuarioId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT UsuarioId, LimiteCredito, PlazoCreditoDias, DeudaActual, DeudaVencida,
           TieneDeudaVencida, VencimientoMasAntiguo, DiasAtraso
    FROM dbo.fn_Cliente_EstadoDeuda(@UsuarioId);
END;
GO

/* ─────────────────────────────────────────────────────────────────────────
   3. Configuracion y detalle del credito.
   ───────────────────────────────────────────────────────────────────────── */
CREATE OR ALTER PROCEDURE dbo.sp_Admin_UpdateClientCreditSettings
    @UsuarioId INT,
    @LimiteCredito DECIMAL(18,2),
    @CreditoActivo BIT,
    @CreditoBloqueado BIT,
    @MotivoBloqueo NVARCHAR(255) = NULL,
    @RegistradoPorUsuarioId INT = NULL,
    @RegistradoPorNombre NVARCHAR(150) = NULL,
    @PlazoCreditoDias INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @ClientePerfilId INT = (SELECT TOP 1 PerfilId FROM dbo.Perfiles WHERE Nombre = 'Cliente');
    SET @MotivoBloqueo = NULLIF(LTRIM(RTRIM(ISNULL(@MotivoBloqueo, ''))), '');

    IF NOT EXISTS (SELECT 1 FROM dbo.Usuarios WHERE UsuarioId = @UsuarioId AND PerfilId = @ClientePerfilId)
    BEGIN
        THROW 52003, 'El cliente seleccionado no existe.', 1;
    END

    IF @LimiteCredito < 0
    BEGIN
        THROW 52004, 'El límite de crédito no puede ser negativo.', 1;
    END

    IF (@CreditoActivo = 0 OR @CreditoBloqueado = 1) AND @MotivoBloqueo IS NULL
    BEGIN
        THROW 52005, 'Debe indicar un motivo si desactiva o bloquea el crédito.', 1;
    END

    IF @PlazoCreditoDias IS NOT NULL AND @PlazoCreditoDias NOT BETWEEN 0 AND 365
    BEGIN
        THROW 52016, 'El plazo de crédito debe estar entre 0 y 365 días.', 1;
    END

    IF EXISTS (SELECT 1 FROM dbo.ClienteCreditos WHERE UsuarioId = @UsuarioId)
    BEGIN
        UPDATE dbo.ClienteCreditos
        SET
            LimiteCredito = @LimiteCredito,
            CreditoActivo = @CreditoActivo,
            CreditoBloqueado = @CreditoBloqueado,
            MotivoBloqueo = CASE WHEN @CreditoActivo = 1 AND @CreditoBloqueado = 0 THEN NULL ELSE @MotivoBloqueo END,
            PlazoCreditoDias = ISNULL(@PlazoCreditoDias, PlazoCreditoDias),
            FechaActualizacion = SYSDATETIME()
        WHERE UsuarioId = @UsuarioId;
    END
    ELSE
    BEGIN
        INSERT INTO dbo.ClienteCreditos
        (
            UsuarioId,
            LimiteCredito,
            CreditoActivo,
            CreditoBloqueado,
            MotivoBloqueo,
            PlazoCreditoDias
        )
        VALUES
        (
            @UsuarioId,
            @LimiteCredito,
            @CreditoActivo,
            @CreditoBloqueado,
            CASE WHEN @CreditoActivo = 1 AND @CreditoBloqueado = 0 THEN NULL ELSE @MotivoBloqueo END,
            ISNULL(@PlazoCreditoDias, 30)
        );
    END
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Admin_GetClientCreditDetail
    @UsuarioId INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @ClientePerfilId INT = (SELECT TOP 1 PerfilId FROM dbo.Perfiles WHERE Nombre = 'Cliente');

    IF NOT EXISTS (SELECT 1 FROM dbo.Usuarios WHERE UsuarioId = @UsuarioId AND PerfilId = @ClientePerfilId)
    BEGIN
        THROW 52002, 'El cliente seleccionado no existe.', 1;
    END

    ;WITH Movimientos AS
    (
        SELECT
            UsuarioId,
            SUM(CASE WHEN TipoMovimiento IN ('Cargo', 'AjustePositivo') THEN Monto ELSE 0 END) AS TotalCargos,
            SUM(CASE WHEN TipoMovimiento IN ('Abono', 'AjusteNegativo') THEN Monto ELSE 0 END) AS TotalAbonos
        FROM dbo.ClienteCreditoMovimientos
        WHERE UsuarioId = @UsuarioId
        GROUP BY UsuarioId
    )
    SELECT TOP 1
        u.UsuarioId,
        u.NombreCompleto,
        u.Correo,
        u.Telefono,
        u.Direccion,
        CAST(ISNULL(u.Activo, 1) AS BIT) AS ClienteActivo,
        ISNULL(cc.LimiteCredito, 0) AS LimiteCredito,
        CAST(ISNULL(cc.CreditoActivo, 0) AS BIT) AS CreditoActivo,
        CAST(ISNULL(cc.CreditoBloqueado, 0) AS BIT) AS CreditoBloqueado,
        cc.MotivoBloqueo,
        CAST(CASE WHEN ISNULL(m.TotalCargos, 0) - ISNULL(m.TotalAbonos, 0) < 0 THEN 0 ELSE ISNULL(m.TotalCargos, 0) - ISNULL(m.TotalAbonos, 0) END AS DECIMAL(18,2)) AS DeudaActual,
        CAST(ISNULL(cc.LimiteCredito, 0) - CASE WHEN ISNULL(m.TotalCargos, 0) - ISNULL(m.TotalAbonos, 0) < 0 THEN 0 ELSE ISNULL(m.TotalCargos, 0) - ISNULL(m.TotalAbonos, 0) END AS DECIMAL(18,2)) AS CreditoDisponible,
        ISNULL(m.TotalCargos, 0) AS TotalCargos,
        ISNULL(m.TotalAbonos, 0) AS TotalAbonos,
        cc.FechaActualizacion,
        deuda.PlazoCreditoDias,
        deuda.DeudaVencida,
        deuda.VencimientoMasAntiguo
    FROM dbo.Usuarios u
    LEFT JOIN dbo.ClienteCreditos cc
        ON cc.UsuarioId = u.UsuarioId
    LEFT JOIN Movimientos m
        ON m.UsuarioId = u.UsuarioId
    CROSS APPLY dbo.fn_Cliente_EstadoDeuda(u.UsuarioId) deuda
    WHERE u.UsuarioId = @UsuarioId
      AND u.PerfilId = @ClientePerfilId;

    SELECT
        CreditoMovimientoId,
        TipoMovimiento,
        Monto,
        Descripcion,
        Referencia,
        RegistradoPorUsuarioId,
        RegistradoPorNombre,
        FechaMovimiento
    FROM dbo.ClienteCreditoMovimientos
    WHERE UsuarioId = @UsuarioId
    ORDER BY FechaMovimiento DESC, CreditoMovimientoId DESC;
END
GO

/* ─────────────────────────────────────────────────────────────────────────
   4. Cargo de un pedido a credito.
   ───────────────────────────────────────────────────────────────────────── */
CREATE OR ALTER PROCEDURE dbo.sp_Credito_CargarPedido
    @PedidoId INT,
    @RegistradoPorUsuarioId INT = NULL,
    @RegistradoPorNombre NVARCHAR(150) = NULL,
    @MontoFactura DECIMAL(18,2) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @@TRANCOUNT = 0
        THROW 52020, 'El cargo a crédito debe registrarse en la misma transacción que crea el pedido.', 1;

    /* El cargo es lo que el cliente debe pagar: el total facturado con IVA.
       Si la factura ya existe se toma su total; si no (pedido retenido), el
       total que calculó con la misma regla quien crea el pedido
       (@MontoFactura). TR_Facturas_SincronizarCargoCredito lo deja igual a
       la factura cuando esta se genera después. */
    DECLARE @UsuarioId INT, @Total DECIMAL(18,2);
    SELECT @UsuarioId = UsuarioId
    FROM dbo.Pedidos WITH (UPDLOCK, HOLDLOCK)
    WHERE PedidoId = @PedidoId;

    SELECT @Total = COALESCE((SELECT TOP 1 f.Total FROM dbo.Facturas f WHERE f.PedidoId = @PedidoId ORDER BY f.FacturaId),
                             @MontoFactura);

    IF @UsuarioId IS NULL OR ISNULL(@Total, 0) <= 0
        THROW 52020, 'El pedido a crédito no es válido.', 1;

    -- Idempotente: un pedido se carga una sola vez.
    IF EXISTS (SELECT 1 FROM dbo.ClienteCreditoMovimientos
               WHERE PedidoId = @PedidoId AND TipoMovimiento = N'Cargo')
        RETURN;

    -- Serializa los pedidos a credito del mismo cliente.
    DECLARE @CreditoActivo BIT, @CreditoBloqueado BIT, @Limite DECIMAL(18,2);
    SELECT @CreditoActivo = CreditoActivo, @CreditoBloqueado = CreditoBloqueado, @Limite = LimiteCredito
    FROM dbo.ClienteCreditos WITH (UPDLOCK, HOLDLOCK)
    WHERE UsuarioId = @UsuarioId;

    IF ISNULL(@CreditoActivo, 0) = 0 OR ISNULL(@Limite, 0) <= 0
        THROW 52021, 'El cliente no tiene crédito activo.', 1;

    IF @CreditoBloqueado = 1
        THROW 52022, 'El crédito del cliente está bloqueado.', 1;

    DECLARE @DeudaActual DECIMAL(18,2), @TieneVencida BIT;
    SELECT @DeudaActual = DeudaActual, @TieneVencida = TieneDeudaVencida
    FROM dbo.fn_Cliente_EstadoDeuda(@UsuarioId);

    IF @TieneVencida = 1
        THROW 52023, 'El cliente tiene pagos vencidos pendientes.', 1;

    IF @DeudaActual + @Total > @Limite
        THROW 52024, 'El pedido supera el crédito disponible del cliente.', 1;

    INSERT INTO dbo.ClienteCreditoMovimientos
        (UsuarioId, TipoMovimiento, Monto, Descripcion, Referencia,
         RegistradoPorUsuarioId, RegistradoPorNombre, PedidoId)
    VALUES
        (@UsuarioId, N'Cargo', @Total, CONCAT(N'Pedido a crédito #', @PedidoId),
         CONCAT(N'Pedido #', @PedidoId), @RegistradoPorUsuarioId,
         NULLIF(LTRIM(RTRIM(ISNULL(@RegistradoPorNombre, N''))), N''), @PedidoId);
END;
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

    /* 0230: sin método se conserva el valor histórico. */
    SET @MetodoPago = NULLIF(LTRIM(RTRIM(@MetodoPago)), N'');
    IF @MetodoPago IS NOT NULL AND @MetodoPago <> N'Crédito'
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
        @PedidoOfflineGuid, ISNULL(@MetodoPago, N'Crédito vendedor'), N'Pendiente', @DescuentaInv  -- 0230
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

/* ─────────────────────────────────────────────────────────────────────────
   6. Reverso del cargo cuando el pedido a credito se cancela o se rechaza.
   ───────────────────────────────────────────────────────────────────────── */
CREATE OR ALTER TRIGGER dbo.TR_Pedidos_AnularCargoCredito
ON dbo.Pedidos
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT UPDATE(Estado) RETURN;

    INSERT INTO dbo.ClienteCreditoMovimientos
        (UsuarioId, TipoMovimiento, Monto, Descripcion, Referencia,
         RegistradoPorUsuarioId, RegistradoPorNombre, PedidoId)
    SELECT cargo.UsuarioId,
           N'AjusteNegativo',
           cargo.Monto,
           CONCAT(N'Anulación del pedido a crédito #', i.PedidoId, N' (', i.Estado, N')'),
           CONCAT(N'Pedido #', i.PedidoId),
           NULL,
           N'Sistema',
           i.PedidoId
    FROM inserted i
    INNER JOIN deleted d ON d.PedidoId = i.PedidoId
    CROSS APPLY
    (
        SELECT m.UsuarioId, SUM(m.Monto) AS Monto
        FROM dbo.ClienteCreditoMovimientos m
        WHERE m.PedidoId = i.PedidoId AND m.TipoMovimiento = N'Cargo'
        GROUP BY m.UsuarioId
    ) cargo
    WHERE i.Estado IN (N'Cancelado', N'Rechazado')
      AND d.Estado NOT IN (N'Cancelado', N'Rechazado')
      AND NOT EXISTS (SELECT 1 FROM dbo.ClienteCreditoMovimientos r
                      WHERE r.PedidoId = i.PedidoId AND r.TipoMovimiento = N'AjusteNegativo');
END;
GO

/* ─────────────────────────────────────────────────────────────────────────
   7. El cargo de un pedido a credito es el total de su factura. Un pedido
      retenido se carga al crearse con el total calculado por Venta movil y se
      factura despues (aprobacion del gerente o Facturacion); si ese total
      difiriera, el cargo queda igual a la factura dentro de la misma
      transaccion que la genera. No se toca un cargo ya anulado.
   ───────────────────────────────────────────────────────────────────────── */
CREATE OR ALTER TRIGGER dbo.TR_Facturas_SincronizarCargoCredito
ON dbo.Facturas
AFTER INSERT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE m
    SET m.Monto = f.Total
    FROM dbo.ClienteCreditoMovimientos m
    INNER JOIN inserted f ON f.PedidoId = m.PedidoId
    WHERE m.TipoMovimiento = N'Cargo'
      AND m.Monto <> f.Total
      AND f.Total > 0
      AND NOT EXISTS (SELECT 1 FROM dbo.ClienteCreditoMovimientos r
                      WHERE r.PedidoId = m.PedidoId AND r.TipoMovimiento = N'AjusteNegativo');
END;
GO

/* ─────────────────────────────────────────────────────────────────────────
   Verificacion y ledger.
   ───────────────────────────────────────────────────────────────────────── */
IF OBJECT_ID(N'dbo.fn_Cliente_EstadoDeuda', N'IF') IS NULL
   OR OBJECT_ID(N'dbo.sp_Cliente_EstadoDeuda', N'P') IS NULL
   OR OBJECT_ID(N'dbo.sp_Credito_CargarPedido', N'P') IS NULL
   OR OBJECT_ID(N'dbo.TR_Pedidos_AnularCargoCredito', N'TR') IS NULL
   OR OBJECT_ID(N'dbo.TR_Facturas_SincronizarCargoCredito', N'TR') IS NULL
    THROW 56305, N'0230 no dejó creados todos sus objetos.', 1;

IF NOT EXISTS (SELECT 1 FROM sys.parameters WHERE object_id = OBJECT_ID(N'dbo.sp_Seller_CreateOrder') AND name = N'@MetodoPago')
   OR NOT EXISTS (SELECT 1 FROM sys.parameters WHERE object_id = OBJECT_ID(N'dbo.sp_Admin_UpdateClientCreditSettings') AND name = N'@PlazoCreditoDias')
    THROW 56306, N'Venta móvil o la configuración de crédito no quedaron actualizadas.', 1;

IF XACT_STATE() <> 1
    THROW 56307, N'La transacción 0230 no está disponible.', 1;

DECLARE @MigrationSha256 NVARCHAR(128) = N'$(MigrationSha256)';
IF LEN(@MigrationSha256) <> 64 OR @MigrationSha256 LIKE N'%[^0-9A-Fa-f]%'
    THROW 56308, N'SHA-256 inválido para 0230.', 1;

INSERT dbo.SchemaMigrationHistory
    (MigrationId, FileName, FileSha256, Status, AppliedBy, EnvironmentName, Notes)
VALUES
    (N'0230_cliente_estado_deuda',
     N'0230_cliente_estado_deuda.sql',
     UPPER(@MigrationSha256),
     N'Applied',
     ORIGINAL_LOGIN(),
     DB_NAME(),
     N'BUG-06-02: plazo de crédito, deuda vencida (fn_Cliente_EstadoDeuda), pedido a crédito en Venta móvil y reverso al anular.');

COMMIT TRANSACTION;
GO
