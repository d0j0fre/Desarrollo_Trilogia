-- Explicit department/operation grants; owner OR current explicit grant, never an administrator bypass.
-- No business assignments, real documents, profile grants or data purges are installed here.
SET NOCOUNT ON;SET XACT_ABORT ON;
IF SESSIONPROPERTY('QUOTED_IDENTIFIER')<>1 OR SESSIONPROPERTY('ANSI_NULLS')<>1 THROW 57510,N'SQL options unavailable.',1;
DECLARE @Hash nvarchar(128)=N'$(MigrationSha256)';
IF LEN(@Hash)<>64 OR @Hash LIKE N'%[^0-9a-fA-F]%' THROW 57510,N'Invalid migration hash.',1;
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL THROW 57510,N'Ledger unavailable.',1;
BEGIN TRY BEGIN TRANSACTION;
DECLARE @Lock int;EXEC @Lock=sys.sp_getapplock @Resource=N'Stage2:0055:install',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=5000;
IF @Lock<0 THROW 57510,N'Installation conflict.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0055_stage2_document_delegation') BEGIN
 IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0055_stage2_document_delegation' AND Status=N'Applied' AND FileSha256=UPPER(@Hash)) THROW 57510,N'Ledger conflict.',1;
 COMMIT;RETURN;END;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId LIKE N'0055%') OR SCHEMA_ID(N'DocumentSecurity') IS NOT NULL OR EXISTS(SELECT 1 FROM dbo.Permisos WHERE Codigo=N'DOCUMENTOS_DELEGAR') THROW 57510,N'Identifier or authority collision.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0030_stage2_authorization_invalidation' AND Status=N'Applied' AND FileSha256=N'90AFC1B036ED72525FE319B77A6CE44826C1F9EF8CBD413B258F398B9B695C66') THROW 57510,N'Exact prerequisite unavailable.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0036_stage2_transactional_login' AND Status=N'Applied' AND FileSha256=N'98816A77160064B6B86579A25C9A6B41962197C60E38FE32FE2BAC58A7C02FDC') THROW 57510,N'Exact prerequisite unavailable.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0040_stage2_document_ownership' AND Status=N'Applied' AND FileSha256=N'A5ED17FFA9249CF76E24AA4F2D42DBAE2D756886FFF952A9EA37D9D6DACD3D62') THROW 57510,N'Exact prerequisite unavailable.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0042_stage2_operational_contract_edges' AND Status=N'Applied' AND FileSha256=N'92A8600915DEF83DAB32BC8CB17C9C260C6092A694269733B2F881821DDBEB58') THROW 57510,N'Exact prerequisite unavailable.',1;
INSERT dbo.Permisos(Codigo,Modulo,Nombre,Descripcion,Activo) VALUES(N'DOCUMENTOS_DELEGAR',N'Documentos',N'Gestionar delegaciones documentales',N'Conceder y revocar autorizaciones departamentales explícitas por operación y vigencia. No permite leer archivos, autoasignarse ni omitir ownership.',1);
EXEC(N'CREATE SCHEMA DocumentSecurity AUTHORIZATION dbo');
CREATE TABLE DocumentSecurity.Grants(
 DepartmentId int NOT NULL REFERENCES dbo.DepartamentosOperativos(DepartamentoId),
 Grantee int NOT NULL REFERENCES dbo.Usuarios(UsuarioId),
 Operation varchar(10) COLLATE Latin1_General_100_BIN2 NOT NULL,
 Enabled bit NOT NULL,Revision bigint NOT NULL,
 GranteeStamp uniqueidentifier NOT NULL,
 GrantedBy int NOT NULL REFERENCES dbo.Usuarios(UsuarioId),GrantorStamp uniqueidentifier NOT NULL,
 CreatedUtc datetime2(7) NOT NULL,ChangedUtc datetime2(7) NOT NULL,ExpiresUtc datetime2(7) NOT NULL,
 CONSTRAINT PK_DocumentDepartmentGrants PRIMARY KEY(DepartmentId,Grantee,Operation),
 CONSTRAINT CK_DocumentGrantOperation CHECK(Operation IN('Read','Edit','Version','Download','Alert','Create')),
 CONSTRAINT CK_DocumentGrantRevision CHECK(Revision>0),
 CONSTRAINT CK_DocumentGrantActors CHECK(Grantee<>GrantedBy),
 CONSTRAINT CK_DocumentGrantTimes CHECK(ChangedUtc>=CreatedUtc AND ExpiresUtc>CreatedUtc)
);
CREATE TABLE DocumentSecurity.GrantAudit(
 DepartmentId int NOT NULL,Grantee int NOT NULL,Operation varchar(10) COLLATE Latin1_General_100_BIN2 NOT NULL,Revision bigint NOT NULL,
 Enabled bit NOT NULL,ExpiresUtc datetime2(7) NOT NULL,Actor int NOT NULL REFERENCES dbo.Usuarios(UsuarioId),
 OriginalLoginId uniqueidentifier NOT NULL REFERENCES dbo.Stage2OriginalLogins(LoginId),
 TimestampUtc datetime2(7) NOT NULL,Reason nvarchar(250) NOT NULL,
 CONSTRAINT PK_DocumentGrantAudit PRIMARY KEY(DepartmentId,Grantee,Operation,Revision),
 CONSTRAINT FK_DocumentGrantAudit FOREIGN KEY(DepartmentId,Grantee,Operation)
 REFERENCES DocumentSecurity.Grants(DepartmentId,Grantee,Operation)
);
EXEC(N'CREATE FUNCTION DocumentSecurity.CanAccess(@Actor int,@Owner int,@Department int,@Operation varchar(10),@Now datetime2(7))
RETURNS bit AS BEGIN
 IF @Actor=@Owner AND @Actor>0 RETURN 1;
 IF @Department IS NULL OR @Actor IS NULL OR @Now IS NULL RETURN 0;
 IF EXISTS(SELECT 1 FROM DocumentSecurity.Grants g WITH(HOLDLOCK)
 JOIN dbo.DepartamentosOperativos d WITH(HOLDLOCK) ON d.DepartamentoId=g.DepartmentId AND d.Activo=1
 JOIN dbo.Usuarios u WITH(HOLDLOCK) ON u.UsuarioId=g.Grantee AND u.Activo=1 AND u.SecurityStamp=g.GranteeStamp
 JOIN dbo.Perfiles p WITH(HOLDLOCK) ON p.PerfilId=u.PerfilId AND p.Activo=1 AND p.Nombre<>N''Cliente''
 JOIN dbo.Usuarios issuer WITH(HOLDLOCK) ON issuer.UsuarioId=g.GrantedBy AND issuer.Activo=1 AND issuer.SecurityStamp=g.GrantorStamp
 JOIN dbo.Perfiles ip WITH(HOLDLOCK) ON ip.PerfilId=issuer.PerfilId AND ip.Activo=1 AND ip.Nombre<>N''Cliente''
 JOIN dbo.PerfilPermisos pp WITH(HOLDLOCK) ON pp.PerfilId=issuer.PerfilId
 JOIN dbo.Permisos pe WITH(HOLDLOCK) ON pe.PermisoId=pp.PermisoId AND pe.Codigo=N''DOCUMENTOS_DELEGAR'' AND pe.Activo=1
 WHERE g.DepartmentId=@Department AND g.Grantee=@Actor AND g.Operation=@Operation AND g.Enabled=1 AND g.ExpiresUtc>@Now)
 RETURN 1;
 RETURN 0;
END;
');
EXEC(N'CREATE PROCEDURE DocumentSecurity.SetGrant
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
 IF NOT EXISTS(SELECT 1 FROM dbo.Perfiles WITH(HOLDLOCK) WHERE PerfilId=@PerfilId AND Activo=1 AND Nombre<>N''Cliente'') THROW 56802,N''Internal delegation authority required.'',1;
 DECLARE @Now datetime2(7)=SYSUTCDATETIME(),@Revision bigint,@OldEnabled bit,@OldExpiry datetime2(7),
 @TargetStamp uniqueidentifier,@OldTarget uniqueidentifier,@OldIssuer uniqueidentifier,@OldGrantor int;
 SELECT @Revision=Revision,@OldEnabled=Enabled,@OldExpiry=ExpiresUtc,@OldTarget=GranteeStamp,@OldIssuer=GrantorStamp,@OldGrantor=GrantedBy
 FROM DocumentSecurity.Grants WITH(UPDLOCK,HOLDLOCK) WHERE DepartmentId=@Department AND Grantee=@Grantee AND Operation=@Operation;
 IF ISNULL(@Revision,0)<>@ExpectedRevision OR (@Enabled=0 AND @Revision IS NULL) THROW 57504,N''Authorization revision changed.'',1;
 IF @Enabled=1
 BEGIN
  IF @GrantExpiresUtc IS NULL OR @GrantExpiresUtc<=@Now OR NOT EXISTS(SELECT 1 FROM dbo.DepartamentosOperativos WITH(UPDLOCK,HOLDLOCK) WHERE DepartamentoId=@Department AND Activo=1)
  THROW 57505,N''Department or expiry unavailable.'',1;
  SELECT @TargetStamp=u.SecurityStamp FROM dbo.Usuarios u WITH(UPDLOCK,HOLDLOCK) JOIN dbo.Perfiles p WITH(UPDLOCK,HOLDLOCK) ON p.PerfilId=u.PerfilId AND p.Activo=1 AND p.Nombre<>N''Cliente'' WHERE u.UsuarioId=@Grantee AND u.Activo=1;
  IF @TargetStamp IS NULL THROW 57505,N''Internal grantee unavailable.'',1;
 END ELSE BEGIN SET @GrantExpiresUtc=@OldExpiry;SET @TargetStamp=@OldTarget;END;
 IF @OldEnabled=@Enabled AND @OldExpiry=@GrantExpiresUtc AND (@Enabled=0 OR (@OldTarget=@TargetStamp AND @OldGrantor=@UsuarioId AND @OldIssuer=@SecurityStamp))
 BEGIN
  EXEC dbo.sp_Stage2_RequireOriginalLogin @LoginId,@UsuarioId,@PerfilId,@SecurityStamp,@BindingHash,@IssuedUtc,@ExpiresUtc,N''DOCUMENTOS_DELEGAR'';
 IF NOT EXISTS(SELECT 1 FROM dbo.Perfiles WITH(HOLDLOCK) WHERE PerfilId=@PerfilId AND Activo=1 AND Nombre<>N''Cliente'') THROW 56802,N''Internal delegation authority required.'',1;
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
 IF NOT EXISTS(SELECT 1 FROM dbo.Perfiles WITH(HOLDLOCK) WHERE PerfilId=@PerfilId AND Activo=1 AND Nombre<>N''Cliente'') THROW 56802,N''Internal delegation authority required.'',1;
 COMMIT;SELECT @Revision AS Revision;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK;THROW;END CATCH;
END;
');
EXEC(N'CREATE PROCEDURE DocumentSecurity.GrantOptions
 @LoginId uniqueidentifier,@UsuarioId int,@PerfilId int,@SecurityStamp uniqueidentifier,@BindingHash binary(32),
 @IssuedUtc datetime2(7),@ExpiresUtc datetime2(7)
AS BEGIN SET NOCOUNT ON;SET XACT_ABORT ON;
 BEGIN TRY BEGIN TRANSACTION;
 EXEC dbo.sp_Stage2_RequireOriginalLogin @LoginId,@UsuarioId,@PerfilId,@SecurityStamp,@BindingHash,@IssuedUtc,@ExpiresUtc,N''DOCUMENTOS_DELEGAR'';
 IF NOT EXISTS(SELECT 1 FROM dbo.Perfiles WITH(HOLDLOCK) WHERE PerfilId=@PerfilId AND Activo=1 AND Nombre<>N''Cliente'') THROW 56802,N''Internal delegation authority required.'',1;
 SELECT DepartamentoId,Nombre FROM dbo.DepartamentosOperativos WHERE Activo=1 ORDER BY Nombre,DepartamentoId;
 SELECT u.UsuarioId,u.NombreCompleto FROM dbo.Usuarios u JOIN dbo.Perfiles p ON p.PerfilId=u.PerfilId WHERE u.Activo=1 AND p.Activo=1 AND p.Nombre<>N''Cliente'' AND u.UsuarioId<>@UsuarioId ORDER BY u.NombreCompleto,u.UsuarioId;
 EXEC dbo.sp_Stage2_RequireOriginalLogin @LoginId,@UsuarioId,@PerfilId,@SecurityStamp,@BindingHash,@IssuedUtc,@ExpiresUtc,N''DOCUMENTOS_DELEGAR'';
 IF NOT EXISTS(SELECT 1 FROM dbo.Perfiles WITH(HOLDLOCK) WHERE PerfilId=@PerfilId AND Activo=1 AND Nombre<>N''Cliente'') THROW 56802,N''Internal delegation authority required.'',1;
 COMMIT;END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK;THROW;END CATCH;
END;
');
EXEC(N'CREATE PROCEDURE DocumentSecurity.ListGrants
 @LoginId uniqueidentifier,@UsuarioId int,@PerfilId int,@SecurityStamp uniqueidentifier,@BindingHash binary(32),
 @IssuedUtc datetime2(7),@ExpiresUtc datetime2(7),@Page int=1
AS BEGIN SET NOCOUNT ON;SET XACT_ABORT ON;
 IF @Page<1 OR @Page>100000 THROW 57505,N''Invalid page.'',1;
 BEGIN TRY BEGIN TRANSACTION;
 EXEC dbo.sp_Stage2_RequireOriginalLogin @LoginId,@UsuarioId,@PerfilId,@SecurityStamp,@BindingHash,@IssuedUtc,@ExpiresUtc,N''DOCUMENTOS_DELEGAR'';
 IF NOT EXISTS(SELECT 1 FROM dbo.Perfiles WITH(HOLDLOCK) WHERE PerfilId=@PerfilId AND Activo=1 AND Nombre<>N''Cliente'') THROW 56802,N''Internal delegation authority required.'',1;
 SELECT COUNT_BIG(*) AS Total FROM DocumentSecurity.Grants;
 SELECT g.DepartmentId,d.Nombre AS DepartmentName,g.Grantee,u.NombreCompleto AS GranteeName,g.Operation,g.Enabled,g.Revision,g.ExpiresUtc,
 DocumentSecurity.CanAccess(g.Grantee,0,g.DepartmentId,g.Operation,SYSUTCDATETIME()) AS Effective
 FROM DocumentSecurity.Grants g JOIN dbo.DepartamentosOperativos d ON d.DepartamentoId=g.DepartmentId JOIN dbo.Usuarios u ON u.UsuarioId=g.Grantee
 ORDER BY g.ChangedUtc DESC,g.DepartmentId,g.Grantee,g.Operation OFFSET (CONVERT(bigint,@Page)-1)*50 ROWS FETCH NEXT 50 ROWS ONLY;
 EXEC dbo.sp_Stage2_RequireOriginalLogin @LoginId,@UsuarioId,@PerfilId,@SecurityStamp,@BindingHash,@IssuedUtc,@ExpiresUtc,N''DOCUMENTOS_DELEGAR'';
 IF NOT EXISTS(SELECT 1 FROM dbo.Perfiles WITH(HOLDLOCK) WHERE PerfilId=@PerfilId AND Activo=1 AND Nombre<>N''Cliente'') THROW 56802,N''Internal delegation authority required.'',1;
 COMMIT;END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK;THROW;END CATCH;
END;
');
EXEC(N'CREATE TRIGGER DocumentSecurity.GrantAuditImmutable ON DocumentSecurity.GrantAudit INSTEAD OF UPDATE,DELETE AS
BEGIN THROW 57500,N''Immutable authorization audit.'',1;END;
');
EXEC(N'CREATE PROCEDURE DocumentSecurity.Document_GetOptions @OriginalLoginId uniqueidentifier,@ContextActor int,@ContextProfile int,@ContextStamp uniqueidentifier,@ContextBinding binary(32),@ContextIssuedUtc datetime2(7),@ContextExpiresUtc datetime2(7),@Actor int AS

BEGIN
IF @@TRANCOUNT=0 THROW 57400,N''Falta transaccion de login original.'',1;
IF @Actor IS NULL OR @Actor<>@ContextActor THROW 57401,N''Documento no disponible.'',1;
EXEC dbo.sp_Stage2_RequireOriginalLogin @OriginalLoginId,@ContextActor,@ContextProfile,@ContextStamp,@ContextBinding,@ContextIssuedUtc,@ContextExpiresUtc;

    SET NOCOUNT ON;
    SELECT TipoDocumentoId, Nombre FROM dbo.TiposDocumento WHERE Activo=1 ORDER BY Nombre;
    SELECT dep.DepartamentoId,dep.Nombre FROM dbo.DepartamentosOperativos dep WHERE dep.Activo=1 AND (EXISTS(SELECT 1 FROM dbo.DocumentosDigitales d WITH(HOLDLOCK) WHERE d.DepartamentoId=dep.DepartamentoId AND d.ResponsableUsuarioId=@Actor) OR EXISTS(SELECT 1 FROM DocumentSecurity.Grants g WITH(HOLDLOCK) WHERE g.DepartmentId=dep.DepartamentoId AND g.Grantee=@Actor AND DocumentSecurity.CanAccess(@Actor,0,dep.DepartamentoId,g.Operation,SYSUTCDATETIME())=1)) ORDER BY dep.Nombre;
END;
');
EXEC(N'CREATE PROCEDURE DocumentSecurity.Document_List @OriginalLoginId uniqueidentifier,@ContextActor int,@ContextProfile int,@ContextStamp uniqueidentifier,@ContextBinding binary(32),@ContextIssuedUtc datetime2(7),@ContextExpiresUtc datetime2(7),@Actor int ,@Busqueda NVARCHAR(180)=NULL, @TipoDocumentoId INT=NULL, @DepartamentoId INT=NULL,
    @Estado NVARCHAR(30)=NULL, @Vencimiento NVARCHAR(30)=NULL, @FechaNegocio DATE,
    @DiasAviso INT=30, @Pagina INT=1, @TamanoPagina INT=20 AS

BEGIN
IF @@TRANCOUNT=0 THROW 57400,N''Falta transaccion de login original.'',1;
IF @Actor IS NULL OR @Actor<>@ContextActor THROW 57401,N''Documento no disponible.'',1;
EXEC dbo.sp_Stage2_RequireOriginalLogin @OriginalLoginId,@ContextActor,@ContextProfile,@ContextStamp,@ContextBinding,@ContextIssuedUtc,@ContextExpiresUtc;

    SET NOCOUNT ON;
    SET @Pagina=CASE WHEN @Pagina<1 THEN 1 ELSE @Pagina END;
    SET @TamanoPagina=CASE WHEN @TamanoPagina<1 THEN 20 WHEN @TamanoPagina>100 THEN 100 ELSE @TamanoPagina END;
    ;WITH base AS
    (
        SELECT DocumentSecurity.CanAccess(@Actor,d.ResponsableUsuarioId,d.DepartamentoId,''Read'',SYSUTCDATETIME()) AS CanRead,DocumentSecurity.CanAccess(@Actor,d.ResponsableUsuarioId,d.DepartamentoId,''Edit'',SYSUTCDATETIME()) AS CanEdit,DocumentSecurity.CanAccess(@Actor,d.ResponsableUsuarioId,d.DepartamentoId,''Version'',SYSUTCDATETIME()) AS CanVersion,DocumentSecurity.CanAccess(@Actor,d.ResponsableUsuarioId,d.DepartamentoId,''Download'',SYSUTCDATETIME()) AS CanDownload,DocumentSecurity.CanAccess(@Actor,d.ResponsableUsuarioId,d.DepartamentoId,''Alert'',SYSUTCDATETIME()) AS CanAlert,d.DocumentoId, t.Nombre TipoDocumento, dep.Nombre Departamento, d.Titulo, d.NumeroReferencia,
               d.FechaEmision, d.FechaVencimiento, d.NoVence, d.Estado, d.Activo, v.Version, d.FechaActualizacionUtc,
               CASE WHEN d.NoVence=1 THEN N''No vence'' WHEN d.FechaVencimiento<@FechaNegocio THEN N''Vencido''
                    WHEN DATEDIFF(DAY,@FechaNegocio,d.FechaVencimiento)<=@DiasAviso THEN N''Por vencer'' ELSE N''Vigente'' END EstadoVencimiento,
               CASE WHEN d.NoVence=1 THEN NULL ELSE DATEDIFF(DAY,@FechaNegocio,d.FechaVencimiento) END DiasRestantes
        FROM dbo.DocumentosDigitales d WITH(HOLDLOCK)
        INNER JOIN dbo.TiposDocumento t ON t.TipoDocumentoId=d.TipoDocumentoId
        LEFT JOIN dbo.DepartamentosOperativos dep ON dep.DepartamentoId=d.DepartamentoId
        INNER JOIN dbo.DocumentoVersiones v ON v.DocumentoVersionId=d.VersionActualId AND v.StorageStatus=N''Ready''
        WHERE DocumentSecurity.CanAccess(@Actor,d.ResponsableUsuarioId,d.DepartamentoId,''Read'',SYSUTCDATETIME())=1 AND (@Busqueda IS NULL OR d.Titulo LIKE N''%''+@Busqueda+N''%'' OR d.NumeroReferencia LIKE N''%''+@Busqueda+N''%'')
          AND (@TipoDocumentoId IS NULL OR d.TipoDocumentoId=@TipoDocumentoId)
          AND (@DepartamentoId IS NULL OR d.DepartamentoId=@DepartamentoId)
          AND (@Estado IS NULL OR d.Estado=@Estado)
    ), filtrado AS
    (
        SELECT *, COUNT(*) OVER() TotalResultados FROM base
        WHERE @Vencimiento IS NULL OR EstadoVencimiento=@Vencimiento
    )
    SELECT * FROM filtrado ORDER BY FechaActualizacionUtc DESC, DocumentoId DESC
    OFFSET (@Pagina-1)*@TamanoPagina ROWS FETCH NEXT @TamanoPagina ROWS ONLY;
END;
');
EXEC(N'CREATE PROCEDURE DocumentSecurity.Document_Summary @OriginalLoginId uniqueidentifier,@ContextActor int,@ContextProfile int,@ContextStamp uniqueidentifier,@ContextBinding binary(32),@ContextIssuedUtc datetime2(7),@ContextExpiresUtc datetime2(7),@Actor int ,@FechaNegocio DATE, @DiasAviso INT=30 AS

BEGIN
IF @@TRANCOUNT=0 THROW 57400,N''Falta transaccion de login original.'',1;
IF @Actor IS NULL OR @Actor<>@ContextActor THROW 57401,N''Documento no disponible.'',1;
EXEC dbo.sp_Stage2_RequireOriginalLogin @OriginalLoginId,@ContextActor,@ContextProfile,@ContextStamp,@ContextBinding,@ContextIssuedUtc,@ContextExpiresUtc;

    SET NOCOUNT ON;
    SELECT
      COALESCE(SUM(CASE WHEN NoVence=0 AND FechaVencimiento>@FechaNegocio AND DATEDIFF(DAY,@FechaNegocio,FechaVencimiento)>@DiasAviso THEN 1 ELSE 0 END),0),
      COALESCE(SUM(CASE WHEN NoVence=0 AND FechaVencimiento>=@FechaNegocio AND DATEDIFF(DAY,@FechaNegocio,FechaVencimiento)<=@DiasAviso THEN 1 ELSE 0 END),0),
      COALESCE(SUM(CASE WHEN NoVence=0 AND FechaVencimiento<@FechaNegocio THEN 1 ELSE 0 END),0),
      COALESCE(SUM(CASE WHEN NoVence=1 THEN 1 ELSE 0 END),0)
    FROM dbo.DocumentosDigitales WITH(HOLDLOCK) WHERE Activo=1 AND DocumentSecurity.CanAccess(@Actor,ResponsableUsuarioId,DepartamentoId,''Read'',SYSUTCDATETIME())=1;
END;
');
EXEC(N'CREATE PROCEDURE DocumentSecurity.Document_GetById @OriginalLoginId uniqueidentifier,@ContextActor int,@ContextProfile int,@ContextStamp uniqueidentifier,@ContextBinding binary(32),@ContextIssuedUtc datetime2(7),@ContextExpiresUtc datetime2(7),@Actor int ,@DocumentoId INT, @FechaNegocio DATE, @DiasAviso INT=30 AS

BEGIN
IF @@TRANCOUNT=0 THROW 57400,N''Falta transaccion de login original.'',1;
IF @Actor IS NULL OR @Actor<>@ContextActor THROW 57401,N''Documento no disponible.'',1;
EXEC dbo.sp_Stage2_RequireOriginalLogin @OriginalLoginId,@ContextActor,@ContextProfile,@ContextStamp,@ContextBinding,@ContextIssuedUtc,@ContextExpiresUtc;
IF NOT EXISTS(SELECT 1 FROM dbo.DocumentosDigitales WITH(UPDLOCK,HOLDLOCK) WHERE DocumentoId=@DocumentoId AND DocumentSecurity.CanAccess(@Actor,ResponsableUsuarioId,DepartamentoId,''Read'',SYSUTCDATETIME())=1) THROW 57401,N''Documento no disponible.'',1;

IF NOT EXISTS(SELECT 1 FROM dbo.DocumentosDigitales d WITH(HOLDLOCK) JOIN dbo.DocumentoVersiones v WITH(HOLDLOCK) ON v.DocumentoVersionId=d.VersionActualId WHERE d.DocumentoId=@DocumentoId AND (v.StorageStatus=N''Ready'' OR d.ResponsableUsuarioId=@Actor)) THROW 57401,N''Documento no disponible.'',1;
    SET NOCOUNT ON;
    SELECT DocumentSecurity.CanAccess(@Actor,d.ResponsableUsuarioId,d.DepartamentoId,''Read'',SYSUTCDATETIME()) AS CanRead,DocumentSecurity.CanAccess(@Actor,d.ResponsableUsuarioId,d.DepartamentoId,''Edit'',SYSUTCDATETIME()) AS CanEdit,DocumentSecurity.CanAccess(@Actor,d.ResponsableUsuarioId,d.DepartamentoId,''Version'',SYSUTCDATETIME()) AS CanVersion,DocumentSecurity.CanAccess(@Actor,d.ResponsableUsuarioId,d.DepartamentoId,''Download'',SYSUTCDATETIME()) AS CanDownload,DocumentSecurity.CanAccess(@Actor,d.ResponsableUsuarioId,d.DepartamentoId,''Alert'',SYSUTCDATETIME()) AS CanAlert,d.DocumentoId,t.Nombre TipoDocumento,dep.Nombre Departamento,d.Titulo,d.NumeroReferencia,d.FechaEmision,d.FechaVencimiento,d.NoVence,d.Estado,d.Activo,
           v.Version,d.FechaActualizacionUtc,
           CASE WHEN d.NoVence=1 THEN N''No vence'' WHEN d.FechaVencimiento<@FechaNegocio THEN N''Vencido'' WHEN DATEDIFF(DAY,@FechaNegocio,d.FechaVencimiento)<=@DiasAviso THEN N''Por vencer'' ELSE N''Vigente'' END EstadoVencimiento,
           CASE WHEN d.NoVence=1 THEN NULL ELSE DATEDIFF(DAY,@FechaNegocio,d.FechaVencimiento) END DiasRestantes,
           d.Descripcion,v.NombreOriginal,v.MimeType,v.TamanoBytes,v.HashSha256,d.CreadoPorNombre,d.FechaCreacionUtc
    FROM dbo.DocumentosDigitales d INNER JOIN dbo.TiposDocumento t ON t.TipoDocumentoId=d.TipoDocumentoId
    LEFT JOIN dbo.DepartamentosOperativos dep ON dep.DepartamentoId=d.DepartamentoId
    INNER JOIN dbo.DocumentoVersiones v ON v.DocumentoVersionId=d.VersionActualId
    WHERE d.DocumentoId=@DocumentoId;
    SELECT DocumentoVersionId,Version,NombreOriginal,MimeType,TamanoBytes,HashSha256,StorageStatus,CreadoPorUsuarioId,CreadoPorNombre,FechaCreacionUtc
    FROM dbo.DocumentoVersiones WHERE DocumentoId=@DocumentoId AND (StorageStatus=N''Ready'' OR CreadoPorUsuarioId=@Actor OR EXISTS(SELECT 1 FROM dbo.DocumentosDigitales WITH(HOLDLOCK) WHERE DocumentoId=@DocumentoId AND ResponsableUsuarioId=@Actor)) ORDER BY Version DESC;
END;
');
EXEC(N'CREATE PROCEDURE DocumentSecurity.Document_GetForEdit @OriginalLoginId uniqueidentifier,@ContextActor int,@ContextProfile int,@ContextStamp uniqueidentifier,@ContextBinding binary(32),@ContextIssuedUtc datetime2(7),@ContextExpiresUtc datetime2(7),@Actor int ,@DocumentoId INT AS

BEGIN
IF @@TRANCOUNT=0 THROW 57400,N''Falta transaccion de login original.'',1;
IF @Actor IS NULL OR @Actor<>@ContextActor THROW 57401,N''Documento no disponible.'',1;
EXEC dbo.sp_Stage2_RequireOriginalLogin @OriginalLoginId,@ContextActor,@ContextProfile,@ContextStamp,@ContextBinding,@ContextIssuedUtc,@ContextExpiresUtc;
IF NOT EXISTS(SELECT 1 FROM dbo.DocumentosDigitales WITH(UPDLOCK,HOLDLOCK) WHERE DocumentoId=@DocumentoId AND DocumentSecurity.CanAccess(@Actor,ResponsableUsuarioId,DepartamentoId,''Edit'',SYSUTCDATETIME())=1) THROW 57401,N''Documento no disponible.'',1;
 SET NOCOUNT ON; SELECT DocumentoId,TipoDocumentoId,DepartamentoId,Titulo,Descripcion,NumeroReferencia,FechaEmision,FechaVencimiento,NoVence,Estado FROM dbo.DocumentosDigitales WITH(HOLDLOCK) WHERE DocumentoId=@DocumentoId; END;
');
EXEC(N'CREATE PROCEDURE DocumentSecurity.Document_Create @OriginalLoginId uniqueidentifier,@ContextActor int,@ContextProfile int,@ContextStamp uniqueidentifier,@ContextBinding binary(32),@ContextIssuedUtc datetime2(7),@ContextExpiresUtc datetime2(7),@Actor int ,@TipoDocumentoId INT,@DepartamentoId INT=NULL,@Titulo NVARCHAR(180),@Descripcion NVARCHAR(1000)=NULL,@NumeroReferencia NVARCHAR(100)=NULL,
    @FechaEmision DATE=NULL,@FechaVencimiento DATE=NULL,@NoVence BIT,@Estado NVARCHAR(30),@NombreOriginal NVARCHAR(255),@StorageKey NVARCHAR(80),
    @MimeType NVARCHAR(100),@Extension NVARCHAR(10),@TamanoBytes BIGINT,@HashSha256 CHAR(64),@UsuarioId INT,@UsuarioNombre NVARCHAR(150) AS

BEGIN
IF @@TRANCOUNT=0 THROW 57400,N''Falta transaccion de login original.'',1;
IF @Actor IS NULL OR @Actor<>@ContextActor THROW 57401,N''Documento no disponible.'',1;
EXEC dbo.sp_Stage2_RequireOriginalLogin @OriginalLoginId,@ContextActor,@ContextProfile,@ContextStamp,@ContextBinding,@ContextIssuedUtc,@ContextExpiresUtc;
IF @UsuarioId<>@Actor OR @UsuarioId IS NULL THROW 57401,N''Documento no disponible.'',1;
SELECT @UsuarioNombre=NombreCompleto FROM dbo.Usuarios WHERE UsuarioId=@Actor AND Activo=1;
IF @DepartamentoId IS NOT NULL AND NOT (DocumentSecurity.CanAccess(@Actor,0,@DepartamentoId,''Create'',SYSUTCDATETIME())=1) THROW 57402,N''Autorización de clasificación departamental no vigente.'',1;

    SET NOCOUNT ON; SET XACT_ABORT ON;
    IF @Estado NOT IN (N''Vigente'',N''Suspendido'',N''Archivado'') THROW 54000,N''Estado documental inválido.'',1;
    IF (@NoVence=0 AND @FechaVencimiento IS NULL) OR (@NoVence=1 AND @FechaVencimiento IS NOT NULL) THROW 54001,N''Vencimiento documental inconsistente.'',1;
    IF NULLIF(LTRIM(RTRIM(@Titulo)),N'''') IS NULL OR NOT EXISTS(SELECT 1 FROM dbo.TiposDocumento WHERE TipoDocumentoId=@TipoDocumentoId AND Activo=1) THROW 54008,N''Título o tipo documental inválido.'',1;
    IF @DepartamentoId IS NOT NULL AND NOT EXISTS(SELECT 1 FROM dbo.DepartamentosOperativos WHERE DepartamentoId=@DepartamentoId AND Activo=1) THROW 54009,N''El departamento no está activo.'',1;
    BEGIN TRANSACTION;
    INSERT dbo.DocumentosDigitales(TipoDocumentoId,DepartamentoId,ResponsableUsuarioId,Titulo,Descripcion,NumeroReferencia,FechaEmision,FechaVencimiento,NoVence,Estado,CreadoPorUsuarioId,CreadoPorNombre,ActualizadoPorUsuarioId,ActualizadoPorNombre)
    VALUES(@TipoDocumentoId,@DepartamentoId,@UsuarioId,LTRIM(RTRIM(@Titulo)),NULLIF(LTRIM(RTRIM(@Descripcion)),N''''),NULLIF(LTRIM(RTRIM(@NumeroReferencia)),N''''),@FechaEmision,@FechaVencimiento,@NoVence,@Estado,@UsuarioId,@UsuarioNombre,@UsuarioId,@UsuarioNombre);
    DECLARE @DocumentoId INT=CONVERT(INT,SCOPE_IDENTITY());
    INSERT dbo.DocumentoVersiones(DocumentoId,Version,NombreOriginal,StorageKey,MimeType,Extension,TamanoBytes,HashSha256,StorageStatus,CreadoPorUsuarioId,CreadoPorNombre)
    VALUES(@DocumentoId,1,@NombreOriginal,@StorageKey,@MimeType,@Extension,@TamanoBytes,@HashSha256,N''Pending'',@UsuarioId,@UsuarioNombre);
    UPDATE dbo.DocumentosDigitales SET VersionActualId=CONVERT(INT,SCOPE_IDENTITY()) WHERE DocumentoId=@DocumentoId;
    COMMIT; SELECT @DocumentoId;
END;
');
EXEC(N'CREATE PROCEDURE DocumentSecurity.Document_MarkReady @OriginalLoginId uniqueidentifier,@ContextActor int,@ContextProfile int,@ContextStamp uniqueidentifier,@ContextBinding binary(32),@ContextIssuedUtc datetime2(7),@ContextExpiresUtc datetime2(7),@Actor int ,@DocumentoId INT,@UsuarioId INT,@UsuarioNombre NVARCHAR(150) AS

BEGIN
IF @@TRANCOUNT=0 THROW 57400,N''Falta transaccion de login original.'',1;
IF @Actor IS NULL OR @Actor<>@ContextActor THROW 57401,N''Documento no disponible.'',1;
EXEC dbo.sp_Stage2_RequireOriginalLogin @OriginalLoginId,@ContextActor,@ContextProfile,@ContextStamp,@ContextBinding,@ContextIssuedUtc,@ContextExpiresUtc;
IF @UsuarioId<>@Actor OR @UsuarioId IS NULL THROW 57401,N''Documento no disponible.'',1;
SELECT @UsuarioNombre=NombreCompleto FROM dbo.Usuarios WHERE UsuarioId=@Actor AND Activo=1;
IF NOT EXISTS(SELECT 1 FROM dbo.DocumentosDigitales WITH(UPDLOCK,HOLDLOCK) WHERE DocumentoId=@DocumentoId AND ResponsableUsuarioId=@Actor) THROW 57401,N''Documento no disponible.'',1;

IF EXISTS(SELECT 1 FROM dbo.DocumentosDigitales WITH(HOLDLOCK) WHERE DocumentoId=@DocumentoId AND DepartamentoId IS NOT NULL AND DocumentSecurity.CanAccess(@Actor,0,DepartamentoId,''Create'',SYSUTCDATETIME())=0) THROW 57402,N''Autorización de clasificación departamental no vigente.'',1;
    SET NOCOUNT ON; SET XACT_ABORT ON; BEGIN TRANSACTION;
    UPDATE v SET StorageStatus=N''Ready'' FROM dbo.DocumentoVersiones v INNER JOIN dbo.DocumentosDigitales d ON d.VersionActualId=v.DocumentoVersionId
    WHERE d.DocumentoId=@DocumentoId AND v.StorageStatus=N''Pending'' AND v.CreadoPorUsuarioId=@UsuarioId;
    IF @@ROWCOUNT<>1 THROW 54002,N''No existe una carga documental pendiente autorizada.'',1;
    INSERT dbo.DocumentoAuditoria(DocumentoId,Accion,UsuarioId,UsuarioNombre,Detalle) VALUES(@DocumentoId,N''Crear'',@UsuarioId,@UsuarioNombre,N''Archivo validado y confirmado en almacenamiento privado.'');
    COMMIT;
END;
');
EXEC(N'CREATE PROCEDURE DocumentSecurity.Document_DeletePending @OriginalLoginId uniqueidentifier,@ContextActor int,@ContextProfile int,@ContextStamp uniqueidentifier,@ContextBinding binary(32),@ContextIssuedUtc datetime2(7),@ContextExpiresUtc datetime2(7),@Actor int ,@DocumentoId INT,@UsuarioId INT AS

BEGIN
IF @@TRANCOUNT=0 THROW 57400,N''Falta transaccion de login original.'',1;
IF @Actor IS NULL OR @Actor<>@ContextActor THROW 57401,N''Documento no disponible.'',1;
EXEC dbo.sp_Stage2_RequireOriginalLogin @OriginalLoginId,@ContextActor,@ContextProfile,@ContextStamp,@ContextBinding,@ContextIssuedUtc,@ContextExpiresUtc;
IF @UsuarioId<>@Actor OR @UsuarioId IS NULL THROW 57401,N''Documento no disponible.'',1;
IF NOT EXISTS(SELECT 1 FROM dbo.DocumentosDigitales WITH(UPDLOCK,HOLDLOCK) WHERE DocumentoId=@DocumentoId AND ResponsableUsuarioId=@Actor) THROW 57401,N''Documento no disponible.'',1;

    SET NOCOUNT ON; SET XACT_ABORT ON; BEGIN TRANSACTION;
    IF EXISTS(SELECT 1 FROM dbo.DocumentosDigitales d INNER JOIN dbo.DocumentoVersiones v ON v.DocumentoVersionId=d.VersionActualId WHERE d.DocumentoId=@DocumentoId AND d.CreadoPorUsuarioId=@UsuarioId AND v.StorageStatus=N''Pending'')
    BEGIN UPDATE dbo.DocumentosDigitales SET VersionActualId=NULL WHERE DocumentoId=@DocumentoId; DELETE dbo.DocumentoVersiones WHERE DocumentoId=@DocumentoId; DELETE dbo.DocumentosDigitales WHERE DocumentoId=@DocumentoId; END;
    COMMIT;
END;
');
EXEC(N'CREATE PROCEDURE DocumentSecurity.Document_Update @OriginalLoginId uniqueidentifier,@ContextActor int,@ContextProfile int,@ContextStamp uniqueidentifier,@ContextBinding binary(32),@ContextIssuedUtc datetime2(7),@ContextExpiresUtc datetime2(7),@Actor int ,@DocumentoId INT,@TipoDocumentoId INT,@DepartamentoId INT=NULL,@Titulo NVARCHAR(180),@Descripcion NVARCHAR(1000)=NULL,@NumeroReferencia NVARCHAR(100)=NULL,
    @FechaEmision DATE=NULL,@FechaVencimiento DATE=NULL,@NoVence BIT,@Estado NVARCHAR(30),@UsuarioId INT,@UsuarioNombre NVARCHAR(150) AS

BEGIN
IF @@TRANCOUNT=0 THROW 57400,N''Falta transaccion de login original.'',1;
IF @Actor IS NULL OR @Actor<>@ContextActor THROW 57401,N''Documento no disponible.'',1;
EXEC dbo.sp_Stage2_RequireOriginalLogin @OriginalLoginId,@ContextActor,@ContextProfile,@ContextStamp,@ContextBinding,@ContextIssuedUtc,@ContextExpiresUtc;
IF @UsuarioId<>@Actor OR @UsuarioId IS NULL THROW 57401,N''Documento no disponible.'',1;
SELECT @UsuarioNombre=NombreCompleto FROM dbo.Usuarios WHERE UsuarioId=@Actor AND Activo=1;
IF NOT EXISTS(SELECT 1 FROM dbo.DocumentosDigitales WITH(UPDLOCK,HOLDLOCK) WHERE DocumentoId=@DocumentoId AND DocumentSecurity.CanAccess(@Actor,ResponsableUsuarioId,DepartamentoId,''Edit'',SYSUTCDATETIME())=1) THROW 57401,N''Documento no disponible.'',1;
IF EXISTS(SELECT 1 FROM dbo.DocumentosDigitales WITH(HOLDLOCK) WHERE DocumentoId=@DocumentoId AND ISNULL(DepartamentoId,-1)<>ISNULL(@DepartamentoId,-1))
BEGIN
 IF NOT EXISTS(SELECT 1 FROM dbo.DocumentosDigitales WITH(HOLDLOCK) WHERE DocumentoId=@DocumentoId AND ResponsableUsuarioId=@Actor) OR (@DepartamentoId IS NOT NULL AND NOT (DocumentSecurity.CanAccess(@Actor,0,@DepartamentoId,''Create'',SYSUTCDATETIME())=1)) THROW 57402,N''Clasificación departamental no autorizada.'',1;
END;

    SET NOCOUNT ON; SET XACT_ABORT ON;
    IF @Estado NOT IN (N''Vigente'',N''Suspendido'',N''Archivado'') THROW 54003,N''Estado documental inválido.'',1;
    UPDATE dbo.DocumentosDigitales SET TipoDocumentoId=@TipoDocumentoId,DepartamentoId=@DepartamentoId,Titulo=LTRIM(RTRIM(@Titulo)),Descripcion=NULLIF(LTRIM(RTRIM(@Descripcion)),N''''),
      NumeroReferencia=NULLIF(LTRIM(RTRIM(@NumeroReferencia)),N''''),FechaEmision=@FechaEmision,FechaVencimiento=CASE WHEN @NoVence=1 THEN NULL ELSE @FechaVencimiento END,
      NoVence=@NoVence,Estado=@Estado,ActualizadoPorUsuarioId=@UsuarioId,ActualizadoPorNombre=@UsuarioNombre,FechaActualizacionUtc=SYSUTCDATETIME() WHERE DocumentoId=@DocumentoId;
    IF @@ROWCOUNT<>1 THROW 54004,N''El documento no existe.'',1;
    INSERT dbo.DocumentoAuditoria(DocumentoId,Accion,UsuarioId,UsuarioNombre,Detalle) VALUES(@DocumentoId,N''Editar metadatos'',@UsuarioId,@UsuarioNombre,NULL);
END;
');
EXEC(N'CREATE PROCEDURE DocumentSecurity.Document_ReplaceFile @OriginalLoginId uniqueidentifier,@ContextActor int,@ContextProfile int,@ContextStamp uniqueidentifier,@ContextBinding binary(32),@ContextIssuedUtc datetime2(7),@ContextExpiresUtc datetime2(7),@Actor int ,@DocumentoId INT,@NombreOriginal NVARCHAR(255),@StorageKey NVARCHAR(80),@MimeType NVARCHAR(100),@Extension NVARCHAR(10),@TamanoBytes BIGINT,@HashSha256 CHAR(64),@UsuarioId INT,@UsuarioNombre NVARCHAR(150) AS

BEGIN
IF @@TRANCOUNT=0 THROW 57400,N''Falta transaccion de login original.'',1;
IF @Actor IS NULL OR @Actor<>@ContextActor THROW 57401,N''Documento no disponible.'',1;
EXEC dbo.sp_Stage2_RequireOriginalLogin @OriginalLoginId,@ContextActor,@ContextProfile,@ContextStamp,@ContextBinding,@ContextIssuedUtc,@ContextExpiresUtc;
IF @UsuarioId<>@Actor OR @UsuarioId IS NULL THROW 57401,N''Documento no disponible.'',1;
SELECT @UsuarioNombre=NombreCompleto FROM dbo.Usuarios WHERE UsuarioId=@Actor AND Activo=1;
IF NOT EXISTS(SELECT 1 FROM dbo.DocumentosDigitales WITH(UPDLOCK,HOLDLOCK) WHERE DocumentoId=@DocumentoId AND DocumentSecurity.CanAccess(@Actor,ResponsableUsuarioId,DepartamentoId,''Version'',SYSUTCDATETIME())=1) THROW 57401,N''Documento no disponible.'',1;

    SET NOCOUNT ON; SET XACT_ABORT ON; SET TRANSACTION ISOLATION LEVEL SERIALIZABLE; BEGIN TRANSACTION;
    IF NOT EXISTS(SELECT 1 FROM dbo.DocumentosDigitales WITH(UPDLOCK,HOLDLOCK) WHERE DocumentoId=@DocumentoId AND Activo=1) THROW 54005,N''El documento no está activo.'',1;
    IF EXISTS(SELECT 1 FROM dbo.DocumentoVersiones WHERE DocumentoId=@DocumentoId AND HashSha256=@HashSha256) THROW 54010,N''El mismo contenido ya existe en el historial del documento.'',1;
    DECLARE @Version INT=ISNULL((SELECT MAX(Version) FROM dbo.DocumentoVersiones WHERE DocumentoId=@DocumentoId),0)+1;
    INSERT dbo.DocumentoVersiones(DocumentoId,Version,NombreOriginal,StorageKey,MimeType,Extension,TamanoBytes,HashSha256,StorageStatus,CreadoPorUsuarioId,CreadoPorNombre)
    VALUES(@DocumentoId,@Version,@NombreOriginal,@StorageKey,@MimeType,@Extension,@TamanoBytes,@HashSha256,N''Pending'',@UsuarioId,@UsuarioNombre);
    DECLARE @VersionId INT=CONVERT(INT,SCOPE_IDENTITY()); COMMIT; SELECT @VersionId;
END;
');
EXEC(N'CREATE PROCEDURE DocumentSecurity.Document_MarkVersionReady @OriginalLoginId uniqueidentifier,@ContextActor int,@ContextProfile int,@ContextStamp uniqueidentifier,@ContextBinding binary(32),@ContextIssuedUtc datetime2(7),@ContextExpiresUtc datetime2(7),@Actor int ,@DocumentoId INT,@DocumentoVersionId INT,@UsuarioId INT,@UsuarioNombre NVARCHAR(150) AS

BEGIN
IF @@TRANCOUNT=0 THROW 57400,N''Falta transaccion de login original.'',1;
IF @Actor IS NULL OR @Actor<>@ContextActor THROW 57401,N''Documento no disponible.'',1;
EXEC dbo.sp_Stage2_RequireOriginalLogin @OriginalLoginId,@ContextActor,@ContextProfile,@ContextStamp,@ContextBinding,@ContextIssuedUtc,@ContextExpiresUtc;
IF @UsuarioId<>@Actor OR @UsuarioId IS NULL THROW 57401,N''Documento no disponible.'',1;
SELECT @UsuarioNombre=NombreCompleto FROM dbo.Usuarios WHERE UsuarioId=@Actor AND Activo=1;
IF NOT EXISTS(SELECT 1 FROM dbo.DocumentosDigitales WITH(UPDLOCK,HOLDLOCK) WHERE DocumentoId=@DocumentoId AND DocumentSecurity.CanAccess(@Actor,ResponsableUsuarioId,DepartamentoId,''Version'',SYSUTCDATETIME())=1) THROW 57401,N''Documento no disponible.'',1;

    SET NOCOUNT ON; SET XACT_ABORT ON; BEGIN TRANSACTION;
    UPDATE dbo.DocumentoVersiones SET StorageStatus=N''Ready'' WHERE DocumentoVersionId=@DocumentoVersionId AND DocumentoId=@DocumentoId AND StorageStatus=N''Pending'' AND CreadoPorUsuarioId=@UsuarioId;
    IF @@ROWCOUNT<>1 THROW 54006,N''No existe una versión pendiente autorizada.'',1;
    UPDATE dbo.DocumentosDigitales SET VersionActualId=@DocumentoVersionId,ActualizadoPorUsuarioId=@UsuarioId,ActualizadoPorNombre=@UsuarioNombre,FechaActualizacionUtc=SYSUTCDATETIME() WHERE DocumentoId=@DocumentoId;
    INSERT dbo.DocumentoAuditoria(DocumentoId,Accion,UsuarioId,UsuarioNombre,Detalle) VALUES(@DocumentoId,N''Reemplazar archivo'',@UsuarioId,@UsuarioNombre,CONCAT(N''Versión '',@DocumentoVersionId,N'' confirmada.''));
    COMMIT;
END;
');
EXEC(N'CREATE PROCEDURE DocumentSecurity.Document_DeletePendingVersion @OriginalLoginId uniqueidentifier,@ContextActor int,@ContextProfile int,@ContextStamp uniqueidentifier,@ContextBinding binary(32),@ContextIssuedUtc datetime2(7),@ContextExpiresUtc datetime2(7),@Actor int ,@DocumentoId INT,@DocumentoVersionId INT,@UsuarioId INT AS

BEGIN
IF @@TRANCOUNT=0 THROW 57400,N''Falta transaccion de login original.'',1;
IF @Actor IS NULL OR @Actor<>@ContextActor THROW 57401,N''Documento no disponible.'',1;
EXEC dbo.sp_Stage2_RequireOriginalLogin @OriginalLoginId,@ContextActor,@ContextProfile,@ContextStamp,@ContextBinding,@ContextIssuedUtc,@ContextExpiresUtc;
IF @UsuarioId<>@Actor OR @UsuarioId IS NULL THROW 57401,N''Documento no disponible.'',1;
IF NOT EXISTS(SELECT 1 FROM dbo.DocumentosDigitales WITH(UPDLOCK,HOLDLOCK) WHERE DocumentoId=@DocumentoId AND DocumentSecurity.CanAccess(@Actor,ResponsableUsuarioId,DepartamentoId,''Version'',SYSUTCDATETIME())=1) THROW 57401,N''Documento no disponible.'',1;
 SET NOCOUNT ON; DELETE dbo.DocumentoVersiones WHERE DocumentoVersionId=@DocumentoVersionId AND DocumentoId=@DocumentoId AND StorageStatus=N''Pending'' AND CreadoPorUsuarioId=@UsuarioId; END;
');
EXEC(N'CREATE PROCEDURE DocumentSecurity.Document_SetStatus @OriginalLoginId uniqueidentifier,@ContextActor int,@ContextProfile int,@ContextStamp uniqueidentifier,@ContextBinding binary(32),@ContextIssuedUtc datetime2(7),@ContextExpiresUtc datetime2(7),@Actor int ,@DocumentoId INT,@Activo BIT,@UsuarioId INT,@UsuarioNombre NVARCHAR(150) AS

BEGIN
IF @@TRANCOUNT=0 THROW 57400,N''Falta transaccion de login original.'',1;
IF @Actor IS NULL OR @Actor<>@ContextActor THROW 57401,N''Documento no disponible.'',1;
EXEC dbo.sp_Stage2_RequireOriginalLogin @OriginalLoginId,@ContextActor,@ContextProfile,@ContextStamp,@ContextBinding,@ContextIssuedUtc,@ContextExpiresUtc;
IF @UsuarioId<>@Actor OR @UsuarioId IS NULL THROW 57401,N''Documento no disponible.'',1;
SELECT @UsuarioNombre=NombreCompleto FROM dbo.Usuarios WHERE UsuarioId=@Actor AND Activo=1;
IF NOT EXISTS(SELECT 1 FROM dbo.DocumentosDigitales WITH(UPDLOCK,HOLDLOCK) WHERE DocumentoId=@DocumentoId AND DocumentSecurity.CanAccess(@Actor,ResponsableUsuarioId,DepartamentoId,''Edit'',SYSUTCDATETIME())=1) THROW 57401,N''Documento no disponible.'',1;

    SET NOCOUNT ON; UPDATE dbo.DocumentosDigitales SET Activo=@Activo,ActualizadoPorUsuarioId=@UsuarioId,ActualizadoPorNombre=@UsuarioNombre,FechaActualizacionUtc=SYSUTCDATETIME() WHERE DocumentoId=@DocumentoId;
    IF @@ROWCOUNT<>1 THROW 54007,N''El documento no existe.'',1;
    INSERT dbo.DocumentoAuditoria(DocumentoId,Accion,UsuarioId,UsuarioNombre,Detalle) VALUES(@DocumentoId,CASE WHEN @Activo=1 THEN N''Reactivar'' ELSE N''Desactivar'' END,@UsuarioId,@UsuarioNombre,N''Borrado lógico; historial conservado.'');
END;
');
EXEC(N'CREATE PROCEDURE DocumentSecurity.Document_GetFileMetadata @OriginalLoginId uniqueidentifier,@ContextActor int,@ContextProfile int,@ContextStamp uniqueidentifier,@ContextBinding binary(32),@ContextIssuedUtc datetime2(7),@ContextExpiresUtc datetime2(7),@Actor int ,@DocumentoId INT,@DocumentoVersionId INT=NULL,@UsuarioId INT,@PuedeAdministrar BIT=0 AS

BEGIN
IF @@TRANCOUNT=0 THROW 57400,N''Falta transaccion de login original.'',1;
IF @Actor IS NULL OR @Actor<>@ContextActor THROW 57401,N''Documento no disponible.'',1;
EXEC dbo.sp_Stage2_RequireOriginalLogin @OriginalLoginId,@ContextActor,@ContextProfile,@ContextStamp,@ContextBinding,@ContextIssuedUtc,@ContextExpiresUtc;
IF @UsuarioId<>@Actor OR @UsuarioId IS NULL THROW 57401,N''Documento no disponible.'',1;
IF NOT EXISTS(SELECT 1 FROM dbo.DocumentosDigitales WITH(UPDLOCK,HOLDLOCK) WHERE DocumentoId=@DocumentoId AND DocumentSecurity.CanAccess(@Actor,ResponsableUsuarioId,DepartamentoId,''Download'',SYSUTCDATETIME())=1) THROW 57401,N''Documento no disponible.'',1;

    SET NOCOUNT ON;
    SELECT v.DocumentoVersionId,v.StorageKey,v.NombreOriginal,v.MimeType FROM dbo.DocumentosDigitales d INNER JOIN dbo.DocumentoVersiones v
      ON v.DocumentoId=d.DocumentoId AND v.DocumentoVersionId=COALESCE(@DocumentoVersionId,d.VersionActualId)
    WHERE d.DocumentoId=@DocumentoId AND v.StorageStatus=N''Ready'' AND d.Activo=1;
END;
');
EXEC(N'CREATE PROCEDURE DocumentSecurity.DocumentAlert_Generate @OriginalLoginId uniqueidentifier,@ContextActor int,@ContextProfile int,@ContextStamp uniqueidentifier,@ContextBinding binary(32),@ContextIssuedUtc datetime2(7),@ContextExpiresUtc datetime2(7),@Actor int ,@FechaNegocio DATE,@Umbrales NVARCHAR(100),@UsuarioId INT AS

BEGIN
IF @@TRANCOUNT=0 THROW 57400,N''Falta transaccion de login original.'',1;
IF @Actor IS NULL OR @Actor<>@ContextActor THROW 57401,N''Documento no disponible.'',1;
EXEC dbo.sp_Stage2_RequireOriginalLogin @OriginalLoginId,@ContextActor,@ContextProfile,@ContextStamp,@ContextBinding,@ContextIssuedUtc,@ContextExpiresUtc;
IF @UsuarioId<>@Actor OR @UsuarioId IS NULL THROW 57401,N''Documento no disponible.'',1;

    SET NOCOUNT ON; SET XACT_ABORT ON;
    DECLARE @t TABLE(Dias INT PRIMARY KEY);
    INSERT @t(Dias) SELECT DISTINCT TRY_CONVERT(INT,value) FROM STRING_SPLIT(@Umbrales,N'','') WHERE TRY_CONVERT(INT,value) BETWEEN 0 AND 365;
    DECLARE @nuevas TABLE(AlertaId INT,DocumentoId INT,UmbralDias INT);
    BEGIN TRANSACTION;
    INSERT dbo.AlertasDocumento(DocumentoId,FechaVencimiento,UmbralDias,GeneradaPorUsuarioId)
      OUTPUT inserted.AlertaId,inserted.DocumentoId,inserted.UmbralDias INTO @nuevas
    SELECT d.DocumentoId,d.FechaVencimiento,umbral.Dias,@UsuarioId
    FROM dbo.DocumentosDigitales d WITH(HOLDLOCK)
    CROSS APPLY(SELECT MIN(t.Dias) Dias FROM @t t WHERE t.Dias>=DATEDIFF(DAY,@FechaNegocio,d.FechaVencimiento)) umbral
    WHERE DocumentSecurity.CanAccess(@Actor,d.ResponsableUsuarioId,d.DepartamentoId,''Alert'',SYSUTCDATETIME())=1 AND d.Activo=1 AND d.NoVence=0 AND d.Estado=N''Vigente'' AND umbral.Dias IS NOT NULL
      AND NOT EXISTS(SELECT 1 FROM dbo.AlertasDocumento a WITH(UPDLOCK,HOLDLOCK) WHERE a.DocumentoId=d.DocumentoId AND a.FechaVencimiento=d.FechaVencimiento AND a.UmbralDias=umbral.Dias);
    COMMIT;
    SELECT n.AlertaId,n.DocumentoId,n.UmbralDias,d.Titulo,d.FechaVencimiento,ISNULL(u.Correo,N'''') Destinatario
    FROM @nuevas n INNER JOIN dbo.DocumentosDigitales d ON d.DocumentoId=n.DocumentoId
    LEFT JOIN dbo.Usuarios u ON u.UsuarioId=d.ResponsableUsuarioId;
END;
');
EXEC(N'CREATE PROCEDURE DocumentSecurity.DocumentAlert_List @OriginalLoginId uniqueidentifier,@ContextActor int,@ContextProfile int,@ContextStamp uniqueidentifier,@ContextBinding binary(32),@ContextIssuedUtc datetime2(7),@ContextExpiresUtc datetime2(7),@Actor int ,@Estado NVARCHAR(30)=NULL,@DepartamentoId INT=NULL,@MaxDias INT=NULL,@FechaNegocio DATE,@Pagina INT=1,@TamanoPagina INT=20 AS

BEGIN
IF @@TRANCOUNT=0 THROW 57400,N''Falta transaccion de login original.'',1;
IF @Actor IS NULL OR @Actor<>@ContextActor THROW 57401,N''Documento no disponible.'',1;
EXEC dbo.sp_Stage2_RequireOriginalLogin @OriginalLoginId,@ContextActor,@ContextProfile,@ContextStamp,@ContextBinding,@ContextIssuedUtc,@ContextExpiresUtc;

 SET NOCOUNT ON; SET @Pagina=CASE WHEN @Pagina<1 THEN 1 ELSE @Pagina END; SET @TamanoPagina=CASE WHEN @TamanoPagina<1 THEN 20 WHEN @TamanoPagina>100 THEN 100 ELSE @TamanoPagina END;
 ;WITH q AS(
 SELECT a.AlertaId,d.DocumentoId,d.Titulo,t.Nombre TipoDocumento,dep.Nombre Departamento,u.NombreCompleto Responsable,
   a.FechaVencimiento,DATEDIFF(DAY,@FechaNegocio,a.FechaVencimiento) DiasRestantes,
   CASE WHEN a.FechaVencimiento<@FechaNegocio THEN N''Vencido'' WHEN a.FechaVencimiento=@FechaNegocio THEN N''Vence hoy'' ELSE N''Por vencer'' END EstadoVencimiento,
   a.Estado,a.FechaCreacionUtc,COUNT(*) OVER() TotalResultados
 FROM dbo.AlertasDocumento a INNER JOIN dbo.DocumentosDigitales d ON d.DocumentoId=a.DocumentoId
 INNER JOIN dbo.TiposDocumento t ON t.TipoDocumentoId=d.TipoDocumentoId LEFT JOIN dbo.DepartamentosOperativos dep ON dep.DepartamentoId=d.DepartamentoId
 INNER JOIN dbo.Usuarios u ON u.UsuarioId=d.ResponsableUsuarioId
 WHERE DocumentSecurity.CanAccess(@Actor,d.ResponsableUsuarioId,d.DepartamentoId,''Alert'',SYSUTCDATETIME())=1 AND (@Estado IS NULL OR a.Estado=@Estado) AND (@DepartamentoId IS NULL OR d.DepartamentoId=@DepartamentoId)
   AND (@MaxDias IS NULL OR DATEDIFF(DAY,@FechaNegocio,a.FechaVencimiento)<=@MaxDias)
 ) SELECT * FROM q ORDER BY CASE Estado WHEN N''Activa'' THEN 0 ELSE 1 END,DiasRestantes,AlertaId DESC
 OFFSET (@Pagina-1)*@TamanoPagina ROWS FETCH NEXT @TamanoPagina ROWS ONLY;
END;
');
EXEC(N'CREATE PROCEDURE DocumentSecurity.DocumentAlert_Summary @OriginalLoginId uniqueidentifier,@ContextActor int,@ContextProfile int,@ContextStamp uniqueidentifier,@ContextBinding binary(32),@ContextIssuedUtc datetime2(7),@ContextExpiresUtc datetime2(7),@Actor int ,@FechaNegocio DATE AS

BEGIN
IF @@TRANCOUNT=0 THROW 57400,N''Falta transaccion de login original.'',1;
IF @Actor IS NULL OR @Actor<>@ContextActor THROW 57401,N''Documento no disponible.'',1;
EXEC dbo.sp_Stage2_RequireOriginalLogin @OriginalLoginId,@ContextActor,@ContextProfile,@ContextStamp,@ContextBinding,@ContextIssuedUtc,@ContextExpiresUtc;
 SET NOCOUNT ON; SELECT COALESCE(SUM(CASE WHEN Estado=N''Activa'' THEN 1 ELSE 0 END),0),COALESCE(SUM(CASE WHEN Estado=N''Activa'' AND FechaVencimiento<@FechaNegocio THEN 1 ELSE 0 END),0) FROM dbo.AlertasDocumento WHERE DocumentoId IN(SELECT DocumentoId FROM dbo.DocumentosDigitales WITH(HOLDLOCK) WHERE DocumentSecurity.CanAccess(@Actor,ResponsableUsuarioId,DepartamentoId,''Alert'',SYSUTCDATETIME())=1); END;
');
EXEC(N'CREATE PROCEDURE DocumentSecurity.DocumentAlert_MarkHandled @OriginalLoginId uniqueidentifier,@ContextActor int,@ContextProfile int,@ContextStamp uniqueidentifier,@ContextBinding binary(32),@ContextIssuedUtc datetime2(7),@ContextExpiresUtc datetime2(7),@Actor int ,@AlertaId INT,@UsuarioId INT AS

BEGIN
IF @@TRANCOUNT=0 THROW 57400,N''Falta transaccion de login original.'',1;
IF @Actor IS NULL OR @Actor<>@ContextActor THROW 57401,N''Documento no disponible.'',1;
EXEC dbo.sp_Stage2_RequireOriginalLogin @OriginalLoginId,@ContextActor,@ContextProfile,@ContextStamp,@ContextBinding,@ContextIssuedUtc,@ContextExpiresUtc;
IF @UsuarioId<>@Actor OR @UsuarioId IS NULL THROW 57401,N''Documento no disponible.'',1;
IF NOT EXISTS(SELECT 1 FROM dbo.AlertasDocumento a WITH(UPDLOCK,HOLDLOCK) JOIN dbo.DocumentosDigitales d WITH(UPDLOCK,HOLDLOCK) ON d.DocumentoId=a.DocumentoId WHERE a.AlertaId=@AlertaId AND DocumentSecurity.CanAccess(@Actor,d.ResponsableUsuarioId,d.DepartamentoId,''Alert'',SYSUTCDATETIME())=1) THROW 57401,N''Documento no disponible.'',1;
 SET NOCOUNT ON; UPDATE dbo.AlertasDocumento SET Estado=N''Atendida'',AtendidaPorUsuarioId=@UsuarioId,FechaAtencionUtc=SYSUTCDATETIME() WHERE AlertaId=@AlertaId AND Estado=N''Activa''; IF @@ROWCOUNT=0 THROW 54100,N''La alerta no existe o ya fue atendida.'',1; INSERT dbo.DocumentoAuditoria(DocumentoId,Accion,UsuarioId,UsuarioNombre,Detalle) SELECT DocumentoId,N''Atender alerta'',@Actor,(SELECT NombreCompleto FROM dbo.Usuarios WHERE UsuarioId=@Actor),N''Alerta documental atendida.'' FROM dbo.AlertasDocumento WHERE AlertaId=@AlertaId; END;
');
EXEC(N'CREATE PROCEDURE DocumentSecurity.DocumentAlert_RegisterNotification @OriginalLoginId uniqueidentifier,@ContextActor int,@ContextProfile int,@ContextStamp uniqueidentifier,@ContextBinding binary(32),@ContextIssuedUtc datetime2(7),@ContextExpiresUtc datetime2(7),@Actor int ,@AlertaId INT,@Canal NVARCHAR(20),@Destinatario NVARCHAR(320),@EstadoEnvio NVARCHAR(20),@ErrorTecnicoResumido NVARCHAR(100)=NULL AS

BEGIN
IF @@TRANCOUNT=0 THROW 57400,N''Falta transaccion de login original.'',1;
IF @Actor IS NULL OR @Actor<>@ContextActor THROW 57401,N''Documento no disponible.'',1;
EXEC dbo.sp_Stage2_RequireOriginalLogin @OriginalLoginId,@ContextActor,@ContextProfile,@ContextStamp,@ContextBinding,@ContextIssuedUtc,@ContextExpiresUtc;
IF NOT EXISTS(SELECT 1 FROM dbo.AlertasDocumento a WITH(UPDLOCK,HOLDLOCK) JOIN dbo.DocumentosDigitales d WITH(UPDLOCK,HOLDLOCK) ON d.DocumentoId=a.DocumentoId WHERE a.AlertaId=@AlertaId AND DocumentSecurity.CanAccess(@Actor,d.ResponsableUsuarioId,d.DepartamentoId,''Alert'',SYSUTCDATETIME())=1) THROW 57401,N''Documento no disponible.'',1;

IF NOT EXISTS(SELECT 1 FROM dbo.AlertasDocumento a WITH(HOLDLOCK) JOIN dbo.DocumentosDigitales d WITH(HOLDLOCK) ON d.DocumentoId=a.DocumentoId JOIN dbo.Usuarios u WITH(HOLDLOCK) ON u.UsuarioId=d.ResponsableUsuarioId AND u.Activo=1 WHERE a.AlertaId=@AlertaId AND u.Correo=@Destinatario) THROW 57401,N''Destinatario canónico no acreditado.'',1;
 SET NOCOUNT ON;
 IF NOT EXISTS(SELECT 1 FROM dbo.NotificacionesAlertaDocumento WHERE AlertaId=@AlertaId AND Canal=@Canal)
   INSERT dbo.NotificacionesAlertaDocumento(AlertaId,Canal,Destinatario,EstadoEnvio,ErrorTecnicoResumido) VALUES(@AlertaId,@Canal,@Destinatario,@EstadoEnvio,@ErrorTecnicoResumido);
END;
');
EXEC(N'CREATE PROCEDURE DocumentSecurity.Document_AuthorizeDownload
 @OriginalLoginId uniqueidentifier,@ContextActor int,@ContextProfile int,@ContextStamp uniqueidentifier,@ContextBinding binary(32),@ContextIssuedUtc datetime2(7),@ContextExpiresUtc datetime2(7),@Actor int,@DocumentoId int,@DocumentoVersionId int=NULL,@UsuarioId int,@PuedeAdministrar bit=0
AS BEGIN SET NOCOUNT ON;SET XACT_ABORT ON;
 IF @@TRANCOUNT=0 OR @Actor<>@UsuarioId THROW 57401,N''Documento no disponible.'',1;
 IF @Actor IS NULL OR @Actor<>@ContextActor THROW 57401,N''Documento no disponible.'',1;
EXEC dbo.sp_Stage2_RequireOriginalLogin @OriginalLoginId,@ContextActor,@ContextProfile,@ContextStamp,@ContextBinding,@ContextIssuedUtc,@ContextExpiresUtc;
 DECLARE @Files TABLE(VersionId int,StorageKey nvarchar(80),OriginalName nvarchar(255),MimeType nvarchar(100));
 INSERT @Files EXEC DocumentSecurity.Document_GetFileMetadata @OriginalLoginId=@OriginalLoginId,@ContextActor=@ContextActor,@ContextProfile=@ContextProfile,@ContextStamp=@ContextStamp,@ContextBinding=@ContextBinding,@ContextIssuedUtc=@ContextIssuedUtc,@ContextExpiresUtc=@ContextExpiresUtc,@Actor=@Actor,@DocumentoId=@DocumentoId,@DocumentoVersionId=@DocumentoVersionId,@UsuarioId=@UsuarioId,@PuedeAdministrar=0;
 IF EXISTS(SELECT 1 FROM @Files) INSERT dbo.DocumentoAuditoria(DocumentoId,Accion,UsuarioId,UsuarioNombre,Detalle)
 SELECT @DocumentoId,N''Autorizar descarga'',@Actor,NombreCompleto,N''Propietario o delegación departamental para descarga acreditados; no afirma transferencia completa del archivo.'' FROM dbo.Usuarios WHERE UsuarioId=@Actor;
 IF @Actor IS NULL OR @Actor<>@ContextActor THROW 57401,N''Documento no disponible.'',1;
EXEC dbo.sp_Stage2_RequireOriginalLogin @OriginalLoginId,@ContextActor,@ContextProfile,@ContextStamp,@ContextBinding,@ContextIssuedUtc,@ContextExpiresUtc;
 SELECT VersionId,StorageKey,OriginalName,MimeType FROM @Files;
END;
');
EXEC(N'CREATE PROCEDURE DocumentSecurity.Document_PublicationReceipt
 @OriginalLoginId uniqueidentifier,@ContextActor int,@ContextProfile int,@ContextStamp uniqueidentifier,@ContextBinding binary(32),@ContextIssuedUtc datetime2(7),@ContextExpiresUtc datetime2(7),@Actor int,@DocumentoId int,@DocumentoVersionId int=NULL,@UsuarioId int,@StorageKey nvarchar(80)
AS BEGIN SET NOCOUNT ON;
 IF @@TRANCOUNT=0 OR @UsuarioId<>@Actor THROW 57401,N''Documento no disponible.'',1;
 IF @Actor IS NULL OR @Actor<>@ContextActor THROW 57401,N''Documento no disponible.'',1;
EXEC dbo.sp_Stage2_RequireOriginalLogin @OriginalLoginId,@ContextActor,@ContextProfile,@ContextStamp,@ContextBinding,@ContextIssuedUtc,@ContextExpiresUtc;
 IF NOT EXISTS(SELECT 1 FROM dbo.DocumentosDigitales d WITH(UPDLOCK,HOLDLOCK) WHERE d.DocumentoId=@DocumentoId AND DocumentSecurity.CanAccess(@Actor,d.ResponsableUsuarioId,d.DepartamentoId,''Version'',SYSUTCDATETIME())=1) THROW 57401,N''Documento no disponible.'',1;
 SELECT v.DocumentoVersionId,v.StorageKey,v.NombreOriginal,v.MimeType FROM dbo.DocumentosDigitales d WITH(HOLDLOCK) JOIN dbo.DocumentoVersiones v WITH(HOLDLOCK) ON v.DocumentoId=d.DocumentoId
 WHERE d.DocumentoId=@DocumentoId AND (@DocumentoVersionId IS NULL OR v.DocumentoVersionId=@DocumentoVersionId) AND v.StorageKey=@StorageKey AND v.StorageStatus=N''Ready'' AND v.CreadoPorUsuarioId=@Actor;
END;
');
INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes) VALUES(N'0055_stage2_document_delegation',N'0055_stage2_document_delegation.sql',UPPER(@Hash),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),N'Explicit department/operation source with stamps, revisions, expiry and original-login audit; versioned document contracts validate original login inside SQL. No profile grant, assignment, automatic cleanup or owner bypass.');
COMMIT;END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK;THROW;END CATCH;
