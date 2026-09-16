SET NOCOUNT ON;
SET XACT_ABORT ON;

/* Obligatorios y explicitos, no heredados de la herramienta.
   Un indice filtrado (WHERE ... IS NOT NULL) exige ambos en ON al crearse.
   SSMS los trae encendidos, pero sqlcmd trae QUOTED_IDENTIFIER apagado: sin
   esta linea, la misma migracion se aplica o falla segun con que se ejecute,
   y eso no puede depender de la herramienta que use el ejecutor. */
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

/* Fase 2 de la aplicación móvil: superficie del chofer.

   Aporta tres cosas:
   1. Permiso MOVIL_ACCESO, que habilita el uso de la aplicación por perfil.
   2. Permiso FLOTA_KILOMETRAJE_PROPIO, que deja al chofer abrir y cerrar SU
      jornada sin darle el módulo completo de administración de flota.
   3. Procedimientos de kilometraje con alcance de chofer e idempotentes por
      SyncGuid, para que un reintento desde la cola offline no cree una
      jornada duplicada.

   Aditiva: agrega una columna anulable a dbo.VehiculoKilometraje y crea
   procedimientos nuevos. No modifica sp_Kilometraje_Abrir, sp_Kilometraje_Cerrar
   ni sp_Kilometraje_List, que siguen siendo los que usa el módulo web de flota. */
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL THROW 54950,N'Falta el ledger 0001.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0025_mobile_driver_surface' AND Status=N'Applied')
    THROW 54951,N'0025 ya figura aplicada.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0024_mobile_auth_jwt' AND Status=N'Applied')
    THROW 54952,N'0025 depende de 0024 (autorización JWT). Aplicar 0024 primero.',1;
IF OBJECT_ID(N'dbo.VehiculoKilometraje',N'U') IS NULL OR OBJECT_ID(N'dbo.Vehiculos',N'U') IS NULL
   OR OBJECT_ID(N'dbo.Rutas',N'U') IS NULL OR OBJECT_ID(N'dbo.RutaPedidos',N'U') IS NULL
    THROW 54953,N'Faltan dependencias de flota o rutas (CU-081 / CU-152).',1;
IF OBJECT_ID(N'dbo.Permisos',N'U') IS NULL OR OBJECT_ID(N'dbo.Perfiles',N'U') IS NULL OR OBJECT_ID(N'dbo.PerfilPermisos',N'U') IS NULL
    THROW 54954,N'Faltan tablas de permisos.',1;
IF COL_LENGTH(N'dbo.Vehiculos',N'KilometrajeActual') IS NULL
    THROW 54957,N'Falta dbo.Vehiculos.KilometrajeActual (odómetro, CU-301). Verificar que el esquema de flota esté completo.',1;

BEGIN TRANSACTION;
GO

/* ─────────────────────────────────────────────────────────────────────────
   1. Idempotencia de la jornada.
   La columna es anulable a propósito: las jornadas que abre el módulo web no
   traen SyncGuid y deben seguir funcionando igual.
   ───────────────────────────────────────────────────────────────────────── */
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.VehiculoKilometraje') AND name=N'SyncGuid')
    ALTER TABLE dbo.VehiculoKilometraje ADD SyncGuid UNIQUEIDENTIFIER NULL;
GO

IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name=N'UQ_VehiculoKilometraje_SyncGuid' AND object_id=OBJECT_ID(N'dbo.VehiculoKilometraje'))
    CREATE UNIQUE INDEX UQ_VehiculoKilometraje_SyncGuid
        ON dbo.VehiculoKilometraje (SyncGuid)
        WHERE SyncGuid IS NOT NULL;
GO

/* ─────────────────────────────────────────────────────────────────────────
   2. Permisos.

   MOVIL_ACCESO permite que administración corte el uso de la aplicación a un
   perfil completo sin tocar roles ni contraseñas. El perfil Administrador no
   lo necesita: el filtro de la API lo deja pasar siempre.
   ───────────────────────────────────────────────────────────────────────── */
UPDATE dbo.Permisos
SET Modulo=N'Movil', Nombre=N'Usar la aplicación móvil',
    Descripcion=N'Habilita el inicio de sesión y el uso de la aplicación móvil.', Activo=1
WHERE Codigo=N'MOVIL_ACCESO';
IF @@ROWCOUNT=0
    INSERT dbo.Permisos(Codigo,Modulo,Nombre,Descripcion,Activo)
    VALUES(N'MOVIL_ACCESO',N'Movil',N'Usar la aplicación móvil',N'Habilita el inicio de sesión y el uso de la aplicación móvil.',1);

UPDATE dbo.Permisos
SET Modulo=N'Flota', Nombre=N'Registrar kilometraje propio',
    Descripcion=N'Permite al chofer abrir y cerrar la jornada del vehículo asignado a su ruta activa.', Activo=1
WHERE Codigo=N'FLOTA_KILOMETRAJE_PROPIO';
IF @@ROWCOUNT=0
    INSERT dbo.Permisos(Codigo,Modulo,Nombre,Descripcion,Activo)
    VALUES(N'FLOTA_KILOMETRAJE_PROPIO',N'Flota',N'Registrar kilometraje propio',N'Permite al chofer abrir y cerrar la jornada del vehículo asignado a su ruta activa.',1);

INSERT dbo.PerfilPermisos(PerfilId,PermisoId,UsuarioAsignacionId,UsuarioAsignacionNombre)
SELECT profile.PerfilId,permission.PermisoId,NULL,N'Migración 0025 superficie móvil'
FROM dbo.Perfiles profile CROSS JOIN dbo.Permisos permission
WHERE profile.Nombre IN(N'Administrador',N'Gerente',N'Chofer',N'Bodeguero',N'Bodega',N'Vendedor',N'Empleado')
  AND permission.Codigo=N'MOVIL_ACCESO'
  AND NOT EXISTS(SELECT 1 FROM dbo.PerfilPermisos assigned WHERE assigned.PerfilId=profile.PerfilId AND assigned.PermisoId=permission.PermisoId);

INSERT dbo.PerfilPermisos(PerfilId,PermisoId,UsuarioAsignacionId,UsuarioAsignacionNombre)
SELECT profile.PerfilId,permission.PermisoId,NULL,N'Migración 0025 kilometraje propio del chofer'
FROM dbo.Perfiles profile CROSS JOIN dbo.Permisos permission
WHERE profile.Nombre IN(N'Administrador',N'Chofer')
  AND permission.Codigo=N'FLOTA_KILOMETRAJE_PROPIO'
  AND NOT EXISTS(SELECT 1 FROM dbo.PerfilPermisos assigned WHERE assigned.PerfilId=profile.PerfilId AND assigned.PermisoId=permission.PermisoId);
GO

/* ─────────────────────────────────────────────────────────────────────────
   3. Vehículos sobre los que el chofer puede registrar jornada.
   Únicamente los de sus rutas activas. Alimenta el selector de la aplicación.
   ───────────────────────────────────────────────────────────────────────── */
CREATE OR ALTER PROCEDURE dbo.sp_Chofer_GetVehiculosDisponibles
    @ChoferUsuarioId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT DISTINCT
        v.VehiculoId,
        v.Placa,
        v.Descripcion,
        v.KilometrajeActual,
        CAST(CASE WHEN EXISTS(
            SELECT 1 FROM dbo.VehiculoKilometraje k
            WHERE k.VehiculoId = v.VehiculoId AND k.KmFinal IS NULL) THEN 1 ELSE 0 END AS BIT) AS JornadaAbierta
    FROM dbo.Rutas r
    INNER JOIN dbo.Vehiculos v ON v.VehiculoId = r.VehiculoId
    WHERE r.ChoferUsuarioId = @ChoferUsuarioId
      AND r.Estado IN (N'Planificada', N'Despachada')
      AND ISNULL(v.Activo, 0) = 1
    ORDER BY v.Placa;
END;
GO

/* ─────────────────────────────────────────────────────────────────────────
   4. Abrir jornada con alcance de chofer.

   La pertenencia se valida aquí, en SQL, igual que sp_Chofer_GetRouteDeliveries:
   el chofer solo puede abrir jornada del vehículo asignado a una ruta suya que
   esté activa. Un identificador de vehículo enviado desde el teléfono no basta.

   Idempotente: si el mismo @SyncGuid ya se registró, devuelve la jornada
   existente con Duplicado = 1 en vez de crear otra.
   ───────────────────────────────────────────────────────────────────────── */
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

/* ─────────────────────────────────────────────────────────────────────────
   5. Cerrar jornada con alcance de chofer.
   Solo cierra jornadas propias. Idempotente por @SyncGuid.
   ───────────────────────────────────────────────────────────────────────── */
CREATE OR ALTER PROCEDURE dbo.sp_Chofer_Kilometraje_Cerrar
    @ChoferUsuarioId INT,
    @KilometrajeId   INT,
    @KmFinal         INT,
    @SyncGuid        UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @SyncGuid IS NULL THROW 54965, N'Falta el identificador de sincronización.', 1;

    DECLARE @KmInicial INT, @KmFinalActual INT, @Duenio INT, @VehiculoId INT;

    BEGIN TRANSACTION;

    SELECT TOP (1)
        @KmInicial = k.KmInicial,
        @KmFinalActual = k.KmFinal,
        @Duenio = k.ChoferUsuarioId,
        @VehiculoId = k.VehiculoId
    FROM dbo.VehiculoKilometraje AS k WITH (UPDLOCK, HOLDLOCK)
    WHERE k.KilometrajeId = @KilometrajeId;

    IF @KmInicial IS NULL
    BEGIN
    /* Sin ROLLBACK explicito: con XACT_ABORT ON, THROW revierte la
       transaccion igual. El ROLLBACK explicito ademas rompe si alguien
       invoca este procedimiento desde un INSERT ... EXEC (error 3915). */
        THROW 54966, N'No se encontró la jornada indicada.', 1;
    END;

    IF @Duenio IS NULL OR @Duenio <> @ChoferUsuarioId
    BEGIN
    /* Sin ROLLBACK explicito: con XACT_ABORT ON, THROW revierte la
       transaccion igual. El ROLLBACK explicito ademas rompe si alguien
       invoca este procedimiento desde un INSERT ... EXEC (error 3915). */
        THROW 54967, N'La jornada no le pertenece.', 1;
    END;

    /* Ya cerrada: si fue este mismo envío, es un reintento legítimo. */
    IF @KmFinalActual IS NOT NULL
    BEGIN
        DECLARE @MismoEnvio BIT = CASE WHEN EXISTS(
            SELECT 1 FROM dbo.VehiculoKilometraje
            WHERE KilometrajeId = @KilometrajeId AND SyncGuid = @SyncGuid) THEN 1 ELSE 0 END;

        COMMIT TRANSACTION;

        IF @MismoEnvio = 1
        BEGIN
            SELECT @KilometrajeId AS KilometrajeId, @KmFinalActual AS KmFinal,
                   (@KmFinalActual - @KmInicial) AS KmRecorridos, CAST(1 AS BIT) AS Duplicado;
            RETURN;
        END;

        THROW 54968, N'La jornada ya fue cerrada.', 1;
    END;

    IF @KmFinal < @KmInicial
    BEGIN
    /* Sin ROLLBACK explicito: con XACT_ABORT ON, THROW revierte la
       transaccion igual. El ROLLBACK explicito ademas rompe si alguien
       invoca este procedimiento desde un INSERT ... EXEC (error 3915). */
        THROW 54969, N'El kilometraje final no puede ser menor al inicial.', 1;
    END;

    UPDATE dbo.VehiculoKilometraje
    SET KmFinal = @KmFinal,
        FechaCierre = SYSDATETIME(),
        SyncGuid = @SyncGuid
    WHERE KilometrajeId = @KilometrajeId;

    /* El odómetro del vehículo avanza con el cierre, nunca retrocede. */
    IF COL_LENGTH(N'dbo.Vehiculos', N'KilometrajeActual') IS NOT NULL
        UPDATE dbo.Vehiculos
        SET KilometrajeActual = @KmFinal
        WHERE VehiculoId = @VehiculoId
          AND ISNULL(KilometrajeActual, 0) < @KmFinal;

    COMMIT TRANSACTION;

    SELECT @KilometrajeId AS KilometrajeId, @KmFinal AS KmFinal,
           (@KmFinal - @KmInicial) AS KmRecorridos, CAST(0 AS BIT) AS Duplicado;
END;
GO

/* Jornada abierta del chofer, si tiene alguna. La app la necesita al arrancar
   para saber si mostrar "abrir jornada" o "cerrar jornada". */
CREATE OR ALTER PROCEDURE dbo.sp_Chofer_Kilometraje_Abierto
    @ChoferUsuarioId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP (1)
        k.KilometrajeId,
        k.VehiculoId,
        v.Placa AS VehiculoPlaca,
        k.KmInicial,
        k.Fecha,
        k.FechaRegistro
    FROM dbo.VehiculoKilometraje k
    INNER JOIN dbo.Vehiculos v ON v.VehiculoId = k.VehiculoId
    WHERE k.ChoferUsuarioId = @ChoferUsuarioId
      AND k.KmFinal IS NULL
    ORDER BY k.FechaRegistro DESC;
END;
GO

/* ─────────────────────────────────────────────────────────────────────────
   6. Resumen del día del chofer. Una sola consulta para la pantalla de
   inicio: evita que la aplicación pida tres cosas por separado, que en el
   plan gratuito de Azure se nota.
   ───────────────────────────────────────────────────────────────────────── */
CREATE OR ALTER PROCEDURE dbo.sp_Chofer_ResumenDia
    @ChoferUsuarioId INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Hoy DATE = CAST(SYSDATETIME() AS DATE);

    SELECT
        (SELECT COUNT(*) FROM dbo.Rutas r
         WHERE r.ChoferUsuarioId = @ChoferUsuarioId
           AND r.Estado IN (N'Planificada', N'Despachada')) AS RutasActivas,

        (SELECT COUNT(*) FROM dbo.RutaPedidos rp
         INNER JOIN dbo.Rutas r ON r.RutaId = rp.RutaId
         WHERE r.ChoferUsuarioId = @ChoferUsuarioId
           AND r.Estado IN (N'Planificada', N'Despachada')
           AND rp.EstadoEntrega IN (N'Pendiente', N'EnRuta')) AS EntregasPendientes,

        (SELECT COUNT(*) FROM dbo.RutaPedidos rp
         INNER JOIN dbo.Rutas r ON r.RutaId = rp.RutaId
         WHERE r.ChoferUsuarioId = @ChoferUsuarioId
           AND rp.EstadoEntrega = N'Entregado'
           AND CAST(rp.FechaEntrega AS DATE) = @Hoy) AS EntregasCompletadasHoy,

        (SELECT COUNT(*) FROM dbo.RutaPedidos rp
         INNER JOIN dbo.Rutas r ON r.RutaId = rp.RutaId
         WHERE r.ChoferUsuarioId = @ChoferUsuarioId
           AND rp.EstadoEntrega = N'Fallido'
           AND CAST(rp.FechaEntrega AS DATE) = @Hoy) AS EntregasFallidasHoy,

        CAST(CASE WHEN EXISTS(
            SELECT 1 FROM dbo.VehiculoKilometraje k
            WHERE k.ChoferUsuarioId = @ChoferUsuarioId AND k.KmFinal IS NULL)
        THEN 1 ELSE 0 END AS BIT) AS JornadaAbierta;
END;
GO

IF XACT_STATE()<>1 THROW 54955,N'La transacción 0025 no está disponible.',1;
DECLARE @MigrationSha256 NVARCHAR(128)=N'$(MigrationSha256)';
IF LEN(@MigrationSha256)<>64 OR @MigrationSha256 LIKE N'%[^0-9A-Fa-f]%' THROW 54956,N'SHA-256 inválido para 0025.',1;
INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes)
VALUES(N'0025_mobile_driver_surface',N'0025_mobile_driver_surface.sql',UPPER(@MigrationSha256),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),N'Fase 2 app movil: permisos MOVIL_ACCESO y FLOTA_KILOMETRAJE_PROPIO, kilometraje con alcance de chofer e idempotente, resumen del dia. Aditiva.');
COMMIT TRANSACTION;
GO
