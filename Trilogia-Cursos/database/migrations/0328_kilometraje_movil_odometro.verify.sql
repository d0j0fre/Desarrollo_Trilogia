SET NOCOUNT ON;

/* Verificacion de 0328. Solo lectura. */
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0328_kilometraje_movil_odometro' AND Status = N'Applied')
    THROW 58380, N'0328 no figura aplicada en el ledger.', 1;
IF OBJECT_ID(N'dbo.sp_Chofer_Kilometraje_Abrir', N'P') IS NULL
    THROW 58381, N'Falta dbo.sp_Chofer_Kilometraje_Abrir.', 1;
IF CHARINDEX(N'53046', OBJECT_DEFINITION(OBJECT_ID(N'dbo.sp_Chofer_Kilometraje_Abrir'))) = 0
    THROW 58382, N'sp_Chofer_Kilometraje_Abrir no valida el odómetro (53046).', 1;
PRINT N'Verificacion 0328 completada.';
