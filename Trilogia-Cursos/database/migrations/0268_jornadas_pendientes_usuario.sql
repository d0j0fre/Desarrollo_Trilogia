SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL OR OBJECT_ID(N'dbo.EmpleadoJornadas',N'U') IS NULL THROW 58680,N'Faltan dependencias de jornadas (0018).',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0268_jornadas_pendientes_usuario' AND Status=N'Applied') THROW 58681,N'La migracion ya fue aplicada.',1;
BEGIN TRANSACTION;
GO
-- G2-26: la lista de jornadas por aprobar devuelve ademas el UsuarioId del empleado (ultima columna) para que la pantalla
-- no ofrezca a nadie aprobar su propia jornada. Las columnas anteriores conservan su orden.
CREATE OR ALTER PROCEDURE dbo.sp_RRHH_ListarJornadasPendientes @Desde DATE,@Hasta DATE AS
BEGIN
    SET NOCOUNT ON;
    SELECT j.JornadaId,j.EmpleadoId,u.NombreCompleto,j.Fecha,j.HorasOrdinarias,j.HorasExtra,j.HorasAusencia,j.Observaciones,j.Estado,j.RespuestaSupervisor,j.VersionFila,e.UsuarioId
    FROM dbo.EmpleadoJornadas j
    JOIN dbo.Empleados e ON e.EmpleadoId=j.EmpleadoId
    JOIN dbo.Usuarios u ON u.UsuarioId=e.UsuarioId
    WHERE j.Estado=N'Enviada' AND j.Fecha BETWEEN @Desde AND @Hasta
    ORDER BY j.Fecha,u.NombreCompleto;
END;
GO
DECLARE @sha NVARCHAR(128)=N'$(MigrationSha256)';
IF LEN(@sha)<>64 OR @sha LIKE N'%[^0-9A-Fa-f]%' THROW 58682,N'SHA-256 invalido.',1;
INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes) VALUES(N'0268_jornadas_pendientes_usuario',N'0268_jornadas_pendientes_usuario.sql',UPPER(@sha),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),N'G2-26: sp_RRHH_ListarJornadasPendientes devuelve UsuarioId para ocultar la propia jornada al aprobador.');
COMMIT TRANSACTION;
GO
