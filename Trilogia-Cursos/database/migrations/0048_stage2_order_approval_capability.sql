-- Register the capability already required by ManagerOrders. No profile grants.
SET NOCOUNT ON;
SET XACT_ABORT ON;
DECLARE @Hash nvarchar(128)=N'$(MigrationSha256)';
IF LEN(@Hash)<>64 OR @Hash LIKE N'%[^0-9a-fA-F]%' THROW 58710,N'Invalid migration hash.',1;
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL THROW 58710,N'Migration ledger missing.',1;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @Lock int;
 EXEC @Lock=sys.sp_getapplock @Resource=N'Stage2:0048:install',@LockMode=N'Exclusive',@LockOwner=N'Transaction',@LockTimeout=10000;
 IF @Lock<0 THROW 58710,N'Concurrent installation.',1;
 IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0048_stage2_order_approval_capability')
 BEGIN
  IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0048_stage2_order_approval_capability' AND Status=N'Applied' AND FileSha256=UPPER(@Hash)) THROW 58710,N'Ledger conflict.',1;
  COMMIT; RETURN;
 END;
 IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId LIKE N'0048%') THROW 58710,N'Migration identifier collision.',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0036_stage2_transactional_login' AND Status=N'Applied' AND FileSha256=N'98816A77160064B6B86579A25C9A6B41962197C60E38FE32FE2BAC58A7C02FDC') THROW 58710,N'Original-login prerequisite missing.',1;
 IF EXISTS(SELECT 1 FROM dbo.Permisos WHERE Codigo=N'PEDIDOS_AUTORIZAR_RECHAZAR') THROW 58710,N'Existing approval capability requires reconciliation.',1;
 INSERT dbo.Permisos(Codigo,Modulo,Nombre,Descripcion,Activo)
 VALUES(N'PEDIDOS_AUTORIZAR_RECHAZAR',N'Pedidos',N'Aprobar y rechazar pedidos retenidos',N'Capacidad explícita; requiere además relación supervisora vigente y transacción de pedidos. No concede asignaciones ni acceso general.',1);
 INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes)
 VALUES(N'0048_stage2_order_approval_capability',N'0048_stage2_order_approval_capability.sql',UPPER(@Hash),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),N'Registers missing canonical ManagerOrders capability only. No profile grants, role inference, relationships or order mutations.');
 COMMIT;
END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
