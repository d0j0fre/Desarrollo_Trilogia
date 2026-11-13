SET NOCOUNT ON;
IF OBJECT_DEFINITION(OBJECT_ID(N'dbo.sp_Kpi_CumplimientoGlobal')) NOT LIKE N'%VentaSinMeta%' THROW 57953,N'sp_Kpi_CumplimientoGlobal no separa la venta sin meta.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0295_kpi_cumplimiento_solo_con_meta' AND Status=N'Applied') THROW 57954,N'La migracion 0295 no esta registrada.',1;
PRINT N'Verificacion 0295 completada.';
