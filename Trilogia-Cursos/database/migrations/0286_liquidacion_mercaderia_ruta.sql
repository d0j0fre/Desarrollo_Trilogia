SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

/* BUG-10-07 · CU-105: liquidación de mercadería de una ruta finalizada.

   Una ruta Completada puede cerrar con pedidos no entregados (Fallido). Esa mercadería
   volvió en el camión y debe regresar al stock, pero el único procedimiento que lo hacía
   (sp_Rutas_Liquidar) solo acepta rutas Despachadas o Planificadas y no recibe cantidades.

   Flujo nuevo:
     1. sp_RutaMercaderia_Preparar lista, por pedido no entregado, los productos y cantidades
        que salieron en la ruta (no cuenta pedidos Cancelados ni Rechazados: su stock ya se
        restituyó).
     2. Quien liquida cuenta la mercadería física y la anota en la pantalla.
     3. sp_RutaMercaderia_Registrar compara lo contado con lo despachado:
          - si falta mercadería (contado < despachado) NO cierra y avisa qué falta (53416);
          - si sobra (contado > despachado) NO cierra (53415);
          - si cuadra, suma cada producto al stock y deja un movimiento de inventario
            «Entrada» por línea (la misma tabla y forma que usa sp_Rutas_Liquidar), guarda
            la liquidación y sus líneas, y marca la ruta como liquidada (Rutas.Liquidada = 1),
            igual que sp_Rutas_Liquidar. Una ruta liquidada no se puede liquidar otra vez.
   sp_RutaMercaderia_List arma la lista de rutas pendientes y liquidadas.

   Aditiva: no modifica procedimientos ni tablas existentes. Rollback en
   0286_liquidacion_mercaderia_ruta.rollback.md. */
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL THROW 56860,N'Falta el ledger 0001.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0286_liquidacion_mercaderia_ruta' AND Status=N'Applied')
    THROW 56861,N'0286 ya figura aplicada.',1;
IF OBJECT_ID(N'dbo.Rutas',N'U') IS NULL OR OBJECT_ID(N'dbo.RutaPedidos',N'U') IS NULL OR OBJECT_ID(N'dbo.PedidoDetalle',N'U') IS NULL
   OR OBJECT_ID(N'dbo.Productos',N'U') IS NULL OR OBJECT_ID(N'dbo.MovimientosInventario',N'U') IS NULL
    THROW 56862,N'Faltan las tablas de rutas, pedidos o inventario.',1;

BEGIN TRANSACTION;
GO

IF OBJECT_ID(N'dbo.RutaLiquidacionMercaderia', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RutaLiquidacionMercaderia
    (
        MercaderiaId          INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_RutaLiquidacionMercaderia PRIMARY KEY,
        RutaId                INT           NOT NULL CONSTRAINT FK_RutaLiqMerc_Ruta REFERENCES dbo.Rutas (RutaId),
        FechaLiquidacion      DATETIME2     NOT NULL CONSTRAINT DF_RutaLiqMerc_Fecha DEFAULT SYSDATETIME(),
        LiquidadoPorUsuarioId INT           NULL,
        LiquidadoPorNombre    NVARCHAR(150) NULL,
        Observaciones         NVARCHAR(400) NULL,
        TotalUnidades         INT           NOT NULL,
        CONSTRAINT UQ_RutaLiqMerc_Ruta UNIQUE (RutaId)
    );
END;
GO

IF OBJECT_ID(N'dbo.RutaLiquidacionMercaderiaDetalle', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RutaLiquidacionMercaderiaDetalle
    (
        DetalleId        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_RutaLiqMercDetalle PRIMARY KEY,
        MercaderiaId     INT           NOT NULL CONSTRAINT FK_RutaLiqMercDetalle_Cab REFERENCES dbo.RutaLiquidacionMercaderia (MercaderiaId),
        PedidoId         INT           NOT NULL,
        ProductoId       INT           NOT NULL,
        ProductoNombre   NVARCHAR(150) NOT NULL,
        CantidadEsperada INT           NOT NULL,
        CantidadRecibida INT           NOT NULL,
        MovimientoId     INT           NULL
    );
    CREATE INDEX IX_RutaLiqMercDetalle_Cab ON dbo.RutaLiquidacionMercaderiaDetalle (MercaderiaId);
END;
GO

/* 1) Encabezado de la ruta; 2) productos y cantidades de los pedidos no entregados. */
CREATE OR ALTER PROCEDURE dbo.sp_RutaMercaderia_Preparar
    @RutaId INT
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM dbo.Rutas WHERE RutaId = @RutaId) THROW 53410, N'No se encontró la ruta indicada.', 1;

    SELECT r.RutaId, r.Codigo, r.Estado, r.Liquidada,
           ISNULL((SELECT TOP (1) m.MercaderiaId FROM dbo.RutaLiquidacionMercaderia m WHERE m.RutaId = r.RutaId), 0) AS MercaderiaId,
           r.Zona, ISNULL(u.NombreCompleto, N'') AS ChoferNombre,
           (SELECT COUNT(*) FROM dbo.RutaPedidos x INNER JOIN dbo.Pedidos p ON p.PedidoId = x.PedidoId
             WHERE x.RutaId = r.RutaId AND x.EstadoEntrega <> N'Entregado' AND p.Estado NOT IN (N'Cancelado', N'Rechazado')) AS PedidosNoEntregados
    FROM dbo.Rutas r
    LEFT JOIN dbo.Usuarios u ON u.UsuarioId = r.ChoferUsuarioId
    WHERE r.RutaId = @RutaId;

    SELECT pd.PedidoId, pd.ProductoId, ISNULL(pr.Nombre, N'') AS ProductoNombre, SUM(pd.Cantidad) AS Cantidad
    FROM dbo.RutaPedidos rp
    INNER JOIN dbo.Pedidos p ON p.PedidoId = rp.PedidoId AND p.Estado NOT IN (N'Cancelado', N'Rechazado')
    INNER JOIN dbo.PedidoDetalle pd ON pd.PedidoId = rp.PedidoId
    INNER JOIN dbo.Productos pr ON pr.ProductoId = pd.ProductoId
    WHERE rp.RutaId = @RutaId AND rp.EstadoEntrega <> N'Entregado'
    GROUP BY pd.PedidoId, pd.ProductoId, pr.Nombre
    ORDER BY pd.PedidoId, pr.Nombre;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_RutaMercaderia_Registrar
    @RutaId        INT,
    @DetalleJson   NVARCHAR(MAX),            -- [{PedidoId, ProductoId, Cantidad}] lo contado físicamente
    @Observaciones NVARCHAR(400) = NULL,
    @UsuarioId     INT,
    @Nombre        NVARCHAR(150) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Estado NVARCHAR(40), @Liquidada BIT, @Codigo NVARCHAR(60), @Msg NVARCHAR(2000);
    SELECT @Estado = Estado, @Liquidada = Liquidada, @Codigo = Codigo FROM dbo.Rutas WHERE RutaId = @RutaId;

    IF @Estado IS NULL THROW 53410, N'No se encontró la ruta indicada.', 1;
    IF @Estado <> N'Completada'
        THROW 53411, N'Solo se puede liquidar la mercadería de una ruta completada.', 1;
    IF @Liquidada = 1 OR EXISTS (SELECT 1 FROM dbo.RutaLiquidacionMercaderia WHERE RutaId = @RutaId)
        THROW 53412, N'La mercadería de esta ruta ya fue liquidada.', 1;
    IF ISNULL(@UsuarioId, 0) <= 0 THROW 53417, N'No se identificó al usuario que liquida.', 1;
    SET @Nombre = ISNULL(NULLIF(LTRIM(RTRIM(@Nombre)), N''), N'Usuario');

    DECLARE @Esperado TABLE (PedidoId INT, ProductoId INT, ProductoNombre NVARCHAR(150), Cantidad INT, PRIMARY KEY (PedidoId, ProductoId));
    INSERT @Esperado (PedidoId, ProductoId, ProductoNombre, Cantidad)
    SELECT pd.PedidoId, pd.ProductoId, pr.Nombre, SUM(pd.Cantidad)
    FROM dbo.RutaPedidos rp
    INNER JOIN dbo.Pedidos p ON p.PedidoId = rp.PedidoId AND p.Estado NOT IN (N'Cancelado', N'Rechazado')
    INNER JOIN dbo.PedidoDetalle pd ON pd.PedidoId = rp.PedidoId
    INNER JOIN dbo.Productos pr ON pr.ProductoId = pd.ProductoId
    WHERE rp.RutaId = @RutaId AND rp.EstadoEntrega <> N'Entregado'
    GROUP BY pd.PedidoId, pd.ProductoId, pr.Nombre;

    IF NOT EXISTS (SELECT 1 FROM @Esperado)
        THROW 53413, N'La ruta no tiene pedidos sin entregar: no hay mercadería que devolver.', 1;

    DECLARE @Contado TABLE (PedidoId INT, ProductoId INT, Cantidad INT);
    IF @DetalleJson IS NOT NULL AND LTRIM(RTRIM(@DetalleJson)) <> N''
    BEGIN
        IF ISJSON(@DetalleJson) <> 1 THROW 53414, N'El detalle de la mercadería no tiene un formato válido.', 1;
        INSERT @Contado (PedidoId, ProductoId, Cantidad)
        SELECT j.PedidoId, j.ProductoId, SUM(ISNULL(j.Cantidad, 0))
        FROM OPENJSON(@DetalleJson) WITH (PedidoId INT '$.PedidoId', ProductoId INT '$.ProductoId', Cantidad INT '$.Cantidad') j
        GROUP BY j.PedidoId, j.ProductoId;
    END;

    IF EXISTS (SELECT 1 FROM @Contado c WHERE c.Cantidad < 0)
        THROW 53414, N'Las cantidades contadas no pueden ser negativas.', 1;
    IF EXISTS (SELECT 1 FROM @Contado c WHERE NOT EXISTS (SELECT 1 FROM @Esperado e WHERE e.PedidoId = c.PedidoId AND e.ProductoId = c.ProductoId))
        THROW 53414, N'La mercadería indicada no corresponde a los pedidos no entregados de la ruta.', 1;

    DECLARE @Sobra NVARCHAR(MAX) = (
        SELECT STRING_AGG(CAST(CONCAT(e.ProductoNombre, N' (pedido #', e.PedidoId, N': contó ', c.Cantidad, N', salieron ', e.Cantidad, N')') AS NVARCHAR(MAX)), N'; ')
        FROM @Esperado e INNER JOIN @Contado c ON c.PedidoId = e.PedidoId AND c.ProductoId = e.ProductoId
        WHERE c.Cantidad > e.Cantidad);
    IF @Sobra IS NOT NULL
    BEGIN
        SET @Msg = CONCAT(N'Se contó más mercadería de la que salió en la ruta: ', LEFT(@Sobra, 1500), N'. Revise el conteo.');
        THROW 53415, @Msg, 1;
    END;

    DECLARE @Falta NVARCHAR(MAX) = (
        SELECT STRING_AGG(CAST(CONCAT(e.ProductoNombre, N' (pedido #', e.PedidoId, N': faltan ', e.Cantidad - ISNULL(c.Cantidad, 0), N' de ', e.Cantidad, N')') AS NVARCHAR(MAX)), N'; ')
        FROM @Esperado e LEFT JOIN @Contado c ON c.PedidoId = e.PedidoId AND c.ProductoId = e.ProductoId
        WHERE ISNULL(c.Cantidad, 0) < e.Cantidad);
    IF @Falta IS NOT NULL
    BEGIN
        SET @Msg = CONCAT(N'Falta mercadería: la ruta ', @Codigo, N' no se puede cerrar hasta que aparezca. Faltan: ', LEFT(@Falta, 1500), N'.');
        THROW 53416, @Msg, 1;
    END;

    DECLARE @MercaderiaId INT, @Unidades INT = 0, @Lineas INT = 0;
    DECLARE @PedidoId INT, @ProductoId INT, @ProductoNombre NVARCHAR(150), @Cantidad INT, @StockAnterior INT, @StockNuevo INT, @MovId INT;

    BEGIN TRANSACTION;
    BEGIN TRY
        INSERT dbo.RutaLiquidacionMercaderia (RutaId, LiquidadoPorUsuarioId, LiquidadoPorNombre, Observaciones, TotalUnidades)
        VALUES (@RutaId, @UsuarioId, @Nombre, NULLIF(LTRIM(RTRIM(@Observaciones)), N''), 0);
        SET @MercaderiaId = SCOPE_IDENTITY();

        DECLARE cur CURSOR LOCAL FAST_FORWARD FOR
            SELECT PedidoId, ProductoId, ProductoNombre, Cantidad FROM @Esperado ORDER BY PedidoId, ProductoId;
        OPEN cur;
        FETCH NEXT FROM cur INTO @PedidoId, @ProductoId, @ProductoNombre, @Cantidad;
        WHILE @@FETCH_STATUS = 0
        BEGIN
            SELECT @StockAnterior = Stock FROM dbo.Productos WITH (UPDLOCK, HOLDLOCK) WHERE ProductoId = @ProductoId;
            SET @StockNuevo = @StockAnterior + @Cantidad;
            UPDATE dbo.Productos SET Stock = @StockNuevo WHERE ProductoId = @ProductoId;
            INSERT dbo.MovimientosInventario
                (ProductoId, ProductoNombre, TipoMovimiento, Cantidad, StockAnterior, StockNuevo, Motivo, UsuarioId, UsuarioNombre)
            VALUES
                (@ProductoId, @ProductoNombre, N'Entrada', @Cantidad, @StockAnterior, @StockNuevo,
                 CONCAT(N'Devolución de mercadería por liquidación de la ruta ', @Codigo, N' (pedido #', @PedidoId, N' no entregado)'),
                 @UsuarioId, @Nombre);
            SET @MovId = SCOPE_IDENTITY();
            INSERT dbo.RutaLiquidacionMercaderiaDetalle (MercaderiaId, PedidoId, ProductoId, ProductoNombre, CantidadEsperada, CantidadRecibida, MovimientoId)
            VALUES (@MercaderiaId, @PedidoId, @ProductoId, @ProductoNombre, @Cantidad, @Cantidad, @MovId);
            SET @Unidades += @Cantidad;
            SET @Lineas += 1;
            FETCH NEXT FROM cur INTO @PedidoId, @ProductoId, @ProductoNombre, @Cantidad;
        END;
        CLOSE cur; DEALLOCATE cur;

        UPDATE dbo.RutaLiquidacionMercaderia SET TotalUnidades = @Unidades WHERE MercaderiaId = @MercaderiaId;

        UPDATE dbo.Rutas
        SET Liquidada = 1, FechaLiquidacion = SYSDATETIME(), LiquidadaPorUsuarioId = @UsuarioId,
            LiquidadaPorNombre = @Nombre, FechaActualizacion = SYSDATETIME()
        WHERE RutaId = @RutaId;

        COMMIT;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK;
        THROW;
    END CATCH;

    SELECT @MercaderiaId AS MercaderiaId, @Lineas AS Lineas, @Unidades AS UnidadesReingresadas;
END;
GO

/* Rutas completadas con pedidos no entregados: pendientes de liquidar mercadería y ya liquidadas. */
CREATE OR ALTER PROCEDURE dbo.sp_RutaMercaderia_List
AS
BEGIN
    SET NOCOUNT ON;
    SELECT r.RutaId, r.Codigo, r.Zona, ISNULL(u.NombreCompleto, N'') AS ChoferNombre, r.FechaCierre,
           x.PedidosNoEntregados, x.Unidades,
           CAST(CASE WHEN r.Liquidada = 1 OR m.MercaderiaId IS NOT NULL THEN 0 ELSE 1 END AS BIT) AS Pendiente,
           ISNULL(m.FechaLiquidacion, r.FechaLiquidacion) AS FechaLiquidacion,
           ISNULL(ISNULL(m.LiquidadoPorNombre, r.LiquidadaPorNombre), N'') AS LiquidadoPorNombre
    FROM dbo.Rutas r
    LEFT JOIN dbo.Usuarios u ON u.UsuarioId = r.ChoferUsuarioId
    LEFT JOIN dbo.RutaLiquidacionMercaderia m ON m.RutaId = r.RutaId
    CROSS APPLY (
        SELECT COUNT(DISTINCT rp.PedidoId) AS PedidosNoEntregados, ISNULL(SUM(pd.Cantidad), 0) AS Unidades
        FROM dbo.RutaPedidos rp
        INNER JOIN dbo.Pedidos p ON p.PedidoId = rp.PedidoId AND p.Estado NOT IN (N'Cancelado', N'Rechazado')
        LEFT JOIN dbo.PedidoDetalle pd ON pd.PedidoId = rp.PedidoId
        WHERE rp.RutaId = r.RutaId AND rp.EstadoEntrega <> N'Entregado'
    ) x
    WHERE r.Estado = N'Completada' AND x.PedidosNoEntregados > 0
    ORDER BY CASE WHEN r.Liquidada = 1 OR m.MercaderiaId IS NOT NULL THEN 1 ELSE 0 END, r.FechaCierre DESC, r.RutaId DESC;
END;
GO

IF XACT_STATE()<>1 THROW 56863,N'La transacción 0286 no está disponible.',1;
DECLARE @MigrationSha256 NVARCHAR(128)=N'$(MigrationSha256)';
IF LEN(@MigrationSha256)<>64 OR @MigrationSha256 LIKE N'%[^0-9A-Fa-f]%' THROW 56864,N'SHA-256 inválido para 0286.',1;
INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes)
VALUES(N'0286_liquidacion_mercaderia_ruta',N'0286_liquidacion_mercaderia_ruta.sql',UPPER(@MigrationSha256),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),
       N'BUG-10-07: liquidación de mercadería de una ruta completada: valida lo contado contra lo despachado y devuelve al stock con movimientos de entrada.');
COMMIT TRANSACTION;
GO
