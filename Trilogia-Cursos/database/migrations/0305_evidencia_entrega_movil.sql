SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

/* G3-04: foto y firma de la entrega desde la app del chofer.

   La web ya registra evidencia con sp_Entrega_RegisterEvidence (0004). La API móvil
   usa ese mismo procedimiento; esta migración solo agrega lo que la app necesita para
   que subir un archivo por una red mala sea seguro:

   1) dbo.EntregaEvidencias.SyncGuid (nullable) + índice único filtrado. La app genera
      un identificador por cada archivo y lo reenvía igual en cada reintento; el servidor
      reconoce el reenvío y no crea una segunda evidencia (misma idea que
      EntregaSyncLog para el estado de la entrega). Las evidencias de la web quedan con
      SyncGuid NULL.

   2) sp_Entrega_RegisterEvidence recibe @SyncGuid OPCIONAL (valor por defecto NULL). La
      validación y el INSERT son los de siempre: el sitio web la sigue llamando sin el
      parámetro y no cambia en nada.

   3) sp_Chofer_PrepararEvidencia: a partir de la parada (RutaPedidoId) y del chofer del
      token, devuelve la ruta y el pedido y decide si esa parada admite evidencia:
        - la ruta debe ser del chofer (52086; no se distingue «no existe» de «no es suya»);
        - la ruta debe estar Despachada, o Completada hace 48 horas o menos (52087). Esto
          último existe porque la app envía primero el estado y después los archivos, y la
          última entrega de la ruta la completa sola: sin esa tolerancia la foto de la
          última parada nunca podría subirse. También cubre el envío sin señal que llega
          horas después;
        - si el SyncGuid ya tiene una evidencia lista del mismo chofer, la devuelve
          (EvidenciaIdExistente) para que la API conteste «duplicado» sin tocar el disco;
        - un SyncGuid de otro chofer se rechaza (52088).

   Aditiva y sin datos nuevos. Rollback en 0305_evidencia_entrega_movil.rollback.md. */
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL THROW 57050,N'Falta el ledger 0001.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0305_evidencia_entrega_movil' AND Status=N'Applied')
    THROW 57051,N'0305 ya figura aplicada.',1;
IF OBJECT_ID(N'dbo.EntregaEvidencias',N'U') IS NULL OR OBJECT_ID(N'dbo.sp_Entrega_RegisterEvidence',N'P') IS NULL
    THROW 57052,N'Falta la evidencia de entrega (0004).',1;
IF OBJECT_ID(N'dbo.Rutas',N'U') IS NULL OR OBJECT_ID(N'dbo.RutaPedidos',N'U') IS NULL
    THROW 57053,N'Faltan las tablas de rutas.',1;

BEGIN TRANSACTION;
GO

IF COL_LENGTH(N'dbo.EntregaEvidencias', N'SyncGuid') IS NULL
    ALTER TABLE dbo.EntregaEvidencias ADD SyncGuid UNIQUEIDENTIFIER NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.EntregaEvidencias') AND name = N'UX_EntregaEvidencias_SyncGuid')
    CREATE UNIQUE INDEX UX_EntregaEvidencias_SyncGuid ON dbo.EntregaEvidencias (SyncGuid) WHERE SyncGuid IS NOT NULL;
GO

/* Igual que en 0004; solo suma @SyncGuid (opcional) al final de los parámetros y al INSERT. */
CREATE OR ALTER PROCEDURE dbo.sp_Entrega_RegisterEvidence
    @PedidoId INT,
    @RutaId INT,
    @TipoEvidencia NVARCHAR(20),
    @StorageKey NVARCHAR(160),
    @MimeType NVARCHAR(100),
    @Observaciones NVARCHAR(300) = NULL,
    @RegistradoPorUsuarioId INT,
    @RegistradoPorNombre NVARCHAR(150) = NULL,
    @SyncGuid UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    SET @TipoEvidencia = NULLIF(LTRIM(RTRIM(@TipoEvidencia)), N'');
    SET @StorageKey = NULLIF(LTRIM(RTRIM(@StorageKey)), N'');
    SET @MimeType = LOWER(NULLIF(LTRIM(RTRIM(@MimeType)), N''));

    IF @TipoEvidencia NOT IN (N'Foto', N'Firma')
        THROW 52080, N'El tipo de evidencia no es válido.', 1;
    IF @MimeType NOT IN (N'image/jpeg', N'image/png', N'image/webp')
        THROW 52081, N'El tipo de contenido no es válido.', 1;
    IF @StorageKey IS NULL OR @StorageKey LIKE N'%/%' OR @StorageKey LIKE N'%\%' OR @StorageKey LIKE N'%:%'
        THROW 52082, N'La referencia de almacenamiento no es válida.', 1;
    IF (@MimeType = N'image/jpeg' AND RIGHT(@StorageKey, 4) <> N'.jpg')
       OR (@MimeType = N'image/png' AND RIGHT(@StorageKey, 4) <> N'.png')
       OR (@MimeType = N'image/webp' AND RIGHT(@StorageKey, 5) <> N'.webp')
        THROW 52083, N'La referencia no coincide con el tipo de contenido.', 1;

    IF NOT EXISTS
    (
        SELECT 1
        FROM dbo.RutaPedidos rp
        INNER JOIN dbo.Rutas r ON r.RutaId = rp.RutaId
        INNER JOIN dbo.Usuarios actor ON actor.UsuarioId = @RegistradoPorUsuarioId AND actor.Activo = 1
        INNER JOIN dbo.Perfiles perfil ON perfil.PerfilId = actor.PerfilId
        WHERE rp.RutaId = @RutaId
          AND rp.PedidoId = @PedidoId
          AND (r.ChoferUsuarioId = @RegistradoPorUsuarioId OR perfil.Nombre = N'Administrador')
    )
        THROW 52084, N'El usuario no está autorizado para registrar evidencia en esta entrega.', 1;

    INSERT INTO dbo.EntregaEvidencias
        (PedidoId, RutaId, TipoEvidencia, ArchivoUrl, StorageKey, MimeType, StorageStatus,
         Observaciones, RegistradoPorUsuarioId, RegistradoPorNombre, FechaRegistro, SyncGuid)
    VALUES
        (@PedidoId, @RutaId, @TipoEvidencia, N'[private]', @StorageKey, @MimeType, N'Pending',
         NULLIF(LTRIM(RTRIM(@Observaciones)), N''), @RegistradoPorUsuarioId,
         NULLIF(LTRIM(RTRIM(@RegistradoPorNombre)), N''), SYSUTCDATETIME(), @SyncGuid);

    SELECT CONVERT(INT, SCOPE_IDENTITY());
END;
GO

/* Resultado: una fila (RutaId, PedidoId, EvidenciaIdExistente). EvidenciaIdExistente es NULL
   salvo que ese SyncGuid ya tenga una evidencia lista del mismo chofer (reenvío). */
CREATE OR ALTER PROCEDURE dbo.sp_Chofer_PrepararEvidencia
    @RutaPedidoId    INT,
    @ChoferUsuarioId INT,
    @SyncGuid        UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @RutaId INT, @PedidoId INT, @EstadoRuta NVARCHAR(40), @FechaCierre DATETIME2(7), @Existente INT = NULL;

    SELECT @RutaId = rp.RutaId, @PedidoId = rp.PedidoId, @EstadoRuta = r.Estado, @FechaCierre = r.FechaCierre
    FROM dbo.RutaPedidos rp
    INNER JOIN dbo.Rutas r ON r.RutaId = rp.RutaId
    WHERE rp.RutaPedidoId = @RutaPedidoId
      AND r.ChoferUsuarioId = @ChoferUsuarioId;

    IF @RutaId IS NULL
        THROW 52086, N'No se encontró la entrega indicada o no pertenece a este chofer.', 1;

    IF NOT (@EstadoRuta = N'Despachada'
            OR (@EstadoRuta = N'Completada' AND @FechaCierre IS NOT NULL
                AND DATEDIFF(HOUR, @FechaCierre, SYSDATETIME()) <= 48))
        THROW 52087, N'Solo se puede registrar evidencia en una ruta despachada (o cerrada en las últimas 48 horas).', 1;

    IF @SyncGuid IS NOT NULL
    BEGIN
        DECLARE @DuenoGuid INT, @EstadoGuid NVARCHAR(40), @IdGuid INT;
        SELECT @IdGuid = EvidenciaId, @DuenoGuid = RegistradoPorUsuarioId, @EstadoGuid = StorageStatus
        FROM dbo.EntregaEvidencias WHERE SyncGuid = @SyncGuid;

        IF @IdGuid IS NOT NULL
        BEGIN
            IF @DuenoGuid IS NULL OR @DuenoGuid <> @ChoferUsuarioId
                THROW 52088, N'El identificador de sincronización ya fue usado.', 1;

            IF @EstadoGuid = N'Ready'
                SET @Existente = @IdGuid;
            ELSE
                -- Quedó a medias (el proceso murió entre la fila y el archivo): se descarta para que el
                -- reintento la cree completa. El archivo suelto, si lo hubo, no está referenciado por nadie.
                DELETE FROM dbo.EntregaEvidencias
                WHERE EvidenciaId = @IdGuid AND RegistradoPorUsuarioId = @ChoferUsuarioId AND StorageStatus = N'Pending';
        END;
    END;

    SELECT @RutaId AS RutaId, @PedidoId AS PedidoId, @Existente AS EvidenciaIdExistente;
END;
GO

IF XACT_STATE()<>1 THROW 57054,N'La transacción 0305 no está disponible.',1;
DECLARE @MigrationSha256 NVARCHAR(128)=N'$(MigrationSha256)';
IF LEN(@MigrationSha256)<>64 OR @MigrationSha256 LIKE N'%[^0-9A-Fa-f]%' THROW 57055,N'SHA-256 inválido para 0305.',1;
INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes)
VALUES(N'0305_evidencia_entrega_movil',N'0305_evidencia_entrega_movil.sql',UPPER(@MigrationSha256),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),
       N'G3-04: evidencia de entrega desde la app. SyncGuid en EntregaEvidencias, sp_Entrega_RegisterEvidence acepta @SyncGuid opcional y sp_Chofer_PrepararEvidencia valida la parada del chofer.');
COMMIT TRANSACTION;
GO
