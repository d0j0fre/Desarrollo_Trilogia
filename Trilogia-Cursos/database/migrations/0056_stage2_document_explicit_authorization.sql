-- Restore the approved owner OR explicit department/operation authorization policy.
-- Profile labels confer no access and do not exclude an otherwise explicitly authorized active user.
SET NOCOUNT ON;SET XACT_ABORT ON;
IF SESSIONPROPERTY('QUOTED_IDENTIFIER')<>1 OR SESSIONPROPERTY('ANSI_NULLS')<>1 THROW 57520,N'SQL options unavailable.',1;
DECLARE @Hash nvarchar(128)=N'$(MigrationSha256)';
IF LEN(@Hash)<>64 OR @Hash LIKE N'%[^0-9a-fA-F]%' THROW 57520,N'Invalid migration hash.',1;
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL THROW 57520,N'Ledger unavailable.',1;
BEGIN TRY BEGIN TRANSACTION;
DECLARE @Lock int;EXEC @Lock=sys.sp_getapplock @Resource=N'Stage2:0056:install',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=5000;
IF @Lock<0 THROW 57520,N'Installation conflict.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0056_stage2_document_explicit_authorization') BEGIN
 IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0056_stage2_document_explicit_authorization' AND FileSha256=UPPER(@Hash) AND Status=N'Applied') THROW 57520,N'Ledger conflict.',1;
 COMMIT;RETURN;END;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId LIKE N'0056%') THROW 57520,N'Migration identifier collision.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0055_stage2_document_delegation' AND Status=N'Applied' AND FileSha256=N'F5011FFEE0E4DAF7F053E7389C37699570361B8F4E9854928CE306BC80C229B9') THROW 57520,N'Exact document authority prerequisite missing.',1;
IF ISNULL(HASHBYTES('SHA2_256',CONVERT(varbinary(max),OBJECT_DEFINITION(OBJECT_ID(N'DocumentSecurity.CanAccess')))),0x)<>0xE3661AADF166D4E8DDB7F71707F740E17982E64AE5649B5EBAC977EF6014D9E4 THROW 57520,N'Previous document module differs: CanAccess.',1;
IF ISNULL(HASHBYTES('SHA2_256',CONVERT(varbinary(max),OBJECT_DEFINITION(OBJECT_ID(N'DocumentSecurity.SetGrant')))),0x)<>0x522C04B186DE3F0F2004834419B4E6CA3D19FA9CA828270C98EB5D1C15C923DF THROW 57520,N'Previous document module differs: SetGrant.',1;
IF ISNULL(HASHBYTES('SHA2_256',CONVERT(varbinary(max),OBJECT_DEFINITION(OBJECT_ID(N'DocumentSecurity.GrantOptions')))),0x)<>0x570F871037C23464135CB28B803ABFD2259176DC2CFAEBE82359802638E3C55D THROW 57520,N'Previous document module differs: GrantOptions.',1;
IF ISNULL(HASHBYTES('SHA2_256',CONVERT(varbinary(max),OBJECT_DEFINITION(OBJECT_ID(N'DocumentSecurity.ListGrants')))),0x)<>0xCA95D736BE9937E0D9B4AFD1A5CF7D195F94A09FC68459E18F8441528DAAB919 THROW 57520,N'Previous document module differs: ListGrants.',1;
EXEC(N'ALTER FUNCTION DocumentSecurity.CanAccess(@Actor int,@Owner int,@Department int,@Operation varchar(10),@Now datetime2(7))
RETURNS bit AS BEGIN
 IF @Actor=@Owner AND @Actor>0 RETURN 1;
 IF @Department IS NULL OR @Actor IS NULL OR @Now IS NULL RETURN 0;
 IF EXISTS(SELECT 1 FROM DocumentSecurity.Grants g WITH(HOLDLOCK)
 JOIN dbo.DepartamentosOperativos d WITH(HOLDLOCK) ON d.DepartamentoId=g.DepartmentId AND d.Activo=1
 JOIN dbo.Usuarios u WITH(HOLDLOCK) ON u.UsuarioId=g.Grantee AND u.Activo=1 AND u.SecurityStamp=g.GranteeStamp
 JOIN dbo.Perfiles p WITH(HOLDLOCK) ON p.PerfilId=u.PerfilId AND p.Activo=1
 JOIN dbo.Usuarios issuer WITH(HOLDLOCK) ON issuer.UsuarioId=g.GrantedBy AND issuer.Activo=1 AND issuer.SecurityStamp=g.GrantorStamp
 JOIN dbo.Perfiles ip WITH(HOLDLOCK) ON ip.PerfilId=issuer.PerfilId AND ip.Activo=1
 JOIN dbo.PerfilPermisos pp WITH(HOLDLOCK) ON pp.PerfilId=issuer.PerfilId
 JOIN dbo.Permisos pe WITH(HOLDLOCK) ON pe.PermisoId=pp.PermisoId AND pe.Codigo=N''DOCUMENTOS_DELEGAR'' AND pe.Activo=1
 WHERE g.DepartmentId=@Department AND g.Grantee=@Actor AND g.Operation=@Operation AND g.Enabled=1 AND g.ExpiresUtc>@Now)
 RETURN 1;
 RETURN 0;
END;
');
EXEC(N'ALTER PROCEDURE DocumentSecurity.SetGrant
 @LoginId uniqueidentifier,@UsuarioId int,@PerfilId int,@SecurityStamp uniqueidentifier,@BindingHash binary(32),
 @IssuedUtc datetime2(7),@ExpiresUtc datetime2(7),@Department int,@Grantee int,@Operation varchar(10),
 @Enabled bit,@ExpectedRevision bigint,@GrantExpiresUtc datetime2(7)=NULL,@Reason nvarchar(250)
AS BEGIN SET NOCOUNT ON;SET XACT_ABORT ON;
 IF @Department IS NULL OR @Department<=0 OR @Grantee IS NULL OR @Grantee<=0 OR @Grantee=@UsuarioId
 OR @Operation IS NULL OR @Operation COLLATE Latin1_General_100_BIN2 NOT IN(''Read'',''Edit'',''Version'',''Download'',''Alert'',''Create'')
 OR @Enabled IS NULL OR @ExpectedRevision IS NULL OR @ExpectedRevision<0 OR NULLIF(LTRIM(RTRIM(@Reason)),N'''') IS NULL
 THROW 57505,N''Invalid department authorization.'',1;
 BEGIN TRY BEGIN TRANSACTION;
 EXEC dbo.sp_Stage2_RequireOriginalLogin @LoginId,@UsuarioId,@PerfilId,@SecurityStamp,@BindingHash,@IssuedUtc,@ExpiresUtc,N''DOCUMENTOS_DELEGAR'';
 DECLARE @Now datetime2(7)=SYSUTCDATETIME(),@Revision bigint,@OldEnabled bit,@OldExpiry datetime2(7),
 @TargetStamp uniqueidentifier,@OldTarget uniqueidentifier,@OldIssuer uniqueidentifier,@OldGrantor int;
 SELECT @Revision=Revision,@OldEnabled=Enabled,@OldExpiry=ExpiresUtc,@OldTarget=GranteeStamp,@OldIssuer=GrantorStamp,@OldGrantor=GrantedBy
 FROM DocumentSecurity.Grants WITH(UPDLOCK,HOLDLOCK) WHERE DepartmentId=@Department AND Grantee=@Grantee AND Operation=@Operation;
 IF ISNULL(@Revision,0)<>@ExpectedRevision OR (@Enabled=0 AND @Revision IS NULL) THROW 57504,N''Authorization revision changed.'',1;
 IF @Enabled=1
 BEGIN
  IF @GrantExpiresUtc IS NULL OR @GrantExpiresUtc<=@Now OR NOT EXISTS(SELECT 1 FROM dbo.DepartamentosOperativos WITH(UPDLOCK,HOLDLOCK) WHERE DepartamentoId=@Department AND Activo=1)
  THROW 57505,N''Department or expiry unavailable.'',1;
  SELECT @TargetStamp=u.SecurityStamp FROM dbo.Usuarios u WITH(UPDLOCK,HOLDLOCK) JOIN dbo.Perfiles p WITH(UPDLOCK,HOLDLOCK) ON p.PerfilId=u.PerfilId AND p.Activo=1 WHERE u.UsuarioId=@Grantee AND u.Activo=1;
  IF @TargetStamp IS NULL THROW 57505,N''Internal grantee unavailable.'',1;
 END ELSE BEGIN SET @GrantExpiresUtc=@OldExpiry;SET @TargetStamp=@OldTarget;END;
 IF @OldEnabled=@Enabled AND @OldExpiry=@GrantExpiresUtc AND (@Enabled=0 OR (@OldTarget=@TargetStamp AND @OldGrantor=@UsuarioId AND @OldIssuer=@SecurityStamp))
 BEGIN
  EXEC dbo.sp_Stage2_RequireOriginalLogin @LoginId,@UsuarioId,@PerfilId,@SecurityStamp,@BindingHash,@IssuedUtc,@ExpiresUtc,N''DOCUMENTOS_DELEGAR'';
  COMMIT;SELECT @Revision AS Revision;RETURN;
 END;
 SET @Revision=ISNULL(@Revision,0)+1;
 IF @Revision=1 INSERT DocumentSecurity.Grants(DepartmentId,Grantee,Operation,Enabled,Revision,GranteeStamp,GrantedBy,GrantorStamp,CreatedUtc,ChangedUtc,ExpiresUtc)
 VALUES(@Department,@Grantee,@Operation,@Enabled,@Revision,@TargetStamp,@UsuarioId,@SecurityStamp,@Now,@Now,@GrantExpiresUtc);
 ELSE UPDATE DocumentSecurity.Grants SET Enabled=@Enabled,Revision=@Revision,GranteeStamp=@TargetStamp,
 GrantedBy=CASE WHEN @Enabled=1 THEN @UsuarioId ELSE GrantedBy END,GrantorStamp=CASE WHEN @Enabled=1 THEN @SecurityStamp ELSE GrantorStamp END,
 ChangedUtc=@Now,ExpiresUtc=@GrantExpiresUtc WHERE DepartmentId=@Department AND Grantee=@Grantee AND Operation=@Operation;
 INSERT DocumentSecurity.GrantAudit(DepartmentId,Grantee,Operation,Revision,Enabled,ExpiresUtc,Actor,OriginalLoginId,TimestampUtc,Reason)
 VALUES(@Department,@Grantee,@Operation,@Revision,@Enabled,@GrantExpiresUtc,@UsuarioId,@LoginId,@Now,LTRIM(RTRIM(@Reason)));
 EXEC dbo.sp_Stage2_RequireOriginalLogin @LoginId,@UsuarioId,@PerfilId,@SecurityStamp,@BindingHash,@IssuedUtc,@ExpiresUtc,N''DOCUMENTOS_DELEGAR'';
 COMMIT;SELECT @Revision AS Revision;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK;THROW;END CATCH;
END;
');
EXEC(N'ALTER PROCEDURE DocumentSecurity.GrantOptions
 @LoginId uniqueidentifier,@UsuarioId int,@PerfilId int,@SecurityStamp uniqueidentifier,@BindingHash binary(32),
 @IssuedUtc datetime2(7),@ExpiresUtc datetime2(7)
AS BEGIN SET NOCOUNT ON;SET XACT_ABORT ON;
 BEGIN TRY BEGIN TRANSACTION;
 EXEC dbo.sp_Stage2_RequireOriginalLogin @LoginId,@UsuarioId,@PerfilId,@SecurityStamp,@BindingHash,@IssuedUtc,@ExpiresUtc,N''DOCUMENTOS_DELEGAR'';
 SELECT DepartamentoId,Nombre FROM dbo.DepartamentosOperativos WHERE Activo=1 ORDER BY Nombre,DepartamentoId;
 SELECT u.UsuarioId,u.NombreCompleto FROM dbo.Usuarios u JOIN dbo.Perfiles p ON p.PerfilId=u.PerfilId WHERE u.Activo=1 AND p.Activo=1 AND u.UsuarioId<>@UsuarioId ORDER BY u.NombreCompleto,u.UsuarioId;
 EXEC dbo.sp_Stage2_RequireOriginalLogin @LoginId,@UsuarioId,@PerfilId,@SecurityStamp,@BindingHash,@IssuedUtc,@ExpiresUtc,N''DOCUMENTOS_DELEGAR'';
 COMMIT;END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK;THROW;END CATCH;
END;
');
EXEC(N'ALTER PROCEDURE DocumentSecurity.ListGrants
 @LoginId uniqueidentifier,@UsuarioId int,@PerfilId int,@SecurityStamp uniqueidentifier,@BindingHash binary(32),
 @IssuedUtc datetime2(7),@ExpiresUtc datetime2(7),@Page int=1
AS BEGIN SET NOCOUNT ON;SET XACT_ABORT ON;
 IF @Page<1 OR @Page>100000 THROW 57505,N''Invalid page.'',1;
 BEGIN TRY BEGIN TRANSACTION;
 EXEC dbo.sp_Stage2_RequireOriginalLogin @LoginId,@UsuarioId,@PerfilId,@SecurityStamp,@BindingHash,@IssuedUtc,@ExpiresUtc,N''DOCUMENTOS_DELEGAR'';
 SELECT COUNT_BIG(*) AS Total FROM DocumentSecurity.Grants;
 SELECT g.DepartmentId,d.Nombre AS DepartmentName,g.Grantee,u.NombreCompleto AS GranteeName,g.Operation,g.Enabled,g.Revision,g.ExpiresUtc,
 DocumentSecurity.CanAccess(g.Grantee,0,g.DepartmentId,g.Operation,SYSUTCDATETIME()) AS Effective
 FROM DocumentSecurity.Grants g JOIN dbo.DepartamentosOperativos d ON d.DepartamentoId=g.DepartmentId JOIN dbo.Usuarios u ON u.UsuarioId=g.Grantee
 ORDER BY g.ChangedUtc DESC,g.DepartmentId,g.Grantee,g.Operation OFFSET (CONVERT(bigint,@Page)-1)*50 ROWS FETCH NEXT 50 ROWS ONLY;
 EXEC dbo.sp_Stage2_RequireOriginalLogin @LoginId,@UsuarioId,@PerfilId,@SecurityStamp,@BindingHash,@IssuedUtc,@ExpiresUtc,N''DOCUMENTOS_DELEGAR'';
 COMMIT;END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK;THROW;END CATCH;
END;
');
INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes) VALUES(N'0056_stage2_document_explicit_authorization',N'0056_stage2_document_explicit_authorization.sql',UPPER(@Hash),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),N'Removes an unapproved profile-label exclusion from 0055. Active identity, explicit operation/capability, department, expiry, stamps, revisions, original login and no self-grant remain required. No profile grants, data rewrite, automatic membership or content bypass.');
COMMIT;END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK;THROW;END CATCH;
