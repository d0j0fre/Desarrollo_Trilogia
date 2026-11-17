SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

/* Evidencia de entrega guardada en la base de datos.

   La foto y la firma de la entrega se guardaban en una carpeta del servidor
   (EvidenceStorage:RootPath) que el sitio web y la API móvil debían compartir. En Azure
   son dos App Service separados y no comparten disco: la API rechazaba la subida y el
   sitio no habría visto las fotos de la app.

   Esta migración agrega dbo.EvidenciaArchivos, donde se guarda el contenido de cada
   imagen (máximo 5 MB, lo mismo que antes). Se usa cuando EvidenceStorage:Provider es
   «Database»; sin esa configuración todo sigue en la carpeta como hasta ahora.

   El ciclo es el mismo de la carpeta:
     1) sp_EvidenciaArchivo_Preparar: guarda la imagen sin confirmar (staging) y, de paso,
        borra lo que quedó sin confirmar hace más de un día;
     2) sp_EvidenciaArchivo_Confirmar: la marca como definitiva una vez registrada la
        evidencia (sp_Entrega_RegisterEvidence);
     3) sp_EvidenciaArchivo_Descartar: borra la preparada si el registro falló;
     4) sp_EvidenciaArchivo_Obtener: devuelve el contenido solo si está confirmada.

   Aditiva y sin datos nuevos. Rollback en 0306_evidencia_archivos_en_base.rollback.md. */
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL THROW 57100,N'Falta el ledger 0001.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0306_evidencia_archivos_en_base' AND Status=N'Applied')
    THROW 57101,N'0306 ya figura aplicada.',1;
IF OBJECT_ID(N'dbo.EntregaEvidencias',N'U') IS NULL
    THROW 57102,N'Falta la evidencia de entrega (0004).',1;

BEGIN TRANSACTION;
GO

IF OBJECT_ID(N'dbo.EvidenciaArchivos', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.EvidenciaArchivos
    (
        StorageKey     NVARCHAR(40)    NOT NULL CONSTRAINT PK_EvidenciaArchivos PRIMARY KEY,
        TemporaryKey   NVARCHAR(40)    NOT NULL,
        ContentType    NVARCHAR(30)    NOT NULL,
        TamanoBytes    BIGINT          NOT NULL,
        Contenido      VARBINARY(MAX)  NOT NULL,
        Confirmado     BIT             NOT NULL CONSTRAINT DF_EvidenciaArchivos_Confirmado DEFAULT (0),
        CreadoUtc      DATETIME2(0)    NOT NULL CONSTRAINT DF_EvidenciaArchivos_CreadoUtc DEFAULT (SYSUTCDATETIME()),
        ConfirmadoUtc  DATETIME2(0)    NULL,
        CONSTRAINT UQ_EvidenciaArchivos_TemporaryKey UNIQUE (TemporaryKey),
        CONSTRAINT CK_EvidenciaArchivos_StorageKey CHECK (StorageKey LIKE N'%.jpg' OR StorageKey LIKE N'%.png' OR StorageKey LIKE N'%.webp'),
        CONSTRAINT CK_EvidenciaArchivos_ContentType CHECK (ContentType IN (N'image/jpeg', N'image/png', N'image/webp')),
        CONSTRAINT CK_EvidenciaArchivos_Tamano CHECK (TamanoBytes > 0 AND TamanoBytes <= 5242880)
    );
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_EvidenciaArchivo_Preparar
    @StorageKey   NVARCHAR(40),
    @TemporaryKey NVARCHAR(40),
    @ContentType  NVARCHAR(30),
    @Contenido    VARBINARY(MAX)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @Contenido IS NULL OR DATALENGTH(@Contenido) = 0
        THROW 57103, N'La imagen está vacía.', 1;
    IF DATALENGTH(@Contenido) > 5242880
        THROW 57104, N'La imagen supera el tamaño máximo permitido de 5 MB.', 1;

    -- Lo que quedó preparado y nunca se confirmó (un proceso que murió a medias) no se acumula.
    DELETE FROM dbo.EvidenciaArchivos
    WHERE Confirmado = 0 AND CreadoUtc < DATEADD(DAY, -1, SYSUTCDATETIME());

    INSERT dbo.EvidenciaArchivos (StorageKey, TemporaryKey, ContentType, TamanoBytes, Contenido)
    VALUES (@StorageKey, @TemporaryKey, @ContentType, DATALENGTH(@Contenido), @Contenido);
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_EvidenciaArchivo_Confirmar
    @TemporaryKey NVARCHAR(40),
    @StorageKey   NVARCHAR(40)
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.EvidenciaArchivos
    SET Confirmado = 1, ConfirmadoUtc = SYSUTCDATETIME()
    WHERE TemporaryKey = @TemporaryKey AND StorageKey = @StorageKey AND Confirmado = 0;

    IF @@ROWCOUNT = 0
        THROW 57105, N'No se encontró la imagen preparada de la evidencia.', 1;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_EvidenciaArchivo_Descartar
    @TemporaryKey NVARCHAR(40)
AS
BEGIN
    SET NOCOUNT ON;

    DELETE FROM dbo.EvidenciaArchivos
    WHERE TemporaryKey = @TemporaryKey AND Confirmado = 0;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_EvidenciaArchivo_Obtener
    @StorageKey NVARCHAR(40)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT ContentType, Contenido
    FROM dbo.EvidenciaArchivos
    WHERE StorageKey = @StorageKey AND Confirmado = 1;
END;
GO

IF XACT_STATE()<>1 THROW 57106,N'La transacción 0306 no está disponible.',1;
DECLARE @MigrationSha256 NVARCHAR(128)=N'$(MigrationSha256)';
IF LEN(@MigrationSha256)<>64 OR @MigrationSha256 LIKE N'%[^0-9A-Fa-f]%' THROW 57107,N'SHA-256 inválido para 0306.',1;
INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes)
VALUES(N'0306_evidencia_archivos_en_base',N'0306_evidencia_archivos_en_base.sql',UPPER(@MigrationSha256),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),
       N'Evidencia de entrega en la base (EvidenciaArchivos y sp_EvidenciaArchivo_*) para que el sitio y la API la compartan sin disco común. Se usa con EvidenceStorage:Provider=Database.');
COMMIT TRANSACTION;
GO
