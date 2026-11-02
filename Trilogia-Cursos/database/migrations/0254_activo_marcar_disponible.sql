SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

/* G2-03: un equipo devuelto con destino «Enviar a mantenimiento» quedaba en
   Mantenimiento para siempre. sp_Activos_UpdateInfo no toca el estado (a
   proposito: lo maneja Comodatos), Editar activo no tiene campo de estado y
   Asignar comodato solo lista equipos Disponibles.

   Se agrega dbo.sp_Activos_MarcarDisponible: pasa un activo de Mantenimiento a
   Disponible cuando el equipo ya esta reparado. Solo esa transicion: un activo
   Prestado vuelve por la devolucion del comodato y uno de Baja no se reactiva.
   La pantalla lo ofrece en el detalle del activo, con el permiso que ya exige
   todo el modulo (ACTIVOS_GESTIONAR). No toca Mantenimiento de flota.

   Aditiva: no modifica tablas ni procedimientos existentes. Rollback en
   0254_activo_marcar_disponible.rollback.md. */
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL THROW 56540,N'Falta el ledger 0001.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0254_activo_marcar_disponible' AND Status=N'Applied')
    THROW 56541,N'0254 ya figura aplicada.',1;
IF OBJECT_ID(N'dbo.Activos',N'U') IS NULL OR COL_LENGTH(N'dbo.Activos', N'Estado') IS NULL
    THROW 56542,N'Falta dbo.Activos con la columna Estado.',1;

BEGIN TRANSACTION;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Activos_MarcarDisponible
    @ActivoId INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Estado NVARCHAR(20);
    SELECT @Estado = Estado FROM dbo.Activos WITH (UPDLOCK, HOLDLOCK) WHERE ActivoId = @ActivoId;

    IF @Estado IS NULL THROW 53062, N'No se encontró el activo.', 1;
    IF @Estado <> N'Mantenimiento'
        THROW 53064, N'Solo un activo en mantenimiento se puede marcar como disponible.', 1;

    UPDATE dbo.Activos
    SET Estado = N'Disponible',
        ClientePrestamo = NULL,
        FechaActualizacion = SYSDATETIME()
    WHERE ActivoId = @ActivoId AND Estado = N'Mantenimiento';
END;
GO

IF XACT_STATE()<>1 THROW 56543,N'La transacción 0254 no está disponible.',1;
DECLARE @MigrationSha256 NVARCHAR(128)=N'$(MigrationSha256)';
IF LEN(@MigrationSha256)<>64 OR @MigrationSha256 LIKE N'%[^0-9A-Fa-f]%' THROW 56544,N'SHA-256 inválido para 0254.',1;
INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes)
VALUES(N'0254_activo_marcar_disponible',N'0254_activo_marcar_disponible.sql',UPPER(@MigrationSha256),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),
       N'G2-03: sp_Activos_MarcarDisponible pasa un activo de Mantenimiento a Disponible. Aditiva.');
COMMIT TRANSACTION;
GO
