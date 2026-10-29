SET NOCOUNT ON;
-- Verification reads metadata and the ledger only; it sends no email.
IF SCHEMA_ID(N'InvoiceNotifications') IS NULL THROW 59310,N'Invoice notification namespace unavailable.',1;
DECLARE @Columns table(T sysname,C sysname,Ty sysname,L smallint,N bit,S tinyint);
INSERT @Columns VALUES
 (N'Requests',N'OperationId',N'uniqueidentifier',16,0,0),
 (N'Requests',N'InvoiceId',N'int',4,0,0),(N'Requests',N'OriginalLoginId',N'uniqueidentifier',16,0,0),
 (N'Requests',N'Actor',N'int',4,0,0),(N'Requests',N'IsInitial',N'bit',1,0,0),
 (N'Requests',N'SourceHash',N'binary',32,0,0),(N'Requests',N'CreatedUtc',N'datetime2',8,0,7),
 (N'Attempts',N'OperationId',N'uniqueidentifier',16,0,0),(N'Attempts',N'State',N'nvarchar',40,0,0),
 (N'Attempts',N'ClaimHash',N'binary',32,1,0),(N'Attempts',N'AttemptedUtc',N'datetime2',8,1,7),
 (N'Attempts',N'CompletedUtc',N'datetime2',8,1,7),
 (N'Events',N'EventId',N'bigint',8,0,0),(N'Events',N'OperationId',N'uniqueidentifier',16,0,0),
 (N'Events',N'State',N'nvarchar',40,0,0),(N'Events',N'CreatedUtc',N'datetime2',8,0,7);
IF EXISTS(SELECT 1 FROM @Columns e LEFT JOIN sys.columns c ON c.object_id=OBJECT_ID(N'InvoiceNotifications.'+e.T) AND c.name=e.C
 WHERE c.object_id IS NULL OR TYPE_NAME(c.user_type_id)<>e.Ty OR c.max_length<>e.L OR c.is_nullable<>e.N OR c.scale<>e.S)
 OR (SELECT COUNT(*) FROM sys.columns WHERE object_id IN(SELECT object_id FROM sys.tables WHERE schema_id=SCHEMA_ID(N'InvoiceNotifications')))<>16
 OR (SELECT COUNT(*) FROM sys.tables WHERE schema_id=SCHEMA_ID(N'InvoiceNotifications'))<>3
 THROW 59310,N'Invoice notification column contract differs.',1;
IF (SELECT COUNT(*) FROM sys.triggers WHERE OBJECT_SCHEMA_NAME(object_id)=N'InvoiceNotifications' AND is_disabled=0 AND is_instead_of_trigger=0 AND
 ((name=N'ImmutableRequests' AND parent_id=OBJECT_ID(N'InvoiceNotifications.Requests')) OR
  (name=N'ImmutableEvents' AND parent_id=OBJECT_ID(N'InvoiceNotifications.Events')) OR
  (name=N'AttemptTransitions' AND parent_id=OBJECT_ID(N'InvoiceNotifications.Attempts'))))<>3
 THROW 59310,N'Invoice notification trigger contract differs.',1;
IF (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id IN(SELECT object_id FROM sys.tables WHERE schema_id=SCHEMA_ID(N'InvoiceNotifications')) AND is_disabled=0 AND is_not_trusted=0)<>5
 OR (SELECT COUNT(*) FROM sys.check_constraints WHERE parent_object_id IN(SELECT object_id FROM sys.tables WHERE schema_id=SCHEMA_ID(N'InvoiceNotifications')) AND is_disabled=0 AND is_not_trusted=0)<>2
 OR NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'InvoiceNotifications.Attempts') AND name=N'CK_InvoiceNotification_State' AND parent_column_id=0 AND is_disabled=0 AND is_not_trusted=0)
 OR NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'InvoiceNotifications.Requests') AND name=N'CK_InvoiceNotification_Operation' AND parent_column_id=COLUMNPROPERTY(OBJECT_ID(N'InvoiceNotifications.Requests'),N'OperationId','ColumnId') AND is_disabled=0 AND is_not_trusted=0)
 OR (SELECT COUNT(*) FROM sys.indexes WHERE object_id IN(SELECT object_id FROM sys.tables WHERE schema_id=SCHEMA_ID(N'InvoiceNotifications')) AND is_primary_key=1 AND is_disabled=0)<>3
 THROW 59310,N'Invoice notification constraints differ.',1;
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'InvoiceNotifications.Requests') AND name=N'UX_InvoiceNotification_Initial' AND is_unique=1 AND has_filter=1 AND is_disabled=0
 AND REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(filter_definition,N'[',N''),N']',N''),N'(',N''),N')',N''),N' ',N'')=N'IsInitial=1')
 OR NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'InvoiceNotifications.Requests') AND name=N'IX_InvoiceNotification_Owner' AND is_disabled=0)
 THROW 59310,N'Invoice notification indexes differ.',1;
DECLARE @Hash nvarchar(128)=N'$(MigrationSha256)';
IF LEN(@Hash)<>64 OR @Hash LIKE N'%[^0-9a-fA-F]%' OR NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0057_stage2_invoice_notifications' AND FileSha256=UPPER(@Hash) AND Status=N'Applied') THROW 59310,N'Invoice notification ledger differs.',1;
IF ISNULL(HASHBYTES('SHA2_256',CONVERT(varbinary(max),OBJECT_DEFINITION(OBJECT_ID(N'InvoiceNotifications.AttemptTransitions')))),0x)<>0xE41F00912069D761A5E3436B27650FF2458354838065C2B5ED6598DD84B36DA5 THROW 59310,N'Invoice notification module differs: InvoiceNotifications.AttemptTransitions.',1;
IF ISNULL(HASHBYTES('SHA2_256',CONVERT(varbinary(max),OBJECT_DEFINITION(OBJECT_ID(N'InvoiceNotifications.Claim')))),0x)<>0x25A6EA7CEDB67697221E740E1EC1A93E38983B7AD945522AB67E7A8524323BF5 THROW 59310,N'Invoice notification module differs: InvoiceNotifications.Claim.',1;
IF ISNULL(HASHBYTES('SHA2_256',CONVERT(varbinary(max),OBJECT_DEFINITION(OBJECT_ID(N'InvoiceNotifications.Complete')))),0x)<>0x3B7FFA78FC25DE2F3203F83982407A267D5564CDD888D23D712022F960562C24 THROW 59310,N'Invoice notification module differs: InvoiceNotifications.Complete.',1;
IF ISNULL(HASHBYTES('SHA2_256',CONVERT(varbinary(max),OBJECT_DEFINITION(OBJECT_ID(N'InvoiceNotifications.ImmutableEvents')))),0x)<>0x0BE8966905CAE78720CCC62013D1F1C97FEB8EADCD39A7836E157C7D76B0E8DA THROW 59310,N'Invoice notification module differs: InvoiceNotifications.ImmutableEvents.',1;
IF ISNULL(HASHBYTES('SHA2_256',CONVERT(varbinary(max),OBJECT_DEFINITION(OBJECT_ID(N'InvoiceNotifications.ImmutableRequests')))),0x)<>0x33E108133E605C4226D96ED9822D42C509816B13B625A1811DE5F849F3B0CC91 THROW 59310,N'Invoice notification module differs: InvoiceNotifications.ImmutableRequests.',1;
IF ISNULL(HASHBYTES('SHA2_256',CONVERT(varbinary(max),OBJECT_DEFINITION(OBJECT_ID(N'InvoiceNotifications.Receipt')))),0x)<>0xC646ED74D648BC1BB56E3DC23FE121F763416F5C179DBAAE557C264BE35A3558 THROW 59310,N'Invoice notification module differs: InvoiceNotifications.Receipt.',1;
IF ISNULL(HASHBYTES('SHA2_256',CONVERT(varbinary(max),OBJECT_DEFINITION(OBJECT_ID(N'InvoiceNotifications.Request')))),0x)<>0x8561421F9B791AF6D05B4C61BCDAFFD6C2364D311AA003C1615D85389F65CDF9 THROW 59310,N'Invoice notification module differs: InvoiceNotifications.Request.',1;
IF ISNULL(HASHBYTES('SHA2_256',CONVERT(varbinary(max),OBJECT_DEFINITION(OBJECT_ID(N'InvoiceNotifications.RequireAccess')))),0x)<>0x5430DBD2FAF9F1394C5EC427BC52841D0566DDE9FEABE0EB95186B48D2D0C5B3 THROW 59310,N'Invoice notification module differs: InvoiceNotifications.RequireAccess.',1;
IF ISNULL(HASHBYTES('SHA2_256',CONVERT(varbinary(max),OBJECT_DEFINITION(OBJECT_ID(N'InvoiceNotifications.SnapshotHash')))),0x)<>0x0CF1E06208E7307BA6C8EF212218CB1431EAA8248B88B83150FAD262B0F1A774 THROW 59310,N'Invoice notification module differs: InvoiceNotifications.SnapshotHash.',1;
