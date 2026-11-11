SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL OR OBJECT_ID(N'dbo.Usuarios',N'U') IS NULL OR OBJECT_ID(N'dbo.Perfiles',N'U') IS NULL OR OBJECT_ID(N'dbo.UsuarioTokensRefresco',N'U') IS NULL THROW 58200,N'Faltan el ledger, Usuarios, Perfiles o UsuarioTokensRefresco (migracion 0024 movil).',1;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0310_credenciales_solo_hash' AND Status=N'Applied') THROW 58201,N'0311 depende de 0310 (credenciales solo con hash).',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0311_revocacion_sesiones' AND Status=N'Applied') THROW 58202,N'La migracion ya fue aplicada.',1;
BEGIN TRANSACTION;
GO
/* Revocacion de sesiones.

   Cada usuario tiene un SecurityStamp que cambia solo cuando su acceso cambia:
   se inactiva o reactiva, cambia de perfil o cambia su contrasena. La sesion web
   guarda el sello con que inicio y cada JWT de la aplicacion movil lo lleva como
   claim: si el sello actual ya no coincide, la sesion deja de valer. Ademas, el
   disparador revoca de inmediato las cadenas de tokens de refresco del usuario.
   Completa BUG-11-06 (inactivo o cambio de perfil), que ya cerraba la sesion web
   por estado y nombre de perfil, y agrega el cambio de contrasena y la API. */
IF COL_LENGTH(N'dbo.Usuarios',N'SecurityStamp') IS NULL
    ALTER TABLE dbo.Usuarios ADD SecurityStamp UNIQUEIDENTIFIER NOT NULL
        CONSTRAINT DF_Usuarios_SecurityStamp DEFAULT NEWID() WITH VALUES;
GO

CREATE OR ALTER TRIGGER dbo.tr_Usuarios_RevocarSesiones
ON dbo.Usuarios
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT (UPDATE(Activo) OR UPDATE(PerfilId) OR UPDATE(ContrasenaHash) OR UPDATE(Contrasena)) RETURN;
    -- Cambiar el algoritmo del hash en el inicio de sesion no es un cambio de acceso (sp_Auth_ActualizarHash).
    IF CONVERT(INT, ISNULL(SESSION_CONTEXT(N'sin_revocar_sesiones'), 0)) = 1 RETURN;

    DECLARE @Cambiados TABLE (UsuarioId INT NOT NULL PRIMARY KEY);
    INSERT @Cambiados (UsuarioId)
    SELECT nuevo.UsuarioId
    FROM inserted nuevo
    INNER JOIN deleted anterior ON anterior.UsuarioId = nuevo.UsuarioId
    WHERE nuevo.Activo <> anterior.Activo
       OR nuevo.PerfilId <> anterior.PerfilId
       OR ISNULL(nuevo.ContrasenaHash, N'') <> ISNULL(anterior.ContrasenaHash, N'')
       OR ISNULL(nuevo.Contrasena, N'') <> ISNULL(anterior.Contrasena, N'');

    IF NOT EXISTS (SELECT 1 FROM @Cambiados) RETURN;

    UPDATE usuario
    SET SecurityStamp = NEWID()
    FROM dbo.Usuarios usuario
    INNER JOIN @Cambiados cambiado ON cambiado.UsuarioId = usuario.UsuarioId;

    UPDATE token
    SET RevocadoUtc = SYSUTCDATETIME(),
        MotivoRevocacion = N'Cambio de acceso de la cuenta (estado, perfil o contraseña).'
    FROM dbo.UsuarioTokensRefresco token
    INNER JOIN @Cambiados cambiado ON cambiado.UsuarioId = token.UsuarioId
    WHERE token.RevocadoUtc IS NULL;
END;
GO

-- Estado vigente de una sesion: la web lo consulta en cada solicitud y la API en cada JWT.
CREATE OR ALTER PROCEDURE dbo.sp_Auth_GetSessionState
    @UsuarioId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT CAST(CASE WHEN usuario.Activo = 1 AND perfil.Activo = 1 THEN 1 ELSE 0 END AS BIT) AS Activo,
           perfil.Nombre AS PerfilNombre,
           usuario.SecurityStamp
    FROM dbo.Usuarios usuario
    INNER JOIN dbo.Perfiles perfil ON perfil.PerfilId = usuario.PerfilId
    WHERE usuario.UsuarioId = @UsuarioId;
END;
GO

-- Cambio de algoritmo del hash (transicion legacy-sha256 a PBKDF2) tras una verificacion correcta.
-- Solo escribe si la clave no cambio mientras tanto (@HashAnterior) y no revoca sesiones.
CREATE OR ALTER PROCEDURE dbo.sp_Auth_ActualizarHash
    @UsuarioId INT,
    @HashAnterior NVARCHAR(512),
    @HashNuevo NVARCHAR(512)
AS
BEGIN
    SET NOCOUNT ON;
    IF @UsuarioId <= 0 OR NULLIF(LTRIM(RTRIM(@HashNuevo)), N'') IS NULL OR NULLIF(LTRIM(RTRIM(@HashAnterior)), N'') IS NULL
        THROW 58210, N'Hash inválido.', 1;

    EXEC sys.sp_set_session_context @key = N'sin_revocar_sesiones', @value = 1;
    BEGIN TRY
        UPDATE dbo.Usuarios
        SET ContrasenaHash = @HashNuevo,
            PasswordVersion = 1,
            PasswordActualizadaUtc = SYSUTCDATETIME()
        WHERE UsuarioId = @UsuarioId
          AND ContrasenaHash = @HashAnterior;
        DECLARE @Filas INT = @@ROWCOUNT;
        EXEC sys.sp_set_session_context @key = N'sin_revocar_sesiones', @value = NULL;
    END TRY
    BEGIN CATCH
        EXEC sys.sp_set_session_context @key = N'sin_revocar_sesiones', @value = NULL;
        THROW;
    END CATCH;

    SELECT @Filas AS Actualizadas;
END;
GO

-- El inicio de sesion devuelve el sello para el JWT y la sesion web.
CREATE OR ALTER PROCEDURE dbo.sp_Auth_GetLoginCredential
    @Correo NVARCHAR(150),
    @Contrasena NVARCHAR(255) = NULL   -- Obsoleto: se ignora. La API verifica el hash.
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP (1)
        u.UsuarioId,
        u.NombreCompleto,
        u.Correo,
        p.Nombre AS PerfilNombre,
        u.Activo,
        u.ContrasenaHash,
        CAST(0 AS BIT) AS LegacyPasswordMatches,   -- Compatibilidad con la API anterior: nunca coincide.
        u.SecurityStamp
    FROM dbo.Usuarios u
    INNER JOIN dbo.Perfiles p ON p.PerfilId = u.PerfilId
    WHERE u.Correo = @Correo;
END;
GO

-- Al refrescar: usuario y perfil activos, con el sello vigente para el access token nuevo.
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
        CAST(ISNULL(u.Activo, 0) AS BIT) AS Activo,
        u.SecurityStamp
    FROM dbo.Usuarios u
    INNER JOIN dbo.Perfiles pf ON pf.PerfilId = u.PerfilId
    WHERE u.UsuarioId = @UsuarioId
      AND ISNULL(u.Activo, 0) = 1
      AND ISNULL(pf.Activo, 0) = 1;
END;
GO
DECLARE @sha NVARCHAR(128)=N'$(MigrationSha256)';
IF LEN(@sha)<>64 OR @sha LIKE N'%[^0-9A-Fa-f]%' THROW 58203,N'SHA-256 invalido.',1;
INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes) VALUES(N'0311_revocacion_sesiones',N'0311_revocacion_sesiones.sql',UPPER(@sha),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),N'Seguridad: SecurityStamp por usuario, disparador que lo renueva y revoca tokens de refresco al inactivar, cambiar de perfil o de contrasena; estado de sesion para web y JWT.');
COMMIT TRANSACTION;
GO
