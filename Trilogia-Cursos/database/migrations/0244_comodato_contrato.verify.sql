SET NOCOUNT ON;

/* Verificacion de 0244. Solo lectura. */
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0244_comodato_contrato' AND Status = N'Applied')
    THROW 56455, N'0244 no figura aplicada en el ledger.', 1;
IF OBJECT_ID(N'dbo.sp_Comodato_GetContrato', N'P') IS NULL
    THROW 56456, N'Falta dbo.sp_Comodato_GetContrato.', 1;
IF (SELECT COUNT(*) FROM sys.dm_exec_describe_first_result_set(N'EXEC dbo.sp_Comodato_GetContrato @ComodatoId = 0', NULL, 0)) <> 13
    THROW 56457, N'sp_Comodato_GetContrato no devuelve las 13 columnas esperadas.', 1;

DECLARE @Ultimo INT = (SELECT MAX(ComodatoId) FROM dbo.Comodatos);
IF @Ultimo IS NOT NULL EXEC dbo.sp_Comodato_GetContrato @ComodatoId = @Ultimo;

PRINT N'0244 verificada.';
