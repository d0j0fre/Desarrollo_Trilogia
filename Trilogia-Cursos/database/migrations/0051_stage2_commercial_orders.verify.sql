SET NOCOUNT ON;
DECLARE @Hash nvarchar(128)=N'$(MigrationSha256)';
IF LEN(@Hash)<>64 OR @Hash LIKE N'%[^0-9a-fA-F]%' THROW 58920,N'Invalid verification hash.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0051_stage2_commercial_orders' AND Status=N'Applied' AND FileSha256=UPPER(@Hash)) THROW 58920,N'Commercial order ledger mismatch.',1;
IF (SELECT COUNT(*) FROM sys.tables WHERE schema_id=SCHEMA_ID(N'CommercialOrders'))<>3
 OR (SELECT COUNT(*) FROM sys.check_constraints WHERE parent_object_id IN(SELECT object_id FROM sys.tables WHERE schema_id=SCHEMA_ID(N'CommercialOrders')) AND is_disabled=0 AND is_not_trusted=0)<>3
 OR (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id IN(SELECT object_id FROM sys.tables WHERE schema_id=SCHEMA_ID(N'CommercialOrders')) AND is_disabled=0 AND is_not_trusted=0)<>9 THROW 58920,N'Commercial order schema mismatch.',1;
IF ISNULL(HASHBYTES('SHA2_256',CONVERT(varbinary(max),OBJECT_DEFINITION(OBJECT_ID(N'CommercialOrders.Result')))),0x)<>0x928541B7D6E98974CAC721EB9C62C1ABA5F08DB36999925E3E6D6C6A40FEEB76 THROW 58920,N'Commercial module mismatch: Result',1;
IF ISNULL(HASHBYTES('SHA2_256',CONVERT(varbinary(max),OBJECT_DEFINITION(OBJECT_ID(N'CommercialOrders.Finalize')))),0x)<>0xF9787A059DFE46E84A94261B8E7AEEBE3063837A5B66BD146A55E014E55B18C2 THROW 58920,N'Commercial module mismatch: Finalize',1;
IF ISNULL(HASHBYTES('SHA2_256',CONVERT(varbinary(max),OBJECT_DEFINITION(OBJECT_ID(N'CommercialOrders.Consume')))),0x)<>0x29CB6334044AFC6F0A4FDB32CAA26E817776D5BE0B862B69B18048F90D60B0A4 THROW 58920,N'Commercial module mismatch: Consume',1;
IF ISNULL(HASHBYTES('SHA2_256',CONVERT(varbinary(max),OBJECT_DEFINITION(OBJECT_ID(N'CommercialOrders.RequireSupervisor')))),0x)<>0x4A3F97D6FE3287554524A4D68C3F4D79BE4CC844606536D49ABAD5AFAE34442B THROW 58920,N'Commercial module mismatch: RequireSupervisor',1;
IF ISNULL(HASHBYTES('SHA2_256',CONVERT(varbinary(max),OBJECT_DEFINITION(OBJECT_ID(N'CommercialOrders.Decide')))),0x)<>0x2C7A129B0254CF1AFCE94DBCE94ADA2DC560188CFA1B9AF5AF938577EE97999A THROW 58920,N'Commercial module mismatch: Decide',1;
IF ISNULL(HASHBYTES('SHA2_256',CONVERT(varbinary(max),OBJECT_DEFINITION(OBJECT_ID(N'CommercialOrders.Clients')))),0x)<>0x789FE735C0E3B28A3E1A00F774EC8B8D067FDA7F9A04DCD5069F4F3E188C9A16 THROW 58920,N'Commercial module mismatch: Clients',1;
IF ISNULL(HASHBYTES('SHA2_256',CONVERT(varbinary(max),OBJECT_DEFINITION(OBJECT_ID(N'CommercialOrders.History')))),0x)<>0x32CB16830A6405A36B4B80DDC859831BCFDB4CF46F4116EF6133543AE1E995D1 THROW 58920,N'Commercial module mismatch: History',1;
IF ISNULL(HASHBYTES('SHA2_256',CONVERT(varbinary(max),OBJECT_DEFINITION(OBJECT_ID(N'CommercialOrders.Details')))),0x)<>0x41F722E5B87BEC55E05F0929AD0CF10695D52D4CE34828807A496FB386BAAAAA THROW 58920,N'Commercial module mismatch: Details',1;
IF ISNULL(HASHBYTES('SHA2_256',CONVERT(varbinary(max),OBJECT_DEFINITION(OBJECT_ID(N'CommercialOrders.Retained')))),0x)<>0xB06393DC409A06F6FDB8C78FF4847701B024FF07830DA351B2A4E9CC218AE983 THROW 58920,N'Commercial module mismatch: Retained',1;
IF (SELECT COUNT(*) FROM sys.triggers t JOIN sys.objects o ON o.object_id=t.object_id WHERE o.schema_id=SCHEMA_ID(N'CommercialOrders') AND t.is_disabled=0)<>3 THROW 58920,N'Commercial immutability triggers unavailable.',1;
SELECT N'PASS' AS CommercialOrdersVerification;
