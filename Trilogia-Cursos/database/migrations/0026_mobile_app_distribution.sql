SET NOCOUNT ON;
SET XACT_ABORT ON;

/* Obligatorios y explicitos, no heredados de la herramienta.
   Un indice filtrado (WHERE ... IS NOT NULL) exige ambos en ON al crearse.
   SSMS los trae encendidos, pero sqlcmd trae QUOTED_IDENTIFIER apagado: sin
   esta linea, la misma migracion se aplica o falla segun con que se ejecute,
   y eso no puede depender de la herramienta que use el ejecutor. */
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

/* Fase 7 de la aplicación móvil: distribución por APK.

   Sin tienda de aplicaciones no hay actualización automática, así que el
   catálogo de versiones vive en la base y no en un archivo de configuración:
   publicar una versión nueva no debe requerir un despliegue del sistema.

   Aditiva: crea una tabla nueva, cuatro procedimientos y un permiso. No toca
   nada existente. */
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL THROW 55000,N'Falta el ledger 0001.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0026_mobile_app_distribution' AND Status=N'Applied')
    THROW 55001,N'0026 ya figura aplicada.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0025_mobile_driver_surface' AND Status=N'Applied')
    THROW 55002,N'0026 depende de 0025. Aplicar 0025 primero.',1;
IF OBJECT_ID(N'dbo.Permisos',N'U') IS NULL OR OBJECT_ID(N'dbo.Perfiles',N'U') IS NULL OR OBJECT_ID(N'dbo.PerfilPermisos',N'U') IS NULL
    THROW 55003,N'Faltan tablas de permisos.',1;

BEGIN TRANSACTION;
GO

/* ─────────────────────────────────────────────────────────────────────────
   Catálogo de versiones publicadas del APK.

   BuildNumero es el versionCode de Android: creciente y sin repetirse nunca.
   MinBuildSoportado es la compuerta: por debajo de ese número la aplicación se
   bloquea y obliga a descargar. Es la única forma de retirar del campo una
   versión con un error grave cuando la distribución es por archivo.

   Sha256 se publica junto al enlace para que quien instala pueda verificar que
   el archivo es el que el equipo publicó.
   ───────────────────────────────────────────────────────────────────────── */
IF OBJECT_ID(N'dbo.AppMovilVersiones',N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AppMovilVersiones
    (
        VersionId             INT IDENTITY(1,1) NOT NULL,
        VersionNombre         NVARCHAR(20)   NOT NULL,
        BuildNumero           INT            NOT NULL,
        MinBuildSoportado     INT            NOT NULL,
        UrlDescarga           NVARCHAR(500)  NOT NULL,
        Sha256                CHAR(64)       NOT NULL,
        TamanoBytes           BIGINT         NULL,
        Notas                 NVARCHAR(500)  NULL,
        MensajeObligatorio    NVARCHAR(300)  NULL,
        Publicada             BIT            NOT NULL CONSTRAINT DF_AppMovilVersiones_Publicada DEFAULT (1),
        FechaPublicacion      DATETIME2(3)   NOT NULL CONSTRAINT DF_AppMovilVersiones_Fecha DEFAULT (SYSUTCDATETIME()),
        PublicadaPorUsuarioId INT            NULL,
        PublicadaPorNombre    NVARCHAR(150)  NULL,
        CONSTRAINT PK_AppMovilVersiones PRIMARY KEY CLUSTERED (VersionId),
        CONSTRAINT UQ_AppMovilVersiones_Build UNIQUE (BuildNumero),
        CONSTRAINT CK_AppMovilVersiones_Build CHECK (BuildNumero > 0),
        CONSTRAINT CK_AppMovilVersiones_MinBuild CHECK (MinBuildSoportado > 0 AND MinBuildSoportado <= BuildNumero),
        CONSTRAINT CK_AppMovilVersiones_Url CHECK (UrlDescarga LIKE N'https://%')
    );
END;
GO

IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name=N'IX_AppMovilVersiones_Publicada' AND object_id=OBJECT_ID(N'dbo.AppMovilVersiones'))
    CREATE INDEX IX_AppMovilVersiones_Publicada ON dbo.AppMovilVersiones (Publicada, BuildNumero DESC);
GO

/* Permiso para publicar versiones. Consultar la página de descarga no requiere
   permiso propio: basta con tener sesión y un perfil de la empresa. */
UPDATE dbo.Permisos
SET Modulo=N'Movil', Nombre=N'Publicar versiones de la aplicación móvil',
    Descripcion=N'Permite registrar y retirar versiones del APK distribuido por QR.', Activo=1
WHERE Codigo=N'MOVIL_APP_PUBLICAR';
IF @@ROWCOUNT=0
    INSERT dbo.Permisos(Codigo,Modulo,Nombre,Descripcion,Activo)
    VALUES(N'MOVIL_APP_PUBLICAR',N'Movil',N'Publicar versiones de la aplicación móvil',N'Permite registrar y retirar versiones del APK distribuido por QR.',1);

INSERT dbo.PerfilPermisos(PerfilId,PermisoId,UsuarioAsignacionId,UsuarioAsignacionNombre)
SELECT profile.PerfilId,permission.PermisoId,NULL,N'Migración 0026 distribución móvil'
FROM dbo.Perfiles profile CROSS JOIN dbo.Permisos permission
WHERE profile.Nombre IN(N'Administrador')
  AND permission.Codigo=N'MOVIL_APP_PUBLICAR'
  AND NOT EXISTS(SELECT 1 FROM dbo.PerfilPermisos assigned WHERE assigned.PerfilId=profile.PerfilId AND assigned.PermisoId=permission.PermisoId);
GO

/* Versión vigente: la publicada con el build más alto. Es lo que consulta la
   aplicación al arrancar y lo que muestra la página del QR. */
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
        v.FechaPublicacion
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
        v.PublicadaPorNombre
    FROM dbo.AppMovilVersiones v
    ORDER BY v.BuildNumero DESC;
END;
GO

/* ─────────────────────────────────────────────────────────────────────────
   Publicar una versión.

   Rechaza un build que no supere al ya publicado: una numeración que retrocede
   rompe la compuerta de versión mínima y, más adelante, impide subir la
   aplicación a Google Play.
   ───────────────────────────────────────────────────────────────────────── */
CREATE OR ALTER PROCEDURE dbo.sp_AppMovil_Publicar
    @VersionNombre      NVARCHAR(20),
    @BuildNumero        INT,
    @MinBuildSoportado  INT,
    @UrlDescarga        NVARCHAR(500),
    @Sha256             CHAR(64),
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
        THROW 55010, N'El SHA-256 del archivo no es válido.', 1;
    IF @UrlDescarga IS NULL OR @UrlDescarga NOT LIKE N'https://%'
        THROW 55011, N'La dirección de descarga debe ser HTTPS.', 1;
    IF @BuildNumero IS NULL OR @BuildNumero <= 0
        THROW 55012, N'El número de compilación no es válido.', 1;
    IF @MinBuildSoportado IS NULL OR @MinBuildSoportado <= 0 OR @MinBuildSoportado > @BuildNumero
        THROW 55013, N'La versión mínima soportada no puede ser mayor que la que se publica.', 1;

    BEGIN TRANSACTION;

    DECLARE @BuildMaximo INT =
        (SELECT ISNULL(MAX(BuildNumero), 0) FROM dbo.AppMovilVersiones WITH (UPDLOCK, HOLDLOCK));

    IF @BuildNumero <= @BuildMaximo
    BEGIN
    /* Sin ROLLBACK explicito: con XACT_ABORT ON, THROW revierte la
       transaccion igual. El ROLLBACK explicito ademas rompe si alguien
       invoca este procedimiento desde un INSERT ... EXEC (error 3915). */
        THROW 55014, N'El número de compilación debe ser mayor que el de la última versión publicada.', 1;
    END;

    INSERT dbo.AppMovilVersiones
        (VersionNombre, BuildNumero, MinBuildSoportado, UrlDescarga, Sha256,
         TamanoBytes, Notas, MensajeObligatorio, Publicada, PublicadaPorUsuarioId, PublicadaPorNombre)
    VALUES
        (LTRIM(RTRIM(@VersionNombre)), @BuildNumero, @MinBuildSoportado, LTRIM(RTRIM(@UrlDescarga)), UPPER(@Sha256),
         @TamanoBytes, NULLIF(LTRIM(RTRIM(@Notas)), N''), NULLIF(LTRIM(RTRIM(@MensajeObligatorio)), N''),
         1, @UsuarioId, @UsuarioNombre);

    DECLARE @NuevaId INT = CAST(SCOPE_IDENTITY() AS INT);

    COMMIT TRANSACTION;

    SELECT @NuevaId AS VersionId;
END;
GO

/* Retira una versión de circulación sin borrar su historial. */
CREATE OR ALTER PROCEDURE dbo.sp_AppMovil_Despublicar
    @VersionId INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    UPDATE dbo.AppMovilVersiones
    SET Publicada = 0
    WHERE VersionId = @VersionId;

    SELECT @@ROWCOUNT AS Afectadas;
END;
GO

IF XACT_STATE()<>1 THROW 55004,N'La transacción 0026 no está disponible.',1;
DECLARE @MigrationSha256 NVARCHAR(128)=N'$(MigrationSha256)';
IF LEN(@MigrationSha256)<>64 OR @MigrationSha256 LIKE N'%[^0-9A-Fa-f]%' THROW 55005,N'SHA-256 inválido para 0026.',1;
INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes)
VALUES(N'0026_mobile_app_distribution',N'0026_mobile_app_distribution.sql',UPPER(@MigrationSha256),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),N'Fase 7 app movil: catalogo de versiones del APK, compuerta de version minima y permiso MOVIL_APP_PUBLICAR. Aditiva.');
COMMIT TRANSACTION;
GO
