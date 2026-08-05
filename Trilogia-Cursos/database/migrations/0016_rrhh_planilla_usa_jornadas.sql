SET NOCOUNT ON;
SET XACT_ABORT ON;

-- Confirmar que RegistroHorasEmpleado sigue vacía antes de eliminarla (nunca se usó en producción)
IF OBJECT_ID(N'dbo.RegistroHorasEmpleado', N'U') IS NOT NULL
BEGIN
    IF EXISTS (SELECT 1 FROM dbo.RegistroHorasEmpleado)
    BEGIN
        THROW 52020, 'RegistroHorasEmpleado contiene datos; revisar manualmente antes de continuar.', 1;
    END;

    BEGIN TRANSACTION;
    DROP TABLE dbo.RegistroHorasEmpleado;
    COMMIT TRANSACTION;
END;
GO

-- Se reemplazan por el flujo real de Danny (empleado registra, supervisor aprueba vía EmpleadoJornadas)
IF OBJECT_ID(N'dbo.sp_RRHH_RegistrarHorasEmpleado', N'P') IS NOT NULL
    DROP PROCEDURE dbo.sp_RRHH_RegistrarHorasEmpleado;
GO

IF OBJECT_ID(N'dbo.sp_RRHH_ListarHorasPorPeriodo', N'P') IS NOT NULL
    DROP PROCEDURE dbo.sp_RRHH_ListarHorasPorPeriodo;
GO

-- CU-113: motor de cálculo, ahora toma las horas extra desde EmpleadoJornadas (solo jornadas Aprobadas)
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

        -- Fuente de horas extra: EmpleadoJornadas (Danny), solo jornadas ya aprobadas por el supervisor
        ;WITH Horas AS (
            SELECT EmpleadoId, SUM(HorasExtra) AS TotalHorasExtra
            FROM dbo.EmpleadoJornadas
            WHERE Fecha BETWEEN @FechaInicio AND @FechaFin
              AND Estado = N'Aprobada'
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

INSERT INTO dbo.SchemaMigrationHistory
    (MigrationId, FileName, FileSha256, Status, AppliedBy, EnvironmentName, Notes)
VALUES
    ('0016_rrhh_planilla_usa_jornadas', 'database/migrations/0016_rrhh_planilla_usa_jornadas.sql', 'PENDIENTE_CALCULAR_SHA256',
     'Applied', SUSER_SNAME(), 'DEV', 'Elimina RegistroHorasEmpleado duplicada; sp_Planilla_CalcularPeriodo ahora usa dbo.EmpleadoJornadas (Estado=Aprobada) de CU-112.');
GO

PRINT 'Migración 0016_rrhh_planilla_usa_jornadas aplicada correctamente.';
GO