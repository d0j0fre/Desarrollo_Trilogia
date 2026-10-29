-- Durable complaint creation: original login, immutable normalized payload and one committed receipt.
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF SESSIONPROPERTY('QUOTED_IDENTIFIER')<>1 OR SESSIONPROPERTY('ANSI_NULLS')<>1 THROW 59600,N'SQL options unavailable.',1;
DECLARE @Hash nvarchar(128)=N'$(MigrationSha256)';
IF LEN(@Hash)<>64 OR @Hash LIKE N'%[^0-9a-fA-F]%' THROW 59600,N'Invalid migration hash.',1;
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL THROW 59600,N'Ledger unavailable.',1;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @Lock int;
 EXEC @Lock=sys.sp_getapplock @Resource=N'Stage2:0060:install',@LockMode=N'Exclusive',@LockOwner=N'Transaction',@LockTimeout=10000;
 IF @Lock<0 THROW 59600,N'Concurrent installation.',1;
 IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WITH(UPDLOCK,HOLDLOCK) WHERE MigrationId=N'0060_stage2_complaint_request_receipts')
 BEGIN
  IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0060_stage2_complaint_request_receipts' AND FileSha256=UPPER(@Hash) AND Status=N'Applied') THROW 59600,N'Ledger conflict.',1;
  COMMIT; RETURN;
 END;
 IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId LIKE N'0060%') OR SCHEMA_ID(N'ComplaintRequests') IS NOT NULL
  THROW 59600,N'Migration or namespace collision.',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0059_stage2_route_dispatch_notifications' AND FileSha256=N'9CF9E0526E0142AB1B6A268ACF23C6DEF1B0B46781FAC03BC4338F9D7D2BCD21' AND Status=N'Applied')
  THROW 59600,N'Exact preceding 0059 prerequisite unavailable.',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0033_stage2_complaints' AND FileSha256=N'21B329A75AEC24D9D83204ABAAE59BD5E6EF5A865348C380064DC2DCDA0755DA' AND Status=N'Applied')
  OR NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0036_stage2_transactional_login' AND FileSha256=N'98816A77160064B6B86579A25C9A6B41962197C60E38FE32FE2BAC58A7C02FDC' AND Status=N'Applied')
  THROW 59600,N'Exact complaint or original-login prerequisite unavailable.',1;
 IF ISNULL(HASHBYTES('SHA2_256',CONVERT(varbinary(max),OBJECT_DEFINITION(OBJECT_ID(N'dbo.sp_Reclamos_Create')))),0x)<>0x19C259FE0E487D755C51AE253EB10BB32C2A4DF3DAB5329977B60A46D350781E
  THROW 59600,N'Previous complaint creation definition differs.',1;
 IF ISNULL(HASHBYTES('SHA2_256',CONVERT(varbinary(max),OBJECT_DEFINITION(OBJECT_ID(N'dbo.sp_Stage2_RequireOriginalLogin')))),0x)<>0x8C8515B2D2DD516A737A548EE7A03AB3E12D101BB8D6B27A1FBDC8846E31479C
  THROW 59600,N'Original-login guard differs.',1;
 IF OBJECT_ID(N'dbo.Reclamos',N'U') IS NULL OR OBJECT_ID(N'dbo.Stage2OriginalLogins',N'U') IS NULL
  OR OBJECT_ID(N'dbo.AuditoriaSistema',N'U') IS NULL OR COL_LENGTH(N'dbo.Reclamos',N'ReclamoId')<>4
  OR COL_LENGTH(N'dbo.Reclamos',N'UsuarioId')<>4 OR COL_LENGTH(N'dbo.Reclamos',N'Asunto')<>300
  OR COL_LENGTH(N'dbo.Reclamos',N'Descripcion')<>2000
  THROW 59600,N'Complaint schema unavailable.',1;

 EXEC(N'CREATE SCHEMA ComplaintRequests AUTHORIZATION dbo;');
 CREATE TABLE ComplaintRequests.Receipts(
  RequestId uniqueidentifier NOT NULL CONSTRAINT PK_ComplaintRequests PRIMARY KEY,
  OriginalLoginId uniqueidentifier NOT NULL CONSTRAINT FK_ComplaintRequests_Login REFERENCES dbo.Stage2OriginalLogins(LoginId),
  Actor int NOT NULL CONSTRAINT FK_ComplaintRequests_Actor REFERENCES dbo.Usuarios(UsuarioId),
  ProfileId int NOT NULL CONSTRAINT FK_ComplaintRequests_Profile REFERENCES dbo.Perfiles(PerfilId),
  SecurityStamp uniqueidentifier NOT NULL,
  BindingHash binary(32) NOT NULL,
  IssuedUtc datetime2(7) NOT NULL,
  ExpiresUtc datetime2(7) NOT NULL,
  PayloadHash binary(32) NOT NULL,
  ReclamoId int NOT NULL CONSTRAINT UQ_ComplaintRequests_Reclamo UNIQUE CONSTRAINT FK_ComplaintRequests_Reclamo REFERENCES dbo.Reclamos(ReclamoId),
  CreatedUtc datetime2(7) NOT NULL CONSTRAINT DF_ComplaintRequests_Created DEFAULT SYSUTCDATETIME(),
  CONSTRAINT CK_ComplaintRequests_Context CHECK(RequestId<>'00000000-0000-0000-0000-000000000000' AND SecurityStamp<>'00000000-0000-0000-0000-000000000000' AND ExpiresUtc>IssuedUtc)
 );
 CREATE INDEX IX_ComplaintRequests_Owner ON ComplaintRequests.Receipts(OriginalLoginId,Actor,ReclamoId);
 EXEC(N'CREATE TRIGGER ComplaintRequests.ImmutableReceipts ON ComplaintRequests.Receipts INSTEAD OF UPDATE,DELETE AS BEGIN SET NOCOUNT ON;THROW 59603,N''Immutable complaint receipt.'',1;END;');
 EXEC(N'ALTER PROCEDURE dbo.sp_Reclamos_Create
 @LoginId uniqueidentifier,@UsuarioId int,@PerfilId int,@SecurityStamp uniqueidentifier,
 @BindingHash binary(32),@IssuedUtc datetime2(7),@ExpiresUtc datetime2(7),
 @RequestId uniqueidentifier,@ClienteUsuarioId int,@Asunto nvarchar(150),@Descripcion nvarchar(1000),
 @FacturaId int=NULL,@PedidoId int=NULL,@Categoria nvarchar(40)=N''Otro'',@Prioridad nvarchar(20)=N''Media''
 AS
 BEGIN
  SET NOCOUNT ON;SET XACT_ABORT ON;
  IF @RequestId IS NULL OR @RequestId=''00000000-0000-0000-0000-000000000000'' THROW 59601,N''Original request required.'',1;
  SET @Asunto=NULLIF(LTRIM(RTRIM(@Asunto)),N'''');
  SET @Descripcion=NULLIF(LTRIM(RTRIM(@Descripcion)),N'''');
  SET @FacturaId=NULLIF(@FacturaId,0);SET @PedidoId=NULLIF(@PedidoId,0);
  SET @Categoria=ISNULL(@Categoria,N''Otro'');SET @Prioridad=ISNULL(@Prioridad,N''Media'');
  IF @Asunto IS NULL THROW 53111,N''El asunto del reclamo es obligatorio.'',1;
  IF @Descripcion IS NULL THROW 53112,N''La descripcion del reclamo es obligatoria.'',1;
  IF @Categoria NOT IN(N''Producto'',N''Entrega'',N''Facturación'',N''Servicio'',N''Otro'') THROW 53113,N''Categoria de reclamo invalida.'',1;
  IF @Prioridad NOT IN(N''Baja'',N''Media'',N''Alta'') THROW 53114,N''Prioridad de reclamo invalida.'',1;
  DECLARE @Payload nvarchar(max)=(SELECT @ClienteUsuarioId AS customer,@FacturaId AS invoice,@PedidoId AS [order],@Asunto AS subject,@Descripcion AS description,@Categoria AS category,@Prioridad AS priority FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER);
  DECLARE @PayloadHash binary(32)=HASHBYTES(''SHA2_256'',CONVERT(varbinary(max),@Payload));
  BEGIN TRY
   BEGIN TRANSACTION;
   EXEC dbo.sp_Stage2_RequireOriginalLogin @LoginId,@UsuarioId,@PerfilId,@SecurityStamp,@BindingHash,@IssuedUtc,@ExpiresUtc,N''RECLAMOS_GESTIONAR'';
   DECLARE @ExistingId int;
   SELECT @ExistingId=ReclamoId FROM ComplaintRequests.Receipts WITH(UPDLOCK,HOLDLOCK) WHERE RequestId=@RequestId;
   IF @ExistingId IS NOT NULL
   BEGIN
    IF NOT EXISTS(SELECT 1 FROM ComplaintRequests.Receipts WHERE RequestId=@RequestId AND OriginalLoginId=@LoginId AND Actor=@UsuarioId
     AND ProfileId=@PerfilId AND SecurityStamp=@SecurityStamp AND BindingHash=@BindingHash AND IssuedUtc=@IssuedUtc AND ExpiresUtc=@ExpiresUtc AND PayloadHash=@PayloadHash)
     THROW 59602,N''Original request context conflict.'',1;
    IF NOT EXISTS(SELECT 1 FROM dbo.Reclamos WITH(HOLDLOCK) WHERE ReclamoId=@ExistingId AND RegistradoPorUsuarioId=@UsuarioId AND UsuarioId=@ClienteUsuarioId)
     THROW 59603,N''Committed complaint evidence unavailable.'',1;
    EXEC dbo.sp_Stage2_RequireOriginalLogin @LoginId,@UsuarioId,@PerfilId,@SecurityStamp,@BindingHash,@IssuedUtc,@ExpiresUtc,N''RECLAMOS_GESTIONAR'';
    COMMIT;SELECT @ExistingId AS ReclamoId;RETURN;
   END;
   IF NOT EXISTS(SELECT 1 FROM dbo.Usuarios u WITH(UPDLOCK,HOLDLOCK) JOIN dbo.Perfiles p WITH(UPDLOCK,HOLDLOCK) ON p.PerfilId=u.PerfilId
     WHERE u.UsuarioId=@ClienteUsuarioId AND u.Activo=1 AND p.Activo=1 AND p.Nombre=N''Cliente'') THROW 53110,N''El cliente indicado no existe.'',1;
   IF @FacturaId IS NOT NULL AND NOT EXISTS(SELECT 1 FROM dbo.Facturas WITH(UPDLOCK,HOLDLOCK) WHERE FacturaId=@FacturaId AND UsuarioId=@ClienteUsuarioId)
    THROW 53115,N''La factura indicada no existe o no pertenece a ese cliente.'',1;
   IF @PedidoId IS NOT NULL AND NOT EXISTS(SELECT 1 FROM dbo.Pedidos WITH(UPDLOCK,HOLDLOCK) WHERE PedidoId=@PedidoId AND UsuarioId=@ClienteUsuarioId)
    THROW 53116,N''El pedido indicado no existe o no pertenece a ese cliente.'',1;
   IF @FacturaId IS NOT NULL AND @PedidoId IS NOT NULL AND NOT EXISTS(SELECT 1 FROM dbo.Facturas WITH(UPDLOCK,HOLDLOCK) WHERE FacturaId=@FacturaId AND PedidoId=@PedidoId AND UsuarioId=@ClienteUsuarioId)
    THROW 56503,N''Factura y pedido no corresponden al mismo recurso.'',1;
   DECLARE @Name nvarchar(150),@Email nvarchar(256),@Role nvarchar(100);
   SELECT @Name=u.NombreCompleto,@Email=u.Correo,@Role=p.Nombre FROM dbo.Usuarios u JOIN dbo.Perfiles p ON p.PerfilId=u.PerfilId WHERE u.UsuarioId=@UsuarioId;
   INSERT dbo.Reclamos(UsuarioId,FacturaId,PedidoId,Asunto,Descripcion,Categoria,Prioridad,Estado,RegistradoPorUsuarioId,RegistradoPorNombre)
   VALUES(@ClienteUsuarioId,@FacturaId,@PedidoId,@Asunto,@Descripcion,@Categoria,@Prioridad,N''Abierto'',@UsuarioId,@Name);
   DECLARE @Id int=CONVERT(int,SCOPE_IDENTITY());
   INSERT ComplaintRequests.Receipts(RequestId,OriginalLoginId,Actor,ProfileId,SecurityStamp,BindingHash,IssuedUtc,ExpiresUtc,PayloadHash,ReclamoId)
   VALUES(@RequestId,@LoginId,@UsuarioId,@PerfilId,@SecurityStamp,@BindingHash,@IssuedUtc,@ExpiresUtc,@PayloadHash,@Id);
   INSERT dbo.AuditoriaSistema(UsuarioId,UsuarioNombre,UsuarioCorreo,Rol,Accion,Modulo,Descripcion)
   VALUES(@UsuarioId,@Name,@Email,@Role,N''CREAR_RECLAMO'',N''Reclamos'',N''Transactional customer-service event'');
   EXEC dbo.sp_Stage2_RequireOriginalLogin @LoginId,@UsuarioId,@PerfilId,@SecurityStamp,@BindingHash,@IssuedUtc,@ExpiresUtc,N''RECLAMOS_GESTIONAR'';
   COMMIT;SELECT @Id AS ReclamoId;
  END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK;THROW;END CATCH;
 END;');
 INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes)
 VALUES(N'0060_stage2_complaint_request_receipts',N'0060_stage2_complaint_request_receipts.sql',UPPER(@Hash),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),N'Immutable original-login complaint UUID receipt and normalized server payload hash; one complaint and audit atomically. No historical rewrite, grants or assignments.');
 COMMIT;
END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK;THROW;END CATCH;
