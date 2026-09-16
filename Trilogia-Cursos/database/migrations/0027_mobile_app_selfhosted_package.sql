SET NOCOUNT ON;
SET XACT_ABORT ON;

/* Obligatorios y explicitos, no heredados de la herramienta. */
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

/* La aplicacion se guarda en el propio sistema.

   La 0026 obligaba a subir el APK a algun lugar externo y pegar la direccion a
   mano, junto con su huella y su tamaño. Eso convertia la publicacion en un
   tramite tecnico y dejaba tres datos que se podian escribir mal.

   Ahora el archivo se sube al sistema, que calcula la huella y el tamaño solo.
   La direccion externa se conserva como alternativa opcional, por si algun dia
   conviene servirlo desde otro lado.

   Aditiva: agrega una columna anulable y relaja una restriccion. Ninguna fila
   existente deja de ser valida. */
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL THROW 55100,N'Falta el ledger 0001.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0027_mobile_app_selfhosted_package' AND Status=N'Applied')
    THROW 55101,N'0027 ya figura aplicada.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0026_mobile_app_distribution' AND Status=N'Applied')
    THROW 55102,N'0027 depende de 0026. Aplicar 0026 primero.',1;
IF OBJECT_ID(N'dbo.AppMovilVersiones',N'U') IS NULL
    THROW 55103,N'Falta dbo.AppMovilVersiones (migración 0026).',1;

BEGIN TRANSACTION;
GO

/* Identificador del archivo dentro del almacen privado del sistema. */
IF COL_LENGTH(N'dbo.AppMovilVersiones', N'ArchivoAlmacenado') IS NULL
    ALTER TABLE dbo.AppMovilVersiones ADD ArchivoAlmacenado NVARCHAR(120) NULL;
GO

IF COL_LENGTH(N'dbo.AppMovilVersiones', N'ArchivoNombre') IS NULL
    ALTER TABLE dbo.AppMovilVersiones ADD ArchivoNombre NVARCHAR(200) NULL;
GO

/* La direccion externa pasa a ser opcional: ahora hay dos formas validas de
   entregar el archivo, y al menos una tiene que existir. */
IF EXISTS(SELECT 1 FROM sys.check_constraints WHERE name = N'CK_AppMovilVersiones_Url')
    ALTER TABLE dbo.AppMovilVersiones DROP CONSTRAINT CK_AppMovilVersiones_Url;
GO

IF EXISTS(SELECT 1 FROM sys.columns
          WHERE object_id = OBJECT_ID(N'dbo.AppMovilVersiones')
            AND name = N'UrlDescarga' AND is_nullable = 0)
    ALTER TABLE dbo.AppMovilVersiones ALTER COLUMN UrlDescarga NVARCHAR(500) NULL;
GO

IF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE name = N'CK_AppMovilVersiones_Origen')
    ALTER TABLE dbo.AppMovilVersiones ADD CONSTRAINT CK_AppMovilVersiones_Origen
        CHECK (
            ArchivoAlmacenado IS NOT NULL
            OR (UrlDescarga IS NOT NULL AND UrlDescarga LIKE N'https://%')
        );
GO

/* Version vigente, ahora con el origen del archivo. */
CREATE OR ALTER PROCEDURE dbo.sp_AppMovil_GetVigente
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP (1)
        v.VersionId,
        v.VersionNombre,
        v.BuildNumero,
        v.MinBuildSoportado,
        v.UrlDescarga,
        v.Sha256,
        v.TamanoBytes,
        v.Notas,
        v.MensajeObligatorio,
        v.FechaPublicacion,
        v.ArchivoAlmacenado,
        v.ArchivoNombre
    FROM dbo.AppMovilVersiones v
    WHERE v.Publicada = 1
    ORDER BY v.BuildNumero DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_AppMovil_List
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        v.VersionId,
        v.VersionNombre,
        v.BuildNumero,
        v.MinBuildSoportado,
        v.UrlDescarga,
        v.Sha256,
        v.TamanoBytes,
        v.Notas,
        v.Publicada,
        v.FechaPublicacion,
        v.PublicadaPorNombre,
        v.ArchivoAlmacenado,
        v.ArchivoNombre
    FROM dbo.AppMovilVersiones v
    ORDER BY v.BuildNumero DESC;
END;
GO

/* ─────────────────────────────────────────────────────────────────────────
   Publicar. @UrlDescarga y @ArchivoAlmacenado son alternativos: uno de los dos
   tiene que venir. La huella y el tamaño los calcula el sistema al recibir el
   archivo, no se escriben a mano.
   ───────────────────────────────────────────────────────────────────────── */
CREATE OR ALTER PROCEDURE dbo.sp_AppMovil_Publicar
    @VersionNombre      NVARCHAR(20),
    @BuildNumero        INT,
    @MinBuildSoportado  INT,
    @Sha256             CHAR(64),
    @UrlDescarga        NVARCHAR(500)  = NULL,
    @ArchivoAlmacenado  NVARCHAR(120)  = NULL,
    @ArchivoNombre      NVARCHAR(200)  = NULL,
    @TamanoBytes        BIGINT         = NULL,
    @Notas              NVARCHAR(500)  = NULL,
    @MensajeObligatorio NVARCHAR(300)  = NULL,
    @UsuarioId          INT            = NULL,
    @UsuarioNombre      NVARCHAR(150)  = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @Sha256 IS NULL OR LEN(@Sha256) <> 64 OR @Sha256 LIKE N'%[^0-9A-Fa-f]%'
        THROW 55110, N'El SHA-256 del archivo no es válido.', 1;

    IF @ArchivoAlmacenado IS NULL AND (@UrlDescarga IS NULL OR @UrlDescarga NOT LIKE N'https://%')
        THROW 55111, N'Hay que subir el archivo o indicar una dirección HTTPS.', 1;

    IF @BuildNumero IS NULL OR @BuildNumero <= 0
        THROW 55112, N'El número de compilación no es válido.', 1;

    IF @MinBuildSoportado IS NULL OR @MinBuildSoportado <= 0 OR @MinBuildSoportado > @BuildNumero
        THROW 55113, N'La versión mínima soportada no puede ser mayor que la que se publica.', 1;

    BEGIN TRANSACTION;

    DECLARE @BuildMaximo INT =
        (SELECT ISNULL(MAX(BuildNumero), 0) FROM dbo.AppMovilVersiones WITH (UPDLOCK, HOLDLOCK));

    IF @BuildNumero <= @BuildMaximo
    BEGIN
        /* Sin ROLLBACK explicito: con XACT_ABORT ON, THROW revierte igual, y el
           ROLLBACK rompe si alguien envuelve este procedimiento. */
        THROW 55114, N'El número de compilación debe ser mayor que el de la última versión publicada.', 1;
    END;

    INSERT dbo.AppMovilVersiones
        (VersionNombre, BuildNumero, MinBuildSoportado, UrlDescarga, Sha256,
         TamanoBytes, Notas, MensajeObligatorio, Publicada,
         PublicadaPorUsuarioId, PublicadaPorNombre, ArchivoAlmacenado, ArchivoNombre)
    VALUES
        (LTRIM(RTRIM(@VersionNombre)), @BuildNumero, @MinBuildSoportado,
         NULLIF(LTRIM(RTRIM(@UrlDescarga)), N''), UPPER(@Sha256),
         @TamanoBytes, NULLIF(LTRIM(RTRIM(@Notas)), N''),
         NULLIF(LTRIM(RTRIM(@MensajeObligatorio)), N''),
         1, @UsuarioId, @UsuarioNombre,
         NULLIF(LTRIM(RTRIM(@ArchivoAlmacenado)), N''),
         NULLIF(LTRIM(RTRIM(@ArchivoNombre)), N''));

    DECLARE @NuevaId INT = CAST(SCOPE_IDENTITY() AS INT);

    COMMIT TRANSACTION;

    SELECT @NuevaId AS VersionId;
END;
GO

/* Datos del archivo de una version, para poder entregarlo o borrarlo. */
CREATE OR ALTER PROCEDURE dbo.sp_AppMovil_GetArchivo
    @VersionId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP (1)
        v.VersionId,
        v.ArchivoAlmacenado,
        v.ArchivoNombre,
        v.VersionNombre,
        v.BuildNumero,
        v.Publicada
    FROM dbo.AppMovilVersiones v
    WHERE v.VersionId = @VersionId;
END;
GO

IF XACT_STATE()<>1 THROW 55104,N'La transacción 0027 no está disponible.',1;
DECLARE @MigrationSha256 NVARCHAR(128)=N'$(MigrationSha256)';
IF LEN(@MigrationSha256)<>64 OR @MigrationSha256 LIKE N'%[^0-9A-Fa-f]%' THROW 55105,N'SHA-256 inválido para 0027.',1;
INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes)
VALUES(N'0027_mobile_app_selfhosted_package',N'0027_mobile_app_selfhosted_package.sql',UPPER(@MigrationSha256),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),N'El APK se guarda en el propio sistema: huella y tamaño calculados al subirlo. La direccion externa queda opcional. Aditiva.');
COMMIT TRANSACTION;
GO
