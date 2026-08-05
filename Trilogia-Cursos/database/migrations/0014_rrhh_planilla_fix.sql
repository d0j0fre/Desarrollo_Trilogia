SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

-- La tabla previa "PlanillaDetalle" no correspondía a este módulo (otra estructura, sin relación); estaba vacía
IF OBJECT_ID(N'dbo.PlanillaDetalle', N'U') IS NOT NULL
BEGIN
    IF EXISTS (SELECT 1 FROM dbo.PlanillaDetalle)
    BEGIN
        THROW 52010, 'PlanillaDetalle contiene datos; revisar manualmente antes de continuar.', 1;
    END;
    DROP TABLE dbo.PlanillaDetalle;
END;

-- CU-113: boleta calculada por empleado dentro de un período (estructura correcta)
CREATE TABLE dbo.PlanillaDetalle
(
    PlanillaDetalleId INT IDENTITY(1,1) PRIMARY KEY,
    PeriodoPlanillaId INT NOT NULL,
    EmpleadoId INT NOT NULL,
    SalarioBase DECIMAL(18,2) NOT NULL,
    HorasExtraPagadas DECIMAL(5,2) NOT NULL CONSTRAINT DF_PlanillaDetalle_HorasExtra DEFAULT 0,
    MontoHorasExtra DECIMAL(18,2) NOT NULL CONSTRAINT DF_PlanillaDetalle_MontoHorasExtra DEFAULT 0,
    MontoComisiones DECIMAL(18,2) NOT NULL CONSTRAINT DF_PlanillaDetalle_Comisiones DEFAULT 0,
    SalarioBruto DECIMAL(18,2) NOT NULL,
    DeduccionCcss DECIMAL(18,2) NOT NULL CONSTRAINT DF_PlanillaDetalle_Ccss DEFAULT 0,
    DeduccionRenta DECIMAL(18,2) NOT NULL CONSTRAINT DF_PlanillaDetalle_Renta DEFAULT 0,
    TotalDeducciones DECIMAL(18,2) NOT NULL CONSTRAINT DF_PlanillaDetalle_TotalDeducciones DEFAULT 0,
    SalarioNeto DECIMAL(18,2) NOT NULL,
    FechaCalculo DATETIME2 NOT NULL CONSTRAINT DF_PlanillaDetalle_FechaCalculo DEFAULT SYSDATETIME(),
    CONSTRAINT FK_PlanillaDetalle_Periodo FOREIGN KEY (PeriodoPlanillaId) REFERENCES dbo.PeriodosPlanilla(PeriodoPlanillaId),
    CONSTRAINT FK_PlanillaDetalle_Empleado FOREIGN KEY (EmpleadoId) REFERENCES dbo.Empleados(EmpleadoId),
    CONSTRAINT UQ_PlanillaDetalle_PeriodoEmpleado UNIQUE (PeriodoPlanillaId, EmpleadoId)
);

-- CU-114: bitácora de envío de boleta por correo (no se creó en 0013 por el fallo del FK)
IF OBJECT_ID(N'dbo.PlanillaEnvioComprobante', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PlanillaEnvioComprobante
    (
        EnvioComprobanteId INT IDENTITY(1,1) PRIMARY KEY,
        PlanillaDetalleId INT NOT NULL,
        CorreoDestino NVARCHAR(300) NOT NULL,
        Exitoso BIT NOT NULL,
        MensajeError NVARCHAR(1000) NULL,
        FechaIntento DATETIME2 NOT NULL CONSTRAINT DF_PlanillaEnvio_FechaIntento DEFAULT SYSDATETIME(),
        CONSTRAINT FK_PlanillaEnvio_Detalle FOREIGN KEY (PlanillaDetalleId) REFERENCES dbo.PlanillaDetalle(PlanillaDetalleId)
    );
END;

COMMIT TRANSACTION;
GO

-- Recrear: no se pudo crear en 0013 por columnas inválidas contra la tabla vieja
CREATE OR ALTER PROCEDURE dbo.sp_Planilla_CalcularPeriodo
    @PeriodoPlanillaId INT,
    @UsuarioCalculoId INT,
    @UsuarioCalculoNombre NVARCHAR(150)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;

    BEGIN TRY
        DECLARE @FechaInicio DATE, @FechaFin DATE, @ParametroId INT, @Estado NVARCHAR(20);

        SELECT
            @FechaInicio = FechaInicio, @FechaFin = FechaFin,
            @ParametroId = ParametroId, @Estado = Estado
        FROM dbo.PeriodosPlanilla WITH (UPDLOCK, HOLDLOCK)
        WHERE PeriodoPlanillaId = @PeriodoPlanillaId;

        IF @FechaInicio IS NULL
        BEGIN
            THROW 52003, 'No se encontró el período de planilla indicado.', 1;
        END;

        IF @Estado <> 'Abierto'
        BEGIN
            THROW 52004, 'El período ya fue calculado o no está disponible para cálculo.', 1;
        END;

        DECLARE
            @CcssTotal DECIMAL(6,4), @MontoExento DECIMAL(18,2),
            @T1Limite DECIMAL(18,2), @T1Pct DECIMAL(6,4),
            @T2Limite DECIMAL(18,2), @T2Pct DECIMAL(6,4),
            @T3Limite DECIMAL(18,2), @T3Pct DECIMAL(6,4),
            @T4Pct DECIMAL(6,4), @FactorHoraExtra DECIMAL(4,2);

        SELECT
            @CcssTotal = PorcentajeCcssSem + PorcentajeCcssIvm + PorcentajeCcssLpt,
            @MontoExento = MontoExentoRentaMensual,
            @T1Limite = TramoRenta1Limite, @T1Pct = TramoRenta1Porcentaje,
            @T2Limite = TramoRenta2Limite, @T2Pct = TramoRenta2Porcentaje,
            @T3Limite = TramoRenta3Limite, @T3Pct = TramoRenta3Porcentaje,
            @T4Pct = TramoRenta4Porcentaje,
            @FactorHoraExtra = FactorHoraExtra
        FROM dbo.ParametrosPlanilla
        WHERE ParametroId = @ParametroId;

        ;WITH Horas AS (
            SELECT EmpleadoId, SUM(HorasExtra) AS TotalHorasExtra
            FROM dbo.RegistroHorasEmpleado
            WHERE Fecha BETWEEN @FechaInicio AND @FechaFin
            GROUP BY EmpleadoId
        ),
        Base AS (
            SELECT
                e.EmpleadoId, e.Salario AS SalarioBase,
                ISNULL(h.TotalHorasExtra, 0) AS HorasExtraPagadas,
                ROUND((e.Salario / 30.0 / 8.0) * @FactorHoraExtra * ISNULL(h.TotalHorasExtra, 0), 2) AS MontoHorasExtra
            FROM dbo.Empleados e
            LEFT JOIN Horas h ON h.EmpleadoId = e.EmpleadoId
            WHERE e.Activo = 1
        ),
        Bruto AS (
            SELECT *, (SalarioBase + MontoHorasExtra) AS SalarioBruto FROM Base
        ),
        Deducciones AS (
            SELECT *, ROUND(SalarioBruto * @CcssTotal / 100.0, 2) AS DeduccionCcss FROM Bruto
        ),
        Renta AS (
            SELECT *, (SalarioBruto - DeduccionCcss) AS BaseImponible FROM Deducciones
        ),
        RentaCalculada AS (
            SELECT *,
                CASE
                    WHEN BaseImponible <= @MontoExento THEN 0
                    WHEN BaseImponible <= @T1Limite THEN
                        (BaseImponible - @MontoExento) * @T1Pct / 100.0
                    WHEN BaseImponible <= @T2Limite THEN
                        (@T1Limite - @MontoExento) * @T1Pct / 100.0 +
                        (BaseImponible - @T1Limite) * @T2Pct / 100.0
                    WHEN BaseImponible <= @T3Limite THEN
                        (@T1Limite - @MontoExento) * @T1Pct / 100.0 +
                        (@T2Limite - @T1Limite) * @T2Pct / 100.0 +
                        (BaseImponible - @T2Limite) * @T3Pct / 100.0
                    ELSE
                        (@T1Limite - @MontoExento) * @T1Pct / 100.0 +
                        (@T2Limite - @T1Limite) * @T2Pct / 100.0 +
                        (@T3Limite - @T2Limite) * @T3Pct / 100.0 +
                        (BaseImponible - @T3Limite) * @T4Pct / 100.0
                END AS DeduccionRentaCalc
            FROM Renta
        )
        INSERT INTO dbo.PlanillaDetalle
            (PeriodoPlanillaId, EmpleadoId, SalarioBase, HorasExtraPagadas, MontoHorasExtra,
             MontoComisiones, SalarioBruto, DeduccionCcss, DeduccionRenta, TotalDeducciones, SalarioNeto)
        SELECT
            @PeriodoPlanillaId, EmpleadoId, SalarioBase, HorasExtraPagadas, MontoHorasExtra,
            0, -- Comisiones: pendiente integrar con módulo de Ventas (Gerald)
            SalarioBruto, DeduccionCcss, ROUND(DeduccionRentaCalc, 2),
            ROUND(DeduccionCcss + DeduccionRentaCalc, 2),
            ROUND(SalarioBruto - DeduccionCcss - DeduccionRentaCalc, 2)
        FROM RentaCalculada;

        UPDATE dbo.PeriodosPlanilla
        SET Estado = 'Calculado',
            UsuarioCalculoId = @UsuarioCalculoId,
            UsuarioCalculoNombre = @UsuarioCalculoNombre,
            FechaCalculo = SYSDATETIME()
        WHERE PeriodoPlanillaId = @PeriodoPlanillaId;

        COMMIT TRANSACTION;

        SELECT * FROM dbo.PlanillaDetalle WHERE PeriodoPlanillaId = @PeriodoPlanillaId;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Planilla_ObtenerDetalleEmpleado
    @PlanillaDetalleId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        pd.*, u.NombreCompleto, u.Correo,
        pp.TipoPeriodo, pp.FechaInicio, pp.FechaFin
    FROM dbo.PlanillaDetalle pd
    INNER JOIN dbo.Empleados e ON e.EmpleadoId = pd.EmpleadoId
    INNER JOIN dbo.Usuarios u ON u.UsuarioId = e.UsuarioId
    INNER JOIN dbo.PeriodosPlanilla pp ON pp.PeriodoPlanillaId = pd.PeriodoPlanillaId
    WHERE pd.PlanillaDetalleId = @PlanillaDetalleId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Planilla_MarcarComprobanteEnviado
    @PlanillaDetalleId INT,
    @CorreoDestino NVARCHAR(300),
    @Exitoso BIT,
    @MensajeError NVARCHAR(1000) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.PlanillaDetalle WHERE PlanillaDetalleId = @PlanillaDetalleId)
    BEGIN
        THROW 52005, 'No se encontró el detalle de planilla indicado.', 1;
    END;

    INSERT INTO dbo.PlanillaEnvioComprobante (PlanillaDetalleId, CorreoDestino, Exitoso, MensajeError)
    VALUES (@PlanillaDetalleId, @CorreoDestino, @Exitoso, @MensajeError);
END;
GO

INSERT INTO dbo.SchemaMigrationHistory
    (MigrationId, FileName, FileSha256, Status, AppliedBy, EnvironmentName, Notes)
VALUES
    ('0014_rrhh_planilla_fix', 'database/migrations/0014_rrhh_planilla_fix.sql', 'PENDIENTE_CALCULAR_SHA256',
     'Applied', SUSER_SNAME(), 'DEV', 'Corrige colisión de PlanillaDetalle preexistente y recrea objetos que fallaron en 0013.');
GO

PRINT 'Migración 0014_rrhh_planilla_fix aplicada correctamente.';
GO