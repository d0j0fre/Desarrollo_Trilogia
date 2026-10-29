-- Read-only monthly account archive. No catalog, balances, writer or role seeds.
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF SESSIONPROPERTY('QUOTED_IDENTIFIER')<>1 OR SESSIONPROPERTY('ANSI_NULLS')<>1 THROW 58600,N'SQL options not accredited.',1;
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL THROW 58600,N'Migration ledger missing.',1;
DECLARE @Hash nvarchar(128)=N'$(MigrationSha256)';
IF LEN(@Hash)<>64 OR @Hash LIKE N'%[^0-9a-fA-F]%' THROW 58600,N'Invalid migration hash.',1;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @Lock int;
 EXEC @Lock=sys.sp_getapplock @Resource=N'Stage2:0046:install',@LockMode=N'Exclusive',@LockOwner=N'Transaction',@LockTimeout=10000;
 IF @Lock<0 THROW 58600,N'Concurrent installation.',1;
 IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0046_stage2_legacy_budget_archive')
 BEGIN
  IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0046_stage2_legacy_budget_archive' AND Status=N'Applied' AND FileSha256=UPPER(@Hash)) THROW 58600,N'Ledger conflict.',1;
  COMMIT; RETURN;
 END;
 IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId LIKE N'0046%') THROW 58600,N'Migration identifier collision.',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0036_stage2_transactional_login' AND Status=N'Applied' AND FileSha256=N'98816A77160064B6B86579A25C9A6B41962197C60E38FE32FE2BAC58A7C02FDC') THROW 58600,N'Original-login prerequisite not accredited.',1;
 IF OBJECT_ID(N'dbo.sp_Stage2_RequireOriginalLogin',N'P') IS NULL OR NOT EXISTS(SELECT 1 FROM dbo.Permisos WHERE Codigo=N'GASTOS_LEGADO_VER' AND Activo=1) THROW 58600,N'Authorization contract missing.',1;
 IF OBJECT_ID(N'dbo.sp_CuentasPresupuestarias_List') IS NOT NULL THROW 58600,N'Existing legacy reader requires reconciliation.',1;
 IF OBJECT_ID(N'dbo.CuentasPresupuestarias') IS NOT NULL THROW 58600,N'Existing monthly archive requires separate reconciliation; no automatic conversion.',1;
 CREATE TABLE dbo.CuentasPresupuestarias(
  CuentaId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Stage2LegacyBudgetAccounts PRIMARY KEY,
  Codigo nvarchar(20) NOT NULL CONSTRAINT UQ_Stage2LegacyBudgetCode UNIQUE,
  Nombre nvarchar(120) NOT NULL,
  Descripcion nvarchar(300) NULL,
  PresupuestoMensual decimal(18,2) NOT NULL CONSTRAINT DF_Stage2LegacyBudgetAmount DEFAULT(0),
  Activo bit NOT NULL CONSTRAINT DF_Stage2LegacyBudgetActive DEFAULT(1),
  FechaRegistro datetime2(7) NOT NULL CONSTRAINT DF_Stage2LegacyBudgetCreated DEFAULT(SYSUTCDATETIME()),
  FechaActualizacion datetime2(7) NULL,
  CONSTRAINT CK_Stage2LegacyBudgetAmount CHECK(PresupuestoMensual>=0)
 );
 EXEC(N'CREATE PROCEDURE dbo.sp_CuentasPresupuestarias_List
 @LoginId uniqueidentifier,@UsuarioId int,@PerfilId int,@SecurityStamp uniqueidentifier,@BindingHash binary(32),
 @IssuedUtc datetime2(7),@ExpiresUtc datetime2(7)
 AS
 BEGIN
  SET NOCOUNT ON; SET XACT_ABORT ON;
  BEGIN TRY BEGIN TRANSACTION;
   EXEC dbo.sp_Stage2_RequireOriginalLogin @LoginId,@UsuarioId,@PerfilId,@SecurityStamp,@BindingHash,@IssuedUtc,@ExpiresUtc,N''GASTOS_LEGADO_VER'';
   SELECT CuentaId,Codigo,Nombre,ISNULL(Descripcion,N'''') AS Descripcion,PresupuestoMensual,Activo
   FROM dbo.CuentasPresupuestarias ORDER BY Activo DESC,Nombre,CuentaId;
   EXEC dbo.sp_Stage2_RequireOriginalLogin @LoginId,@UsuarioId,@PerfilId,@SecurityStamp,@BindingHash,@IssuedUtc,@ExpiresUtc,N''GASTOS_LEGADO_VER'';
   COMMIT;
  END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
 END;');
 INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes)
 VALUES(N'0046_stage2_legacy_budget_archive',N'0046_stage2_legacy_budget_archive.sql',UPPER(@Hash),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),N'Empty monthly-account archive with canonical read authorization. No invented records, role grants or old expense writer. Modern annual budget and GastosOperativos unchanged.');
 COMMIT;
END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
