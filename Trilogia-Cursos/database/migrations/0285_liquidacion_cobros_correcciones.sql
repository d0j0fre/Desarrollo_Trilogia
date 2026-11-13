SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

/* BUG-10-03, BUG-10-04, BUG-10-05, G2-37 y G2-38: Liquidación de cobros de ruta (CU-106).

   BUG-10-04 / G2-37: sp_LiquidacionCobros_Registrar aceptaba rutas Despachadas y
     no había forma de corregir una liquidación ya registrada (una sola por ruta).
     Ahora solo se liquida una ruta Completada. Para corregir se agrega
     sp_LiquidacionCobros_Anular (con motivo, queda en la tabla y en la
     auditoría de la aplicación); la restricción única por ruta pasa a un índice
     único filtrado que solo cuenta las liquidaciones vigentes (Anulada = 0), así
     que no se puede liquidar dos veces sin anular antes.
   G2-38: el detalle de comprobantes se valida contra «Total en comprobantes»
     (53401/53402) y se rechaza el registro si no coincide.
   BUG-10-05: dbo.ChoferFaltantes guarda el faltante a cargo del chofer de la ruta
     (uno por liquidación, Pendiente o Anulado). sp_ChoferFaltantes_List lo lee.
     Las liquidaciones con faltante que ya existían se copian a la tabla.
   BUG-10-03: sp_LiquidacionCobros_GetRecibo reúne lo que la aplicación imprime
     en el recibo PDF (encabezado, comprobantes y pedidos entregados).

   Las firmas devuelven columnas nuevas AL FINAL de cada resultado. El monto
   esperado se calcula igual que antes. Sin datos demo. Rollback en
   0285_liquidacion_cobros_correcciones.rollback.md. */
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL THROW 56850,N'Falta el ledger 0001.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0285_liquidacion_cobros_correcciones' AND Status=N'Applied')
    THROW 56851,N'0285 ya figura aplicada.',1;
IF OBJECT_ID(N'dbo.RutaLiquidacionCobros',N'U') IS NULL OR OBJECT_ID(N'dbo.RutaLiquidacionComprobantes',N'U') IS NULL
   OR OBJECT_ID(N'dbo.Rutas',N'U') IS NULL OR OBJECT_ID(N'dbo.RutaPedidos',N'U') IS NULL OR OBJECT_ID(N'dbo.Usuarios',N'U') IS NULL
    THROW 56852,N'Faltan las tablas de rutas o de liquidación (cu106).',1;

BEGIN TRANSACTION;
GO

IF COL_LENGTH(N'dbo.RutaLiquidacionCobros', N'Anulada') IS NULL
BEGIN
    ALTER TABLE dbo.RutaLiquidacionCobros ADD
        ChoferUsuarioId     INT           NULL,
        Anulada             BIT           NOT NULL CONSTRAINT DF_LiqCob_Anulada DEFAULT 0,
        FechaAnulacion      DATETIME2     NULL,
        AnuladaPorUsuarioId INT           NULL,
        AnuladaPorNombre    NVARCHAR(150) NULL,
        MotivoAnulacion     NVARCHAR(300) NULL;
END;
GO

UPDATE l SET ChoferUsuarioId = r.ChoferUsuarioId
FROM dbo.RutaLiquidacionCobros l
INNER JOIN dbo.Rutas r ON r.RutaId = l.RutaId
WHERE l.ChoferUsuarioId IS NULL;
GO

/* Una sola liquidación VIGENTE por ruta: la restricción única pasa a índice filtrado. */
IF EXISTS (SELECT 1 FROM sys.key_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.RutaLiquidacionCobros') AND name = N'UQ_RutaLiquidacionCobros_Ruta')
    ALTER TABLE dbo.RutaLiquidacionCobros DROP CONSTRAINT UQ_RutaLiquidacionCobros_Ruta;
ELSE IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.RutaLiquidacionCobros') AND name = N'UQ_RutaLiquidacionCobros_Ruta')
    DROP INDEX UQ_RutaLiquidacionCobros_Ruta ON dbo.RutaLiquidacionCobros;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.RutaLiquidacionCobros') AND name = N'UX_RutaLiquidacionCobros_RutaVigente')
    CREATE UNIQUE INDEX UX_RutaLiquidacionCobros_RutaVigente ON dbo.RutaLiquidacionCobros (RutaId) WHERE Anulada = 0;
GO

IF OBJECT_ID(N'dbo.ChoferFaltantes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ChoferFaltantes
    (
        FaltanteId          INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ChoferFaltantes PRIMARY KEY,
        ChoferUsuarioId     INT           NOT NULL CONSTRAINT FK_ChoferFaltantes_Chofer REFERENCES dbo.Usuarios (UsuarioId),
        RutaId              INT           NOT NULL CONSTRAINT FK_ChoferFaltantes_Ruta REFERENCES dbo.Rutas (RutaId),
        LiquidacionCobrosId INT           NOT NULL CONSTRAINT FK_ChoferFaltantes_Liquidacion REFERENCES dbo.RutaLiquidacionCobros (LiquidacionCobrosId),
        Monto               DECIMAL(18,2) NOT NULL CONSTRAINT CK_ChoferFaltantes_Monto CHECK (Monto > 0),
        Estado              NVARCHAR(20)  NOT NULL CONSTRAINT DF_ChoferFaltantes_Estado DEFAULT N'Pendiente'
                                          CONSTRAINT CK_ChoferFaltantes_Estado CHECK (Estado IN (N'Pendiente', N'Anulado')),
        FechaRegistro       DATETIME2     NOT NULL CONSTRAINT DF_ChoferFaltantes_Fecha DEFAULT SYSDATETIME(),
        FechaAnulacion      DATETIME2     NULL
    );
    CREATE UNIQUE INDEX UX_ChoferFaltantes_Liquidacion ON dbo.ChoferFaltantes (LiquidacionCobrosId);
    CREATE INDEX IX_ChoferFaltantes_Chofer ON dbo.ChoferFaltantes (ChoferUsuarioId, Estado) INCLUDE (Monto);
END;
GO

/* Faltantes que ya estaban registrados en liquidaciones vigentes. */
INSERT dbo.ChoferFaltantes (ChoferUsuarioId, RutaId, LiquidacionCobrosId, Monto, Estado, FechaRegistro)
SELECT l.ChoferUsuarioId, l.RutaId, l.LiquidacionCobrosId, -l.Diferencia, N'Pendiente', l.FechaLiquidacion
FROM dbo.RutaLiquidacionCobros l
WHERE l.Estado = N'Faltante' AND l.Anulada = 0 AND l.ChoferUsuarioId IS NOT NULL AND l.Diferencia < 0
  AND NOT EXISTS (SELECT 1 FROM dbo.ChoferFaltantes f WHERE f.LiquidacionCobrosId = l.LiquidacionCobrosId);
GO

/* Preparar: la ruta con su chofer, zona y avance, y lo esperado de los pedidos entregados.
   Las columnas 0-6 no cambian; Zona, ChoferNombre, TotalPedidos y Entregados van al final. */
CREATE OR ALTER PROCEDURE dbo.sp_LiquidacionCobros_Preparar
    @RutaId INT
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM dbo.Rutas WHERE RutaId = @RutaId) THROW 53070, N'La ruta indicada no existe.', 1;

    SELECT
        r.RutaId, r.Codigo, r.Estado, r.Liquidada,
        ISNULL((SELECT TOP (1) lc.LiquidacionCobrosId FROM dbo.RutaLiquidacionCobros lc
                WHERE lc.RutaId = r.RutaId AND lc.Anulada = 0), 0) AS LiquidacionCobrosId,
        e.EsperadoEfectivo, e.EsperadoOtros,
        r.Zona, ISNULL(u.NombreCompleto, N'') AS ChoferNombre,
        (SELECT COUNT(*) FROM dbo.RutaPedidos x WHERE x.RutaId = r.RutaId) AS TotalPedidos,
        (SELECT COUNT(*) FROM dbo.RutaPedidos x WHERE x.RutaId = r.RutaId AND x.EstadoEntrega = N'Entregado') AS Entregados
    FROM dbo.Rutas r
    LEFT JOIN dbo.Usuarios u ON u.UsuarioId = r.ChoferUsuarioId
    CROSS APPLY (
        SELECT
            ISNULL(SUM(CASE WHEN rp.EstadoEntrega = N'Entregado' AND p.MetodoPago LIKE N'%Efectivo%' THEN p.Total END), 0) AS EsperadoEfectivo,
            ISNULL(SUM(CASE WHEN rp.EstadoEntrega = N'Entregado' AND p.MetodoPago NOT LIKE N'%Efectivo%' THEN p.Total END), 0) AS EsperadoOtros
        FROM dbo.RutaPedidos rp
        INNER JOIN dbo.Pedidos p ON p.PedidoId = rp.PedidoId
        WHERE rp.RutaId = r.RutaId
    ) e
    WHERE r.RutaId = @RutaId;

    SELECT p.PedidoId, p.Total, p.MetodoPago, p.EstadoPago, ISNULL(u.NombreCompleto, N'') AS Cliente
    FROM dbo.RutaPedidos rp
    INNER JOIN dbo.Pedidos p ON p.PedidoId = rp.PedidoId
    LEFT JOIN dbo.Usuarios u ON u.UsuarioId = p.UsuarioId
    WHERE rp.RutaId = @RutaId AND rp.EstadoEntrega = N'Entregado'
    ORDER BY p.PedidoId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_LiquidacionCobros_Registrar
    @RutaId                INT,
    @MontoEfectivoRecibido DECIMAL(18,2),
    @MontoComprobantes     DECIMAL(18,2) = 0,
    @Observaciones         NVARCHAR(400) = NULL,
    @ComprobantesJson      NVARCHAR(MAX) = NULL,   -- [{Tipo,Referencia,Monto}]
    @UsuarioId             INT           = NULL,
    @Nombre                NVARCHAR(150) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Estado NVARCHAR(40), @Codigo NVARCHAR(60), @ChoferId INT, @ChoferNombre NVARCHAR(150), @Msg NVARCHAR(400);
    SELECT @Estado = r.Estado, @Codigo = r.Codigo, @ChoferId = r.ChoferUsuarioId, @ChoferNombre = ISNULL(u.NombreCompleto, N'')
    FROM dbo.Rutas r LEFT JOIN dbo.Usuarios u ON u.UsuarioId = r.ChoferUsuarioId
    WHERE r.RutaId = @RutaId;

    IF @Estado IS NULL THROW 53070, N'La ruta indicada no existe.', 1;
    IF @Estado <> N'Completada'
    BEGIN
        SET @Msg = CONCAT(N'Solo se puede liquidar una ruta completada. La ruta ', @Codigo, N' está ',
                          CASE @Estado WHEN N'Despachada' THEN N'despachada y todavía no finaliza' ELSE LOWER(@Estado) END, N'.');
        THROW 53400, @Msg, 1;
    END;
    IF EXISTS (SELECT 1 FROM dbo.RutaLiquidacionCobros WHERE RutaId = @RutaId AND Anulada = 0)
        THROW 53071, N'Esta ruta ya tiene una liquidación financiera registrada. Anúlela con un motivo antes de registrar otra.', 1;
    IF ISNULL(@MontoEfectivoRecibido, -1) < 0 THROW 53072, N'El efectivo recibido no puede ser negativo.', 1;
    IF ISNULL(@MontoComprobantes, -1) < 0 THROW 53073, N'El monto de comprobantes no puede ser negativo.', 1;

    /* G2-38: el detalle debe sumar el total de comprobantes. */
    DECLARE @Vouchers TABLE (Tipo NVARCHAR(30), Referencia NVARCHAR(80), Monto DECIMAL(18,2));
    IF @ComprobantesJson IS NOT NULL AND LTRIM(RTRIM(@ComprobantesJson)) <> N''
    BEGIN
        IF ISJSON(@ComprobantesJson) <> 1 THROW 53402, N'El detalle de comprobantes no tiene un formato válido.', 1;
        INSERT @Vouchers (Tipo, Referencia, Monto)
        SELECT NULLIF(LTRIM(RTRIM(j.Tipo)), N''), NULLIF(LTRIM(RTRIM(j.Referencia)), N''), j.Monto
        FROM OPENJSON(@ComprobantesJson) WITH (
            Tipo       NVARCHAR(30)  '$.Tipo',
            Referencia NVARCHAR(80)  '$.Referencia',
            Monto      DECIMAL(18,2) '$.Monto'
        ) j;
    END;
    IF EXISTS (SELECT 1 FROM @Vouchers WHERE Tipo IS NULL OR ISNULL(Monto, 0) <= 0)
        THROW 53402, N'Cada comprobante necesita su tipo y un monto mayor que cero.', 1;

    DECLARE @SumaDetalle DECIMAL(18,2) = ISNULL((SELECT SUM(Monto) FROM @Vouchers), 0);
    IF @SumaDetalle <> @MontoComprobantes
    BEGIN
        SET @Msg = CONCAT(N'El total en comprobantes (₡', FORMAT(@MontoComprobantes, N'N2', N'en-US'),
                          N') no coincide con la suma del detalle (₡', FORMAT(@SumaDetalle, N'N2', N'en-US'),
                          N'). Detalle cada comprobante o corrija el total.');
        THROW 53401, @Msg, 1;
    END;

    DECLARE @Esp DECIMAL(18,2) = 0, @EspOtros DECIMAL(18,2) = 0;
    SELECT
        @Esp      = ISNULL(SUM(CASE WHEN rp.EstadoEntrega = N'Entregado' AND p.MetodoPago LIKE N'%Efectivo%' THEN p.Total END), 0),
        @EspOtros = ISNULL(SUM(CASE WHEN rp.EstadoEntrega = N'Entregado' AND p.MetodoPago NOT LIKE N'%Efectivo%' THEN p.Total END), 0)
    FROM dbo.RutaPedidos rp
    INNER JOIN dbo.Pedidos p ON p.PedidoId = rp.PedidoId
    WHERE rp.RutaId = @RutaId;

    DECLARE @Dif DECIMAL(18,2) = (@MontoEfectivoRecibido + @MontoComprobantes) - (@Esp + @EspOtros);
    DECLARE @EstadoLiq NVARCHAR(20) = CASE WHEN @Dif = 0 THEN N'Cuadrada' WHEN @Dif < 0 THEN N'Faltante' ELSE N'Sobrante' END;
    DECLARE @Id INT;

    BEGIN TRANSACTION;
    BEGIN TRY
        INSERT INTO dbo.RutaLiquidacionCobros
            (RutaId, MontoEsperadoEfectivo, MontoEsperadoOtros, MontoEfectivoRecibido, MontoComprobantes, Estado, Observaciones,
             LiquidadoPorUsuarioId, LiquidadoPorNombre, ChoferUsuarioId)
        VALUES
            (@RutaId, @Esp, @EspOtros, @MontoEfectivoRecibido, @MontoComprobantes, @EstadoLiq, NULLIF(LTRIM(RTRIM(@Observaciones)), N''),
             @UsuarioId, @Nombre, @ChoferId);

        SET @Id = SCOPE_IDENTITY();

        INSERT INTO dbo.RutaLiquidacionComprobantes (LiquidacionCobrosId, Tipo, Referencia, Monto)
        SELECT @Id, Tipo, Referencia, Monto FROM @Vouchers;

        /* BUG-10-05: el faltante queda a cargo del chofer de la ruta. */
        IF @EstadoLiq = N'Faltante' AND @ChoferId IS NOT NULL
            INSERT INTO dbo.ChoferFaltantes (ChoferUsuarioId, RutaId, LiquidacionCobrosId, Monto)
            VALUES (@ChoferId, @RutaId, @Id, -@Dif);

        COMMIT;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK;
        THROW;
    END CATCH;

    SELECT @Id AS LiquidacionCobrosId, @EstadoLiq AS Estado, @Dif AS Diferencia, @ChoferNombre AS ChoferNombre,
           CASE WHEN @EstadoLiq = N'Faltante' AND @ChoferId IS NOT NULL THEN -@Dif ELSE CAST(0 AS DECIMAL(18,2)) END AS FaltanteChofer;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_LiquidacionCobros_List
    @Estado NVARCHAR(20) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET @Estado = NULLIF(LTRIM(RTRIM(@Estado)), N'');
    SELECT lc.LiquidacionCobrosId, lc.RutaId, r.Codigo AS RutaCodigo,
           lc.MontoEsperadoEfectivo, lc.MontoEsperadoOtros, lc.MontoEfectivoRecibido, lc.MontoComprobantes,
           lc.Diferencia, lc.Estado, ISNULL(lc.LiquidadoPorNombre, N'') AS LiquidadoPorNombre, lc.FechaLiquidacion,
           ISNULL(ch.NombreCompleto, N'') AS ChoferNombre,
           lc.Anulada, ISNULL(lc.MotivoAnulacion, N'') AS MotivoAnulacion, lc.FechaAnulacion,
           ISNULL(lc.AnuladaPorNombre, N'') AS AnuladaPorNombre
    FROM dbo.RutaLiquidacionCobros lc
    INNER JOIN dbo.Rutas r ON r.RutaId = lc.RutaId
    LEFT JOIN dbo.Usuarios ch ON ch.UsuarioId = ISNULL(lc.ChoferUsuarioId, r.ChoferUsuarioId)
    WHERE (@Estado IS NULL OR lc.Estado = @Estado)
    ORDER BY lc.FechaLiquidacion DESC, lc.LiquidacionCobrosId DESC;
END;
GO

/* G2-37: corregir una liquidación ya registrada. Conserva la fila (Anulada = 1, con motivo,
   quién y cuándo), anula el faltante del chofer y deja la ruta lista para liquidarse de nuevo. */
CREATE OR ALTER PROCEDURE dbo.sp_LiquidacionCobros_Anular
    @LiquidacionCobrosId INT,
    @Motivo              NVARCHAR(300),
    @UsuarioId           INT           = NULL,
    @Nombre              NVARCHAR(150) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SET @Motivo = NULLIF(LTRIM(RTRIM(@Motivo)), N'');
    DECLARE @Anulada BIT, @RutaId INT, @Codigo NVARCHAR(60);
    SELECT @Anulada = lc.Anulada, @RutaId = lc.RutaId, @Codigo = r.Codigo
    FROM dbo.RutaLiquidacionCobros lc
    INNER JOIN dbo.Rutas r ON r.RutaId = lc.RutaId
    WHERE lc.LiquidacionCobrosId = @LiquidacionCobrosId;

    IF @RutaId IS NULL THROW 53403, N'No se encontró la liquidación indicada.', 1;
    IF @Anulada = 1 THROW 53404, N'Esa liquidación ya estaba anulada.', 1;
    IF @Motivo IS NULL THROW 53405, N'Indique el motivo de la anulación.', 1;

    BEGIN TRANSACTION;
    BEGIN TRY
        UPDATE dbo.RutaLiquidacionCobros
        SET Anulada = 1, FechaAnulacion = SYSDATETIME(), AnuladaPorUsuarioId = @UsuarioId,
            AnuladaPorNombre = @Nombre, MotivoAnulacion = @Motivo
        WHERE LiquidacionCobrosId = @LiquidacionCobrosId;

        UPDATE dbo.ChoferFaltantes
        SET Estado = N'Anulado', FechaAnulacion = SYSDATETIME()
        WHERE LiquidacionCobrosId = @LiquidacionCobrosId AND Estado = N'Pendiente';
        COMMIT;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK;
        THROW;
    END CATCH;

    SELECT @RutaId AS RutaId, @Codigo AS RutaCodigo;
END;
GO

/* BUG-10-05: faltantes a cargo de los choferes (el más reciente primero). */
CREATE OR ALTER PROCEDURE dbo.sp_ChoferFaltantes_List
    @Estado NVARCHAR(20) = N'Pendiente'
AS
BEGIN
    SET NOCOUNT ON;
    SET @Estado = NULLIF(LTRIM(RTRIM(@Estado)), N'');
    SELECT f.FaltanteId, f.ChoferUsuarioId, ISNULL(u.NombreCompleto, N'') AS ChoferNombre,
           f.RutaId, r.Codigo AS RutaCodigo, f.LiquidacionCobrosId, f.Monto, f.Estado, f.FechaRegistro
    FROM dbo.ChoferFaltantes f
    INNER JOIN dbo.Rutas r ON r.RutaId = f.RutaId
    LEFT JOIN dbo.Usuarios u ON u.UsuarioId = f.ChoferUsuarioId
    WHERE @Estado IS NULL OR f.Estado = @Estado
    ORDER BY f.FechaRegistro DESC, f.FaltanteId DESC;
END;
GO

/* BUG-10-03: datos del recibo. 1) encabezado, 2) comprobantes, 3) pedidos entregados. */
CREATE OR ALTER PROCEDURE dbo.sp_LiquidacionCobros_GetRecibo
    @LiquidacionCobrosId INT
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM dbo.RutaLiquidacionCobros WHERE LiquidacionCobrosId = @LiquidacionCobrosId)
        THROW 53403, N'No se encontró la liquidación indicada.', 1;

    SELECT lc.LiquidacionCobrosId, lc.RutaId, r.Codigo AS RutaCodigo, r.Zona,
           ISNULL(ch.NombreCompleto, N'') AS ChoferNombre,
           ISNULL(v.Placa, N'') AS VehiculoPlaca, ISNULL(v.Descripcion, N'') AS VehiculoDescripcion,
           lc.MontoEsperadoEfectivo, lc.MontoEsperadoOtros, lc.MontoEfectivoRecibido, lc.MontoComprobantes,
           lc.Diferencia, lc.Estado, ISNULL(lc.Observaciones, N'') AS Observaciones,
           ISNULL(lc.LiquidadoPorNombre, N'') AS LiquidadoPorNombre, lc.FechaLiquidacion,
           lc.Anulada, ISNULL(lc.MotivoAnulacion, N'') AS MotivoAnulacion
    FROM dbo.RutaLiquidacionCobros lc
    INNER JOIN dbo.Rutas r ON r.RutaId = lc.RutaId
    LEFT JOIN dbo.Usuarios ch ON ch.UsuarioId = ISNULL(lc.ChoferUsuarioId, r.ChoferUsuarioId)
    LEFT JOIN dbo.Vehiculos v ON v.VehiculoId = r.VehiculoId
    WHERE lc.LiquidacionCobrosId = @LiquidacionCobrosId;

    SELECT c.Tipo, ISNULL(c.Referencia, N'') AS Referencia, c.Monto
    FROM dbo.RutaLiquidacionComprobantes c
    WHERE c.LiquidacionCobrosId = @LiquidacionCobrosId
    ORDER BY c.ComprobanteId;

    SELECT p.PedidoId, ISNULL(u.NombreCompleto, N'') AS Cliente, p.MetodoPago, p.Total
    FROM dbo.RutaLiquidacionCobros lc
    INNER JOIN dbo.RutaPedidos rp ON rp.RutaId = lc.RutaId AND rp.EstadoEntrega = N'Entregado'
    INNER JOIN dbo.Pedidos p ON p.PedidoId = rp.PedidoId
    LEFT JOIN dbo.Usuarios u ON u.UsuarioId = p.UsuarioId
    WHERE lc.LiquidacionCobrosId = @LiquidacionCobrosId
    ORDER BY p.PedidoId;
END;
GO

IF XACT_STATE()<>1 THROW 56853,N'La transacción 0285 no está disponible.',1;
DECLARE @MigrationSha256 NVARCHAR(128)=N'$(MigrationSha256)';
IF LEN(@MigrationSha256)<>64 OR @MigrationSha256 LIKE N'%[^0-9A-Fa-f]%' THROW 56854,N'SHA-256 inválido para 0285.',1;
INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes)
VALUES(N'0285_liquidacion_cobros_correcciones',N'0285_liquidacion_cobros_correcciones.sql',UPPER(@MigrationSha256),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),
       N'BUG-10-03/04/05, G2-37/38: liquidación solo de rutas completadas, anulación con motivo, detalle contra total, faltante a cargo del chofer y datos del recibo.');
COMMIT TRANSACTION;
GO
