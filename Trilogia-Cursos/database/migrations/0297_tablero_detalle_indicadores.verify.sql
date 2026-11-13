SET NOCOUNT ON;
IF OBJECT_ID(N'dbo.sp_Reportes_DashboardDetalle',N'P') IS NULL THROW 57974,N'Falta sp_Reportes_DashboardDetalle.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0297_tablero_detalle_indicadores' AND Status=N'Applied') THROW 57975,N'La migracion 0297 no esta registrada.',1;
DECLARE @i TABLE(Grupo NVARCHAR(30),Etiqueta NVARCHAR(150),Valor DECIMAL(18,2),Detalle NVARCHAR(200));
INSERT @i EXEC dbo.sp_Reportes_DashboardDetalle @Indicador=N'ventas',@Desde='20000101',@Hasta='20991231';
DELETE @i; INSERT @i EXEC dbo.sp_Reportes_DashboardDetalle @Indicador=N'stock';
DELETE @i; INSERT @i EXEC dbo.sp_Reportes_DashboardDetalle @Indicador=N'ruta';
DELETE @i; INSERT @i EXEC dbo.sp_Reportes_DashboardDetalle @Indicador=N'cobros';
DECLARE @rechazo BIT=0;
BEGIN TRY EXEC dbo.sp_Reportes_DashboardDetalle @Indicador=N'otro'; END TRY
BEGIN CATCH IF ERROR_NUMBER()=57972 SET @rechazo=1; END CATCH;
IF @rechazo=0 THROW 57976,N'Debio rechazar un indicador desconocido.',1;
PRINT N'Verificacion 0297 completada.';
