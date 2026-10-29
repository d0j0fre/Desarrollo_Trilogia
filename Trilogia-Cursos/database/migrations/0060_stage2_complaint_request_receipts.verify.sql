SET NOCOUNT ON;
-- Metadata and ledger only. This verifier never creates a complaint or invokes a product procedure.
DECLARE @Hash nvarchar(128)=N'$(MigrationSha256)';
IF LEN(@Hash)<>64 OR @Hash LIKE N'%[^0-9a-fA-F]%'
 OR NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0060_stage2_complaint_request_receipts' AND FileSha256=UPPER(@Hash) AND Status=N'Applied')
 THROW 59610,N'Complaint receipt ledger differs.',1;
IF NOT EXISTS(SELECT 1 FROM sys.schemas WHERE name=N'ComplaintRequests' AND principal_id=DATABASE_PRINCIPAL_ID(N'dbo'))
 OR OBJECT_ID(N'ComplaintRequests.Receipts',N'U') IS NULL
 THROW 59610,N'Complaint receipt namespace differs.',1;
DECLARE @Columns table(C sysname,Ty sysname,L smallint,S tinyint);
INSERT @Columns VALUES
 (N'RequestId',N'uniqueidentifier',16,0),(N'OriginalLoginId',N'uniqueidentifier',16,0),
 (N'Actor',N'int',4,0),(N'ProfileId',N'int',4,0),(N'SecurityStamp',N'uniqueidentifier',16,0),
 (N'BindingHash',N'binary',32,0),(N'IssuedUtc',N'datetime2',8,7),(N'ExpiresUtc',N'datetime2',8,7),
 (N'PayloadHash',N'binary',32,0),(N'ReclamoId',N'int',4,0),(N'CreatedUtc',N'datetime2',8,7);
IF EXISTS(SELECT 1 FROM @Columns e LEFT JOIN sys.columns c ON c.object_id=OBJECT_ID(N'ComplaintRequests.Receipts') AND c.name=e.C
 WHERE c.object_id IS NULL OR TYPE_NAME(c.user_type_id)<>e.Ty OR c.max_length<>e.L OR c.scale<>e.S OR c.is_nullable<>0)
 OR (SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID(N'ComplaintRequests.Receipts'))<>11
 THROW 59610,N'Complaint receipt columns differ.',1;
IF (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID(N'ComplaintRequests.Receipts') AND is_disabled=0 AND is_not_trusted=0)<>4
 OR NOT EXISTS(SELECT 1 FROM sys.foreign_key_columns WHERE parent_object_id=OBJECT_ID(N'ComplaintRequests.Receipts') AND parent_column_id=COLUMNPROPERTY(OBJECT_ID(N'ComplaintRequests.Receipts'),N'OriginalLoginId','ColumnId') AND referenced_object_id=OBJECT_ID(N'dbo.Stage2OriginalLogins') AND referenced_column_id=COLUMNPROPERTY(OBJECT_ID(N'dbo.Stage2OriginalLogins'),N'LoginId','ColumnId'))
 OR NOT EXISTS(SELECT 1 FROM sys.foreign_key_columns WHERE parent_object_id=OBJECT_ID(N'ComplaintRequests.Receipts') AND parent_column_id=COLUMNPROPERTY(OBJECT_ID(N'ComplaintRequests.Receipts'),N'Actor','ColumnId') AND referenced_object_id=OBJECT_ID(N'dbo.Usuarios') AND referenced_column_id=COLUMNPROPERTY(OBJECT_ID(N'dbo.Usuarios'),N'UsuarioId','ColumnId'))
 OR NOT EXISTS(SELECT 1 FROM sys.foreign_key_columns WHERE parent_object_id=OBJECT_ID(N'ComplaintRequests.Receipts') AND parent_column_id=COLUMNPROPERTY(OBJECT_ID(N'ComplaintRequests.Receipts'),N'ProfileId','ColumnId') AND referenced_object_id=OBJECT_ID(N'dbo.Perfiles') AND referenced_column_id=COLUMNPROPERTY(OBJECT_ID(N'dbo.Perfiles'),N'PerfilId','ColumnId'))
 OR NOT EXISTS(SELECT 1 FROM sys.foreign_key_columns WHERE parent_object_id=OBJECT_ID(N'ComplaintRequests.Receipts') AND parent_column_id=COLUMNPROPERTY(OBJECT_ID(N'ComplaintRequests.Receipts'),N'ReclamoId','ColumnId') AND referenced_object_id=OBJECT_ID(N'dbo.Reclamos') AND referenced_column_id=COLUMNPROPERTY(OBJECT_ID(N'dbo.Reclamos'),N'ReclamoId','ColumnId'))
 THROW 59610,N'Complaint receipt foreign keys differ.',1;
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'ComplaintRequests.Receipts') AND name=N'PK_ComplaintRequests' AND is_primary_key=1 AND is_unique=1 AND is_disabled=0)
 OR NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'ComplaintRequests.Receipts') AND name=N'UQ_ComplaintRequests_Reclamo' AND is_unique_constraint=1 AND is_unique=1 AND is_disabled=0)
 OR NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'ComplaintRequests.Receipts') AND name=N'IX_ComplaintRequests_Owner' AND is_unique=0 AND is_disabled=0)
 OR NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'ComplaintRequests.Receipts') AND name=N'CK_ComplaintRequests_Context' AND parent_column_id=0 AND is_disabled=0 AND is_not_trusted=0)
 OR NOT EXISTS(SELECT 1 FROM sys.default_constraints WHERE parent_object_id=OBJECT_ID(N'ComplaintRequests.Receipts') AND name=N'DF_ComplaintRequests_Created' AND parent_column_id=COLUMNPROPERTY(OBJECT_ID(N'ComplaintRequests.Receipts'),N'CreatedUtc','ColumnId'))
 THROW 59610,N'Complaint receipt constraints differ.',1;
IF (SELECT COUNT(*) FROM sys.index_columns c JOIN sys.indexes i ON i.object_id=c.object_id AND i.index_id=c.index_id WHERE c.object_id=OBJECT_ID(N'ComplaintRequests.Receipts') AND i.name=N'PK_ComplaintRequests' AND c.key_ordinal>0)<>1
 OR NOT EXISTS(SELECT 1 FROM sys.index_columns c JOIN sys.indexes i ON i.object_id=c.object_id AND i.index_id=c.index_id WHERE c.object_id=OBJECT_ID(N'ComplaintRequests.Receipts') AND i.name=N'PK_ComplaintRequests' AND c.key_ordinal=1 AND c.column_id=COLUMNPROPERTY(c.object_id,N'RequestId','ColumnId'))
 OR (SELECT COUNT(*) FROM sys.index_columns c JOIN sys.indexes i ON i.object_id=c.object_id AND i.index_id=c.index_id WHERE c.object_id=OBJECT_ID(N'ComplaintRequests.Receipts') AND i.name=N'UQ_ComplaintRequests_Reclamo' AND c.key_ordinal>0)<>1
 OR NOT EXISTS(SELECT 1 FROM sys.index_columns c JOIN sys.indexes i ON i.object_id=c.object_id AND i.index_id=c.index_id WHERE c.object_id=OBJECT_ID(N'ComplaintRequests.Receipts') AND i.name=N'UQ_ComplaintRequests_Reclamo' AND c.key_ordinal=1 AND c.column_id=COLUMNPROPERTY(c.object_id,N'ReclamoId','ColumnId'))
 OR (SELECT COUNT(*) FROM sys.index_columns c JOIN sys.indexes i ON i.object_id=c.object_id AND i.index_id=c.index_id WHERE c.object_id=OBJECT_ID(N'ComplaintRequests.Receipts') AND i.name=N'IX_ComplaintRequests_Owner' AND c.key_ordinal>0)<>3
 OR NOT EXISTS(SELECT 1 FROM sys.index_columns c JOIN sys.indexes i ON i.object_id=c.object_id AND i.index_id=c.index_id WHERE c.object_id=OBJECT_ID(N'ComplaintRequests.Receipts') AND i.name=N'IX_ComplaintRequests_Owner' AND c.key_ordinal=1 AND c.column_id=COLUMNPROPERTY(c.object_id,N'OriginalLoginId','ColumnId'))
 OR NOT EXISTS(SELECT 1 FROM sys.index_columns c JOIN sys.indexes i ON i.object_id=c.object_id AND i.index_id=c.index_id WHERE c.object_id=OBJECT_ID(N'ComplaintRequests.Receipts') AND i.name=N'IX_ComplaintRequests_Owner' AND c.key_ordinal=2 AND c.column_id=COLUMNPROPERTY(c.object_id,N'Actor','ColumnId'))
 OR NOT EXISTS(SELECT 1 FROM sys.index_columns c JOIN sys.indexes i ON i.object_id=c.object_id AND i.index_id=c.index_id WHERE c.object_id=OBJECT_ID(N'ComplaintRequests.Receipts') AND i.name=N'IX_ComplaintRequests_Owner' AND c.key_ordinal=3 AND c.column_id=COLUMNPROPERTY(c.object_id,N'ReclamoId','ColumnId'))
 THROW 59610,N'Complaint receipt key columns differ.',1;
IF NOT EXISTS(SELECT 1 FROM sys.triggers WHERE object_id=OBJECT_ID(N'ComplaintRequests.ImmutableReceipts') AND parent_id=OBJECT_ID(N'ComplaintRequests.Receipts') AND is_instead_of_trigger=1 AND is_disabled=0)
 OR (SELECT COUNT(*) FROM sys.trigger_events WHERE object_id=OBJECT_ID(N'ComplaintRequests.ImmutableReceipts') AND type_desc IN(N'UPDATE',N'DELETE'))<>2
 THROW 59610,N'Complaint receipt immutability differs.',1;
DECLARE @Parameters table(N sysname,Ty sysname,L smallint,S tinyint);
INSERT @Parameters VALUES
 (N'@LoginId',N'uniqueidentifier',16,0),(N'@UsuarioId',N'int',4,0),(N'@PerfilId',N'int',4,0),
 (N'@SecurityStamp',N'uniqueidentifier',16,0),(N'@BindingHash',N'binary',32,0),
 (N'@IssuedUtc',N'datetime2',8,7),(N'@ExpiresUtc',N'datetime2',8,7),(N'@RequestId',N'uniqueidentifier',16,0),
 (N'@ClienteUsuarioId',N'int',4,0),(N'@Asunto',N'nvarchar',300,0),(N'@Descripcion',N'nvarchar',2000,0),
 (N'@FacturaId',N'int',4,0),(N'@PedidoId',N'int',4,0),(N'@Categoria',N'nvarchar',80,0),(N'@Prioridad',N'nvarchar',40,0);
IF EXISTS(SELECT 1 FROM @Parameters e LEFT JOIN sys.parameters p ON p.object_id=OBJECT_ID(N'dbo.sp_Reclamos_Create') AND p.name=e.N
 WHERE p.object_id IS NULL OR TYPE_NAME(p.user_type_id)<>e.Ty OR p.max_length<>e.L OR p.scale<>e.S OR p.is_output<>0)
 OR (SELECT COUNT(*) FROM sys.parameters WHERE object_id=OBJECT_ID(N'dbo.sp_Reclamos_Create'))<>15
 THROW 59610,N'Complaint creation signature differs.',1;
IF ISNULL(HASHBYTES('SHA2_256',CONVERT(varbinary(max),OBJECT_DEFINITION(OBJECT_ID(N'dbo.sp_Reclamos_Create')))),0x)<>0x5ABA8D6804DA3A6E3CB8287566FA5951C8B57822E22BDDA46E75DB8FD9F1E273
 THROW 59610,N'Complaint creation module differs.',1;
IF ISNULL(HASHBYTES('SHA2_256',CONVERT(varbinary(max),OBJECT_DEFINITION(OBJECT_ID(N'ComplaintRequests.ImmutableReceipts')))),0x)<>0x0209FCC71CDB787738A31038222E2472E57165922848BC38BB2949FFE1A77924
 THROW 59610,N'Complaint receipt trigger differs.',1;
IF ISNULL(HASHBYTES('SHA2_256',CONVERT(varbinary(max),OBJECT_DEFINITION(OBJECT_ID(N'dbo.sp_Stage2_RequireOriginalLogin')))),0x)<>0x8C8515B2D2DD516A737A548EE7A03AB3E12D101BB8D6B27A1FBDC8846E31479C
 THROW 59610,N'Original-login guard differs.',1;
IF EXISTS(SELECT 1 FROM sys.sql_modules WHERE object_id IN(OBJECT_ID(N'dbo.sp_Reclamos_Create'),OBJECT_ID(N'ComplaintRequests.ImmutableReceipts')) AND (uses_quoted_identifier<>1 OR uses_ansi_nulls<>1))
 THROW 59610,N'Complaint module options differ.',1;
IF EXISTS(SELECT 1 FROM sys.database_permissions WHERE state IN(N'G',N'W') AND ((class=1 AND major_id=OBJECT_ID(N'ComplaintRequests.Receipts')) OR (class=3 AND major_id=SCHEMA_ID(N'ComplaintRequests'))))
 OR EXISTS(SELECT 1 FROM sys.database_permissions WHERE state IN(N'G',N'W') AND grantee_principal_id=DATABASE_PRINCIPAL_ID(N'public') AND permission_name IN(N'EXECUTE',N'CONTROL') AND class=1 AND major_id=OBJECT_ID(N'dbo.sp_Reclamos_Create'))
 THROW 59610,N'Unexpected direct receipt or public creation grant.',1;
-- Report object grants for the separately reviewed APP provisioning; names and metadata only.
SELECT p.name AS PrincipalName,g.state_desc,g.permission_name
FROM sys.database_permissions g JOIN sys.database_principals p ON p.principal_id=g.grantee_principal_id
WHERE g.class=1 AND g.major_id=OBJECT_ID(N'dbo.sp_Reclamos_Create')
ORDER BY p.name,g.permission_name;
