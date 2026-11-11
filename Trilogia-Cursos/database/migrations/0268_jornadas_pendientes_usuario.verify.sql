SET NOCOUNT ON;
IF OBJECT_ID(N'dbo.sp_RRHH_ListarJornadasPendientes',N'P') IS NULL THROW 58683,N'Falta sp_RRHH_ListarJornadasPendientes.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0268_jornadas_pendientes_usuario' AND Status=N'Applied') THROW 58684,N'La migracion 0268 no esta registrada.',1;
IF NOT EXISTS(SELECT 1 FROM sys.dm_exec_describe_first_result_set_for_object(OBJECT_ID(N'dbo.sp_RRHH_ListarJornadasPendientes'),NULL) WHERE column_ordinal=12 AND name=N'UsuarioId') THROW 58685,N'El procedimiento no devuelve UsuarioId como columna 12.',1;
IF NOT EXISTS(SELECT 1 FROM sys.dm_exec_describe_first_result_set_for_object(OBJECT_ID(N'dbo.sp_RRHH_ListarJornadasPendientes'),NULL) WHERE column_ordinal=8 AND name=N'Observaciones') THROW 58686,N'Cambio el orden de las columnas anteriores.',1;
EXEC dbo.sp_RRHH_ListarJornadasPendientes @Desde='20000101',@Hasta='20991231';
PRINT N'Verificacion 0268 completada.';
