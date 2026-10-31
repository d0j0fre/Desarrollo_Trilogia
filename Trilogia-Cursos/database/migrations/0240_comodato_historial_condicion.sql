SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

/* BUG-16-05 · CU-163-03: el historial de comodatos del detalle del activo no
   muestra la condición registrada al entregar ni al devolver el equipo.

   sp_Comodato_HistorialPorActivo agrega CondicionEntrega y
   CondicionDevolucion AL FINAL del resultado (posiciones 7 y 8). Las
   columnas 0–6 no cambian, así que la aplicación anterior sigue leyendo bien.

   Aditiva. Rollback en 0240_comodato_historial_condicion.rollback.md. */
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL THROW 56400,N'Falta el ledger 0001.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0240_comodato_historial_condicion' AND Status=N'Applied')
    THROW 56401,N'0240 ya figura aplicada.',1;
IF OBJECT_ID(N'dbo.Comodatos',N'U') IS NULL
    THROW 56402,N'Falta dbo.Comodatos (CU-162/163/164).',1;

BEGIN TRANSACTION;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Comodato_HistorialPorActivo
    @ActivoId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT ComodatoId, ClienteNombre, FechaAsignacion, FechaDevolucion, Estado,
           ISNULL(DestinoDevolucion, N'') AS DestinoDevolucion, ISNULL(Observaciones, N'') AS Observaciones,
           ISNULL(CondicionEntrega, N'') AS CondicionEntrega, ISNULL(CondicionDevolucion, N'') AS CondicionDevolucion
    FROM dbo.Comodatos WHERE ActivoId = @ActivoId ORDER BY FechaAsignacion DESC;
END;
GO

IF XACT_STATE()<>1 THROW 56403,N'La transacción 0240 no está disponible.',1;
DECLARE @MigrationSha256 NVARCHAR(128)=N'$(MigrationSha256)';
IF LEN(@MigrationSha256)<>64 OR @MigrationSha256 LIKE N'%[^0-9A-Fa-f]%' THROW 56404,N'SHA-256 inválido para 0240.',1;
INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes)
VALUES(N'0240_comodato_historial_condicion',N'0240_comodato_historial_condicion.sql',UPPER(@MigrationSha256),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),
       N'BUG-16-05: sp_Comodato_HistorialPorActivo devuelve la condición de entrega y de devolución.');
COMMIT TRANSACTION;
GO
