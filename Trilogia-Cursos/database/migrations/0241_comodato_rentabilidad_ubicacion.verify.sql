SET NOCOUNT ON;

/* Verificacion de 0241. Solo lectura. */
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0241_comodato_rentabilidad_ubicacion' AND Status = N'Applied')
    THROW 56415, N'0241 no figura aplicada en el ledger.', 1;
IF NOT EXISTS (SELECT 1 FROM sys.dm_exec_describe_first_result_set(N'EXEC dbo.sp_Comodato_Rentabilidad', NULL, 0)
               WHERE column_ordinal = 12 AND name = N'Ubicacion')
    THROW 56416, N'sp_Comodato_Rentabilidad no devuelve Ubicacion en la posición 12.', 1;

EXEC dbo.sp_Comodato_Rentabilidad;

PRINT N'0241 verificada.';
