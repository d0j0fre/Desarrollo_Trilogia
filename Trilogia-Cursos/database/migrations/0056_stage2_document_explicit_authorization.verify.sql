SET NOCOUNT ON;
DECLARE @Hash nvarchar(128)=N'$(MigrationSha256)';
IF LEN(@Hash)<>64 OR @Hash LIKE N'%[^0-9a-fA-F]%' OR NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0056_stage2_document_explicit_authorization' AND FileSha256=UPPER(@Hash) AND Status=N'Applied') THROW 57521,N'Document explicit-authorization ledger mismatch.',1;
IF ISNULL(HASHBYTES('SHA2_256',CONVERT(varbinary(max),OBJECT_DEFINITION(OBJECT_ID(N'DocumentSecurity.CanAccess')))),0x)<>0xCFD8943D94CC972CD30318EF490D0A79649102FCEAF827088BD77C7F78C9AD44 THROW 57521,N'Document explicit-authorization module differs: CanAccess.',1;
IF ISNULL(HASHBYTES('SHA2_256',CONVERT(varbinary(max),OBJECT_DEFINITION(OBJECT_ID(N'DocumentSecurity.SetGrant')))),0x)<>0x70BF43A1EC1A28FE3AF8F6B8BEA0EB30401EEE85B9DD9BB0A5061A9A4CB12A09 THROW 57521,N'Document explicit-authorization module differs: SetGrant.',1;
IF ISNULL(HASHBYTES('SHA2_256',CONVERT(varbinary(max),OBJECT_DEFINITION(OBJECT_ID(N'DocumentSecurity.GrantOptions')))),0x)<>0x72F923230EAB8F589BEFB78EDF615808A5FF758D67A909E5D37622047FBE439F THROW 57521,N'Document explicit-authorization module differs: GrantOptions.',1;
IF ISNULL(HASHBYTES('SHA2_256',CONVERT(varbinary(max),OBJECT_DEFINITION(OBJECT_ID(N'DocumentSecurity.ListGrants')))),0x)<>0x0CC48002A5CE259BB8C440902D4A0F0FF96EDCED6557AEC95F80936285A3A795 THROW 57521,N'Document explicit-authorization module differs: ListGrants.',1;
SELECT N'PASS' AS DocumentExplicitAuthorizationVerification;
