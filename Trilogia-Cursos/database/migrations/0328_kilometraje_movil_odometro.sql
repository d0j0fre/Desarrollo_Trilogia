SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

/* POST-qa3-01 · CU-152-01 (pruebas post instalacion en Azure): la web rechaza
   abrir una jornada con un kilometraje inicial menor al odometro del vehiculo
   (sp_Kilometraje_Abrir, error 53046), pero la aplicacion movil
   (sp_Chofer_Kilometraje_Abrir, 0025) no hacia esa comprobacion: el chofer podia
   abrir la jornada con un numero menor y dejar un recorrido falso.

   Esta migracion vuelve a crear sp_Chofer_Kilometraje_Abrir con la misma
   validacion de la web (error 53046). El resto del procedimiento queda igual.
   La API traduce el error 53046 a un mensaje claro para el chofer.

   Aditiva, sin cambios de tablas ni de datos. Rollback en
   0328_kilometraje_movil_odometro.rollback.md. */
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL THROW 58340,N'Falta el ledger 0001.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0328_kilometraje_movil_odometro' AND Status=N'Applied')
    THROW 58341,N'0328 ya figura aplicada.',1;
IF OBJECT_ID(N'dbo.sp_Chofer_Kilometraje_Abrir',N'P') IS NULL THROW 58342,N'Falta dbo.sp_Chofer_Kilometraje_Abrir (0025).',1;
IF COL_LENGTH(N'dbo.Vehiculos',N'KilometrajeActual') IS NULL THROW 58343,N'Falta dbo.Vehiculos.KilometrajeActual (CU-301).',1;

BEGIN TRANSACTION;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Chofer_Kilometraje_Abrir
    @ChoferUsuarioId INT,
    @VehiculoId      INT,
    @KmInicial       INT,
    @SyncGuid        UNIQUEIDENTIFIER,
    @Observaciones   NVARCHAR(300) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @SyncGuid IS NULL THROW 54960, N'Falta el identificador de sincronización.', 1;
    IF ISNULL(@KmInicial, -1) < 0 THROW 54961, N'El kilometraje inicial no es válido.', 1;

    DECLARE @Existente INT =
        (SELECT TOP (1) KilometrajeId FROM dbo.VehiculoKilometraje WHERE SyncGuid = @SyncGuid);

    IF @Existente IS NOT NULL
    BEGIN
        SELECT @Existente AS KilometrajeId, CAST(1 AS BIT) AS Duplicado;
        RETURN;
    END;

    DECLARE @ChoferNombre NVARCHAR(150) =
        (SELECT TOP (1) u.NombreCompleto FROM dbo.Usuarios u
         WHERE u.UsuarioId = @ChoferUsuarioId AND ISNULL(u.Activo, 0) = 1);

    IF @ChoferNombre IS NULL THROW 54962, N'El chofer no existe o está inactivo.', 1;

    IF NOT EXISTS(
        SELECT 1 FROM dbo.Rutas r
        INNER JOIN dbo.Vehiculos v ON v.VehiculoId = r.VehiculoId
        WHERE r.ChoferUsuarioId = @ChoferUsuarioId
          AND r.VehiculoId = @VehiculoId
          AND r.Estado IN (N'Planificada', N'Despachada')
          AND ISNULL(v.Activo, 0) = 1)
        THROW 54963, N'El vehículo no está asignado a una ruta activa suya.', 1;

    -- POST-qa3-01: igual que la web (sp_Kilometraje_Abrir), el kilometraje inicial no puede ser menor al odómetro.
    DECLARE @Odometro INT = (SELECT ISNULL(KilometrajeActual, 0) FROM dbo.Vehiculos WHERE VehiculoId = @VehiculoId);
    IF @KmInicial < @Odometro
    BEGIN
        DECLARE @msg NVARCHAR(200) = CONCAT(
            N'El kilometraje inicial (', @KmInicial, N' km) no puede ser menor al odómetro actual del vehículo (', @Odometro, N' km).');
        THROW 53046, @msg, 1;
    END;

    BEGIN TRANSACTION;

    IF EXISTS(SELECT 1 FROM dbo.VehiculoKilometraje WITH (UPDLOCK, HOLDLOCK)
              WHERE VehiculoId = @VehiculoId AND KmFinal IS NULL)
    BEGIN
    /* Sin ROLLBACK explicito: con XACT_ABORT ON, THROW revierte la
       transaccion igual. El ROLLBACK explicito ademas rompe si alguien
       invoca este procedimiento desde un INSERT ... EXEC (error 3915). */
        THROW 54964, N'El vehículo tiene una jornada sin cerrar.', 1;
    END;

    INSERT dbo.VehiculoKilometraje
        (VehiculoId, ChoferUsuarioId, ChoferNombre, KmInicial, Observaciones, SyncGuid)
    VALUES
        (@VehiculoId, @ChoferUsuarioId, @ChoferNombre, @KmInicial,
         NULLIF(LTRIM(RTRIM(@Observaciones)), N''), @SyncGuid);

    DECLARE @NuevoId INT = CAST(SCOPE_IDENTITY() AS INT);

    COMMIT TRANSACTION;

    SELECT @NuevoId AS KilometrajeId, CAST(0 AS BIT) AS Duplicado;
END;
GO

IF XACT_STATE()<>1 THROW 58344,N'La transacción 0328 no está disponible.',1;
DECLARE @MigrationSha256 NVARCHAR(128)=N'$(MigrationSha256)';
IF LEN(@MigrationSha256)<>64 OR @MigrationSha256 LIKE N'%[^0-9A-Fa-f]%' THROW 58345,N'SHA-256 inválido para 0328.',1;
INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes)
VALUES(N'0328_kilometraje_movil_odometro',N'0328_kilometraje_movil_odometro.sql',UPPER(@MigrationSha256),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),
       N'POST-qa3-01: sp_Chofer_Kilometraje_Abrir rechaza un km inicial menor al odómetro (53046), como la web. Aditiva.');
COMMIT TRANSACTION;
GO
