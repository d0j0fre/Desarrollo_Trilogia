SET NOCOUNT ON;

/* Verificacion de 0242. Solo lectura: no ejecuta sp_Comodato_GenerarAlertasRentabilidad. */
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0242_comodato_regla_30_dias' AND Status = N'Applied')
    THROW 56426, N'0242 no figura aplicada en el ledger.', 1;
IF OBJECT_ID(N'dbo.fn_Comodato_Rentabilidad', N'IF') IS NULL
    THROW 56427, N'Falta dbo.fn_Comodato_Rentabilidad.', 1;
IF OBJECT_ID(N'dbo.sp_Comodato_GenerarAlertasRentabilidad', N'P') IS NULL
    THROW 56428, N'Falta dbo.sp_Comodato_GenerarAlertasRentabilidad.', 1;
IF NOT EXISTS (SELECT 1 FROM sys.dm_exec_describe_first_result_set(N'EXEC dbo.sp_Comodato_Rentabilidad', NULL, 0)
               WHERE column_ordinal = 13 AND name = N'DiasSinCompra')
   OR NOT EXISTS (SELECT 1 FROM sys.dm_exec_describe_first_result_set(N'EXEC dbo.sp_Comodato_Rentabilidad', NULL, 0)
               WHERE column_ordinal = 14 AND name = N'SugerirRetiro')
    THROW 56429, N'sp_Comodato_Rentabilidad no devuelve DiasSinCompra/SugerirRetiro al final.', 1;

/* Ningun comodato con menos de 30 dias desde la asignacion puede quedar marcado. */
IF EXISTS (SELECT 1 FROM dbo.fn_Comodato_Rentabilidad(30) WHERE SugerirRetiro = 1 AND DiasEnComodato < 30)
    THROW 56430, N'La regla marca comodatos con menos de 30 días.', 1;

SELECT ComodatoId, CodigoActivo, ClienteNombre, DiasEnComodato, UltimaCompra, DiasSinCompra, SugerirRetiro
FROM dbo.fn_Comodato_Rentabilidad(30);

SELECT TOP 10 NotificacionId, UsuarioId, EntidadId AS ComodatoId, Leida, FechaCreacion
FROM dbo.NotificacionesUsuario WHERE Tipo = N'ComodatoBajaRentabilidad' ORDER BY NotificacionId DESC;

PRINT N'0242 verificada.';
