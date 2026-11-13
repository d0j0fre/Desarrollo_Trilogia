SET NOCOUNT ON;

/* Verificacion de 0254. Deja la base como estaba: la prueba corre en una transaccion que se revierte. */
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0254_activo_marcar_disponible' AND Status = N'Applied')
    THROW 56590, N'0254 no figura aplicada en el ledger.', 1;
IF OBJECT_ID(N'dbo.sp_Activos_MarcarDisponible', N'P') IS NULL
    THROW 56591, N'Falta dbo.sp_Activos_MarcarDisponible.', 1;

BEGIN TRANSACTION;
DECLARE @Id INT;
INSERT dbo.Activos (CodigoActivo, Nombre, Tipo, Estado, Activo, FechaRegistro)
VALUES (N'VERIFY-0254', N'Verificación 0254', N'Refrigerador', N'Mantenimiento', 1, SYSDATETIME());
SET @Id = SCOPE_IDENTITY();

EXEC dbo.sp_Activos_MarcarDisponible @ActivoId = @Id;
IF (SELECT Estado FROM dbo.Activos WHERE ActivoId = @Id) <> N'Disponible'
BEGIN ROLLBACK TRANSACTION; THROW 56592, N'El activo en mantenimiento no quedó Disponible.', 1; END;

BEGIN TRY
    EXEC dbo.sp_Activos_MarcarDisponible @ActivoId = @Id;   -- ya está Disponible
    ROLLBACK TRANSACTION;
    THROW 56593, N'Se aceptó marcar como disponible un activo que no estaba en mantenimiento.', 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() = 56593 THROW;
    IF ERROR_NUMBER() <> 53064 BEGIN IF XACT_STATE() <> 0 ROLLBACK TRANSACTION; THROW; END;
END CATCH;
IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
PRINT N'Verificación 0254 completada.';
