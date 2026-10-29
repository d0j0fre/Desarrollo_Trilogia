SET NOCOUNT ON;
-- Verification reads metadata and the ledger only; it sends no email and changes no delivery.
IF SCHEMA_ID(N'DeliveryNotifications') IS NULL THROW 59410,N'Delivery notification namespace unavailable.',1;
DECLARE @Columns table(T sysname,C sysname,Ty sysname,L smallint,N bit,S tinyint);
INSERT @Columns VALUES
 (N'Requests',N'OperationId',N'uniqueidentifier',16,0,0),(N'Requests',N'EventId',N'int',4,0,0),
 (N'Requests',N'RouteOrderId',N'int',4,0,0),(N'Requests',N'OriginalLoginId',N'uniqueidentifier',16,0,0),
 (N'Requests',N'Actor',N'int',4,0,0),(N'Requests',N'SourceHash',N'binary',32,0,0),
 (N'Requests',N'CreatedUtc',N'datetime2',8,0,7),
 (N'Attempts',N'OperationId',N'uniqueidentifier',16,0,0),(N'Attempts',N'State',N'nvarchar',40,0,0),
 (N'Attempts',N'ClaimHash',N'binary',32,1,0),(N'Attempts',N'AttemptedUtc',N'datetime2',8,1,7),
 (N'Attempts',N'CompletedUtc',N'datetime2',8,1,7),
 (N'Events',N'EventId',N'bigint',8,0,0),(N'Events',N'OperationId',N'uniqueidentifier',16,0,0),
 (N'Events',N'State',N'nvarchar',40,0,0),(N'Events',N'CreatedUtc',N'datetime2',8,0,7);
IF EXISTS(SELECT 1 FROM @Columns e LEFT JOIN sys.columns c ON c.object_id=OBJECT_ID(N'DeliveryNotifications.'+e.T) AND c.name=e.C
 WHERE c.object_id IS NULL OR TYPE_NAME(c.user_type_id)<>e.Ty OR c.max_length<>e.L OR c.is_nullable<>e.N OR c.scale<>e.S)
 OR (SELECT COUNT(*) FROM sys.columns WHERE object_id IN(SELECT object_id FROM sys.tables WHERE schema_id=SCHEMA_ID(N'DeliveryNotifications')))<>16
 OR (SELECT COUNT(*) FROM sys.tables WHERE schema_id=SCHEMA_ID(N'DeliveryNotifications'))<>3
 THROW 59410,N'Delivery notification column contract differs.',1;
IF (SELECT COUNT(*) FROM sys.triggers WHERE OBJECT_SCHEMA_NAME(object_id)=N'DeliveryNotifications' AND is_disabled=0 AND is_instead_of_trigger=0 AND
 ((name=N'ImmutableRequests' AND parent_id=OBJECT_ID(N'DeliveryNotifications.Requests')) OR
  (name=N'ImmutableEvents' AND parent_id=OBJECT_ID(N'DeliveryNotifications.Events')) OR
  (name=N'AttemptTransitions' AND parent_id=OBJECT_ID(N'DeliveryNotifications.Attempts'))))<>3
 THROW 59410,N'Delivery notification trigger contract differs.',1;
IF (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id IN(SELECT object_id FROM sys.tables WHERE schema_id=SCHEMA_ID(N'DeliveryNotifications')) AND is_disabled=0 AND is_not_trusted=0)<>6
 OR (SELECT COUNT(*) FROM sys.check_constraints WHERE parent_object_id IN(SELECT object_id FROM sys.tables WHERE schema_id=SCHEMA_ID(N'DeliveryNotifications')) AND is_disabled=0 AND is_not_trusted=0)<>2
 OR NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'DeliveryNotifications.Attempts') AND name=N'CK_DeliveryNotification_State' AND parent_column_id=0 AND is_disabled=0 AND is_not_trusted=0)
 OR NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'DeliveryNotifications.Requests') AND name=N'CK_DeliveryNotification_Operation' AND parent_column_id=COLUMNPROPERTY(OBJECT_ID(N'DeliveryNotifications.Requests'),N'OperationId','ColumnId') AND is_disabled=0 AND is_not_trusted=0)
 OR (SELECT COUNT(*) FROM sys.indexes WHERE object_id IN(SELECT object_id FROM sys.tables WHERE schema_id=SCHEMA_ID(N'DeliveryNotifications')) AND is_primary_key=1 AND is_disabled=0)<>3
 THROW 59410,N'Delivery notification constraints differ.',1;
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'DeliveryNotifications.Requests') AND name=N'UX_DeliveryNotification_Event' AND is_unique=1 AND has_filter=0 AND is_disabled=0)
 OR NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'DeliveryNotifications.Requests') AND name=N'IX_DeliveryNotification_Owner' AND is_unique=0 AND is_disabled=0)
 THROW 59410,N'Delivery notification indexes differ.',1;
DECLARE @Hash nvarchar(128)=N'$(MigrationSha256)';
IF LEN(@Hash)<>64 OR @Hash LIKE N'%[^0-9a-fA-F]%' OR NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0058_stage2_delivery_notifications' AND FileSha256=UPPER(@Hash) AND Status=N'Applied') THROW 59410,N'Delivery notification ledger differs.',1;
IF ISNULL(HASHBYTES('SHA2_256',CONVERT(varbinary(max),OBJECT_DEFINITION(OBJECT_ID(N'DeliveryNotifications.AttemptTransitions')))),0x)<>0x9EDA733B6928F601138488EB6FCFD879BFECE76FE1EE1BE660E915FC7E6BCB19 THROW 59410,N'Delivery notification module differs: AttemptTransitions.',1;
IF ISNULL(HASHBYTES('SHA2_256',CONVERT(varbinary(max),OBJECT_DEFINITION(OBJECT_ID(N'DeliveryNotifications.Claim')))),0x)<>0xA0242E34BB8828ED83D3A04E61BC5DFD5243A82C75BFF1E9A82E7A2E3CBB591D THROW 59410,N'Delivery notification module differs: Claim.',1;
IF ISNULL(HASHBYTES('SHA2_256',CONVERT(varbinary(max),OBJECT_DEFINITION(OBJECT_ID(N'DeliveryNotifications.Complete')))),0x)<>0xC9091B1DDEC8E49293F9FE577EAEEE96B6F9D8B054C18630F4AF52337CDC31B2 THROW 59410,N'Delivery notification module differs: Complete.',1;
IF ISNULL(HASHBYTES('SHA2_256',CONVERT(varbinary(max),OBJECT_DEFINITION(OBJECT_ID(N'DeliveryNotifications.ImmutableEvents')))),0x)<>0x069726F0C8F016101261D9005B2CA6A13343F62C238310499D2077C3F6D0EA01 THROW 59410,N'Delivery notification module differs: ImmutableEvents.',1;
IF ISNULL(HASHBYTES('SHA2_256',CONVERT(varbinary(max),OBJECT_DEFINITION(OBJECT_ID(N'DeliveryNotifications.ImmutableRequests')))),0x)<>0x06CDEF1A1A83BA2F5C170DF5CC16FA7960A8EE0D1CD20D23852D378CE8003282 THROW 59410,N'Delivery notification module differs: ImmutableRequests.',1;
IF ISNULL(HASHBYTES('SHA2_256',CONVERT(varbinary(max),OBJECT_DEFINITION(OBJECT_ID(N'DeliveryNotifications.Receipt')))),0x)<>0x8B15B325E2CD1AF0D7D3FB981523B288F7EA3F2E9BC8CF49C9FE6FD702546BCC THROW 59410,N'Delivery notification module differs: Receipt.',1;
IF ISNULL(HASHBYTES('SHA2_256',CONVERT(varbinary(max),OBJECT_DEFINITION(OBJECT_ID(N'DeliveryNotifications.Request')))),0x)<>0x442E88FE485E94895C807F8198F98A3374EEF8E420A63A6FFD455072344C5C11 THROW 59410,N'Delivery notification module differs: Request.',1;
IF ISNULL(HASHBYTES('SHA2_256',CONVERT(varbinary(max),OBJECT_DEFINITION(OBJECT_ID(N'DeliveryNotifications.RequireAccess')))),0x)<>0x6E62F75B4F0A9D7E227B5EE0BCD7E87F3BAF28614742401F5C24B58A478B05CF THROW 59410,N'Delivery notification module differs: RequireAccess.',1;
IF ISNULL(HASHBYTES('SHA2_256',CONVERT(varbinary(max),OBJECT_DEFINITION(OBJECT_ID(N'DeliveryNotifications.SnapshotHash')))),0x)<>0x077D8EB802BEEF622B1C8ABD16302C9DF0552C57EEEB4017CE4092B54B1F2A73 THROW 59410,N'Delivery notification module differs: SnapshotHash.',1;
IF ISNULL(HASHBYTES('SHA2_256',CONVERT(varbinary(max),OBJECT_DEFINITION(OBJECT_ID(N'dbo.sp_Stage2_RequireOriginalLogin')))),0x)<>0x8C8515B2D2DD516A737A548EE7A03AB3E12D101BB8D6B27A1FBDC8846E31479C THROW 59410,N'Original login guard differs.',1;
IF ISNULL(HASHBYTES('SHA2_256',CONVERT(varbinary(max),OBJECT_DEFINITION(OBJECT_ID(N'dbo.sp_Stage2_ConfirmDelivery')))),0x)<>0x7E8763B2CA8D87462FE2A1D109C07BCD28175E779C1F39E5E752DC5673227866 THROW 59410,N'Committed delivery contract differs.',1;
