SET NOCOUNT ON;

/* Verificacion de 0327. Solo lectura. */
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0327_avisos_reclamo_meta_departamento' AND Status = N'Applied')
    THROW 58370, N'0327 no figura aplicada en el ledger.', 1;
IF OBJECT_ID(N'dbo.TR_Reclamos_NotificarCierre', N'TR') IS NULL
    THROW 58371, N'Falta el disparador dbo.TR_Reclamos_NotificarCierre.', 1;
IF OBJECT_ID(N'dbo.TR_MetasVendedor_NotificarVendedor', N'TR') IS NULL
    THROW 58372, N'Falta el disparador dbo.TR_MetasVendedor_NotificarVendedor.', 1;
IF OBJECT_ID(N'dbo.TR_ChatDepartamentoMiembros_NotificarMiembro', N'TR') IS NULL
    THROW 58373, N'Falta el disparador dbo.TR_ChatDepartamentoMiembros_NotificarMiembro.', 1;
IF EXISTS (SELECT 1 FROM sys.triggers WHERE name IN (N'TR_Reclamos_NotificarCierre', N'TR_MetasVendedor_NotificarVendedor', N'TR_ChatDepartamentoMiembros_NotificarMiembro') AND is_disabled = 1)
    THROW 58374, N'Alguno de los disparadores de 0327 está deshabilitado.', 1;
PRINT N'Verificacion 0327 completada.';
