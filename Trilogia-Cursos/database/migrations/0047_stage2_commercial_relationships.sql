-- Explicit commercial authorization. No person assignments, inferred roles or order effects.
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF SESSIONPROPERTY('QUOTED_IDENTIFIER')<>1 OR SESSIONPROPERTY('ANSI_NULLS')<>1 THROW 58700,N'SQL options not accredited.',1;
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL THROW 58700,N'Migration ledger missing.',1;
DECLARE @Hash nvarchar(128)=N'$(MigrationSha256)';
IF LEN(@Hash)<>64 OR @Hash LIKE N'%[^0-9a-fA-F]%' THROW 58700,N'Invalid migration hash.',1;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @Lock int;
 EXEC @Lock=sys.sp_getapplock @Resource=N'Stage2:0047:install',@LockMode=N'Exclusive',@LockOwner=N'Transaction',@LockTimeout=10000;
 IF @Lock<0 THROW 58700,N'Concurrent installation.',1;
 IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0047_stage2_commercial_relationships')
 BEGIN
  IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0047_stage2_commercial_relationships' AND Status=N'Applied' AND FileSha256=UPPER(@Hash)) THROW 58700,N'Ledger conflict.',1;
  COMMIT; RETURN;
 END;
 IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId LIKE N'0047%') THROW 58700,N'Migration identifier collision.',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0036_stage2_transactional_login' AND Status=N'Applied' AND FileSha256=N'98816A77160064B6B86579A25C9A6B41962197C60E38FE32FE2BAC58A7C02FDC') THROW 58700,N'Original-login prerequisite not accredited.',1;
 IF SCHEMA_ID(N'CommercialSecurity') IS NOT NULL THROW 58700,N'Existing relationship schema requires reconciliation.',1;
 IF OBJECT_ID(N'dbo.sp_Stage2_RequireOriginalLogin',N'P') IS NULL THROW 58700,N'Original login contract missing.',1;
 IF EXISTS(SELECT 1 FROM dbo.Permisos WHERE Codigo=N'RELACIONES_COMERCIALES_GESTIONAR') THROW 58700,N'Existing capability requires reconciliation.',1;
 INSERT dbo.Permisos(Codigo,Modulo,Nombre,Descripcion,Activo)
 VALUES(N'RELACIONES_COMERCIALES_GESTIONAR',N'Comercial',N'Gestionar relaciones comerciales',N'Asignar y revocar explícitamente vendedores, clientes y supervisores; no concede permisos de pedidos, cotización ni contenido de chat.',1);
 EXEC(N'CREATE SCHEMA CommercialSecurity AUTHORIZATION dbo');
 EXEC(N'CREATE TABLE CommercialSecurity.Assignments(
  Kind char(2) NOT NULL,
  ParentUser int NOT NULL REFERENCES dbo.Usuarios(UsuarioId),
  ChildUser int NOT NULL REFERENCES dbo.Usuarios(UsuarioId),
  Enabled bit NOT NULL,
  Revision bigint NOT NULL,
  CreatedUtc datetime2(7) NOT NULL,
  ChangedUtc datetime2(7) NOT NULL,
  ExpiresUtc datetime2(7) NULL,
  ChangedBy int NOT NULL REFERENCES dbo.Usuarios(UsuarioId),
  CONSTRAINT PK_CommercialAssignments PRIMARY KEY(Kind,ParentUser,ChildUser),
  CONSTRAINT CK_CommercialAssignmentKind CHECK(Kind IN(''SC'',''SS'')),
  CONSTRAINT CK_CommercialAssignmentUsers CHECK(ParentUser<>ChildUser),
  CONSTRAINT CK_CommercialAssignmentRevision CHECK(Revision>0),
  CONSTRAINT CK_CommercialAssignmentTimes CHECK(ChangedUtc>=CreatedUtc AND (ExpiresUtc IS NULL OR ExpiresUtc>CreatedUtc))
 ); CREATE INDEX IX_CommercialAssignmentChild ON CommercialSecurity.Assignments(Kind,ChildUser,ParentUser) INCLUDE(Enabled,Revision,ExpiresUtc);');
 EXEC(N'CREATE TABLE CommercialSecurity.Audit(
  AuditId bigint IDENTITY NOT NULL CONSTRAINT PK_CommercialRelationshipAudit PRIMARY KEY,
  Kind char(2) NOT NULL,ParentUser int NOT NULL,ChildUser int NOT NULL,
  Revision bigint NOT NULL,Enabled bit NOT NULL,ExpiresUtc datetime2(7) NULL,
  Actor int NOT NULL,OriginalLoginId uniqueidentifier NOT NULL,
  TimestampUtc datetime2(7) NOT NULL,Reason nvarchar(250) NOT NULL,
  CONSTRAINT FK_CommercialRelationshipAudit FOREIGN KEY(Kind,ParentUser,ChildUser)
   REFERENCES CommercialSecurity.Assignments(Kind,ParentUser,ChildUser),
  CONSTRAINT UQ_CommercialRelationshipAudit UNIQUE(Kind,ParentUser,ChildUser,Revision)
 );');
 EXEC(N'CREATE PROCEDURE CommercialSecurity.Options
 @LoginId uniqueidentifier,@UsuarioId int,@PerfilId int,@SecurityStamp uniqueidentifier,@BindingHash binary(32),
 @IssuedUtc datetime2(7),@ExpiresUtc datetime2(7)
 AS BEGIN SET NOCOUNT ON; SET XACT_ABORT ON;
 BEGIN TRY BEGIN TRANSACTION;
 EXEC dbo.sp_Stage2_RequireOriginalLogin @LoginId,@UsuarioId,@PerfilId,@SecurityStamp,@BindingHash,@IssuedUtc,@ExpiresUtc,N''RELACIONES_COMERCIALES_GESTIONAR'';
 SELECT u.UsuarioId,u.NombreCompleto FROM dbo.Usuarios u JOIN dbo.Perfiles p ON p.PerfilId=u.PerfilId
 WHERE u.Activo=1 AND p.Activo=1 AND p.Nombre<>N''Cliente'' AND EXISTS(SELECT 1 FROM dbo.PerfilPermisos pp JOIN dbo.Permisos pe ON pe.PermisoId=pp.PermisoId WHERE pp.PerfilId=p.PerfilId AND pe.Codigo=N''VENTA_MOVIL_CREAR'' AND pe.Activo=1) ORDER BY u.NombreCompleto,u.UsuarioId;
 SELECT u.UsuarioId,u.NombreCompleto FROM dbo.Usuarios u JOIN dbo.Perfiles p ON p.PerfilId=u.PerfilId
 WHERE u.Activo=1 AND p.Activo=1 AND p.Nombre=N''Cliente'' ORDER BY u.NombreCompleto,u.UsuarioId;
 SELECT u.UsuarioId,u.NombreCompleto FROM dbo.Usuarios u JOIN dbo.Perfiles p ON p.PerfilId=u.PerfilId
 WHERE u.Activo=1 AND p.Activo=1 AND p.Nombre<>N''Cliente'' AND EXISTS(SELECT 1 FROM dbo.PerfilPermisos pp JOIN dbo.Permisos pe ON pe.PermisoId=pp.PermisoId WHERE pp.PerfilId=p.PerfilId AND pe.Codigo=N''PEDIDOS_AUTORIZAR_RECHAZAR'' AND pe.Activo=1) ORDER BY u.NombreCompleto,u.UsuarioId;
 EXEC dbo.sp_Stage2_RequireOriginalLogin @LoginId,@UsuarioId,@PerfilId,@SecurityStamp,@BindingHash,@IssuedUtc,@ExpiresUtc,N''RELACIONES_COMERCIALES_GESTIONAR'';
 COMMIT; END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH; END;');
 EXEC(N'CREATE PROCEDURE CommercialSecurity.List
 @LoginId uniqueidentifier,@UsuarioId int,@PerfilId int,@SecurityStamp uniqueidentifier,@BindingHash binary(32),
 @IssuedUtc datetime2(7),@ExpiresUtc datetime2(7),@Page int=1
 AS BEGIN SET NOCOUNT ON; SET XACT_ABORT ON;
 IF @Page<1 OR @Page>100000 THROW 58705,N''Invalid page.'',1;
 BEGIN TRY BEGIN TRANSACTION;
 EXEC dbo.sp_Stage2_RequireOriginalLogin @LoginId,@UsuarioId,@PerfilId,@SecurityStamp,@BindingHash,@IssuedUtc,@ExpiresUtc,N''RELACIONES_COMERCIALES_GESTIONAR'';
 SELECT COUNT_BIG(*) AS Total FROM CommercialSecurity.Assignments;
 SELECT a.Kind,a.ParentUser,p.NombreCompleto AS ParentName,a.ChildUser,c.NombreCompleto AS ChildName,
 a.Enabled,a.Revision,a.ExpiresUtc,CONVERT(bit,CASE WHEN a.Enabled=1 AND (a.ExpiresUtc IS NULL OR a.ExpiresUtc>SYSUTCDATETIME()) THEN 1 ELSE 0 END) AS WithinValidity
 FROM CommercialSecurity.Assignments a JOIN dbo.Usuarios p ON p.UsuarioId=a.ParentUser JOIN dbo.Usuarios c ON c.UsuarioId=a.ChildUser
 ORDER BY a.ChangedUtc DESC,a.Kind,a.ParentUser,a.ChildUser OFFSET (CONVERT(bigint,@Page)-1)*50 ROWS FETCH NEXT 50 ROWS ONLY;
 EXEC dbo.sp_Stage2_RequireOriginalLogin @LoginId,@UsuarioId,@PerfilId,@SecurityStamp,@BindingHash,@IssuedUtc,@ExpiresUtc,N''RELACIONES_COMERCIALES_GESTIONAR'';
 COMMIT; END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH; END;');
 EXEC(N'CREATE PROCEDURE CommercialSecurity.SetAssignment
 @LoginId uniqueidentifier,@UsuarioId int,@PerfilId int,@SecurityStamp uniqueidentifier,@BindingHash binary(32),
 @IssuedUtc datetime2(7),@ExpiresUtc datetime2(7),@Kind char(2),@ParentUser int,@ChildUser int,
 @Enabled bit,@ExpectedRevision bigint,@RelationshipExpiresUtc datetime2(7)=NULL,@Reason nvarchar(250)
 AS BEGIN SET NOCOUNT ON; SET XACT_ABORT ON;
 IF @Kind NOT IN(''SC'',''SS'') OR @Kind IS NULL OR @ParentUser IS NULL OR @ChildUser IS NULL OR @ExpectedRevision IS NULL OR @ParentUser<=0 OR @ChildUser<=0 OR @ParentUser=@ChildUser OR @ExpectedRevision<0
  OR @Enabled IS NULL OR NULLIF(LTRIM(RTRIM(@Reason)),N'''') IS NULL THROW 58705,N''Invalid relationship input.'',1;
 BEGIN TRY BEGIN TRANSACTION;
 EXEC dbo.sp_Stage2_RequireOriginalLogin @LoginId,@UsuarioId,@PerfilId,@SecurityStamp,@BindingHash,@IssuedUtc,@ExpiresUtc,N''RELACIONES_COMERCIALES_GESTIONAR'';
 DECLARE @Now datetime2(7)=SYSUTCDATETIME(),@Current bigint,@WasEnabled bit,@OldExpiry datetime2(7);
 SELECT @Current=Revision,@WasEnabled=Enabled,@OldExpiry=ExpiresUtc FROM CommercialSecurity.Assignments WITH(UPDLOCK,HOLDLOCK)
 WHERE Kind=@Kind AND ParentUser=@ParentUser AND ChildUser=@ChildUser;
 IF ISNULL(@Current,0)<>@ExpectedRevision THROW 58704,N''Relationship revision changed.'',1;
 IF @Enabled=0 AND @Current IS NULL THROW 58704,N''Relationship missing.'',1;
 IF @Enabled=1
 BEGIN
  IF @RelationshipExpiresUtc IS NOT NULL AND @RelationshipExpiresUtc<=@Now THROW 58705,N''Relationship expiry must be future.'',1;
  IF NOT EXISTS(SELECT 1 FROM dbo.Usuarios u WITH(UPDLOCK,HOLDLOCK) JOIN dbo.Perfiles p WITH(UPDLOCK,HOLDLOCK) ON p.PerfilId=u.PerfilId
    JOIN dbo.PerfilPermisos pp WITH(UPDLOCK,HOLDLOCK) ON pp.PerfilId=p.PerfilId JOIN dbo.Permisos pe WITH(UPDLOCK,HOLDLOCK) ON pe.PermisoId=pp.PermisoId
    WHERE u.UsuarioId=@ParentUser AND u.Activo=1 AND p.Activo=1 AND p.Nombre<>N''Cliente'' AND pe.Activo=1
    AND pe.Codigo=CASE WHEN @Kind=''SC'' THEN N''VENTA_MOVIL_CREAR'' ELSE N''PEDIDOS_AUTORIZAR_RECHAZAR'' END) THROW 58702,N''Parent eligibility not accredited.'',1;
  IF @Kind=''SC'' AND NOT EXISTS(SELECT 1 FROM dbo.Usuarios u WITH(UPDLOCK,HOLDLOCK) JOIN dbo.Perfiles p WITH(UPDLOCK,HOLDLOCK) ON p.PerfilId=u.PerfilId WHERE u.UsuarioId=@ChildUser AND u.Activo=1 AND p.Activo=1 AND p.Nombre=N''Cliente'') THROW 58702,N''Customer eligibility not accredited.'',1;
  IF @Kind=''SS'' AND NOT EXISTS(SELECT 1 FROM dbo.Usuarios u WITH(UPDLOCK,HOLDLOCK) JOIN dbo.Perfiles p WITH(UPDLOCK,HOLDLOCK) ON p.PerfilId=u.PerfilId JOIN dbo.PerfilPermisos pp WITH(UPDLOCK,HOLDLOCK) ON pp.PerfilId=p.PerfilId JOIN dbo.Permisos pe WITH(UPDLOCK,HOLDLOCK) ON pe.PermisoId=pp.PermisoId WHERE u.UsuarioId=@ChildUser AND u.Activo=1 AND p.Activo=1 AND p.Nombre<>N''Cliente'' AND pe.Activo=1 AND pe.Codigo=N''VENTA_MOVIL_CREAR'') THROW 58702,N''Seller eligibility not accredited.'',1;
 END;
 IF @Enabled=0 SET @RelationshipExpiresUtc=@OldExpiry;
 IF @Current IS NOT NULL AND @WasEnabled=@Enabled AND (@RelationshipExpiresUtc=@OldExpiry OR (@RelationshipExpiresUtc IS NULL AND @OldExpiry IS NULL))
 BEGIN
  EXEC dbo.sp_Stage2_RequireOriginalLogin @LoginId,@UsuarioId,@PerfilId,@SecurityStamp,@BindingHash,@IssuedUtc,@ExpiresUtc,N''RELACIONES_COMERCIALES_GESTIONAR'';
  COMMIT; SELECT @Current AS Revision; RETURN;
 END;
 DECLARE @Revision bigint=ISNULL(@Current,0)+1;
 IF @Current IS NULL INSERT CommercialSecurity.Assignments(Kind,ParentUser,ChildUser,Enabled,Revision,CreatedUtc,ChangedUtc,ExpiresUtc,ChangedBy)
 VALUES(@Kind,@ParentUser,@ChildUser,@Enabled,@Revision,@Now,@Now,@RelationshipExpiresUtc,@UsuarioId);
 ELSE UPDATE CommercialSecurity.Assignments SET Enabled=@Enabled,Revision=@Revision,ChangedUtc=@Now,ExpiresUtc=@RelationshipExpiresUtc,ChangedBy=@UsuarioId
 WHERE Kind=@Kind AND ParentUser=@ParentUser AND ChildUser=@ChildUser;
 INSERT CommercialSecurity.Audit(Kind,ParentUser,ChildUser,Revision,Enabled,ExpiresUtc,Actor,OriginalLoginId,TimestampUtc,Reason)
 VALUES(@Kind,@ParentUser,@ChildUser,@Revision,@Enabled,@RelationshipExpiresUtc,@UsuarioId,@LoginId,@Now,LTRIM(RTRIM(@Reason)));
 EXEC dbo.sp_Stage2_RequireOriginalLogin @LoginId,@UsuarioId,@PerfilId,@SecurityStamp,@BindingHash,@IssuedUtc,@ExpiresUtc,N''RELACIONES_COMERCIALES_GESTIONAR'';
 COMMIT; SELECT @Revision AS Revision;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH; END;');
 EXEC(N'CREATE PROCEDURE CommercialSecurity.RequireAssignment
 @Kind char(2),@ParentUser int,@ChildUser int,@ExpectedRevision bigint=NULL,@Revision bigint OUTPUT
 AS BEGIN SET NOCOUNT ON;
 IF @@TRANCOUNT=0 THROW 58700,N''Business transaction required.'',1;
 IF @Kind IS NULL OR @Kind NOT IN(''SC'',''SS'') OR @ParentUser IS NULL OR @ChildUser IS NULL OR @ParentUser<=0 OR @ChildUser<=0 OR @ParentUser=@ChildUser THROW 58702,N''Relationship unavailable.'',1;
 SET @Revision=NULL;
 SELECT @Revision=Revision FROM CommercialSecurity.Assignments WITH(UPDLOCK,HOLDLOCK)
 WHERE Kind=@Kind AND ParentUser=@ParentUser AND ChildUser=@ChildUser AND Enabled=1 AND (ExpiresUtc IS NULL OR ExpiresUtc>SYSUTCDATETIME());
 IF @Revision IS NULL OR (@ExpectedRevision IS NOT NULL AND @ExpectedRevision<>@Revision) THROW 58702,N''Relationship unavailable.'',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.Usuarios u WITH(UPDLOCK,HOLDLOCK) JOIN dbo.Perfiles p WITH(UPDLOCK,HOLDLOCK) ON p.PerfilId=u.PerfilId JOIN dbo.PerfilPermisos pp WITH(UPDLOCK,HOLDLOCK) ON pp.PerfilId=p.PerfilId JOIN dbo.Permisos pe WITH(UPDLOCK,HOLDLOCK) ON pe.PermisoId=pp.PermisoId WHERE u.UsuarioId=@ParentUser AND u.Activo=1 AND p.Activo=1 AND p.Nombre<>N''Cliente'' AND pe.Activo=1 AND pe.Codigo=CASE WHEN @Kind=''SC'' THEN N''VENTA_MOVIL_CREAR'' ELSE N''PEDIDOS_AUTORIZAR_RECHAZAR'' END) THROW 58702,N''Parent unavailable.'',1;
 IF @Kind=''SC'' AND NOT EXISTS(SELECT 1 FROM dbo.Usuarios u WITH(UPDLOCK,HOLDLOCK) JOIN dbo.Perfiles p WITH(UPDLOCK,HOLDLOCK) ON p.PerfilId=u.PerfilId WHERE u.UsuarioId=@ChildUser AND u.Activo=1 AND p.Activo=1 AND p.Nombre=N''Cliente'') THROW 58702,N''Customer unavailable.'',1;
 IF @Kind=''SS'' AND NOT EXISTS(SELECT 1 FROM dbo.Usuarios u WITH(UPDLOCK,HOLDLOCK) JOIN dbo.Perfiles p WITH(UPDLOCK,HOLDLOCK) ON p.PerfilId=u.PerfilId JOIN dbo.PerfilPermisos pp WITH(UPDLOCK,HOLDLOCK) ON pp.PerfilId=p.PerfilId JOIN dbo.Permisos pe WITH(UPDLOCK,HOLDLOCK) ON pe.PermisoId=pp.PermisoId WHERE u.UsuarioId=@ChildUser AND u.Activo=1 AND p.Activo=1 AND p.Nombre<>N''Cliente'' AND pe.Activo=1 AND pe.Codigo=N''VENTA_MOVIL_CREAR'') THROW 58702,N''Seller unavailable.'',1;
 END;');
 INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes)
 VALUES(N'0047_stage2_commercial_relationships',N'0047_stage2_commercial_relationships.sql',UPPER(@Hash),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),N'Explicit versioned relationships, validity, canonical eligibility and audit. No person assignments, capability assignments, price authority, order effects or administrator bypass.');
 COMMIT;
END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
