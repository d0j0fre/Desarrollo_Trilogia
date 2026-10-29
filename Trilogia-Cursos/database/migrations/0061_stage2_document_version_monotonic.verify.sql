SET NOCOUNT ON;
-- Metadata/ledger verification only. No document or file is opened or changed.
DECLARE @Hash nvarchar(128)=N'$(MigrationSha256)';
IF LEN(@Hash)<>64 OR @Hash LIKE N'%[^0-9a-fA-F]%'
 OR NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0061_stage2_document_version_monotonic' AND FileSha256=UPPER(@Hash) AND Status=N'Applied')
 THROW 59710,N'Document monotonic-publication ledger differs.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0056_stage2_document_explicit_authorization' AND FileSha256=N'73ED2CEA48585FD7EA369262B88A176CEB03A91DA5A4EFD36F001A8DD5383455' AND Status=N'Applied')
 OR NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0060_stage2_complaint_request_receipts' AND FileSha256=N'67465E1F6835E99746A51C622D75BCF1EC2080A396C577260239E53AC544F996' AND Status=N'Applied')
 THROW 59710,N'Document monotonic-publication prerequisites differ.',1;
IF ISNULL(HASHBYTES('SHA2_256',CONVERT(varbinary(max),OBJECT_DEFINITION(OBJECT_ID(N'DocumentSecurity.Document_MarkVersionReady')))),0x)<>0x61EC40F559383122E4E662460F2E0024644CBEFDFBEF9C5C15FAEB8E1E13C22D
 THROW 59710,N'Document publication module differs.',1;
IF ISNULL(HASHBYTES('SHA2_256',CONVERT(varbinary(max),OBJECT_DEFINITION(OBJECT_ID(N'DocumentSecurity.CanAccess')))),0x)<>0xCFD8943D94CC972CD30318EF490D0A79649102FCEAF827088BD77C7F78C9AD44
 OR ISNULL(HASHBYTES('SHA2_256',CONVERT(varbinary(max),OBJECT_DEFINITION(OBJECT_ID(N'dbo.sp_Stage2_RequireOriginalLogin')))),0x)<>0x8C8515B2D2DD516A737A548EE7A03AB3E12D101BB8D6B27A1FBDC8846E31479C
 THROW 59710,N'Document publication authorization differs.',1;
IF NOT EXISTS(SELECT 1 FROM sys.sql_modules WHERE object_id=OBJECT_ID(N'DocumentSecurity.Document_MarkVersionReady') AND uses_quoted_identifier=1 AND uses_ansi_nulls=1 AND execute_as_principal_id IS NULL)
 THROW 59710,N'Document publication module options differ.',1;
DECLARE @Parameters table(N sysname,Ty sysname,L smallint,S tinyint);
INSERT @Parameters VALUES
 (N'@OriginalLoginId',N'uniqueidentifier',16,0),(N'@ContextActor',N'int',4,0),(N'@ContextProfile',N'int',4,0),
 (N'@ContextStamp',N'uniqueidentifier',16,0),(N'@ContextBinding',N'binary',32,0),
 (N'@ContextIssuedUtc',N'datetime2',8,7),(N'@ContextExpiresUtc',N'datetime2',8,7),
 (N'@Actor',N'int',4,0),(N'@DocumentoId',N'int',4,0),(N'@DocumentoVersionId',N'int',4,0),
 (N'@UsuarioId',N'int',4,0),(N'@UsuarioNombre',N'nvarchar',300,0);
IF EXISTS(SELECT 1 FROM @Parameters e LEFT JOIN sys.parameters p ON p.object_id=OBJECT_ID(N'DocumentSecurity.Document_MarkVersionReady') AND p.name=e.N
 WHERE p.object_id IS NULL OR TYPE_NAME(p.user_type_id)<>e.Ty OR p.max_length<>e.L OR p.scale<>e.S OR p.is_output<>0)
 OR (SELECT COUNT(*) FROM sys.parameters WHERE object_id=OBJECT_ID(N'DocumentSecurity.Document_MarkVersionReady'))<>12
 THROW 59710,N'Document publication signature differs.',1;
DECLARE @Columns table(T sysname,C sysname,Ty sysname,L smallint,N bit);
INSERT @Columns VALUES
 (N'DocumentosDigitales',N'DocumentoId',N'int',4,0),(N'DocumentosDigitales',N'VersionActualId',N'int',4,1),
 (N'DocumentosDigitales',N'ResponsableUsuarioId',N'int',4,0),(N'DocumentosDigitales',N'DepartamentoId',N'int',4,1),
 (N'DocumentoVersiones',N'DocumentoVersionId',N'int',4,0),(N'DocumentoVersiones',N'DocumentoId',N'int',4,0),
 (N'DocumentoVersiones',N'Version',N'int',4,0),(N'DocumentoVersiones',N'StorageStatus',N'nvarchar',40,0),
 (N'DocumentoVersiones',N'CreadoPorUsuarioId',N'int',4,0);
IF EXISTS(SELECT 1 FROM @Columns e LEFT JOIN sys.columns c ON c.object_id=OBJECT_ID(N'dbo.'+e.T) AND c.name=e.C
 WHERE c.object_id IS NULL OR TYPE_NAME(c.user_type_id)<>e.Ty OR c.max_length<>e.L OR c.is_nullable<>e.N)
 THROW 59710,N'Document version columns differ.',1;
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.DocumentoVersiones') AND name=N'UQ_DocumentoVersiones_DocumentoVersion' AND is_unique=1 AND is_disabled=0 AND has_filter=0)
 OR (SELECT COUNT(*) FROM sys.index_columns c JOIN sys.indexes i ON i.object_id=c.object_id AND i.index_id=c.index_id WHERE c.object_id=OBJECT_ID(N'dbo.DocumentoVersiones') AND i.name=N'UQ_DocumentoVersiones_DocumentoVersion' AND c.key_ordinal>0)<>2
 OR NOT EXISTS(SELECT 1 FROM sys.index_columns c JOIN sys.indexes i ON i.object_id=c.object_id AND i.index_id=c.index_id WHERE c.object_id=OBJECT_ID(N'dbo.DocumentoVersiones') AND i.name=N'UQ_DocumentoVersiones_DocumentoVersion' AND c.key_ordinal=1 AND c.column_id=COLUMNPROPERTY(c.object_id,N'DocumentoId','ColumnId'))
 OR NOT EXISTS(SELECT 1 FROM sys.index_columns c JOIN sys.indexes i ON i.object_id=c.object_id AND i.index_id=c.index_id WHERE c.object_id=OBJECT_ID(N'dbo.DocumentoVersiones') AND i.name=N'UQ_DocumentoVersiones_DocumentoVersion' AND c.key_ordinal=2 AND c.column_id=COLUMNPROPERTY(c.object_id,N'Version','ColumnId'))
 THROW 59710,N'Document version uniqueness differs.',1;
IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys f JOIN sys.foreign_key_columns c ON c.constraint_object_id=f.object_id WHERE f.parent_object_id=OBJECT_ID(N'dbo.DocumentosDigitales') AND c.parent_column_id=COLUMNPROPERTY(f.parent_object_id,N'VersionActualId','ColumnId') AND c.referenced_object_id=OBJECT_ID(N'dbo.DocumentoVersiones') AND c.referenced_column_id=COLUMNPROPERTY(c.referenced_object_id,N'DocumentoVersionId','ColumnId') AND f.is_disabled=0 AND f.is_not_trusted=0)
 THROW 59710,N'Current document-version foreign key differs.',1;
IF EXISTS(SELECT 1 FROM sys.database_permissions WHERE class=1 AND major_id=OBJECT_ID(N'DocumentSecurity.Document_MarkVersionReady') AND grantee_principal_id=DATABASE_PRINCIPAL_ID(N'public') AND state IN(N'G',N'W') AND permission_name IN(N'EXECUTE',N'CONTROL'))
 THROW 59710,N'Unexpected public publication grant.',1;
SELECT p.name AS PrincipalName,g.state_desc,g.permission_name
FROM sys.database_permissions g JOIN sys.database_principals p ON p.principal_id=g.grantee_principal_id
WHERE g.class=1 AND g.major_id=OBJECT_ID(N'DocumentSecurity.Document_MarkVersionReady')
ORDER BY p.name,g.permission_name;
