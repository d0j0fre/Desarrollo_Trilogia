SET NOCOUNT ON;
DECLARE @Hash nvarchar(128)=N'$(MigrationSha256)';
IF LEN(@Hash)<>64 OR @Hash LIKE N'%[^0-9a-fA-F]%' THROW 59020,N'Invalid verification hash.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0052_stage2_seller_history' AND Status=N'Applied' AND FileSha256=UPPER(@Hash)) THROW 59020,N'History ledger mismatch.',1;
IF ISNULL(HASHBYTES('SHA2_256',CONVERT(varbinary(max),OBJECT_DEFINITION(OBJECT_ID(N'CommercialOrders.HistoryV2')))),0x)<>0xCD5174E5909CDFBCB671C22B473DF3264CD23FC9BF32D53B16E26BA3119807B5 THROW 59020,N'History reader module mismatch.',1;
IF ISNULL(HASHBYTES('SHA2_256',CONVERT(varbinary(max),OBJECT_DEFINITION(OBJECT_ID(N'CommercialOrders.DetailsV2')))),0x)<>0xB2673EB109409EE59CFD89DEC91BFE350C481E4AA858D9A9019983505210E7AF THROW 59020,N'History reader module mismatch.',1;
SELECT N'PASS' AS HistoryVerification;
