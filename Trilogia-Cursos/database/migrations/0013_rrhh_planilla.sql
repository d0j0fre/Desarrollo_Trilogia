SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

-- CU-113: parámetros de ley versionados por año fiscal (CCSS, tramos de renta, hora extra)
IF OBJECT_ID(N'dbo.ParametrosPlanilla', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ParametrosPlanilla
    (
        ParametroId INT IDENTITY(1,1) PRIMARY KEY,
        Vigencia INT NOT NULL,
        PorcentajeCcssSem DECIMAL(6,4) NOT NULL,
        PorcentajeCcssIvm DECIMAL(6,4) NOT NULL,
        PorcentajeCcssLpt DECIMAL(6,4) NOT NULL,
        MontoExentoRentaMensual DECIMAL(18,2) NOT NULL,
        TramoRenta1Limite DECIMAL(18,2) NOT NULL,
        TramoRenta1Porcentaje DECIMAL(6,4) NOT NULL,
        TramoRenta2Limite DECIMAL(18,2) NOT NULL,
        TramoRenta2Porcentaje DECIMAL(6,4) NOT NULL,
        TramoRenta3Limite DECIMAL(18,2) NOT NULL,
        TramoRenta3Porcentaje DECIMAL(6,4) NOT NULL,
        TramoRenta4Porcentaje DECIMAL(6,4) NOT NULL,
        CreditoFiscalHijo DECIMAL(18,2) NOT NULL,
        CreditoFiscalConyuge DECIMAL(18,2) NOT NULL,
        FactorHoraExtra DECIMAL(4,2) NOT NULL CONSTRAINT DF_ParametrosPlanilla_FactorHoraExtra DEFAULT 1.50,
        MaxHorasExtraDiarias DECIMAL(4,2) NOT NULL CONSTRAINT DF_ParametrosPlanilla_MaxHorasExtra DEFAULT 4.00,
        Activo BIT NOT NULL CONSTRAINT DF_ParametrosPlanilla_Activo DEFAULT 1,
        FechaCreacion DATETIME2 NOT NULL CONSTRAINT DF_ParametrosPlanilla_FechaCreacion DEFAULT SYSDATETIME(),
        CONSTRAINT UQ_ParametrosPlanilla_Vigencia UNIQUE (Vigencia)
    );
END;

-- CU-112: horas ordinarias, extra y ausencias, registradas exclusivamente por el supervisor
IF OBJECT_ID(N'dbo.RegistroHorasEmpleado', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RegistroHorasEmpleado
    (
        RegistroHorasId INT IDENTITY(1,1) PRIMARY KEY,
        EmpleadoId INT NOT NULL,
        Fecha DATE NOT NULL,
        HorasOrdinarias DECIMAL(5,2) NOT NULL CONSTRAINT DF_RegistroHoras_Ordinarias DEFAULT 0,
        HorasExtra DECIMAL(5,2) NOT NULL CONSTRAINT DF_RegistroHoras_Extra DEFAULT 0,
        EsAusencia BIT NOT NULL CONSTRAINT DF_RegistroHoras_Ausencia DEFAULT 0,
        TipoAusencia NVARCHAR(60) NULL,
        Observaciones NVARCHAR(500) NULL,
        UsuarioSupervisorId INT NOT NULL,
        UsuarioSupervisorNombre NVARCHAR(150) NOT NULL,
        FechaRegistro DATETIME2 NOT NULL CONSTRAINT DF_RegistroHoras_FechaRegistro DEFAULT SYSDATETIME(),
        CONSTRAINT FK_RegistroHoras_Empleado FOREIGN KEY (EmpleadoId) REFERENCES dbo.Empleados(EmpleadoId),
        CONSTRAINT FK_RegistroHoras_Supervisor FOREIGN KEY (UsuarioSupervisorId) REFERENCES dbo.Usuarios(UsuarioId),
        CONSTRAINT CK_RegistroHoras_Ausencia CHECK ((EsAusencia = 1 AND TipoAusencia IS NOT NULL) OR (EsAusencia = 0)),
        CONSTRAINT UQ_RegistroHoras_EmpleadoFecha UNIQUE (EmpleadoId, Fecha)
    );
END;

-- CU-113: cabecera de cada corrida de planilla (quincenal o mensual, configurable)
IF OBJECT_ID(N'dbo.PeriodosPlanilla', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PeriodosPlanilla
    (
        PeriodoPlanillaId INT IDENTITY(1,1) PRIMARY KEY,
        TipoPeriodo NVARCHAR(20) NOT NULL,
        FechaInicio DATE NOT NULL,
        FechaFin DATE NOT NULL,
        Estado NVARCHAR(20) NOT NULL CONSTRAINT DF_PeriodosPlanilla_Estado DEFAULT 'Abierto',
        ParametroId INT NOT NULL,
        UsuarioCalculoId INT NULL,
        UsuarioCalculoNombre NVARCHAR(150) NULL,
        FechaCalculo DATETIME2 NULL,
        FechaCreacion DATETIME2 NOT NULL CONSTRAINT DF_PeriodosPlanilla_FechaCreacion DEFAULT SYSDATETIME(),
        CONSTRAINT FK_PeriodosPlanilla_Parametro FOREIGN KEY (ParametroId) REFERENCES dbo.ParametrosPlanilla(ParametroId),
        CONSTRAINT CK_PeriodosPlanilla_Tipo CHECK (TipoPeriodo IN ('Quincenal','Mensual')),
        CONSTRAINT CK_PeriodosPlanilla_Estado CHECK (Estado IN ('Abierto','Calculado','Pagado','Anulado')),
        CONSTRAINT CK_PeriodosPlanilla_Fechas CHECK (FechaFin >= FechaInicio),
        CONSTRAINT UQ_PeriodosPlanilla_Rango UNIQUE (FechaInicio, FechaFin, TipoPeriodo)
    );
END;

-- CU-113: boleta calculada por empleado dentro de un período
IF OBJECT_ID(N'dbo.PlanillaDetalle', N'U') IS NULL
BEGIN
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
END;

-- CU-114: bitácora de envío de boleta por correo, útil para diagnosticar fallos de SMTP
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

-- Parámetros de ley 2026 (Decreto 45333-H): CCSS 10.83% obrero, tramos de renta vigentes desde enero 2026
IF NOT EXISTS (SELECT 1 FROM dbo.ParametrosPlanilla WHERE Vigencia = 2026)
BEGIN
    INSERT INTO dbo.ParametrosPlanilla
        (Vigencia, PorcentajeCcssSem, PorcentajeCcssIvm, PorcentajeCcssLpt,
         MontoExentoRentaMensual,
         TramoRenta1Limite, TramoRenta1Porcentaje,
         TramoRenta2Limite, TramoRenta2Porcentaje,
         TramoRenta3Limite, TramoRenta3Porcentaje,
         TramoRenta4Porcentaje,
         CreditoFiscalHijo, CreditoFiscalConyuge)
    VALUES
        (2026, 5.50, 5.33, 1.00,
         918000.00,
         1347000.00, 10.00,
         2364000.00, 15.00,
         4727000.00, 20.00,
         25.00,
         1710.00, 2590.00);
END;

COMMIT TRANSACTION;
GO

-- CU-112: alta o corrección de horas del día (un registro por empleado/fecha), solo supervisor
CREATE OR ALTER PROCEDURE dbo.sp_RRHH_RegistrarHorasEmpleado
    @EmpleadoId INT,
    @Fecha DATE,
    @HorasOrdinarias DECIMAL(5,2) = 0,
    @HorasExtra DECIMAL(5,2) = 0,
    @EsAusencia BIT = 0,
    @TipoAusencia NVARCHAR(60) = NULL,
    @Observaciones NVARCHAR(500) = NULL,
    @UsuarioSupervisorId INT,
    @UsuarioSupervisorNombre NVARCHAR(150)
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.Empleados WHERE EmpleadoId = @EmpleadoId AND Activo = 1)
    BEGIN
        THROW 52000, 'No se encontró un empleado activo con el Id indicado.', 1;
    END;

    IF EXISTS (SELECT 1 FROM dbo.RegistroHorasEmpleado WHERE EmpleadoId = @EmpleadoId AND Fecha = @Fecha)
    BEGIN
        UPDATE dbo.RegistroHorasEmpleado
        SET HorasOrdinarias = @HorasOrdinarias,
            HorasExtra = @HorasExtra,
            EsAusencia = @EsAusencia,
            TipoAusencia = @TipoAusencia,
            Observaciones = @Observaciones,
            UsuarioSupervisorId = @UsuarioSupervisorId,
            UsuarioSupervisorNombre = @UsuarioSupervisorNombre
        WHERE EmpleadoId = @EmpleadoId AND Fecha = @Fecha;
    END
    ELSE
    BEGIN
        INSERT INTO dbo.RegistroHorasEmpleado
            (EmpleadoId, Fecha, HorasOrdinarias, HorasExtra, EsAusencia, TipoAusencia, Observaciones, UsuarioSupervisorId, UsuarioSupervisorNombre)
        VALUES
            (@EmpleadoId, @Fecha, @HorasOrdinarias, @HorasExtra, @EsAusencia, @TipoAusencia, @Observaciones, @UsuarioSupervisorId, @UsuarioSupervisorNombre);
    END;

    SELECT SCOPE_IDENTITY() AS RegistroHorasId;
END;
GO

-- CU-112: listado de horas registradas en un rango, para revisión previa al cálculo de planilla
CREATE OR ALTER PROCEDURE dbo.sp_RRHH_ListarHorasPorPeriodo
    @FechaInicio DATE,
    @FechaFin DATE,
    @EmpleadoId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        r.RegistroHorasId, r.EmpleadoId, u.NombreCompleto, r.Fecha,
        r.HorasOrdinarias, r.HorasExtra, r.EsAusencia, r.TipoAusencia,
        r.Observaciones, r.UsuarioSupervisorNombre
    FROM dbo.RegistroHorasEmpleado r
    INNER JOIN dbo.Empleados e ON e.EmpleadoId = r.EmpleadoId
    INNER JOIN dbo.Usuarios u ON u.UsuarioId = e.UsuarioId
    WHERE r.Fecha BETWEEN @FechaInicio AND @FechaFin
      AND (@EmpleadoId IS NULL OR r.EmpleadoId = @EmpleadoId)
    ORDER BY r.Fecha, u.NombreCompleto;
END;
GO

-- CU-113: abre un nuevo período de planilla y lo vincula a los parámetros de ley vigentes
CREATE OR ALTER PROCEDURE dbo.sp_Planilla_CrearPeriodo
    @TipoPeriodo NVARCHAR(20),
    @FechaInicio DATE,
    @FechaFin DATE
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @ParametroId INT = (
        SELECT TOP 1 ParametroId FROM dbo.ParametrosPlanilla
        WHERE Vigencia = YEAR(@FechaFin) AND Activo = 1
    );

    IF @ParametroId IS NULL
    BEGIN
        THROW 52001, 'No existen parámetros de ley vigentes para el año del período.', 1;
    END;

    IF EXISTS (SELECT 1 FROM dbo.PeriodosPlanilla WHERE FechaInicio = @FechaInicio AND FechaFin = @FechaFin AND TipoPeriodo = @TipoPeriodo)
    BEGIN
        THROW 52002, 'Ya existe un período de planilla con ese rango y tipo.', 1;
    END;

    INSERT INTO dbo.PeriodosPlanilla (TipoPeriodo, FechaInicio, FechaFin, ParametroId)
    VALUES (@TipoPeriodo, @FechaInicio, @FechaFin, @ParametroId);

    SELECT SCOPE_IDENTITY() AS PeriodoPlanillaId;
END;
GO

-- CU-113: motor de cálculo (CCSS 10.83%, renta por tramos, horas extra 1.5x); bloqueo atómico contra doble cálculo
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

-- CU-113/CU-114: detalle de boleta para mostrar en pantalla o adjuntar al correo
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

-- CU-114: registra el resultado del envío de la boleta por correo, para poder auditar fallos de SMTP
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

-- Registro en el ledger de migraciones; el SHA-256 real se actualiza al momento de ejecutar en Azure
INSERT INTO dbo.SchemaMigrationHistory
    (MigrationId, FileName, FileSha256, Status, AppliedBy, EnvironmentName, Notes)
VALUES
    ('0013_rrhh_planilla', 'database/migrations/0013_rrhh_planilla.sql', 'PENDIENTE_CALCULAR_SHA256',
     'Applied', SUSER_SNAME(), 'DEV', 'CU-111 a CU-114: expedientes, horas/ausencias, cálculo de planilla y envío de boletas.');
GO

PRINT 'Migración 0013_rrhh_planilla aplicada correctamente.';
GO