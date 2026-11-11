SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL OR OBJECT_ID(N'dbo.EmpleadoSolicitudesTiempoLibre',N'U') IS NULL THROW 58660,N'Faltan dependencias de las licencias.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0266_licencias_transiciones_validas' AND Status=N'Applied') THROW 58661,N'La migracion ya fue aplicada.',1;
BEGIN TRANSACTION;
GO
-- G2-24: la solicitud de licencia solo avanza por transiciones validas:
--   Pendiente -> Aprobada | Rechazada;  Aprobada -> Cancelada (solo si todavia no empezo).
-- Una solicitud resuelta no vuelve a Pendiente ni cambia a otra resolucion. Los mensajes (THROW >= 50000)
-- los muestra la pantalla tal cual.
CREATE OR ALTER PROCEDURE dbo.sp_Admin_UpdateEmployeeLeaveRequestStatus
    @SolicitudId INT,
    @Estado NVARCHAR(30),
    @RespuestaAdmin NVARCHAR(500) = NULL,
    @UsuarioRespuestaId INT = NULL,
    @UsuarioRespuestaNombre NVARCHAR(150) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Actual NVARCHAR(30), @Inicio DATE;

    SELECT @Actual = Estado, @Inicio = FechaInicio
    FROM dbo.EmpleadoSolicitudesTiempoLibre
    WHERE SolicitudId = @SolicitudId;

    IF @Actual IS NULL
        THROW 51006, N'No se encontró la solicitud indicada.', 1;

    IF @Estado = N'Pendiente'
        THROW 51007, N'Una solicitud resuelta no vuelve a Pendiente.', 1;

    IF @Estado IN (N'Aprobada', N'Rechazada')
    BEGIN
        IF @Actual <> N'Pendiente'
            THROW 51008, N'Solo una solicitud Pendiente puede aprobarse o rechazarse; esta ya está resuelta.', 1;
    END
    ELSE IF @Estado = N'Cancelada'
    BEGIN
        IF @Actual <> N'Aprobada'
            THROW 51009, N'Solo una solicitud Aprobada puede cancelarse.', 1;
        IF @Inicio <= CONVERT(DATE, GETDATE())
            THROW 51010, N'La licencia ya empezó y no puede cancelarse.', 1;
    END
    ELSE
        THROW 51011, N'El estado indicado para la solicitud no es válido.', 1;

    UPDATE dbo.EmpleadoSolicitudesTiempoLibre
    SET Estado = @Estado,
        RespuestaAdmin = @RespuestaAdmin,
        UsuarioRespuestaId = @UsuarioRespuestaId,
        UsuarioRespuestaNombre = @UsuarioRespuestaNombre,
        FechaRespuesta = SYSDATETIME()
    WHERE SolicitudId = @SolicitudId AND Estado = @Actual;

    IF @@ROWCOUNT <> 1
        THROW 51012, N'La solicitud cambió mientras se resolvía. Recargue la lista e intente de nuevo.', 1;
END;
GO
DECLARE @sha NVARCHAR(128)=N'$(MigrationSha256)';
IF LEN(@sha)<>64 OR @sha LIKE N'%[^0-9A-Fa-f]%' THROW 58662,N'SHA-256 invalido.',1;
INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes) VALUES(N'0266_licencias_transiciones_validas',N'0266_licencias_transiciones_validas.sql',UPPER(@sha),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),N'G2-24: transiciones validas de las licencias (Pendiente a Aprobada o Rechazada; Aprobada a Cancelada si no empezo).');
COMMIT TRANSACTION;
GO
