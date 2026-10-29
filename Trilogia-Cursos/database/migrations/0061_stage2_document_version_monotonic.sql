-- A late publication can confirm history but cannot move the current version backwards.
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF SESSIONPROPERTY('QUOTED_IDENTIFIER')<>1 OR SESSIONPROPERTY('ANSI_NULLS')<>1 THROW 59700,N'SQL options unavailable.',1;
DECLARE @Hash nvarchar(128)=N'$(MigrationSha256)';
IF LEN(@Hash)<>64 OR @Hash LIKE N'%[^0-9a-fA-F]%' THROW 59700,N'Invalid migration hash.',1;
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL THROW 59700,N'Ledger unavailable.',1;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @Lock int;
 EXEC @Lock=sys.sp_getapplock @Resource=N'Stage2:0061:install',@LockMode=N'Exclusive',@LockOwner=N'Transaction',@LockTimeout=10000;
 IF @Lock<0 THROW 59700,N'Concurrent installation.',1;
 IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WITH(UPDLOCK,HOLDLOCK) WHERE MigrationId=N'0061_stage2_document_version_monotonic')
 BEGIN
  IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0061_stage2_document_version_monotonic' AND FileSha256=UPPER(@Hash) AND Status=N'Applied') THROW 59700,N'Ledger conflict.',1;
  COMMIT;RETURN;
 END;
 IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId LIKE N'0061%') THROW 59700,N'Migration identifier collision.',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0056_stage2_document_explicit_authorization' AND FileSha256=N'73ED2CEA48585FD7EA369262B88A176CEB03A91DA5A4EFD36F001A8DD5383455' AND Status=N'Applied')
  OR NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0060_stage2_complaint_request_receipts' AND FileSha256=N'67465E1F6835E99746A51C622D75BCF1EC2080A396C577260239E53AC544F996' AND Status=N'Applied')
  THROW 59700,N'Exact 0056 or 0060 prerequisite unavailable.',1;
 IF ISNULL(HASHBYTES('SHA2_256',CONVERT(varbinary(max),OBJECT_DEFINITION(OBJECT_ID(N'DocumentSecurity.Document_MarkVersionReady')))),0x)<>0x0802F6C0F5E2B13E6465C97CB614E14446D6374EC61DDC11FE81B9C76BE25A0F
  THROW 59700,N'Previous version publication contract differs.',1;
 IF ISNULL(HASHBYTES('SHA2_256',CONVERT(varbinary(max),OBJECT_DEFINITION(OBJECT_ID(N'DocumentSecurity.CanAccess')))),0x)<>0xCFD8943D94CC972CD30318EF490D0A79649102FCEAF827088BD77C7F78C9AD44
  OR ISNULL(HASHBYTES('SHA2_256',CONVERT(varbinary(max),OBJECT_DEFINITION(OBJECT_ID(N'dbo.sp_Stage2_RequireOriginalLogin')))),0x)<>0x8C8515B2D2DD516A737A548EE7A03AB3E12D101BB8D6B27A1FBDC8846E31479C
  THROW 59700,N'Canonical document or login authorization differs.',1;
 DECLARE @Columns table(T sysname,C sysname,Ty sysname,L smallint,N bit);
 INSERT @Columns VALUES
  (N'DocumentosDigitales',N'DocumentoId',N'int',4,0),(N'DocumentosDigitales',N'VersionActualId',N'int',4,1),
  (N'DocumentosDigitales',N'ResponsableUsuarioId',N'int',4,0),(N'DocumentosDigitales',N'DepartamentoId',N'int',4,1),
  (N'DocumentoVersiones',N'DocumentoVersionId',N'int',4,0),(N'DocumentoVersiones',N'DocumentoId',N'int',4,0),
  (N'DocumentoVersiones',N'Version',N'int',4,0),(N'DocumentoVersiones',N'StorageStatus',N'nvarchar',40,0),
  (N'DocumentoVersiones',N'CreadoPorUsuarioId',N'int',4,0);
 IF EXISTS(SELECT 1 FROM @Columns e LEFT JOIN sys.columns c ON c.object_id=OBJECT_ID(N'dbo.'+e.T) AND c.name=e.C
  WHERE c.object_id IS NULL OR TYPE_NAME(c.user_type_id)<>e.Ty OR c.max_length<>e.L OR c.is_nullable<>e.N)
  OR OBJECT_ID(N'dbo.DocumentoAuditoria',N'U') IS NULL
  OR NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.DocumentoVersiones') AND name=N'UQ_DocumentoVersiones_DocumentoVersion' AND is_unique=1 AND is_disabled=0)
  THROW 59700,N'Document version schema differs.',1;
 DECLARE @Permissions table(Grantee int,Grantor int,PermissionType char(4) COLLATE DATABASE_DEFAULT,State char(1) COLLATE DATABASE_DEFAULT,MinorId int);
 INSERT @Permissions SELECT grantee_principal_id,grantor_principal_id,type COLLATE DATABASE_DEFAULT,state COLLATE DATABASE_DEFAULT,minor_id FROM sys.database_permissions
 WHERE class=1 AND major_id=OBJECT_ID(N'DocumentSecurity.Document_MarkVersionReady');

 EXEC(N'ALTER PROCEDURE DocumentSecurity.Document_MarkVersionReady
 @OriginalLoginId uniqueidentifier,@ContextActor int,@ContextProfile int,@ContextStamp uniqueidentifier,@ContextBinding binary(32),
 @ContextIssuedUtc datetime2(7),@ContextExpiresUtc datetime2(7),@Actor int,@DocumentoId int,@DocumentoVersionId int,@UsuarioId int,@UsuarioNombre nvarchar(150)
 AS
 BEGIN
  SET NOCOUNT ON;SET XACT_ABORT ON;
  IF @@TRANCOUNT=0 THROW 57400,N''Falta transaccion de login original.'',1;
  IF @Actor IS NULL OR @Actor<>@ContextActor OR @UsuarioId IS NULL OR @UsuarioId<>@Actor THROW 57401,N''Documento no disponible.'',1;
  BEGIN TRY
   BEGIN TRANSACTION;
   EXEC dbo.sp_Stage2_RequireOriginalLogin @OriginalLoginId,@ContextActor,@ContextProfile,@ContextStamp,@ContextBinding,@ContextIssuedUtc,@ContextExpiresUtc;
   DECLARE @CurrentId int,@CurrentVersion int,@CurrentStatus nvarchar(20),@CandidateVersion int,@CandidateStatus nvarchar(20),@Creator int;
   SELECT @CurrentId=VersionActualId FROM dbo.DocumentosDigitales WITH(UPDLOCK,HOLDLOCK)
   WHERE DocumentoId=@DocumentoId AND DocumentSecurity.CanAccess(@Actor,ResponsableUsuarioId,DepartamentoId,''Version'',SYSUTCDATETIME())=1;
   IF @CurrentId IS NULL THROW 57401,N''Documento no disponible.'',1;
   SELECT @CurrentVersion=Version,@CurrentStatus=StorageStatus FROM dbo.DocumentoVersiones WITH(UPDLOCK,HOLDLOCK)
   WHERE DocumentoVersionId=@CurrentId AND DocumentoId=@DocumentoId;
   SELECT @CandidateVersion=Version,@CandidateStatus=StorageStatus,@Creator=CreadoPorUsuarioId FROM dbo.DocumentoVersiones WITH(UPDLOCK,HOLDLOCK)
   WHERE DocumentoVersionId=@DocumentoVersionId AND DocumentoId=@DocumentoId;
   IF @CurrentVersion IS NULL OR @CurrentVersion<=0 OR @CurrentStatus<>N''Ready''
    OR @CandidateVersion IS NULL OR @CandidateVersion<=0 OR @CandidateStatus NOT IN(N''Pending'',N''Ready'')
    OR @Creator<>@Actor OR @Creator IS NULL
    OR (@CandidateVersion=@CurrentVersion AND @DocumentoVersionId<>@CurrentId)
    OR EXISTS(SELECT 1 FROM dbo.DocumentoVersiones WITH(HOLDLOCK) WHERE DocumentoId=@DocumentoId AND StorageStatus=N''Ready'' AND Version>@CurrentVersion)
    THROW 59701,N''Estado de publicacion no acreditado.'',1;
   IF @CandidateStatus=N''Ready''
   BEGIN
    -- A committed receipt is already present. Never repromote an older Ready version.
    IF @CandidateVersion>@CurrentVersion THROW 59701,N''Estado de publicacion no acreditado.'',1;
    EXEC dbo.sp_Stage2_RequireOriginalLogin @OriginalLoginId,@ContextActor,@ContextProfile,@ContextStamp,@ContextBinding,@ContextIssuedUtc,@ContextExpiresUtc;
    IF NOT EXISTS(SELECT 1 FROM dbo.DocumentosDigitales WITH(HOLDLOCK) WHERE DocumentoId=@DocumentoId AND DocumentSecurity.CanAccess(@Actor,ResponsableUsuarioId,DepartamentoId,''Version'',SYSUTCDATETIME())=1)
     THROW 57401,N''Documento no disponible.'',1;
    COMMIT;RETURN;
   END;
   IF @CandidateVersion=@CurrentVersion THROW 59701,N''Estado de publicacion no acreditado.'',1;
   SELECT @UsuarioNombre=NombreCompleto FROM dbo.Usuarios WHERE UsuarioId=@Actor AND Activo=1;
   IF @UsuarioNombre IS NULL THROW 57401,N''Documento no disponible.'',1;
   UPDATE dbo.DocumentoVersiones SET StorageStatus=N''Ready''
   WHERE DocumentoVersionId=@DocumentoVersionId AND DocumentoId=@DocumentoId AND StorageStatus=N''Pending'' AND CreadoPorUsuarioId=@Actor;
   IF @@ROWCOUNT<>1 THROW 54006,N''No existe una version pendiente autorizada.'',1;
   IF @CandidateVersion>@CurrentVersion
   BEGIN
    UPDATE dbo.DocumentosDigitales SET VersionActualId=@DocumentoVersionId,ActualizadoPorUsuarioId=@Actor,
     ActualizadoPorNombre=@UsuarioNombre,FechaActualizacionUtc=SYSUTCDATETIME()
    WHERE DocumentoId=@DocumentoId AND VersionActualId=@CurrentId;
    IF @@ROWCOUNT<>1 THROW 59701,N''Estado de publicacion no acreditado.'',1;
   END;
   INSERT dbo.DocumentoAuditoria(DocumentoId,Accion,UsuarioId,UsuarioNombre,Detalle)
   VALUES(@DocumentoId,N''Reemplazar archivo'',@Actor,@UsuarioNombre,
    CONCAT(N''Version '',@DocumentoVersionId,N'' confirmada'',CASE WHEN @CandidateVersion>@CurrentVersion THEN N'' como actual.'' ELSE N'' como historial; actual conservada.'' END));
   EXEC dbo.sp_Stage2_RequireOriginalLogin @OriginalLoginId,@ContextActor,@ContextProfile,@ContextStamp,@ContextBinding,@ContextIssuedUtc,@ContextExpiresUtc;
   IF NOT EXISTS(SELECT 1 FROM dbo.DocumentosDigitales WITH(HOLDLOCK) WHERE DocumentoId=@DocumentoId AND DocumentSecurity.CanAccess(@Actor,ResponsableUsuarioId,DepartamentoId,''Version'',SYSUTCDATETIME())=1)
    THROW 57401,N''Documento no disponible.'',1;
   COMMIT;
  END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK;THROW;END CATCH;
 END;');
 IF EXISTS(SELECT Grantee,Grantor,PermissionType COLLATE DATABASE_DEFAULT,State COLLATE DATABASE_DEFAULT,MinorId FROM @Permissions
  EXCEPT SELECT grantee_principal_id,grantor_principal_id,type COLLATE DATABASE_DEFAULT,state COLLATE DATABASE_DEFAULT,minor_id FROM sys.database_permissions WHERE class=1 AND major_id=OBJECT_ID(N'DocumentSecurity.Document_MarkVersionReady'))
  OR EXISTS(SELECT grantee_principal_id,grantor_principal_id,type COLLATE DATABASE_DEFAULT,state COLLATE DATABASE_DEFAULT,minor_id FROM sys.database_permissions WHERE class=1 AND major_id=OBJECT_ID(N'DocumentSecurity.Document_MarkVersionReady')
  EXCEPT SELECT Grantee,Grantor,PermissionType COLLATE DATABASE_DEFAULT,State COLLATE DATABASE_DEFAULT,MinorId FROM @Permissions)
  THROW 59700,N'Publication permissions changed unexpectedly.',1;
 INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes)
 VALUES(N'0061_stage2_document_version_monotonic',N'0061_stage2_document_version_monotonic.sql',UPPER(@Hash),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),N'Serialize publication per document; late Ready versions remain historical; Ready retry has no pointer or audit effect. Preserves creator, original-login and explicit Version authorization. No historical rewrite or grants.');
 COMMIT;
END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK;THROW;END CATCH;
