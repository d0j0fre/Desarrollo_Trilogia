-- Read-only metadata verification; not authorization to query an operational destination.
SET NOCOUNT ON;
IF COL_LENGTH(N'dbo.Usuarios',N'ChatV2Revision') IS NULL
 OR NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.Usuarios') AND name=N'ChatV2Revision' AND system_type_id=189 AND is_nullable=0)
 THROW 58411,N'Canonical actor revision missing.',1;
IF OBJECT_ID(N'ChatV2P.Intents',N'U') IS NULL OR OBJECT_ID(N'ChatV2P.Messages',N'U') IS NULL
 OR OBJECT_ID(N'ChatV2P.Requests',N'U') IS NULL OR OBJECT_ID(N'ChatV2P.Outbox',N'U') IS NULL
 OR OBJECT_ID(N'ChatV2P.Conversations',N'U') IS NULL
 THROW 58411,N'Durable contract missing.',1;
IF EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id IN(OBJECT_ID(N'ChatV2P.Intents'),OBJECT_ID(N'ChatV2P.Messages'),OBJECT_ID(N'ChatV2P.Conversations')) AND (is_disabled=1 OR is_not_trusted=1))
 THROW 58411,N'Untrusted constraint.',1;
IF EXISTS(SELECT 1 FROM sys.foreign_keys WHERE schema_id=SCHEMA_ID(N'ChatV2P') AND (is_disabled=1 OR is_not_trusted=1))
 THROW 58411,N'Untrusted ownership dependency.',1;
IF (SELECT COUNT(*) FROM sys.triggers t JOIN sys.objects o ON o.object_id=t.object_id
 WHERE o.schema_id=SCHEMA_ID(N'ChatV2P') AND t.is_disabled=0)<>3
 THROW 58411,N'Immutable storage triggers missing.',1;
SELECT s.name AS SchemaName,o.name AS ModuleName,
 CONVERT(varchar(64),HASHBYTES('SHA2_256',CONVERT(varbinary(max),m.definition)),2) AS DefinitionSha256Utf16
FROM sys.sql_modules m JOIN sys.objects o ON o.object_id=m.object_id JOIN sys.schemas s ON s.schema_id=o.schema_id
WHERE s.name=N'ChatV2P' ORDER BY o.name;
-- Structural checks and hashes must be compared to the accredited deployment manifest.
-- They do not certify effective grants, concurrency, expiry or HTTP behavior.
