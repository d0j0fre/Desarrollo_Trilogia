SET NOCOUNT ON;
DECLARE @Hash nvarchar(128)=N'$(MigrationSha256)';
IF LEN(@Hash)<>64 OR @Hash LIKE N'%[^0-9a-fA-F]%' THROW 58500,N'Invalid migration hash.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0045_stage2_personal_legacy_archive' AND Status=N'Applied' AND FileSha256=UPPER(@Hash)) THROW 58500,N'Archive ledger/hash not accredited.',1;
IF OBJECT_ID(N'ChatV2Archive.Own',N'P') IS NULL OR OBJECT_ID(N'ChatV2Archive.History',N'P') IS NULL THROW 58500,N'Archive modules missing.',1;
IF EXISTS(SELECT 1 FROM sys.sql_modules WHERE object_id IN(OBJECT_ID(N'ChatV2Archive.Own'),OBJECT_ID(N'ChatV2Archive.History')) AND (uses_ansi_nulls=0 OR uses_quoted_identifier=0)) THROW 58500,N'Archive SQL options invalid.',1;
SELECT N'PASS' AS ArchiveVerification;
