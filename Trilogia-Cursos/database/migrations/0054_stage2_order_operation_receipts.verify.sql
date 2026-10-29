SET NOCOUNT ON;
DECLARE @Hash nvarchar(128)=N'$(MigrationSha256)';
IF LEN(@Hash)<>64 OR @Hash LIKE N'%[^0-9a-fA-F]%' OR NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0054_stage2_order_operation_receipts' AND Status=N'Applied' AND FileSha256=UPPER(@Hash)) THROW 58931,N'Operation receipt ledger mismatch.',1;
IF ISNULL(HASHBYTES('SHA2_256',CONVERT(varbinary(max),OBJECT_DEFINITION(OBJECT_ID(N'CommercialOrders.Result')))),0x)<>0xA8F920786CD6C2BF71295889D71A0F33128D76258AAFD4F89838B8EA316E3F71 THROW 58931,N'Operation receipt module mismatch: Result.',1;
IF ISNULL(HASHBYTES('SHA2_256',CONVERT(varbinary(max),OBJECT_DEFINITION(OBJECT_ID(N'CommercialOrders.Decide')))),0x)<>0xBCC1741232619D9A32BE87AB407CE4BAEE287F87AAC445290A9B103D4E7754BA THROW 58931,N'Operation receipt module mismatch: Decide.',1;
SELECT N'PASS' AS OrderOperationReceiptsVerification;
