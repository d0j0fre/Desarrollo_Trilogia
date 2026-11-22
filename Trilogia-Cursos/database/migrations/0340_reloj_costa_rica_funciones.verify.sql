SET NOCOUNT ON;

/* Verificacion de 0340. Solo lectura. */
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0340_reloj_costa_rica_funciones' AND Status = N'Applied')
    THROW 58940, N'0340 no figura aplicada en el ledger.', 1;
IF OBJECT_ID(N'dbo.fn_AhoraCR', N'FN') IS NULL OR OBJECT_ID(N'dbo.fn_HoyCR', N'FN') IS NULL
   OR OBJECT_ID(N'dbo.fn_FechaHoraCR', N'FN') IS NULL OR OBJECT_ID(N'dbo.fn_DiaCRaReloj', N'FN') IS NULL
    THROW 58941, N'Falta alguna de las funciones del reloj de Costa Rica.', 1;

-- Costa Rica es UTC-6 todo el anio: «ahora» de Costa Rica = UTC - 6 horas (tolerancia de 5 segundos entre llamadas).
IF ABS(DATEDIFF(SECOND, dbo.fn_AhoraCR(), DATEADD(HOUR, -6, SYSUTCDATETIME()))) > 5
    THROW 58942, N'dbo.fn_AhoraCR no devuelve la hora de Costa Rica.', 1;
IF dbo.fn_HoyCR() <> CAST(dbo.fn_AhoraCR() AS DATE) AND dbo.fn_HoyCR() <> CAST(DATEADD(SECOND, -5, dbo.fn_AhoraCR()) AS DATE)
    THROW 58943, N'dbo.fn_HoyCR no devuelve el dia de Costa Rica.', 1;

-- Una marca del reloj del servidor SQL pasa a hora de Costa Rica; ida y vuelta devuelve el mismo valor.
DECLARE @Marca DATETIME2(7) = SYSDATETIME();
DECLARE @CR DATETIME2(7) = dbo.fn_FechaHoraCR(@Marca);
IF ABS(DATEDIFF(SECOND, @CR, dbo.fn_AhoraCR())) > 5
    THROW 58944, N'dbo.fn_FechaHoraCR no convierte la hora actual del servidor SQL a Costa Rica.', 1;
IF dbo.fn_DiaCRaReloj(@CR) <> @Marca
    THROW 58945, N'dbo.fn_DiaCRaReloj no es la inversa de dbo.fn_FechaHoraCR.', 1;
-- Una fecha sin hora no se mueve.
IF dbo.fn_FechaHoraCR(CAST(N'2026-11-21T00:00:00' AS DATETIME2(7))) <> CAST(N'2026-11-21T00:00:00' AS DATETIME2(7))
    THROW 58946, N'dbo.fn_FechaHoraCR movio una fecha sin hora.', 1;
IF dbo.fn_FechaHoraCR(NULL) IS NOT NULL
    THROW 58947, N'dbo.fn_FechaHoraCR(NULL) debe ser NULL.', 1;
PRINT N'Verificacion 0340 completada.';
