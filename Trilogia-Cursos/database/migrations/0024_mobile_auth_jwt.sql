SET NOCOUNT ON;
SET XACT_ABORT ON;

/* Obligatorios y explicitos, no heredados de la herramienta.
   Un indice filtrado (WHERE ... IS NOT NULL) exige ambos en ON al crearse.
   SSMS los trae encendidos, pero sqlcmd trae QUOTED_IDENTIFIER apagado: sin
   esta linea, la misma migracion se aplica o falla segun con que se ejecute,
   y eso no puede depender de la herramienta que use el ejecutor. */
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

/* Fase 1 de la aplicación móvil: autorización JWT en Proyecto_FinalAPI.
   Agrega la familia de tokens de refresco rotativos y revocables, más la
   consulta de permisos efectivos por perfil que alimenta los claims.
   Es aditiva: no altera Usuarios, Perfiles, Permisos ni PerfilPermisos, y no
   modifica ningún procedimiento existente de autenticación. */
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL THROW 54900,N'Falta el ledger 0001.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0024_mobile_auth_jwt' AND Status=N'Applied')
    THROW 54901,N'0024 ya figura aplicada.',1;
IF OBJECT_ID(N'dbo.Usuarios',N'U') IS NULL OR OBJECT_ID(N'dbo.Perfiles',N'U') IS NULL
   OR OBJECT_ID(N'dbo.Permisos',N'U') IS NULL OR OBJECT_ID(N'dbo.PerfilPermisos',N'U') IS NULL
    THROW 54902,N'Faltan dependencias: dbo.Usuarios, dbo.Perfiles, dbo.Permisos o dbo.PerfilPermisos.',1;

BEGIN TRANSACTION;
GO

/* ─────────────────────────────────────────────────────────────────────────
   Tabla de tokens de refresco.
   Nunca se guarda el token en claro: solo su SHA-256 en hexadecimal. Quien
   lea la tabla no puede suplantar a nadie.
   CadenaId agrupa la familia de tokens de un mismo dispositivo: al rotar, el
   token nuevo hereda la cadena. Si llega un token ya usado, se revoca la
   cadena completa, que es la señal clásica de robo.
   ───────────────────────────────────────────────────────────────────────── */
IF OBJECT_ID(N'dbo.UsuarioTokensRefresco',N'U') IS NULL
BEGIN
    CREATE TABLE dbo.UsuarioTokensRefresco
    (
        TokenId           BIGINT IDENTITY(1,1) NOT NULL,
        UsuarioId         INT              NOT NULL,
        TokenHash         CHAR(64)         NOT NULL,
        CadenaId          UNIQUEIDENTIFIER NOT NULL,
        DispositivoId     NVARCHAR(100)    NULL,
        DispositivoNombre NVARCHAR(150)    NULL,
        CreadoUtc         DATETIME2(3)     NOT NULL CONSTRAINT DF_UsuarioTokensRefresco_CreadoUtc DEFAULT (SYSUTCDATETIME()),
        ExpiraUtc         DATETIME2(3)     NOT NULL,
        UsadoUtc          DATETIME2(3)     NULL,
        RevocadoUtc       DATETIME2(3)     NULL,
        MotivoRevocacion  NVARCHAR(200)    NULL,
        IpOrigen          NVARCHAR(45)     NULL,
        CONSTRAINT PK_UsuarioTokensRefresco PRIMARY KEY CLUSTERED (TokenId),
        CONSTRAINT UQ_UsuarioTokensRefresco_TokenHash UNIQUE (TokenHash),
        CONSTRAINT FK_UsuarioTokensRefresco_Usuarios FOREIGN KEY (UsuarioId)
            REFERENCES dbo.Usuarios (UsuarioId)
    );
END;
GO

IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name=N'IX_UsuarioTokensRefresco_Cadena' AND object_id=OBJECT_ID(N'dbo.UsuarioTokensRefresco'))
    CREATE INDEX IX_UsuarioTokensRefresco_Cadena ON dbo.UsuarioTokensRefresco (CadenaId) INCLUDE (RevocadoUtc);
GO

IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name=N'IX_UsuarioTokensRefresco_Usuario' AND object_id=OBJECT_ID(N'dbo.UsuarioTokensRefresco'))
    CREATE INDEX IX_UsuarioTokensRefresco_Usuario ON dbo.UsuarioTokensRefresco (UsuarioId, ExpiraUtc) INCLUDE (RevocadoUtc, UsadoUtc);
GO

/* ─────────────────────────────────────────────────────────────────────────
   Permisos efectivos de un perfil. Alimenta el claim `perm` del token, que
   sirve para que la aplicación decida qué dibujar. La autorización real se
   revalida por request contra sp_Admin_HasPermissionByCode.
   ───────────────────────────────────────────────────────────────────────── */
CREATE OR ALTER PROCEDURE dbo.sp_Auth_GetPermisosPorPerfil
    @NombreRol NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    SET @NombreRol = NULLIF(LTRIM(RTRIM(ISNULL(@NombreRol, N''))), N'');
    IF @NombreRol IS NULL
    BEGIN
        SELECT CAST(NULL AS NVARCHAR(100)) AS Codigo WHERE 1 = 0;
        RETURN;
    END;

    SELECT DISTINCT pe.Codigo
    FROM dbo.Perfiles p
    INNER JOIN dbo.PerfilPermisos pp ON pp.PerfilId = p.PerfilId
    INNER JOIN dbo.Permisos pe ON pe.PermisoId = pp.PermisoId
    WHERE p.Nombre = @NombreRol
      AND ISNULL(p.Activo, 1) = 1
      AND pe.Activo = 1
      AND pe.Codigo IS NOT NULL
    ORDER BY pe.Codigo;
END;
GO

/* ─────────────────────────────────────────────────────────────────────────
   Usuario activo por identificador. Se usa al refrescar: un token válido de
   un usuario desactivado no debe producir un access token nuevo, y el rol
   pudo cambiar desde el login.
   ───────────────────────────────────────────────────────────────────────── */
CREATE OR ALTER PROCEDURE dbo.sp_Auth_GetUsuarioActivo
    @UsuarioId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP (1)
        u.UsuarioId,
        u.NombreCompleto,
        u.Correo,
        pf.Nombre AS PerfilNombre,
        CAST(ISNULL(u.Activo, 0) AS BIT) AS Activo
    FROM dbo.Usuarios u
    INNER JOIN dbo.Perfiles pf ON pf.PerfilId = u.PerfilId
    WHERE u.UsuarioId = @UsuarioId
      AND ISNULL(u.Activo, 0) = 1;
END;
GO

/* ─────────────────────────────────────────────────────────────────────────
   Emite un token de refresco. @CadenaId nulo abre una cadena nueva (login);
   con valor, continúa la cadena de un dispositivo ya conocido.
   ───────────────────────────────────────────────────────────────────────── */
CREATE OR ALTER PROCEDURE dbo.sp_Auth_EmitirTokenRefresco
    @UsuarioId         INT,
    @TokenHash         CHAR(64),
    @ExpiraUtc         DATETIME2(3),
    @CadenaId          UNIQUEIDENTIFIER = NULL,
    @DispositivoId     NVARCHAR(100)    = NULL,
    @DispositivoNombre NVARCHAR(150)    = NULL,
    @IpOrigen          NVARCHAR(45)     = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @TokenHash IS NULL OR LEN(@TokenHash) <> 64 OR @TokenHash LIKE N'%[^0-9A-Fa-f]%'
        THROW 54920, N'El hash del token de refresco no es un SHA-256 hexadecimal válido.', 1;

    IF @ExpiraUtc IS NULL OR @ExpiraUtc <= SYSUTCDATETIME()
        THROW 54921, N'La expiración del token de refresco debe ser futura.', 1;

    IF NOT EXISTS(SELECT 1 FROM dbo.Usuarios WHERE UsuarioId = @UsuarioId AND ISNULL(Activo, 0) = 1)
        THROW 54922, N'El usuario no existe o está inactivo.', 1;

    IF @CadenaId IS NULL SET @CadenaId = NEWID();

    INSERT dbo.UsuarioTokensRefresco
        (UsuarioId, TokenHash, CadenaId, DispositivoId, DispositivoNombre, ExpiraUtc, IpOrigen)
    VALUES
        (@UsuarioId, UPPER(@TokenHash), @CadenaId, @DispositivoId, @DispositivoNombre, @ExpiraUtc, @IpOrigen);

    SELECT SCOPE_IDENTITY() AS TokenId, @CadenaId AS CadenaId;
END;
GO

/* ─────────────────────────────────────────────────────────────────────────
   Canjea un token de refresco por otro, de forma atómica.

   Resultado (una fila):
     Resultado   = 'Ok' | 'Reutilizado' | 'Invalido'
     UsuarioId   = dueño del token cuando el canje procede
     CadenaId    = cadena continuada

   'Reutilizado' significa que llegó un token que ya se había canjeado. Se
   revoca la cadena entera y el cliente queda obligado a reautenticarse.
   ───────────────────────────────────────────────────────────────────────── */
CREATE OR ALTER PROCEDURE dbo.sp_Auth_CanjearTokenRefresco
    @TokenHash      CHAR(64),
    @NuevoTokenHash CHAR(64),
    @NuevaExpiraUtc DATETIME2(3),
    @IpOrigen       NVARCHAR(45) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @TokenHash IS NULL OR LEN(@TokenHash) <> 64 OR @TokenHash LIKE N'%[^0-9A-Fa-f]%'
        THROW 54923, N'El hash del token presentado no es válido.', 1;

    IF @NuevoTokenHash IS NULL OR LEN(@NuevoTokenHash) <> 64 OR @NuevoTokenHash LIKE N'%[^0-9A-Fa-f]%'
        THROW 54924, N'El hash del token nuevo no es válido.', 1;

    IF @NuevaExpiraUtc IS NULL OR @NuevaExpiraUtc <= SYSUTCDATETIME()
        THROW 54925, N'La expiración del token nuevo debe ser futura.', 1;

    DECLARE @Ahora DATETIME2(3) = SYSUTCDATETIME();
    DECLARE @TokenId BIGINT, @UsuarioId INT, @CadenaId UNIQUEIDENTIFIER;
    DECLARE @UsadoUtc DATETIME2(3), @RevocadoUtc DATETIME2(3), @ExpiraUtc DATETIME2(3);
    DECLARE @DispositivoId NVARCHAR(100), @DispositivoNombre NVARCHAR(150);

    BEGIN TRANSACTION;

    SELECT TOP (1)
        @TokenId = t.TokenId,
        @UsuarioId = t.UsuarioId,
        @CadenaId = t.CadenaId,
        @UsadoUtc = t.UsadoUtc,
        @RevocadoUtc = t.RevocadoUtc,
        @ExpiraUtc = t.ExpiraUtc,
        @DispositivoId = t.DispositivoId,
        @DispositivoNombre = t.DispositivoNombre
    FROM dbo.UsuarioTokensRefresco AS t WITH (UPDLOCK, HOLDLOCK)
    WHERE t.TokenHash = UPPER(@TokenHash);

    IF @TokenId IS NULL
    BEGIN
        COMMIT TRANSACTION;
        SELECT N'Invalido' AS Resultado, CAST(NULL AS INT) AS UsuarioId, CAST(NULL AS UNIQUEIDENTIFIER) AS CadenaId;
        RETURN;
    END;

    /* Token ya canjeado: alguien más lo tenía. Se corta toda la cadena. */
    IF @UsadoUtc IS NOT NULL
    BEGIN
        UPDATE dbo.UsuarioTokensRefresco
        SET RevocadoUtc = @Ahora,
            MotivoRevocacion = N'Reutilización de token de refresco detectada.'
        WHERE CadenaId = @CadenaId
          AND RevocadoUtc IS NULL;

        COMMIT TRANSACTION;
        SELECT N'Reutilizado' AS Resultado, @UsuarioId AS UsuarioId, @CadenaId AS CadenaId;
        RETURN;
    END;

    IF @RevocadoUtc IS NOT NULL OR @ExpiraUtc <= @Ahora
    BEGIN
        COMMIT TRANSACTION;
        SELECT N'Invalido' AS Resultado, CAST(NULL AS INT) AS UsuarioId, CAST(NULL AS UNIQUEIDENTIFIER) AS CadenaId;
        RETURN;
    END;

    IF NOT EXISTS(SELECT 1 FROM dbo.Usuarios WHERE UsuarioId = @UsuarioId AND ISNULL(Activo, 0) = 1)
    BEGIN
        UPDATE dbo.UsuarioTokensRefresco
        SET RevocadoUtc = @Ahora,
            MotivoRevocacion = N'Usuario inactivo al momento del canje.'
        WHERE CadenaId = @CadenaId
          AND RevocadoUtc IS NULL;

        COMMIT TRANSACTION;
        SELECT N'Invalido' AS Resultado, CAST(NULL AS INT) AS UsuarioId, CAST(NULL AS UNIQUEIDENTIFIER) AS CadenaId;
        RETURN;
    END;

    UPDATE dbo.UsuarioTokensRefresco
    SET UsadoUtc = @Ahora
    WHERE TokenId = @TokenId;

    INSERT dbo.UsuarioTokensRefresco
        (UsuarioId, TokenHash, CadenaId, DispositivoId, DispositivoNombre, ExpiraUtc, IpOrigen)
    VALUES
        (@UsuarioId, UPPER(@NuevoTokenHash), @CadenaId, @DispositivoId, @DispositivoNombre, @NuevaExpiraUtc, @IpOrigen);

    COMMIT TRANSACTION;

    SELECT N'Ok' AS Resultado, @UsuarioId AS UsuarioId, @CadenaId AS CadenaId;
END;
GO

/* Cierre de sesión: revoca la cadena a la que pertenece el token presentado.
   Es idempotente y no revela si el token existía. */
CREATE OR ALTER PROCEDURE dbo.sp_Auth_RevocarCadenaTokenRefresco
    @TokenHash CHAR(64),
    @Motivo    NVARCHAR(200) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @TokenHash IS NULL OR LEN(@TokenHash) <> 64 OR @TokenHash LIKE N'%[^0-9A-Fa-f]%'
        RETURN;

    UPDATE t
    SET RevocadoUtc = SYSUTCDATETIME(),
        MotivoRevocacion = ISNULL(@Motivo, N'Cierre de sesión.')
    FROM dbo.UsuarioTokensRefresco AS t
    INNER JOIN dbo.UsuarioTokensRefresco AS origen ON origen.CadenaId = t.CadenaId
    WHERE origen.TokenHash = UPPER(@TokenHash)
      AND t.RevocadoUtc IS NULL;
END;
GO

/* Revoca todas las sesiones móviles de un usuario. Lo usa administración
   cuando se pierde un teléfono o alguien deja la empresa. */
CREATE OR ALTER PROCEDURE dbo.sp_Auth_RevocarTokensUsuario
    @UsuarioId INT,
    @Motivo    NVARCHAR(200) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    UPDATE dbo.UsuarioTokensRefresco
    SET RevocadoUtc = SYSUTCDATETIME(),
        MotivoRevocacion = ISNULL(@Motivo, N'Revocación administrativa.')
    WHERE UsuarioId = @UsuarioId
      AND RevocadoUtc IS NULL;

    SELECT @@ROWCOUNT AS SesionesRevocadas;
END;
GO

/* Purga los tokens vencidos o revocados con más de @DiasRetencion días.
   Se invoca desde mantenimiento; no se llama en la ruta de autenticación. */
CREATE OR ALTER PROCEDURE dbo.sp_Auth_PurgarTokensRefresco
    @DiasRetencion INT = 30
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @DiasRetencion IS NULL OR @DiasRetencion < 1 SET @DiasRetencion = 30;

    DECLARE @Corte DATETIME2(3) = DATEADD(DAY, -@DiasRetencion, SYSUTCDATETIME());

    DELETE FROM dbo.UsuarioTokensRefresco
    WHERE (ExpiraUtc < @Corte)
       OR (RevocadoUtc IS NOT NULL AND RevocadoUtc < @Corte);

    SELECT @@ROWCOUNT AS TokensEliminados;
END;
GO

IF XACT_STATE()<>1 THROW 54903,N'La transacción 0024 no está disponible.',1;
DECLARE @MigrationSha256 NVARCHAR(128)=N'$(MigrationSha256)';
IF LEN(@MigrationSha256)<>64 OR @MigrationSha256 LIKE N'%[^0-9A-Fa-f]%' THROW 54904,N'SHA-256 inválido para 0024.',1;
INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes)
VALUES(N'0024_mobile_auth_jwt',N'0024_mobile_auth_jwt.sql',UPPER(@MigrationSha256),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),N'Fase 1 app movil: tokens de refresco rotativos y revocables, permisos efectivos por perfil. Aditiva, no altera autenticacion existente.');
COMMIT TRANSACTION;
GO
