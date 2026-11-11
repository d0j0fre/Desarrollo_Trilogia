SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL OR OBJECT_ID(N'dbo.VehiculoKilometraje',N'U') IS NULL OR OBJECT_ID(N'dbo.Vehiculos',N'U') IS NULL OR OBJECT_ID(N'dbo.Permisos',N'U') IS NULL OR OBJECT_ID(N'dbo.PerfilPermisos',N'U') IS NULL THROW 58300,N'Faltan el ledger o los objetos de kilometraje y permisos.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0312_kilometraje_propiedad' AND Status=N'Applied') THROW 58301,N'La migracion ya fue aplicada.',1;
BEGIN TRANSACTION;
GO
/* Propiedad del kilometraje.

   sp_Kilometraje_Cerrar (web) cerraba cualquier jornada por su id: un chofer podia
   cerrar la de otro. Ahora exige quien actua y si puede administrar jornadas ajenas
   (permiso FLOTA_KILOMETRAJE_ADMIN o perfil Administrador); un usuario sin ese permiso
   solo cierra las suyas y, para las demas, recibe «no se encontro la jornada» (no se
   revela que existe). El cierre es atomico: la transaccion empieza antes de leer la fila
   con UPDLOCK + HOLDLOCK, el UPDATE solo toca una jornada abierta y @@ROWCOUNT debe ser 1,
   de modo que dos cierres simultaneos terminan con uno correcto y otro con el error 53044.
   El cierre desde la aplicacion movil (sp_Chofer_Kilometraje_Cerrar) ya tenia ambas protecciones. */
IF NOT EXISTS (SELECT 1 FROM dbo.Permisos WHERE Codigo = N'FLOTA_KILOMETRAJE_ADMIN')
    INSERT dbo.Permisos (Codigo, Modulo, Nombre, Descripcion, Activo)
    VALUES (N'FLOTA_KILOMETRAJE_ADMIN', N'Flota', N'Administrar kilometraje',
            N'Consultar y cerrar jornadas de kilometraje de otros usuarios.', 1);

INSERT dbo.PerfilPermisos (PerfilId, PermisoId, UsuarioAsignacionId, UsuarioAsignacionNombre)
SELECT perfil.PerfilId, permiso.PermisoId, NULL, N'Migración 0312'
FROM dbo.Perfiles perfil
CROSS JOIN dbo.Permisos permiso
WHERE perfil.Nombre = N'Administrador'
  AND permiso.Codigo = N'FLOTA_KILOMETRAJE_ADMIN'
  AND NOT EXISTS (SELECT 1 FROM dbo.PerfilPermisos asignacion WHERE asignacion.PerfilId = perfil.PerfilId AND asignacion.PermisoId = permiso.PermisoId);
GO

CREATE OR ALTER PROCEDURE dbo.sp_Kilometraje_Cerrar
    @KilometrajeId INT,
    @KmFinal INT,
    @ActorUsuarioId INT = NULL,
    @PuedeAdministrar BIT = 0
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    -- Sin actor no hay propiedad que verificar: se rechaza (un MVC anterior no lo envia).
    IF ISNULL(@ActorUsuarioId, 0) <= 0 THROW 53047, N'No se pudo identificar al usuario que cierra la jornada.', 1;

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @KmInicial INT, @KmFinalActual INT, @VehiculoId INT, @ChoferUsuarioId INT;
        SELECT @KmInicial = KmInicial,
               @KmFinalActual = KmFinal,
               @VehiculoId = VehiculoId,
               @ChoferUsuarioId = ChoferUsuarioId
        FROM dbo.VehiculoKilometraje WITH (UPDLOCK, HOLDLOCK)
        WHERE KilometrajeId = @KilometrajeId;

        IF @KmInicial IS NULL THROW 53043, N'No se encontró la jornada indicada.', 1;
        -- Jornada ajena sin permiso de administracion: misma respuesta que si no existiera.
        IF ISNULL(@PuedeAdministrar, 0) = 0 AND ISNULL(@ChoferUsuarioId, 0) <> @ActorUsuarioId
            THROW 53043, N'No se encontró la jornada indicada.', 1;
        IF @KmFinalActual IS NOT NULL THROW 53044, N'La jornada ya fue cerrada.', 1;
        IF @KmFinal < @KmInicial THROW 53045, N'El kilometraje final no puede ser menor al inicial.', 1;

        UPDATE dbo.VehiculoKilometraje
        SET KmFinal = @KmFinal, FechaCierre = SYSDATETIME()
        WHERE KilometrajeId = @KilometrajeId
          AND KmFinal IS NULL
          AND (ISNULL(@PuedeAdministrar, 0) = 1 OR ChoferUsuarioId = @ActorUsuarioId);

        IF @@ROWCOUNT <> 1 THROW 53044, N'La jornada ya fue cerrada.', 1;

        -- El odometro del vehiculo avanza al km final de la jornada, nunca retrocede.
        UPDATE dbo.Vehiculos SET KilometrajeActual = @KmFinal
        WHERE VehiculoId = @VehiculoId AND KilometrajeActual < @KmFinal;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO
DECLARE @sha NVARCHAR(128)=N'$(MigrationSha256)';
IF LEN(@sha)<>64 OR @sha LIKE N'%[^0-9A-Fa-f]%' THROW 58302,N'SHA-256 invalido.',1;
INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes) VALUES(N'0312_kilometraje_propiedad',N'0312_kilometraje_propiedad.sql',UPPER(@sha),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),N'Seguridad: el cierre de kilometraje exige propiedad (o FLOTA_KILOMETRAJE_ADMIN) y es atomico; error 53044 en el segundo cierre.');
COMMIT TRANSACTION;
GO
