/*
  DEMO-2026-08 - additive, idempotent seed for DistribuidoraJJ_DB_DEV only.
  Run with the target database selected explicitly. This script never changes
  existing business rows and does not create users or credentials.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @BatchCode NVARCHAR(30) = N'DEMO-2026-08';
DECLARE @ExpectedDatabase SYSNAME = N'DistribuidoraJJ_DB_DEV';

IF DB_NAME() <> @ExpectedDatabase
    THROW 56000, N'Este seed solo puede ejecutarse en DistribuidoraJJ_DB_DEV.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.DemoSeedBatches', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.DemoSeedBatches
        (
            DemoSeedBatchId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_DemoSeedBatches PRIMARY KEY,
            BatchCode NVARCHAR(30) NOT NULL CONSTRAINT UQ_DemoSeedBatches_BatchCode UNIQUE,
            AppliedAtUtc DATETIME2(0) NOT NULL CONSTRAINT DF_DemoSeedBatches_AppliedAtUtc DEFAULT SYSUTCDATETIME(),
            Notes NVARCHAR(500) NOT NULL
        );
    END;

    IF EXISTS (SELECT 1 FROM dbo.DemoSeedBatches WHERE BatchCode = @BatchCode)
    BEGIN
        COMMIT;
        PRINT N'DEMO-2026-08 ya fue aplicado; no se insertaron duplicados.';
        RETURN;
    END;

    IF OBJECT_ID(N'dbo.CategoriasGasto', N'U') IS NOT NULL
    BEGIN
        INSERT dbo.CategoriasGasto(Codigo, Nombre, Activo)
        SELECT source.Codigo, source.Nombre, 1
        FROM (VALUES
            (N'DEMO-2026-08-OPER', N'DEMO-2026-08 Operación'),
            (N'DEMO-2026-08-COMER', N'DEMO-2026-08 Comercial'),
            (N'DEMO-2026-08-LOG', N'DEMO-2026-08 Logística')) source(Codigo, Nombre)
        WHERE NOT EXISTS (SELECT 1 FROM dbo.CategoriasGasto target WHERE target.Codigo = source.Codigo);
    END;

    /* Budget rows are deliberately limited to the active demo category and an
       existing department/user. Financial figures are clearly QA-only. */
    IF OBJECT_ID(N'dbo.PresupuestosAnuales', N'U') IS NOT NULL
       AND OBJECT_ID(N'dbo.DepartamentosOperativos', N'U') IS NOT NULL
       AND OBJECT_ID(N'dbo.Usuarios', N'U') IS NOT NULL
    BEGIN
        DECLARE @DepartmentId INT = (SELECT TOP (1) DepartamentoId FROM dbo.DepartamentosOperativos WHERE Activo = 1 ORDER BY DepartamentoId);
        DECLARE @ActorId INT = (SELECT TOP (1) UsuarioId FROM dbo.Usuarios WHERE Activo = 1 ORDER BY UsuarioId);
        DECLARE @ActorName NVARCHAR(150) = (SELECT TOP (1) NombreCompleto FROM dbo.Usuarios WHERE UsuarioId = @ActorId);
        DECLARE @CategoryId INT = (SELECT TOP (1) CategoriaId FROM dbo.CategoriasGasto WHERE Codigo = N'DEMO-2026-08-OPER');

        IF @DepartmentId IS NOT NULL AND @ActorId IS NOT NULL AND @CategoryId IS NOT NULL
           AND NOT EXISTS
           (
               SELECT 1 FROM dbo.PresupuestosAnuales
               WHERE Anio = YEAR(SYSUTCDATETIME()) AND DepartamentoId = @DepartmentId
                 AND Notas = N'DEMO-2026-08: presupuesto QA aditivo.'
           )
        BEGIN
            INSERT dbo.PresupuestosAnuales(Anio, DepartamentoId, MontoAnual, Notas, CreadoPorUsuarioId, CreadoPorNombre)
            VALUES(YEAR(SYSUTCDATETIME()), @DepartmentId, 1200000.00, N'DEMO-2026-08: presupuesto QA aditivo.', @ActorId, COALESCE(@ActorName, N'DEMO-2026-08'));

            DECLARE @BudgetId INT = CONVERT(INT, SCOPE_IDENTITY());
            ;WITH Months AS (SELECT 1 AS MonthNumber UNION ALL SELECT MonthNumber + 1 FROM Months WHERE MonthNumber < 12)
            INSERT dbo.PresupuestoDetalles(PresupuestoId, CategoriaId, Mes, MontoAsignado, Notas)
            SELECT @BudgetId, @CategoryId, MonthNumber, CASE WHEN MonthNumber = 12 THEN 100000.00 ELSE 100000.00 END, N'DEMO-2026-08: línea QA.'
            FROM Months;

            INSERT dbo.PresupuestoAuditoria(PresupuestoId, Accion, UsuarioId, UsuarioNombre, Detalle)
            VALUES(@BudgetId, N'Crear', @ActorId, COALESCE(@ActorName, N'DEMO-2026-08'), N'DEMO-2026-08: presupuesto QA creado por seed aditivo.');
        END;
    END;

    INSERT dbo.DemoSeedBatches(BatchCode, Notes)
    VALUES(@BatchCode, N'Seed QA aditivo. Sin usuarios, contraseñas ni modificación de filas existentes.');

    COMMIT;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK;
    THROW;
END CATCH;

SELECT
    BatchCode = @BatchCode,
    BudgetsCreated = (SELECT COUNT(*) FROM dbo.PresupuestosAnuales WHERE Notas = N'DEMO-2026-08: presupuesto QA aditivo.'),
    AppliedAtUtc = (SELECT AppliedAtUtc FROM dbo.DemoSeedBatches WHERE BatchCode = @BatchCode);
