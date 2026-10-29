SET NOCOUNT ON;
DECLARE @Hash nvarchar(128)=N'$(MigrationSha256)';
IF LEN(@Hash)<>64 OR @Hash LIKE N'%[^0-9a-fA-F]%' THROW 58600,N'Invalid migration hash.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0046_stage2_legacy_budget_archive' AND Status=N'Applied' AND FileSha256=UPPER(@Hash)) THROW 58600,N'Archive ledger/hash not accredited.',1;
IF OBJECT_ID(N'dbo.CuentasPresupuestarias',N'U') IS NULL OR OBJECT_ID(N'dbo.sp_CuentasPresupuestarias_List',N'P') IS NULL THROW 58600,N'Monthly archive contract missing.',1;
IF (SELECT COUNT(*) FROM sys.parameters WHERE object_id=OBJECT_ID(N'dbo.sp_CuentasPresupuestarias_List'))<>7 THROW 58600,N'Original-login parameters missing.',1;
IF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.CuentasPresupuestarias') AND name=N'CK_Stage2LegacyBudgetAmount' AND is_disabled=0 AND is_not_trusted=0) THROW 58600,N'Monthly archive check missing.',1;
SELECT N'PASS' AS LegacyBudgetArchiveVerification;
