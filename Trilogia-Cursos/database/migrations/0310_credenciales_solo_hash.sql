SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL OR OBJECT_ID(N'dbo.Usuarios',N'U') IS NULL OR OBJECT_ID(N'dbo.Perfiles',N'U') IS NULL THROW 58100,N'Faltan el ledger de migraciones o las tablas Usuarios y Perfiles.',1;
IF COL_LENGTH(N'dbo.Usuarios',N'ContrasenaHash') IS NULL OR COL_LENGTH(N'dbo.Usuarios',N'PasswordVersion') IS NULL THROW 58101,N'Falta la migracion 0022 (columnas ContrasenaHash y PasswordVersion).',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0310_credenciales_solo_hash' AND Status=N'Applied') THROW 58102,N'La migracion ya fue aplicada.',1;
BEGIN TRANSACTION;
GO
/* Credenciales solo con hash.

   Despues de esta migracion ninguna contrasena en texto plano es valida ni
   queda guardada:
   1. Las filas con texto plano pasan a un hash con sal («legacy-sha256$sal$hash»,
      SHA-256 de sal + contrasena en UTF-16). T-SQL no calcula PBKDF2; este formato
      de transicion deja a las cuentas demo entrando con su clave y la API lo
      cambia a PBKDF2 en el primer inicio de sesion correcto de cada usuario.
   2. Un disparador en dbo.Usuarios convierte al instante cualquier texto plano que
      un procedimiento o un script de datos demo escriba en la columna Contrasena:
      nunca queda guardado despues de confirmar la sentencia.
   3. El inicio de sesion ya no compara texto plano (se retira el comodin
      LegacyPasswordMatches) y los procedimientos de registro y cambio de clave
      exigen hash. */

-- Hash de transicion: SHA-256(sal + contrasena UTF-16LE), en hexadecimal.
CREATE OR ALTER FUNCTION dbo.fn_Auth_HashTransicion(@Contrasena NVARCHAR(255), @Sal VARBINARY(16))
RETURNS NVARCHAR(512)
WITH SCHEMABINDING
AS
BEGIN
    RETURN N'legacy-sha256$' + CONVERT(NVARCHAR(32), @Sal, 2) + N'$'
        + CONVERT(NVARCHAR(64), HASHBYTES('SHA2_256', @Sal + CAST(@Contrasena AS VARBINARY(MAX))), 2);
END;
GO

-- 1. Datos existentes: el texto plano se convierte y se borra.
UPDATE usuario
SET ContrasenaHash = dbo.fn_Auth_HashTransicion(usuario.Contrasena, sal.Valor),
    Contrasena = N'',
    PasswordVersion = 0,
    DebeCambiarContrasena = 0,
    PasswordActualizadaUtc = SYSUTCDATETIME()
FROM dbo.Usuarios usuario
CROSS APPLY (SELECT CONVERT(VARBINARY(16), NEWID()) AS Valor) sal
WHERE NULLIF(LTRIM(RTRIM(usuario.Contrasena)), N'') IS NOT NULL
  AND NULLIF(LTRIM(RTRIM(usuario.ContrasenaHash)), N'') IS NULL;

UPDATE dbo.Usuarios
SET Contrasena = N''
WHERE NULLIF(LTRIM(RTRIM(ContrasenaHash)), N'') IS NOT NULL
  AND ISNULL(Contrasena, N'') <> N'';
GO

-- 2. Datos futuros: el texto plano no sobrevive a la sentencia que lo escribe.
CREATE OR ALTER TRIGGER dbo.tr_Usuarios_SinTextoPlano
ON dbo.Usuarios
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    IF TRIGGER_NESTLEVEL(OBJECT_ID(N'dbo.tr_Usuarios_SinTextoPlano')) > 1 RETURN;
    IF NOT EXISTS (SELECT 1 FROM inserted WHERE NULLIF(LTRIM(RTRIM(Contrasena)), N'') IS NOT NULL) RETURN;

    UPDATE usuario
    SET ContrasenaHash = CASE
            -- Si la misma sentencia fijo un hash nuevo, ese hash manda.
            WHEN NULLIF(LTRIM(RTRIM(nuevo.ContrasenaHash)), N'') IS NOT NULL
             AND (anterior.UsuarioId IS NULL OR ISNULL(nuevo.ContrasenaHash, N'') <> ISNULL(anterior.ContrasenaHash, N''))
                THEN nuevo.ContrasenaHash
            ELSE dbo.fn_Auth_HashTransicion(nuevo.Contrasena, sal.Valor)
        END,
        Contrasena = N'',
        PasswordVersion = CASE
            WHEN NULLIF(LTRIM(RTRIM(nuevo.ContrasenaHash)), N'') IS NOT NULL
             AND (anterior.UsuarioId IS NULL OR ISNULL(nuevo.ContrasenaHash, N'') <> ISNULL(anterior.ContrasenaHash, N''))
                THEN nuevo.PasswordVersion
            ELSE 0
        END,
        PasswordActualizadaUtc = SYSUTCDATETIME()
    FROM dbo.Usuarios usuario
    INNER JOIN inserted nuevo ON nuevo.UsuarioId = usuario.UsuarioId
    LEFT JOIN deleted anterior ON anterior.UsuarioId = nuevo.UsuarioId
    CROSS APPLY (SELECT CONVERT(VARBINARY(16), NEWID()) AS Valor) sal
    WHERE NULLIF(LTRIM(RTRIM(nuevo.Contrasena)), N'') IS NOT NULL;
END;
GO

-- 3. Procedimientos de autenticacion sin comparacion de texto plano.
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
        CAST(0 AS BIT) AS LegacyPasswordMatches   -- Compatibilidad con la API anterior: nunca coincide.
    FROM dbo.Usuarios u
    INNER JOIN dbo.Perfiles p ON p.PerfilId = u.PerfilId
    WHERE u.Correo = @Correo;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Auth_GetUserByEmail
    @Correo NVARCHAR(200)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP 1
        u.UsuarioId,
        u.NombreCompleto,
        u.Correo,
        p.Nombre AS PerfilNombre,
        u.Activo
    FROM dbo.Usuarios u
    INNER JOIN dbo.Perfiles p ON p.PerfilId = u.PerfilId
    WHERE u.Correo = @Correo;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Auth_ValidateUser
    @Correo NVARCHAR(150),
    @Contrasena NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;
    -- Retirado: comparaba texto plano. La validacion la hace la API contra el hash.
    THROW 58110, N'sp_Auth_ValidateUser fue retirado: la contrasena se verifica contra el hash en la API.', 1;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Auth_SetPasswordHash
    @UsuarioId INT,
    @ContrasenaHash NVARCHAR(512)
AS
BEGIN
    SET NOCOUNT ON;
    IF @UsuarioId <= 0 OR NULLIF(LTRIM(RTRIM(@ContrasenaHash)), N'') IS NULL
        THROW 55010, N'Credencial hash inválida.', 1;

    UPDATE dbo.Usuarios
    SET ContrasenaHash = @ContrasenaHash,
        Contrasena = N'',
        PasswordVersion = 1,
        DebeCambiarContrasena = 0,
        PasswordActualizadaUtc = SYSUTCDATETIME()
    WHERE UsuarioId = @UsuarioId;

    IF @@ROWCOUNT <> 1 THROW 55011, N'Usuario inexistente.', 1;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Auth_RegisterClient
    @NombreCompleto NVARCHAR(200),
    @Correo NVARCHAR(200),
    @Contrasena NVARCHAR(255) = NULL,   -- Obsoleto: se ignora; solo se acepta hash.
    @ContrasenaHash NVARCHAR(512) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF NULLIF(LTRIM(RTRIM(@NombreCompleto)), N'') IS NULL OR
       NULLIF(LTRIM(RTRIM(@Correo)), N'') IS NULL OR
       NULLIF(LTRIM(RTRIM(@ContrasenaHash)), N'') IS NULL
        THROW 55012, N'Datos de registro inválidos.', 1;
    IF EXISTS (SELECT 1 FROM dbo.Usuarios WHERE Correo = LTRIM(RTRIM(@Correo)))
        THROW 55013, N'El correo ya se encuentra registrado.', 1;

    DECLARE @PerfilId INT = (SELECT TOP (1) PerfilId FROM dbo.Perfiles WHERE Nombre = N'Cliente');
    IF @PerfilId IS NULL THROW 55014, N'No existe el perfil Cliente.', 1;

    INSERT dbo.Usuarios
        (PerfilId, NombreCompleto, Correo, Contrasena, ContrasenaHash, PasswordVersion,
         DebeCambiarContrasena, PasswordActualizadaUtc, Activo)
    VALUES
        (@PerfilId, LTRIM(RTRIM(@NombreCompleto)), LTRIM(RTRIM(@Correo)), N'', @ContrasenaHash, 1,
         0, SYSUTCDATETIME(), 1);
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Auth_UpdatePassword
    @UsuarioId INT,
    @Contrasena NVARCHAR(255) = NULL,   -- Obsoleto: se ignora; solo se acepta hash.
    @ContrasenaHash NVARCHAR(512) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF NULLIF(LTRIM(RTRIM(@ContrasenaHash)), N'') IS NULL THROW 55015, N'Se requiere la credencial en forma de hash.', 1;
    EXEC dbo.sp_Auth_SetPasswordHash @UsuarioId = @UsuarioId, @ContrasenaHash = @ContrasenaHash;
END;
GO

-- Clientes creados o editados por el personal: el MVC envia el hash PBKDF2.
-- @Contrasena sigue aceptandose (un MVC anterior lo envia) y el disparador la convierte.
CREATE OR ALTER PROCEDURE dbo.sp_Admin_CreateClient
    @NombreCompleto NVARCHAR(150),
    @Correo NVARCHAR(150),
    @Contrasena NVARCHAR(255) = NULL,
    @Telefono NVARCHAR(30) = NULL,
    @Direccion NVARCHAR(255) = NULL,
    @Activo BIT = 1,
    @MotivoInactivacion NVARCHAR(255) = NULL,
    @ContrasenaHash NVARCHAR(512) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @ClientePerfilId INT = (SELECT TOP 1 PerfilId FROM dbo.Perfiles WHERE Nombre = 'Cliente');

    SET @NombreCompleto = LTRIM(RTRIM(ISNULL(@NombreCompleto, '')));
    SET @Correo = LOWER(LTRIM(RTRIM(ISNULL(@Correo, ''))));
    SET @Contrasena = LTRIM(RTRIM(ISNULL(@Contrasena, '')));
    SET @ContrasenaHash = NULLIF(LTRIM(RTRIM(ISNULL(@ContrasenaHash, ''))), '');
    SET @Telefono = NULLIF(LTRIM(RTRIM(ISNULL(@Telefono, ''))), '');
    SET @Direccion = NULLIF(LTRIM(RTRIM(ISNULL(@Direccion, ''))), '');
    SET @MotivoInactivacion = NULLIF(LTRIM(RTRIM(ISNULL(@MotivoInactivacion, ''))), '');

    IF @ClientePerfilId IS NULL
    BEGIN
        THROW 51002, 'No existe el perfil Cliente.', 1;
    END

    IF @NombreCompleto = '' OR @Correo = '' OR (@Contrasena = '' AND @ContrasenaHash IS NULL)
    BEGIN
        THROW 51003, 'Nombre, correo y contraseña son obligatorios.', 1;
    END

    IF EXISTS (SELECT 1 FROM dbo.Usuarios WHERE Correo = @Correo)
    BEGIN
        THROW 51004, 'Ya existe un usuario registrado con ese correo.', 1;
    END

    IF ISNULL(@Activo, 1) = 0 AND @MotivoInactivacion IS NULL
    BEGIN
        THROW 51005, 'Debe indicar un motivo para registrar el cliente como inactivo.', 1;
    END

    INSERT INTO dbo.Usuarios
    (
        PerfilId, NombreCompleto, Correo, Contrasena, ContrasenaHash, PasswordVersion, PasswordActualizadaUtc,
        Telefono, Direccion, Activo, MotivoInactivacion, FechaInactivacion, FechaActualizacion
    )
    VALUES
    (
        @ClientePerfilId,
        @NombreCompleto,
        @Correo,
        CASE WHEN @ContrasenaHash IS NULL THEN @Contrasena ELSE N'' END,
        @ContrasenaHash,
        CASE WHEN @ContrasenaHash IS NULL THEN 0 ELSE 1 END,
        SYSUTCDATETIME(),
        @Telefono,
        @Direccion,
        ISNULL(@Activo, 1),
        CASE WHEN ISNULL(@Activo, 1) = 0 THEN @MotivoInactivacion ELSE NULL END,
        CASE WHEN ISNULL(@Activo, 1) = 0 THEN SYSDATETIME() ELSE NULL END,
        SYSDATETIME()
    );

    SELECT CAST(SCOPE_IDENTITY() AS INT) AS UsuarioId;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Admin_UpdateClient
    @UsuarioId INT,
    @NombreCompleto NVARCHAR(150),
    @Correo NVARCHAR(150),
    @Telefono NVARCHAR(30) = NULL,
    @Direccion NVARCHAR(255) = NULL,
    @Contrasena NVARCHAR(255) = NULL,
    @Activo BIT = 1,
    @MotivoInactivacion NVARCHAR(255) = NULL,
    @ContrasenaHash NVARCHAR(512) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @ClientePerfilId INT = (SELECT TOP 1 PerfilId FROM dbo.Perfiles WHERE Nombre = 'Cliente');
    DECLARE @ActivoAnterior BIT;

    SELECT @ActivoAnterior = CAST(ISNULL(Activo, 1) AS BIT)
    FROM dbo.Usuarios
    WHERE UsuarioId = @UsuarioId
      AND PerfilId = @ClientePerfilId;

    IF @ActivoAnterior IS NULL
    BEGIN
        THROW 51006, 'El cliente seleccionado no existe.', 1;
    END

    SET @NombreCompleto = LTRIM(RTRIM(ISNULL(@NombreCompleto, '')));
    SET @Correo = LOWER(LTRIM(RTRIM(ISNULL(@Correo, ''))));
    SET @Telefono = NULLIF(LTRIM(RTRIM(ISNULL(@Telefono, ''))), '');
    SET @Direccion = NULLIF(LTRIM(RTRIM(ISNULL(@Direccion, ''))), '');
    SET @Contrasena = NULLIF(LTRIM(RTRIM(ISNULL(@Contrasena, ''))), '');
    SET @ContrasenaHash = NULLIF(LTRIM(RTRIM(ISNULL(@ContrasenaHash, ''))), '');
    SET @MotivoInactivacion = NULLIF(LTRIM(RTRIM(ISNULL(@MotivoInactivacion, ''))), '');

    IF @NombreCompleto = '' OR @Correo = ''
    BEGIN
        THROW 51007, 'Nombre y correo son obligatorios.', 1;
    END

    IF EXISTS (SELECT 1 FROM dbo.Usuarios WHERE Correo = @Correo AND UsuarioId <> @UsuarioId)
    BEGIN
        THROW 51008, 'Ya existe otro usuario registrado con ese correo.', 1;
    END

    IF ISNULL(@Activo, 1) = 0 AND @MotivoInactivacion IS NULL
    BEGIN
        THROW 51009, 'Debe indicar un motivo para inactivar el cliente.', 1;
    END

    -- 0231: no se inactiva un cliente con saldo pendiente (CU-063-02).
    IF ISNULL(@Activo, 1) = 0 AND @ActivoAnterior = 1
       AND EXISTS (SELECT 1 FROM dbo.fn_Cliente_EstadoDeuda(@UsuarioId) WHERE DeudaActual > 0)
    BEGIN
        THROW 51012, 'El cliente tiene un saldo pendiente. Debe saldarse la cuenta antes de inactivarlo.', 1;
    END

    UPDATE dbo.Usuarios
    SET
        NombreCompleto = @NombreCompleto,
        Correo = @Correo,
        Telefono = @Telefono,
        Direccion = @Direccion,
        Contrasena = CASE WHEN @ContrasenaHash IS NULL AND @Contrasena IS NOT NULL THEN @Contrasena ELSE Contrasena END,
        ContrasenaHash = COALESCE(@ContrasenaHash, ContrasenaHash),
        PasswordVersion = CASE WHEN @ContrasenaHash IS NULL THEN PasswordVersion ELSE 1 END,
        PasswordActualizadaUtc = CASE WHEN @ContrasenaHash IS NULL THEN PasswordActualizadaUtc ELSE SYSUTCDATETIME() END,
        Activo = ISNULL(@Activo, 1),
        MotivoInactivacion = CASE WHEN ISNULL(@Activo, 1) = 0 THEN @MotivoInactivacion ELSE NULL END,
        FechaInactivacion = CASE
            WHEN ISNULL(@Activo, 1) = 0 AND @ActivoAnterior = 1 THEN SYSDATETIME()
            WHEN ISNULL(@Activo, 1) = 0 AND @ActivoAnterior = 0 THEN ISNULL(FechaInactivacion, SYSDATETIME())
            ELSE NULL
        END,
        FechaActualizacion = SYSDATETIME()
    WHERE UsuarioId = @UsuarioId
      AND PerfilId = @ClientePerfilId;
END
GO
DECLARE @sha NVARCHAR(128)=N'$(MigrationSha256)';
IF LEN(@sha)<>64 OR @sha LIKE N'%[^0-9A-Fa-f]%' THROW 58103,N'SHA-256 invalido.',1;
INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes) VALUES(N'0310_credenciales_solo_hash',N'0310_credenciales_solo_hash.sql',UPPER(@sha),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),N'Seguridad: ninguna contrasena en texto plano valida ni almacenada; hash de transicion con sal para datos existentes y disparador que convierte cualquier escritura de texto plano.');
COMMIT TRANSACTION;
GO
