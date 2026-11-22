SET NOCOUNT ON;

/* Verificacion de 0337. Solo lectura. */
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0337_rutas_recalcular_numeracion' AND Status = N'Applied')
    THROW 58550, N'0337 no figura aplicada en el ledger.', 1;
IF OBJECT_ID(N'dbo.sp_Rutas_Recalcular', N'P') IS NULL
    THROW 58551, N'Falta dbo.sp_Rutas_Recalcular.', 1;
IF CHARINDEX(N'@Resueltas', OBJECT_DEFINITION(OBJECT_ID(N'dbo.sp_Rutas_Recalcular'))) = 0
    THROW 58552, N'sp_Rutas_Recalcular no numera las paradas pendientes despues de las resueltas.', 1;
-- La prioridad de 0281 se conserva.
IF CHARINDEX(N'Prioridad', OBJECT_DEFINITION(OBJECT_ID(N'dbo.sp_Rutas_Recalcular'))) = 0
    THROW 58553, N'sp_Rutas_Recalcular perdio la regla de prioridad de 0281.', 1;
PRINT N'Verificacion 0337 completada.';
