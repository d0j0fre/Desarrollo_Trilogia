SET NOCOUNT ON;
SET XACT_ABORT ON;

/*  0214 — Límite de unidades de promociones (BUG-17-02).

    Las promociones no tenían límite de unidades, así que nunca se inactivaban
    por consumo (CU-171-05).

    Cambios
    -------
    - dbo.Promociones.LimiteUnidades INT NULL (NULL = sin límite) y
      dbo.Promociones.UnidadesConsumidas INT NOT NULL DEFAULT 0.
    - sp_Promociones_Create / sp_Promociones_Update aceptan @LimiteUnidades
      (opcional). Editar no permite un límite menor o igual a lo ya consumido
      (error 53103). Conservan la validación de traslape de 0213.
    - sp_Promociones_GetById y sp_Promociones_List devuelven LimiteUnidades y
      UnidadesConsumidas al final.
    - Disparador TR_PromocionAplicaciones_LimiteUnidades (AFTER INSERT en
      dbo.PromocionAplicaciones): cada vez que el checkout registra que una
      promoción se aplicó a un pedido, suma las unidades promocionales
        · descuento: unidades del producto estratégico compradas con descuento
          (línea del pedido que no es regalo ni componente de combo);
        · regalía: unidades regaladas;
      y, si se alcanza el límite, pasa la promoción a Inactiva con el motivo
      «Límite de unidades alcanzado», aunque no haya llegado su fecha de fin.
      Como el checkout sólo aplica promociones Activa, el pedido siguiente se
      cobra a precio regular. El disparador corre dentro de la transacción del
      checkout, así que no cambia sp_Store_CreateOrderWithPromotions.

    Requiere 0213 y las columnas PedidoDetalle.EsRegalo / PedidoComboId (0012).
*/

IF OBJECT_ID(N'dbo.SchemaMigrationHistory', N'U') IS NULL
    THROW 55400, N'Falta el ledger 0001.', 1;

IF EXISTS (SELECT 1 FROM dbo.SchemaMigrationHistory
           WHERE MigrationId = N'0214_promotion_unit_limit'
             AND Status = N'Applied')
    THROW 55401, N'0214 ya figura aplicada.', 1;

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrationHistory
               WHERE MigrationId = N'0213_promotion_overlap_validation'
                 AND Status = N'Applied')
    THROW 55402, N'0214 requiere 0213 aplicada.', 1;

IF OBJECT_ID(N'dbo.PromocionAplicaciones', N'U') IS NULL
   OR COL_LENGTH(N'dbo.PedidoDetalle', N'EsRegalo') IS NULL
   OR COL_LENGTH(N'dbo.PedidoDetalle', N'PedidoComboId') IS NULL
    THROW 55403, N'Faltan dbo.PromocionAplicaciones o las columnas PedidoDetalle.EsRegalo / PedidoComboId.', 1;

BEGIN TRANSACTION;

IF COL_LENGTH(N'dbo.Promociones', N'LimiteUnidades') IS NULL
    EXEC sys.sp_executesql N'ALTER TABLE dbo.Promociones ADD LimiteUnidades INT NULL
        CONSTRAINT CK_Promo_LimiteUnidades CHECK (LimiteUnidades IS NULL OR LimiteUnidades > 0);';

IF COL_LENGTH(N'dbo.Promociones', N'UnidadesConsumidas') IS NULL
    EXEC sys.sp_executesql N'ALTER TABLE dbo.Promociones ADD UnidadesConsumidas INT NOT NULL
        CONSTRAINT DF_Promo_UnidadesConsumidas DEFAULT (0);';

EXEC sys.sp_executesql N'
CREATE OR ALTER PROCEDURE dbo.sp_Promociones_Create
    @Nombre              NVARCHAR(150),
    @Descripcion         NVARCHAR(300) = NULL,
    @Tipo                NVARCHAR(25),
    @ProductoId          INT,
    @CantidadMinima      INT           = 1,
    @PorcentajeDescuento DECIMAL(5,2)  = NULL,
    @ProductoRegaloId    INT           = NULL,
    @CantidadRegalo      INT           = NULL,
    @SegmentoCliente     NVARCHAR(20)  = N''Todos'',
    @FechaInicio         DATE,
    @FechaFin            DATE,
    @Prioridad           INT           = 0,
    @UsuarioId           INT           = NULL,
    @NombreUsr           NVARCHAR(150) = NULL,
    @LimiteUnidades      INT           = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET @Nombre = NULLIF(LTRIM(RTRIM(@Nombre)), N'''');
    SET @SegmentoCliente = ISNULL(@SegmentoCliente, N''Todos'');
    IF @Nombre IS NULL THROW 53090, ''El nombre de la promoción es obligatorio.'', 1;
    IF @Tipo NOT IN (N''DescuentoPorcentual'', N''RegaliaPorVolumen'') THROW 53091, ''Tipo de promoción inválido.'', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.Productos WHERE ProductoId = @ProductoId AND Activo = 1) THROW 53092, ''El producto estratégico no existe.'', 1;
    IF @FechaFin < @FechaInicio THROW 53093, ''La fecha fin no puede ser anterior a la de inicio.'', 1;
    IF @Tipo = N''DescuentoPorcentual'' AND (@PorcentajeDescuento IS NULL OR @PorcentajeDescuento <= 0 OR @PorcentajeDescuento > 100)
        THROW 53094, ''El porcentaje de descuento debe estar entre 0 y 100.'', 1;
    IF @Tipo = N''RegaliaPorVolumen'' AND (@ProductoRegaloId IS NULL OR @CantidadRegalo IS NULL OR @CantidadRegalo <= 0)
        THROW 53095, ''Debe indicar producto de regalía y cantidad.'', 1;
    IF @Tipo = N''RegaliaPorVolumen'' AND NOT EXISTS (SELECT 1 FROM dbo.Productos WHERE ProductoId = @ProductoRegaloId AND Activo = 1)
        THROW 53096, ''El producto de regalía no existe.'', 1;
    IF @SegmentoCliente NOT IN (N''Todos'', N''Mayorista'', N''Minorista'') THROW 53097, ''Segmento de cliente inválido.'', 1;
    IF @LimiteUnidades IS NOT NULL AND @LimiteUnidades <= 0 THROW 53103, ''El límite de unidades debe ser mayor que cero.'', 1;

    BEGIN TRANSACTION;

    /* BUG-17-01: no se permiten reglas traslapadas para el mismo producto. */
    IF EXISTS (SELECT 1 FROM dbo.Promociones WITH (UPDLOCK, HOLDLOCK)
               WHERE ProductoId = @ProductoId
                 AND Estado = N''Activa''
                 AND FechaInicio <= @FechaFin
                 AND FechaFin >= @FechaInicio
                 AND (SegmentoCliente = N''Todos'' OR @SegmentoCliente = N''Todos'' OR SegmentoCliente = @SegmentoCliente))
        THROW 53102, ''Reglas traslapadas: el producto ya tiene una promoción activa en ese periodo.'', 1;

    INSERT INTO dbo.Promociones
        (Nombre, Descripcion, Tipo, ProductoId, CantidadMinima, PorcentajeDescuento, ProductoRegaloId, CantidadRegalo,
         SegmentoCliente, FechaInicio, FechaFin, Prioridad, RegistradoPorUsuarioId, RegistradoPorNombre, LimiteUnidades)
    VALUES
        (@Nombre, NULLIF(LTRIM(RTRIM(@Descripcion)), N''''), @Tipo, @ProductoId, ISNULL(NULLIF(@CantidadMinima, 0), 1),
         CASE WHEN @Tipo = N''DescuentoPorcentual'' THEN @PorcentajeDescuento END,
         CASE WHEN @Tipo = N''RegaliaPorVolumen'' THEN @ProductoRegaloId END,
         CASE WHEN @Tipo = N''RegaliaPorVolumen'' THEN @CantidadRegalo END,
         @SegmentoCliente, @FechaInicio, @FechaFin, ISNULL(@Prioridad, 0), @UsuarioId, @NombreUsr, @LimiteUnidades);

    DECLARE @PromocionId INT = CAST(SCOPE_IDENTITY() AS INT);
    COMMIT TRANSACTION;

    SELECT @PromocionId AS PromocionId;
END';

EXEC sys.sp_executesql N'
CREATE OR ALTER PROCEDURE dbo.sp_Promociones_Update
    @PromocionId         INT,
    @Nombre              NVARCHAR(150),
    @Descripcion         NVARCHAR(300) = NULL,
    @Tipo                NVARCHAR(25),
    @ProductoId          INT,
    @CantidadMinima      INT           = 1,
    @PorcentajeDescuento DECIMAL(5,2)  = NULL,
    @ProductoRegaloId    INT           = NULL,
    @CantidadRegalo      INT           = NULL,
    @SegmentoCliente     NVARCHAR(20)  = N''Todos'',
    @FechaInicio         DATE,
    @FechaFin            DATE,
    @Prioridad           INT           = 0,
    @LimiteUnidades      INT           = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DECLARE @EstadoActual NVARCHAR(20) = (SELECT Estado FROM dbo.Promociones WHERE PromocionId = @PromocionId);
    IF @EstadoActual IS NULL       THROW 53098, ''No se encontró la promoción.'', 1;
    IF @EstadoActual <> N''Activa''  THROW 53099, ''Solo se pueden editar promociones activas.'', 1;
    SET @Nombre = NULLIF(LTRIM(RTRIM(@Nombre)), N'''');
    SET @SegmentoCliente = ISNULL(@SegmentoCliente, N''Todos'');
    IF @Nombre IS NULL THROW 53090, ''El nombre de la promoción es obligatorio.'', 1;
    IF @Tipo NOT IN (N''DescuentoPorcentual'', N''RegaliaPorVolumen'') THROW 53091, ''Tipo de promoción inválido.'', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.Productos WHERE ProductoId = @ProductoId AND Activo = 1) THROW 53092, ''El producto estratégico no existe.'', 1;
    IF @FechaFin < @FechaInicio THROW 53093, ''La fecha fin no puede ser anterior a la de inicio.'', 1;
    IF @Tipo = N''DescuentoPorcentual'' AND (@PorcentajeDescuento IS NULL OR @PorcentajeDescuento <= 0 OR @PorcentajeDescuento > 100)
        THROW 53094, ''El porcentaje de descuento debe estar entre 0 y 100.'', 1;
    IF @Tipo = N''RegaliaPorVolumen'' AND (@ProductoRegaloId IS NULL OR @CantidadRegalo IS NULL OR @CantidadRegalo <= 0)
        THROW 53095, ''Debe indicar producto de regalía y cantidad.'', 1;

    BEGIN TRANSACTION;

    /* BUG-17-02: el límite no puede quedar por debajo de lo ya consumido. */
    IF @LimiteUnidades IS NOT NULL
       AND @LimiteUnidades <= (SELECT UnidadesConsumidas FROM dbo.Promociones WITH (UPDLOCK, HOLDLOCK) WHERE PromocionId = @PromocionId)
        THROW 53103, ''El límite de unidades debe ser mayor que las unidades ya consumidas.'', 1;

    /* BUG-17-01: no se permiten reglas traslapadas para el mismo producto. */
    IF EXISTS (SELECT 1 FROM dbo.Promociones WITH (UPDLOCK, HOLDLOCK)
               WHERE ProductoId = @ProductoId
                 AND PromocionId <> @PromocionId
                 AND Estado = N''Activa''
                 AND FechaInicio <= @FechaFin
                 AND FechaFin >= @FechaInicio
                 AND (SegmentoCliente = N''Todos'' OR @SegmentoCliente = N''Todos'' OR SegmentoCliente = @SegmentoCliente))
        THROW 53102, ''Reglas traslapadas: el producto ya tiene una promoción activa en ese periodo.'', 1;

    UPDATE dbo.Promociones
    SET Nombre = @Nombre,
        Descripcion = NULLIF(LTRIM(RTRIM(@Descripcion)), N''''),
        Tipo = @Tipo,
        ProductoId = @ProductoId,
        CantidadMinima = ISNULL(NULLIF(@CantidadMinima, 0), 1),
        PorcentajeDescuento = CASE WHEN @Tipo = N''DescuentoPorcentual'' THEN @PorcentajeDescuento END,
        ProductoRegaloId = CASE WHEN @Tipo = N''RegaliaPorVolumen'' THEN @ProductoRegaloId END,
        CantidadRegalo = CASE WHEN @Tipo = N''RegaliaPorVolumen'' THEN @CantidadRegalo END,
        SegmentoCliente = @SegmentoCliente,
        FechaInicio = @FechaInicio, FechaFin = @FechaFin, Prioridad = ISNULL(@Prioridad, 0),
        LimiteUnidades = @LimiteUnidades,
        FechaActualizacion = SYSDATETIME()
    WHERE PromocionId = @PromocionId;

    COMMIT TRANSACTION;
END';

/* El orden de columnas es el que lee PromotionsDbService.GetPromotionByIdAsync. */
EXEC sys.sp_executesql N'
CREATE OR ALTER PROCEDURE dbo.sp_Promociones_GetById
    @PromocionId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT PromocionId, Nombre, ISNULL(Descripcion, N'''') AS Descripcion, Tipo, ProductoId, CantidadMinima,
           PorcentajeDescuento, ProductoRegaloId, CantidadRegalo, SegmentoCliente, FechaInicio, FechaFin,
           Estado, Prioridad, LimiteUnidades, UnidadesConsumidas
    FROM dbo.Promociones WHERE PromocionId = @PromocionId;
END';

/* El orden de columnas es el que lee PromotionsDbService.GetPromotionsAsync. */
EXEC sys.sp_executesql N'
CREATE OR ALTER PROCEDURE dbo.sp_Promociones_List
    @Estado NVARCHAR(20)  = NULL,
    @Buscar NVARCHAR(150) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET @Estado = NULLIF(LTRIM(RTRIM(@Estado)), N'''');
    SET @Buscar = NULLIF(LTRIM(RTRIM(@Buscar)), N'''');
    SELECT
        pr.PromocionId, pr.Nombre, pr.Tipo, pr.SegmentoCliente, pr.Estado, pr.FechaInicio, pr.FechaFin,
        pr.CantidadMinima, pr.PorcentajeDescuento, pr.CantidadRegalo, pr.Prioridad,
        p.Nombre AS ProductoNombre, pg.Nombre AS ProductoRegaloNombre,
        CASE WHEN pr.Estado = N''Activa'' AND CAST(SYSDATETIME() AS DATE) BETWEEN pr.FechaInicio AND pr.FechaFin THEN 1 ELSE 0 END AS Vigente,
        pr.LimiteUnidades, pr.UnidadesConsumidas
    FROM dbo.Promociones pr
    INNER JOIN dbo.Productos p ON p.ProductoId = pr.ProductoId
    LEFT JOIN dbo.Productos pg ON pg.ProductoId = pr.ProductoRegaloId
    WHERE (@Estado IS NULL OR pr.Estado = @Estado)
      AND (@Buscar IS NULL OR pr.Nombre LIKE N''%'' + @Buscar + N''%'' OR p.Nombre LIKE N''%'' + @Buscar + N''%'')
    ORDER BY pr.Estado, pr.Prioridad DESC, pr.FechaFin;
END';

EXEC sys.sp_executesql N'
CREATE OR ALTER TRIGGER dbo.TR_PromocionAplicaciones_LimiteUnidades
ON dbo.PromocionAplicaciones
AFTER INSERT
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM inserted) RETURN;

    /* Unidades promocionales de cada aplicación: regalías = unidades regaladas;
       descuento = unidades del producto estratégico cobradas con descuento. */
    ;WITH Consumo AS
    (
        SELECT applied.PromocionId,
               SUM(CASE WHEN applied.TipoBeneficio = N''Regalia''
                        THEN ISNULL(applied.UnidadesRegalo, 0)
                        ELSE ISNULL(line.Cantidad, 0) END) AS Unidades
        FROM inserted applied
        OUTER APPLY
        (
            SELECT SUM(detail.Cantidad) AS Cantidad
            FROM dbo.PedidoDetalle detail
            WHERE detail.PedidoId = applied.PedidoId
              AND detail.ProductoId = applied.ProductoId
              AND ISNULL(detail.EsRegalo, 0) = 0
              AND detail.PedidoComboId IS NULL
        ) line
        GROUP BY applied.PromocionId
    )
    UPDATE promotion
    SET UnidadesConsumidas = promotion.UnidadesConsumidas + consumo.Unidades,
        Estado = CASE WHEN alcanzado.Si = 1 THEN N''Inactiva'' ELSE promotion.Estado END,
        MotivoInactivacion = CASE WHEN alcanzado.Si = 1
                                  THEN CONCAT(N''Límite de unidades alcanzado ('', promotion.UnidadesConsumidas + consumo.Unidades,
                                              N'' de '', promotion.LimiteUnidades, N'').'')
                                  ELSE promotion.MotivoInactivacion END,
        InactivadaPorNombre = CASE WHEN alcanzado.Si = 1 THEN N''Sistema'' ELSE promotion.InactivadaPorNombre END,
        FechaInactivacion = CASE WHEN alcanzado.Si = 1 THEN SYSDATETIME() ELSE promotion.FechaInactivacion END,
        FechaActualizacion = SYSDATETIME()
    FROM dbo.Promociones promotion
    INNER JOIN Consumo consumo ON consumo.PromocionId = promotion.PromocionId
    CROSS APPLY
    (
        SELECT CASE WHEN promotion.Estado = N''Activa''
                         AND promotion.LimiteUnidades IS NOT NULL
                         AND promotion.UnidadesConsumidas + consumo.Unidades >= promotion.LimiteUnidades
                    THEN 1 ELSE 0 END AS Si
    ) alcanzado;
END';

/* Validación posterior. */
IF COL_LENGTH(N'dbo.Promociones', N'LimiteUnidades') IS NULL OR COL_LENGTH(N'dbo.Promociones', N'UnidadesConsumidas') IS NULL
    THROW 55406, N'Faltan las columnas LimiteUnidades / UnidadesConsumidas.', 1;

IF NOT EXISTS (SELECT 1 FROM sys.parameters WHERE object_id = OBJECT_ID(N'dbo.sp_Promociones_Create') AND name = N'@LimiteUnidades')
   OR NOT EXISTS (SELECT 1 FROM sys.parameters WHERE object_id = OBJECT_ID(N'dbo.sp_Promociones_Update') AND name = N'@LimiteUnidades')
    THROW 55406, N'Crear o editar promoción no acepta @LimiteUnidades.', 1;

IF NOT EXISTS (SELECT 1 FROM sys.sql_modules WHERE object_id = OBJECT_ID(N'dbo.sp_Promociones_Create') AND definition LIKE N'%53102%')
    THROW 55406, N'Se perdió la validación de traslape de 0213.', 1;

IF OBJECT_ID(N'dbo.TR_PromocionAplicaciones_LimiteUnidades', N'TR') IS NULL
    THROW 55406, N'No quedó creado el disparador de límite de unidades.', 1;

IF XACT_STATE() <> 1
    THROW 55404, N'La transacción 0214 no está disponible.', 1;

DECLARE @MigrationSha256 NVARCHAR(128) = N'$(MigrationSha256)';
IF LEN(@MigrationSha256) <> 64 OR @MigrationSha256 LIKE N'%[^0-9A-Fa-f]%'
    THROW 55405, N'SHA-256 inválido para 0214.', 1;

INSERT dbo.SchemaMigrationHistory
    (MigrationId, FileName, FileSha256, Status, AppliedBy, EnvironmentName, Notes)
VALUES
    (N'0214_promotion_unit_limit',
     N'0214_promotion_unit_limit.sql',
     UPPER(@MigrationSha256),
     N'Applied',
     ORIGINAL_LOGIN(),
     DB_NAME(),
     N'BUG-17-02: límite de unidades por promoción e inactivación automática al alcanzarlo.');

COMMIT TRANSACTION;
