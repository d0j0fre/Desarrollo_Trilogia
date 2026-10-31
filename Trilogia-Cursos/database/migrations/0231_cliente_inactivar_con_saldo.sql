SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

/* BUG-06-01 · CU-063-02: el sistema permitía inactivar un cliente con deuda
   pendiente (₡60 000 en el caso de QA) sin advertir que debía saldarse la
   cuenta.

   sp_Admin_UpdateClient (Editar cliente) y sp_Admin_ToggleClientStatus
   (botón Inactivar del listado y del detalle) rechazan la inactivación con
   el error 51012 cuando la deuda actual del cliente es mayor que cero. La
   deuda se toma de dbo.fn_Cliente_EstadoDeuda (0230), la misma que muestran
   Créditos y el estado de cuenta. Reactivar y editar a un cliente con deuda
   sigue permitido.

   Copias de las versiones de cu061_cu063_clientes_patch.sql con el bloqueo
   (líneas marcadas con 0231). Rollback en 0231_cliente_inactivar_con_saldo.rollback.md. */
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL THROW 56320,N'Falta el ledger 0001.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0231_cliente_inactivar_con_saldo' AND Status=N'Applied')
    THROW 56321,N'0231 ya figura aplicada.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0230_cliente_estado_deuda' AND Status=N'Applied')
    THROW 56322,N'0231 requiere 0230 aplicada.',1;

BEGIN TRANSACTION;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Admin_UpdateClient
    @UsuarioId INT,
    @NombreCompleto NVARCHAR(150),
    @Correo NVARCHAR(150),
    @Telefono NVARCHAR(30) = NULL,
    @Direccion NVARCHAR(255) = NULL,
    @Contrasena NVARCHAR(255) = NULL,
    @Activo BIT = 1,
    @MotivoInactivacion NVARCHAR(255) = NULL
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
        Contrasena = CASE WHEN @Contrasena IS NULL THEN Contrasena ELSE @Contrasena END,
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

CREATE OR ALTER PROCEDURE dbo.sp_Admin_ToggleClientStatus
    @UsuarioId INT,
    @MotivoInactivacion NVARCHAR(255) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @ClientePerfilId INT = (SELECT TOP 1 PerfilId FROM dbo.Perfiles WHERE Nombre = 'Cliente');
    DECLARE @ActivoActual BIT;

    SELECT @ActivoActual = CAST(ISNULL(Activo, 1) AS BIT)
    FROM dbo.Usuarios
    WHERE UsuarioId = @UsuarioId
      AND PerfilId = @ClientePerfilId;

    IF @ActivoActual IS NULL
    BEGIN
        THROW 51010, 'El cliente seleccionado no existe.', 1;
    END

    -- 0231: no se inactiva un cliente con saldo pendiente (CU-063-02).
    IF @ActivoActual = 1
       AND EXISTS (SELECT 1 FROM dbo.fn_Cliente_EstadoDeuda(@UsuarioId) WHERE DeudaActual > 0)
    BEGIN
        THROW 51012, 'El cliente tiene un saldo pendiente. Debe saldarse la cuenta antes de inactivarlo.', 1;
    END

    SET @MotivoInactivacion = NULLIF(LTRIM(RTRIM(ISNULL(@MotivoInactivacion, ''))), '');

    IF @ActivoActual = 1 AND @MotivoInactivacion IS NULL
    BEGIN
        SET @MotivoInactivacion = 'Inactivación administrativa del perfil de cliente.';
    END

    UPDATE dbo.Usuarios
    SET
        Activo = CASE WHEN @ActivoActual = 1 THEN 0 ELSE 1 END,
        MotivoInactivacion = CASE WHEN @ActivoActual = 1 THEN @MotivoInactivacion ELSE NULL END,
        FechaInactivacion = CASE WHEN @ActivoActual = 1 THEN SYSDATETIME() ELSE NULL END,
        FechaActualizacion = SYSDATETIME()
    WHERE UsuarioId = @UsuarioId
      AND PerfilId = @ClientePerfilId;

    SELECT CAST(CASE WHEN @ActivoActual = 1 THEN 0 ELSE 1 END AS BIT) AS NuevoEstado;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.sql_modules WHERE object_id = OBJECT_ID(N'dbo.sp_Admin_UpdateClient') AND definition LIKE N'%51012%')
   OR NOT EXISTS (SELECT 1 FROM sys.sql_modules WHERE object_id = OBJECT_ID(N'dbo.sp_Admin_ToggleClientStatus') AND definition LIKE N'%51012%')
    THROW 56323, N'Los procedimientos de clientes no quedaron con el bloqueo por saldo.', 1;

IF XACT_STATE() <> 1
    THROW 56324, N'La transacción 0231 no está disponible.', 1;

DECLARE @MigrationSha256 NVARCHAR(128) = N'$(MigrationSha256)';
IF LEN(@MigrationSha256) <> 64 OR @MigrationSha256 LIKE N'%[^0-9A-Fa-f]%'
    THROW 56325, N'SHA-256 inválido para 0231.', 1;

INSERT dbo.SchemaMigrationHistory
    (MigrationId, FileName, FileSha256, Status, AppliedBy, EnvironmentName, Notes)
VALUES
    (N'0231_cliente_inactivar_con_saldo',
     N'0231_cliente_inactivar_con_saldo.sql',
     UPPER(@MigrationSha256),
     N'Applied',
     ORIGINAL_LOGIN(),
     DB_NAME(),
     N'BUG-06-01: no se inactiva un cliente con saldo pendiente (error 51012).');

COMMIT TRANSACTION;
GO
