SET NOCOUNT ON;
SET XACT_ABORT ON;

-- Ampliar estados: se agrega 'Aprobado' entre 'Calculado' y 'Pagado'
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_PeriodosPlanilla_Estado')
BEGIN
    BEGIN TRANSACTION;
    ALTER TABLE dbo.PeriodosPlanilla DROP CONSTRAINT CK_PeriodosPlanilla_Estado;
    ALTER TABLE dbo.PeriodosPlanilla ADD CONSTRAINT CK_PeriodosPlanilla_Estado
        CHECK (Estado IN ('Abierto','Calculado','Aprobado','Pagado','Anulado'));
    COMMIT TRANSACTION;
END;
GO

-- Trazabilidad de aprobación, pago y reversión
IF COL_LENGTH('dbo.PeriodosPlanilla', 'UsuarioAprobacionId') IS NULL
BEGIN
    BEGIN TRANSACTION;
    ALTER TABLE dbo.PeriodosPlanilla ADD
        UsuarioAprobacionId INT NULL,
        UsuarioAprobacionNombre NVARCHAR(150) NULL,
        FechaAprobacion DATETIME2 NULL,
        UsuarioPagoId INT NULL,
        UsuarioPagoNombre NVARCHAR(150) NULL,
        FechaPago DATETIME2 NULL,
        UsuarioReversionId INT NULL,
        UsuarioReversionNombre NVARCHAR(150) NULL,
        FechaReversion DATETIME2 NULL,
        MotivoReversion NVARCHAR(500) NULL;
    COMMIT TRANSACTION;
END;
GO

-- CU-113: aprueba una planilla ya calculada, previo a pagarla
CREATE OR ALTER PROCEDURE dbo.sp_Planilla_AprobarPeriodo
    @PeriodoPlanillaId INT,
    @UsuarioAprobacionId INT,
    @UsuarioAprobacionNombre NVARCHAR(150)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    UPDATE dbo.PeriodosPlanilla WITH (UPDLOCK, HOLDLOCK)
    SET Estado = 'Aprobado',
        UsuarioAprobacionId = @UsuarioAprobacionId,
        UsuarioAprobacionNombre = @UsuarioAprobacionNombre,
        FechaAprobacion = SYSDATETIME()
    WHERE PeriodoPlanillaId = @PeriodoPlanillaId AND Estado = 'Calculado';

    IF @@ROWCOUNT <> 1
    BEGIN
        THROW 52006, 'Solo se puede aprobar un período que esté en estado Calculado.', 1;
    END;
END;
GO

-- CU-113/CU-114: marca la planilla como pagada; punto de partida para el envío masivo de boletas
CREATE OR ALTER PROCEDURE dbo.sp_Planilla_MarcarPagado
    @PeriodoPlanillaId INT,
    @UsuarioPagoId INT,
    @UsuarioPagoNombre NVARCHAR(150)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    UPDATE dbo.PeriodosPlanilla WITH (UPDLOCK, HOLDLOCK)
    SET Estado = 'Pagado',
        UsuarioPagoId = @UsuarioPagoId,
        UsuarioPagoNombre = @UsuarioPagoNombre,
        FechaPago = SYSDATETIME()
    WHERE PeriodoPlanillaId = @PeriodoPlanillaId AND Estado = 'Aprobado';

    IF @@ROWCOUNT <> 1
    BEGIN
        THROW 52007, 'Solo se puede pagar un período que esté en estado Aprobado.', 1;
    END;
END;
GO

-- CU-113: revierte un cálculo/aprobación (nunca un período ya Pagado) y limpia el detalle para recalcular
CREATE OR ALTER PROCEDURE dbo.sp_Planilla_RevertirPeriodo
    @PeriodoPlanillaId INT,
    @UsuarioReversionId INT,
    @UsuarioReversionNombre NVARCHAR(150),
    @MotivoReversion NVARCHAR(500)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;

    BEGIN TRY
        DECLARE @Estado NVARCHAR(20);

        SELECT @Estado = Estado
        FROM dbo.PeriodosPlanilla WITH (UPDLOCK, HOLDLOCK)
        WHERE PeriodoPlanillaId = @PeriodoPlanillaId;

        IF @Estado IS NULL
        BEGIN
            THROW 52008, 'No se encontró el período de planilla indicado.', 1;
        END;

        IF @Estado NOT IN ('Calculado', 'Aprobado')
        BEGIN
            THROW 52009, 'Solo se puede revertir un período Calculado o Aprobado (no Pagado).', 1;
        END;

        DELETE FROM dbo.PlanillaDetalle WHERE PeriodoPlanillaId = @PeriodoPlanillaId;

        UPDATE dbo.PeriodosPlanilla
        SET Estado = 'Abierto',
            UsuarioCalculoId = NULL, UsuarioCalculoNombre = NULL, FechaCalculo = NULL,
            UsuarioAprobacionId = NULL, UsuarioAprobacionNombre = NULL, FechaAprobacion = NULL,
            UsuarioReversionId = @UsuarioReversionId,
            UsuarioReversionNombre = @UsuarioReversionNombre,
            FechaReversion = SYSDATETIME(),
            MotivoReversion = @MotivoReversion
        WHERE PeriodoPlanillaId = @PeriodoPlanillaId;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO

INSERT INTO dbo.SchemaMigrationHistory
    (MigrationId, FileName, FileSha256, Status, AppliedBy, EnvironmentName, Notes)
VALUES
    ('0017_planilla_flujo_aprobacion', 'database/migrations/0017_planilla_flujo_aprobacion.sql', 'PENDIENTE_CALCULAR_SHA256',
     'Applied', SUSER_SNAME(), 'DEV', 'CU-113: amplía flujo a Calculado→Aprobado→Pagado, con reversión, alineado a permisos PLANILLA_* ya existentes.');
GO

PRINT 'Migración 0017_planilla_flujo_aprobacion aplicada correctamente.';
GO