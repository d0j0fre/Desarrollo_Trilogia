SET NOCOUNT ON;

/* Verificacion de 0240. Solo lectura. */
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0240_comodato_historial_condicion' AND Status = N'Applied')
    THROW 56405, N'0240 no figura aplicada en el ledger.', 1;
IF NOT EXISTS (SELECT 1 FROM sys.dm_exec_describe_first_result_set(N'EXEC dbo.sp_Comodato_HistorialPorActivo @ActivoId = 0', NULL, 0)
               WHERE column_ordinal = 8 AND name = N'CondicionEntrega')
   OR NOT EXISTS (SELECT 1 FROM sys.dm_exec_describe_first_result_set(N'EXEC dbo.sp_Comodato_HistorialPorActivo @ActivoId = 0', NULL, 0)
               WHERE column_ordinal = 9 AND name = N'CondicionDevolucion')
    THROW 56406, N'sp_Comodato_HistorialPorActivo no devuelve CondicionEntrega/CondicionDevolucion al final.', 1;

SELECT TOP 10 ComodatoId, ActivoId, ClienteNombre, CondicionEntrega, CondicionDevolucion
FROM dbo.Comodatos ORDER BY ComodatoId DESC;

PRINT N'0240 verificada.';
