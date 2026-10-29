SET NOCOUNT ON;
DECLARE @Hash nvarchar(128)=N'$(MigrationSha256)';
IF LEN(@Hash)<>64 OR @Hash LIKE N'%[^0-9a-fA-F]%' THROW 58810,N'Invalid migration hash.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0050_stage2_quote_empty_promotions' AND FileSha256=UPPER(@Hash) AND Status=N'Applied') THROW 58810,N'Empty-promotions ledger/hash missing.',1;
-- SQL Server stores CREATE OR ALTER as CREATE plus three spaces in this module.
-- The complete definition matches the submitted body with only that verb normalization.
IF ISNULL(HASHBYTES('SHA2_256',CONVERT(varbinary(max),OBJECT_DEFINITION(OBJECT_ID(N'CommerceQuotes.CaptureSource')))),0x)<>0xBC7A7EAFB8B856821003C872D8D83E874F46F74A7322A996A6F5EC373463E5CE THROW 58810,N'Corrected source module differs.',1;
SELECT N'PASS' AS EmptyPromotionsVerification;
