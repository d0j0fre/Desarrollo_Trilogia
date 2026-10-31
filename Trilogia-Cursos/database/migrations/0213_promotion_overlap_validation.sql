SET NOCOUNT ON;
SET XACT_ABORT ON;

/*  0213 — Bloquear promociones traslapadas del mismo producto (BUG-17-01).

    sp_Promociones_Create y sp_Promociones_Update permitían registrar una
    segunda promoción activa del mismo producto con vigencia cruzada, de modo
    que el producto quedaba con dos reglas de precio a la vez (CU-171-03).

    Regla
    -----
    Dos promociones se traslapan cuando son del mismo producto estratégico,
    ambas están activas, sus vigencias se cruzan (inicio <= fin de la otra y
    fin >= inicio de la otra) y sus segmentos coinciden ("Todos" coincide con
    cualquier segmento; Mayorista y Minorista no coinciden entre sí). En ese
    caso el procedimiento lanza 53102 y no guarda.

    Sólo redefine los dos procedimientos con CREATE OR ALTER (conserva
    object_id y permisos). No modifica tablas ni filas: las promociones
    traslapadas que ya existan no se tocan.
*/

IF OBJECT_ID(N'dbo.SchemaMigrationHistory', N'U') IS NULL
    THROW 55400, N'Falta el ledger 0001.', 1;

IF EXISTS (SELECT 1 FROM dbo.SchemaMigrationHistory
           WHERE MigrationId = N'0213_promotion_overlap_validation'
             AND Status = N'Applied')
    THROW 55401, N'0213 ya figura aplicada.', 1;

IF OBJECT_ID(N'dbo.Promociones', N'U') IS NULL
    THROW 55402, N'Falta la tabla dbo.Promociones.', 1;

BEGIN TRANSACTION;

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
    @NombreUsr           NVARCHAR(150) = NULL
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
         SegmentoCliente, FechaInicio, FechaFin, Prioridad, RegistradoPorUsuarioId, RegistradoPorNombre)
    VALUES
        (@Nombre, NULLIF(LTRIM(RTRIM(@Descripcion)), N''''), @Tipo, @ProductoId, ISNULL(NULLIF(@CantidadMinima, 0), 1),
         CASE WHEN @Tipo = N''DescuentoPorcentual'' THEN @PorcentajeDescuento END,
         CASE WHEN @Tipo = N''RegaliaPorVolumen'' THEN @ProductoRegaloId END,
         CASE WHEN @Tipo = N''RegaliaPorVolumen'' THEN @CantidadRegalo END,
         @SegmentoCliente, @FechaInicio, @FechaFin, ISNULL(@Prioridad, 0), @UsuarioId, @NombreUsr);

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
    @Prioridad           INT           = 0
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
        FechaActualizacion = SYSDATETIME()
    WHERE PromocionId = @PromocionId;

    COMMIT TRANSACTION;
END';

IF NOT EXISTS (SELECT 1 FROM sys.sql_modules WHERE object_id = OBJECT_ID(N'dbo.sp_Promociones_Create') AND definition LIKE N'%53102%')
   OR NOT EXISTS (SELECT 1 FROM sys.sql_modules WHERE object_id = OBJECT_ID(N'dbo.sp_Promociones_Update') AND definition LIKE N'%53102%')
    THROW 55406, N'Crear o editar promoción no quedó con la validación de traslape.', 1;

IF XACT_STATE() <> 1
    THROW 55404, N'La transacción 0213 no está disponible.', 1;

DECLARE @MigrationSha256 NVARCHAR(128) = N'$(MigrationSha256)';
IF LEN(@MigrationSha256) <> 64 OR @MigrationSha256 LIKE N'%[^0-9A-Fa-f]%'
    THROW 55405, N'SHA-256 inválido para 0213.', 1;

INSERT dbo.SchemaMigrationHistory
    (MigrationId, FileName, FileSha256, Status, AppliedBy, EnvironmentName, Notes)
VALUES
    (N'0213_promotion_overlap_validation',
     N'0213_promotion_overlap_validation.sql',
     UPPER(@MigrationSha256),
     N'Applied',
     ORIGINAL_LOGIN(),
     DB_NAME(),
     N'BUG-17-01: crear/editar promoción rechaza reglas traslapadas del mismo producto (error 53102).');

COMMIT TRANSACTION;
