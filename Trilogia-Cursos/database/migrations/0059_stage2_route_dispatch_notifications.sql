-- Stage 2: durable route-dispatch event and separately claimed SMTP notification.
-- Dispatch remains a logistics state change; it is not physical delivery.
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF SESSIONPROPERTY('QUOTED_IDENTIFIER')<>1 OR SESSIONPROPERTY('ANSI_NULLS')<>1 THROW 59500,N'SQL options unavailable.',1;
DECLARE @Hash nvarchar(128)=N'$(MigrationSha256)';
IF LEN(@Hash)<>64 OR @Hash LIKE N'%[^0-9a-fA-F]%' THROW 59500,N'Invalid migration hash.',1;
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL THROW 59500,N'Ledger unavailable.',1;
BEGIN TRY
 BEGIN TRAN;
 IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WITH(UPDLOCK,HOLDLOCK) WHERE MigrationId=N'0059_stage2_route_dispatch_notifications')
 BEGIN
  IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0059_stage2_route_dispatch_notifications' AND FileSha256=UPPER(@Hash) AND Status=N'Applied') THROW 59500,N'Ledger conflict.',1;
  COMMIT; RETURN;
 END;
 IF SCHEMA_ID(N'RouteDispatch') IS NOT NULL OR OBJECT_ID(N'dbo.sp_Stage2_RouteOperation',N'P') IS NULL
  THROW 59500,N'Namespace or route-operation contract collision.',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0058_stage2_delivery_notifications' AND FileSha256=N'30DD0D7ACEDE56D067CA6A583C02692D071619017987A5D1E0ECF6B98FF38D63' AND Status=N'Applied')
  THROW 59500,N'0058 is not applied with the explicitly supplied installed hash.',1;
 IF ISNULL(HASHBYTES('SHA2_256',CONVERT(varbinary(max),OBJECT_DEFINITION(OBJECT_ID(N'dbo.sp_Rutas_Dispatch')))),0x)<>0xEC93FA51F9F94E578D774CE8A4B2F54875872097BC73F3D6CD74633EFC63D00E
  THROW 59500,N'Historical route-dispatch definition differs.',1;
 IF ISNULL(HASHBYTES('SHA2_256',CONVERT(varbinary(max),OBJECT_DEFINITION(OBJECT_ID(N'dbo.sp_Stage2_RequireOriginalLogin')))),0x)<>0x8C8515B2D2DD516A737A548EE7A03AB3E12D101BB8D6B27A1FBDC8846E31479C
  THROW 59500,N'Original-login guard differs.',1;
 IF ISNULL(HASHBYTES('SHA2_256',CONVERT(varbinary(max),OBJECT_DEFINITION(OBJECT_ID(N'dbo.sp_Stage2_RouteOperation')))),0x)<>0x7F52ACA7DFDCE89DB55DDC35F2519D7CD233A091115891371FD49FE1D32D0888
  THROW 59500,N'Route-operation wrapper differs.',1;
 IF COL_LENGTH(N'dbo.RutaPedidos',N'RutaPedidoId')<>4 OR COL_LENGTH(N'dbo.RutaPedidos',N'RutaId')<>4 OR COL_LENGTH(N'dbo.RutaPedidos',N'PedidoId')<>4 OR COL_LENGTH(N'dbo.RutaPedidos',N'EstadoEntrega') IS NULL
  OR COL_LENGTH(N'dbo.Pedidos',N'UsuarioId')<>4 OR COL_LENGTH(N'dbo.Usuarios',N'NombreCompleto') IS NULL OR COL_LENGTH(N'dbo.Usuarios',N'Correo') IS NULL
  OR OBJECT_ID(N'dbo.AuditoriaSistema',N'U') IS NULL OR OBJECT_ID(N'dbo.Facturas',N'U') IS NULL OR OBJECT_ID(N'dbo.Stage2PhysicalDeliveries',N'U') IS NULL
  THROW 59500,N'Required logistics schema unavailable.',1;

 EXEC(N'CREATE SCHEMA RouteDispatch AUTHORIZATION dbo;');
 EXEC(N'
 CREATE TABLE RouteDispatch.Dispatches(
  DispatchId uniqueidentifier NOT NULL CONSTRAINT PK_RouteDispatch_Dispatches PRIMARY KEY,
  RutaId int NOT NULL CONSTRAINT UQ_RouteDispatch_Dispatch_Route UNIQUE REFERENCES dbo.Rutas(RutaId),
  OriginalLoginId uniqueidentifier NOT NULL REFERENCES dbo.Stage2OriginalLogins(LoginId),
  Actor int NOT NULL REFERENCES dbo.Usuarios(UsuarioId),
  CreatedUtc datetime2(7) NOT NULL CONSTRAINT DF_RouteDispatch_Created DEFAULT SYSUTCDATETIME(),
  CONSTRAINT CK_RouteDispatch_DispatchId CHECK(DispatchId<>''00000000-0000-0000-0000-000000000000'')
 );
 CREATE INDEX IX_RouteDispatch_Owner ON RouteDispatch.Dispatches(OriginalLoginId,Actor,RutaId,CreatedUtc);
 CREATE TABLE RouteDispatch.Items(
  EventId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_RouteDispatch_Items PRIMARY KEY,
  EventGuid uniqueidentifier NOT NULL CONSTRAINT UQ_RouteDispatch_EventGuid UNIQUE,
  DispatchId uniqueidentifier NOT NULL REFERENCES RouteDispatch.Dispatches(DispatchId),
  RutaPedidoId int NOT NULL CONSTRAINT UQ_RouteDispatch_RouteOrder UNIQUE REFERENCES dbo.RutaPedidos(RutaPedidoId),
  PedidoId int NOT NULL REFERENCES dbo.Pedidos(PedidoId),
  CustomerId int NOT NULL REFERENCES dbo.Usuarios(UsuarioId),
  CustomerName nvarchar(150) NOT NULL,
  CustomerEmail nvarchar(150) NULL,
  NotifyEligible bit NOT NULL,
  RecordedUtc datetime2(7) NOT NULL CONSTRAINT DF_RouteDispatch_ItemRecorded DEFAULT SYSUTCDATETIME(),
  CONSTRAINT CK_RouteDispatch_EventGuid CHECK(EventGuid<>''00000000-0000-0000-0000-000000000000'')
 );
 CREATE INDEX IX_RouteDispatch_Items_Dispatch ON RouteDispatch.Items(DispatchId,EventId);
 CREATE TABLE RouteDispatch.Requests(
  OperationId uniqueidentifier NOT NULL CONSTRAINT PK_RouteDispatch_Requests PRIMARY KEY,
  EventId bigint NOT NULL CONSTRAINT UQ_RouteDispatch_Request_Event UNIQUE REFERENCES RouteDispatch.Items(EventId),
  OriginalLoginId uniqueidentifier NOT NULL REFERENCES dbo.Stage2OriginalLogins(LoginId),
  Actor int NOT NULL REFERENCES dbo.Usuarios(UsuarioId),
  SourceHash binary(32) NOT NULL,
  CreatedUtc datetime2(7) NOT NULL CONSTRAINT DF_RouteDispatch_RequestCreated DEFAULT SYSUTCDATETIME(),
  CONSTRAINT CK_RouteDispatch_OperationId CHECK(OperationId<>''00000000-0000-0000-0000-000000000000'')
 );
 CREATE TABLE RouteDispatch.Attempts(
  OperationId uniqueidentifier NOT NULL CONSTRAINT PK_RouteDispatch_Attempts PRIMARY KEY REFERENCES RouteDispatch.Requests(OperationId),
  State nvarchar(20) NOT NULL,ClaimHash binary(32) NULL,AttemptedUtc datetime2(7) NULL,CompletedUtc datetime2(7) NULL,
  CONSTRAINT CK_RouteDispatch_AttemptState CHECK(
   (State=N''Pending'' AND ClaimHash IS NULL AND AttemptedUtc IS NULL AND CompletedUtc IS NULL) OR
   (State=N''Sending'' AND ClaimHash IS NOT NULL AND AttemptedUtc IS NOT NULL AND CompletedUtc IS NULL) OR
   (State IN(N''SmtpAccepted'',N''Unknown'') AND ClaimHash IS NOT NULL AND AttemptedUtc IS NOT NULL AND CompletedUtc IS NOT NULL))
 );
 CREATE TABLE RouteDispatch.Events(
  AuditId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_RouteDispatch_Events PRIMARY KEY,
  OperationId uniqueidentifier NOT NULL REFERENCES RouteDispatch.Requests(OperationId),
  State nvarchar(20) NOT NULL,CreatedUtc datetime2(7) NOT NULL CONSTRAINT DF_RouteDispatch_EventCreated DEFAULT SYSUTCDATETIME()
 );');
 EXEC(N'CREATE TRIGGER RouteDispatch.ImmutableDispatches ON RouteDispatch.Dispatches AFTER UPDATE,DELETE AS BEGIN SET NOCOUNT ON; THROW 59503,N''Dispatch ownership is immutable.'',1; END;');
 EXEC(N'CREATE TRIGGER RouteDispatch.ImmutableItems ON RouteDispatch.Items AFTER UPDATE,DELETE AS BEGIN SET NOCOUNT ON; THROW 59503,N''Dispatch snapshots are immutable.'',1; END;');
 EXEC(N'CREATE TRIGGER RouteDispatch.ImmutableRequests ON RouteDispatch.Requests AFTER UPDATE,DELETE AS BEGIN SET NOCOUNT ON; THROW 59503,N''Notification ownership is immutable.'',1; END;');
 EXEC(N'CREATE TRIGGER RouteDispatch.ImmutableEvents ON RouteDispatch.Events AFTER UPDATE,DELETE AS BEGIN SET NOCOUNT ON; THROW 59503,N''Notification audit is append-only.'',1; END;');
 EXEC(N'CREATE TRIGGER RouteDispatch.AttemptTransitions ON RouteDispatch.Attempts AFTER UPDATE,DELETE AS BEGIN SET NOCOUNT ON;
  IF EXISTS(SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.OperationId=d.OperationId WHERE i.OperationId IS NULL OR NOT((d.State=N''Pending'' AND i.State=N''Sending'') OR (d.State=N''Sending'' AND i.State IN(N''SmtpAccepted'',N''Unknown'') AND d.ClaimHash=i.ClaimHash AND d.AttemptedUtc=i.AttemptedUtc))) THROW 59503,N''Invalid notification transition.'',1;
 END;');

 -- Replace the old wrapper body, retaining every supported non-dispatch branch;
 -- Dispatch is deliberately denied here so callers cannot bypass the UUID owner.
 EXEC(N'ALTER PROCEDURE dbo.sp_Stage2_RouteOperation
 @Operation nvarchar(20),@Actor int,@RutaId int,@PedidoId int=NULL,@UsuarioId int=NULL,@UsuarioNombre nvarchar(150)=NULL,@ItemsJson nvarchar(max)=NULL,@ExcluirRutaPedidoId int=NULL,@MotivoFallo nvarchar(300)=NULL,@Motivo nvarchar(300)=NULL
 AS BEGIN
 SET NOCOUNT ON;SET XACT_ABORT ON;
 IF @@TRANCOUNT=0 OR (@UsuarioId IS NOT NULL AND @Actor<>@UsuarioId) THROW 57500,N''Falta contexto original.'',1;
 IF @Operation=N''Dispatch'' THROW 59504,N''Use RouteDispatch.Dispatch.'',1;
 IF @Operation NOT IN(N''AddOrder'',N''RemoveOrder'',N''Sequence'',N''SaveSequence'',N''Recalculate'',N''Cancel'') THROW 57502,N''Operacion no acreditada.'',1;
 DECLARE @State nvarchar(30),@Driver int;
 SELECT @State=Estado,@Driver=ChoferUsuarioId FROM dbo.Rutas WITH(UPDLOCK,HOLDLOCK) WHERE RutaId=@RutaId;
 IF @State IS NULL THROW 57502,N''Ruta no disponible.'',1;
 IF @Operation IN(N''AddOrder'',N''RemoveOrder'',N''Cancel'') AND @State<>N''Planificada'' THROW 57502,N''Cambios posteriores al despacho requieren conciliacion.'',1;
 IF @Operation=N''AddOrder'' BEGIN
  IF NOT EXISTS(SELECT 1 FROM dbo.Usuarios u WITH(UPDLOCK,HOLDLOCK) JOIN dbo.Perfiles p ON p.PerfilId=u.PerfilId WHERE u.UsuarioId=@Driver AND u.Activo=1 AND p.Activo=1 AND p.Nombre IN(N''Chofer'',N''Administrador'')) THROW 57503,N''Asignacion logistica no vigente.'',1;
  IF EXISTS(SELECT 1 FROM (SELECT @PedidoId Id) selected LEFT JOIN dbo.Pedidos p WITH(UPDLOCK,HOLDLOCK) ON p.PedidoId=selected.Id WHERE p.PedidoId IS NULL OR p.Estado<>N''Facturado'' OR p.InventarioDescontado<>1 OR NOT EXISTS(SELECT 1 FROM dbo.Facturas f WITH(UPDLOCK,HOLDLOCK) WHERE f.PedidoId=p.PedidoId AND f.Estado=N''Generada'') OR EXISTS(SELECT 1 FROM dbo.Stage2PhysicalDeliveries pd WHERE pd.PedidoId=p.PedidoId) OR EXISTS(SELECT 1 FROM dbo.RutaPedidos failed WITH(UPDLOCK,HOLDLOCK) WHERE failed.PedidoId=p.PedidoId AND failed.EstadoEntrega=N''Fallido'')) THROW 57501,N''Pedido no despachable; requiere factura vigente y conciliacion de devoluciones si corresponde.'',1;
 END;
 SELECT @UsuarioNombre=NombreCompleto FROM dbo.Usuarios WHERE UsuarioId=@Actor;
 IF @Operation=N''AddOrder'' EXEC dbo.sp_Rutas_AddOrder @RutaId=@RutaId,@PedidoId=@PedidoId;
 ELSE IF @Operation=N''RemoveOrder'' EXEC dbo.sp_Rutas_RemoveOrder @RutaId=@RutaId,@PedidoId=@PedidoId;
 ELSE IF @Operation=N''Sequence'' EXEC dbo.sp_Rutas_Secuenciar @RutaId=@RutaId;
 ELSE IF @Operation=N''SaveSequence'' EXEC dbo.sp_Rutas_GuardarSecuencia @RutaId=@RutaId,@ItemsJson=@ItemsJson;
 ELSE IF @Operation=N''Recalculate'' EXEC dbo.sp_Rutas_Recalcular @RutaId=@RutaId,@ExcluirRutaPedidoId=@ExcluirRutaPedidoId,@MotivoFallo=@MotivoFallo;
 ELSE IF @Operation=N''Cancel'' EXEC dbo.sp_Rutas_Cancel @RutaId=@RutaId,@Motivo=@Motivo,@UsuarioId=@Actor,@UsuarioNombre=@UsuarioNombre;
 INSERT dbo.AuditoriaSistema(UsuarioId,UsuarioNombre,UsuarioCorreo,Rol,Accion,Modulo,Descripcion)
 SELECT @Actor,u.NombreCompleto,u.Correo,p.Nombre,@Operation,N''Rutas'',CONCAT(N''Ruta #'',@RutaId,N''; operacion logistica sin acreditar entrega por despacho.'') FROM dbo.Usuarios u JOIN dbo.Perfiles p ON p.PerfilId=u.PerfilId WHERE u.UsuarioId=@Actor;
 END;');

 EXEC(N'CREATE PROCEDURE RouteDispatch.Dispatch
 @LoginId uniqueidentifier,@UsuarioId int,@PerfilId int,@SecurityStamp uniqueidentifier,@BindingHash binary(32),@IssuedUtc datetime2(7),@ExpiresUtc datetime2(7),@RutaId int,@DispatchId uniqueidentifier,@UsuarioNombre nvarchar(150)=NULL
 AS BEGIN SET NOCOUNT ON;SET XACT_ABORT ON;
 IF @@TRANCOUNT=0 THROW 59501,N''Dispatch requires the caller transaction.'',1;
 IF @DispatchId IS NULL OR @DispatchId=''00000000-0000-0000-0000-000000000000'' OR @RutaId<=0 THROW 59501,N''Invalid dispatch selector.'',1;
 BEGIN TRY
  EXEC dbo.sp_Stage2_RequireOriginalLogin @LoginId,@UsuarioId,@PerfilId,@SecurityStamp,@BindingHash,@IssuedUtc,@ExpiresUtc,N''RUTAS_GESTIONAR'';
  DECLARE @Resource nvarchar(255)=CONCAT(N''RouteDispatch:'',CONVERT(nvarchar(36),@DispatchId)),@Lock int;
  EXEC @Lock=sys.sp_getapplock @Resource=@Resource,@LockMode=N''Exclusive'',@LockOwner=N''Transaction'',@LockTimeout=10000;
  IF @Lock<0 THROW 59501,N''Dispatch serialization unavailable.'',1;
  DECLARE @State nvarchar(30),@Driver int,@StoredRoute int,@StoredLogin uniqueidentifier,@StoredActor int;
  SELECT @State=Estado,@Driver=ChoferUsuarioId FROM dbo.Rutas WITH(UPDLOCK,HOLDLOCK) WHERE RutaId=@RutaId;
  IF @State IS NULL THROW 59502,N''Unavailable.'',1;
  SELECT @StoredRoute=RutaId,@StoredLogin=OriginalLoginId,@StoredActor=Actor FROM RouteDispatch.Dispatches WITH(UPDLOCK,HOLDLOCK) WHERE DispatchId=@DispatchId;
  IF @StoredRoute IS NOT NULL
  BEGIN
   IF @StoredRoute<>@RutaId OR @StoredLogin<>@LoginId OR @StoredActor<>@UsuarioId THROW 59502,N''Unavailable.'',1;
   SELECT @DispatchId AS DispatchId,i.EventId,i.EventGuid,i.RutaPedidoId,i.PedidoId,i.NotifyEligible FROM RouteDispatch.Items i WHERE i.DispatchId=@DispatchId ORDER BY i.EventId;
   RETURN;
  END;
  IF @State<>N''Planificada'' THROW 59502,N''Unavailable.'',1;
  IF NOT EXISTS(SELECT 1 FROM dbo.Usuarios u WITH(UPDLOCK,HOLDLOCK) JOIN dbo.Perfiles p ON p.PerfilId=u.PerfilId WHERE u.UsuarioId=@Driver AND u.Activo=1 AND p.Activo=1 AND p.Nombre IN(N''Chofer'',N''Administrador'')) THROW 57503,N''Asignacion logistica no vigente.'',1;
  DECLARE @Selected TABLE(RutaPedidoId int NOT NULL PRIMARY KEY,PedidoId int NOT NULL UNIQUE);
  INSERT @Selected SELECT RutaPedidoId,PedidoId FROM dbo.RutaPedidos WITH(UPDLOCK,HOLDLOCK) WHERE RutaId=@RutaId;
  IF NOT EXISTS(SELECT 1 FROM @Selected) THROW 59502,N''Unavailable.'',1;
  -- Prevent the historic procedure from returning pre-existing EnRuta rows that it did not transition.
  IF EXISTS(SELECT 1 FROM dbo.RutaPedidos rp WITH(UPDLOCK,HOLDLOCK) JOIN @Selected s ON s.RutaPedidoId=rp.RutaPedidoId WHERE rp.EstadoEntrega IS NULL OR rp.EstadoEntrega<>N''Pendiente'') THROW 59505,N''Route contains non-pending items.'',1;
  IF EXISTS(SELECT 1 FROM @Selected s LEFT JOIN dbo.Pedidos p WITH(UPDLOCK,HOLDLOCK) ON p.PedidoId=s.PedidoId WHERE p.PedidoId IS NULL OR p.Estado<>N''Facturado'' OR p.InventarioDescontado<>1 OR NOT EXISTS(SELECT 1 FROM dbo.Facturas f WITH(UPDLOCK,HOLDLOCK) WHERE f.PedidoId=p.PedidoId AND f.Estado=N''Generada'') OR EXISTS(SELECT 1 FROM dbo.Stage2PhysicalDeliveries pd WHERE pd.PedidoId=p.PedidoId) OR EXISTS(SELECT 1 FROM dbo.RutaPedidos failed WITH(UPDLOCK,HOLDLOCK) WHERE failed.PedidoId=p.PedidoId AND failed.EstadoEntrega=N''Fallido'')) THROW 57501,N''Pedido no despachable; requiere factura vigente y conciliacion de devoluciones si corresponde.'',1;
  DECLARE @Recipients TABLE(PedidoId int NOT NULL,ClienteNombre nvarchar(150) NULL,ClienteCorreo nvarchar(150) NULL);
  SELECT @UsuarioNombre=NombreCompleto FROM dbo.Usuarios WITH(UPDLOCK,HOLDLOCK) WHERE UsuarioId=@UsuarioId AND Activo=1 AND SecurityStamp=@SecurityStamp;
  IF @UsuarioNombre IS NULL THROW 59502,N''Unavailable.'',1;
  INSERT @Recipients EXEC dbo.sp_Rutas_Dispatch @RutaId=@RutaId,@UsuarioId=@UsuarioId,@UsuarioNombre=@UsuarioNombre;
  IF (SELECT COUNT(*) FROM @Recipients)<>(SELECT COUNT(*) FROM @Selected) OR EXISTS(SELECT 1 FROM @Recipients r LEFT JOIN @Selected s ON s.PedidoId=r.PedidoId WHERE s.PedidoId IS NULL) THROW 59505,N''Dispatch result did not match transitioned route items.'',1;
  INSERT RouteDispatch.Dispatches(DispatchId,RutaId,OriginalLoginId,Actor) VALUES(@DispatchId,@RutaId,@LoginId,@UsuarioId);
   INSERT RouteDispatch.Items(EventGuid,DispatchId,RutaPedidoId,PedidoId,CustomerId,CustomerName,CustomerEmail,NotifyEligible)
    SELECT NEWID(),@DispatchId,rp.RutaPedidoId,rp.PedidoId,p.UsuarioId,
     COALESCE(NULLIF(LTRIM(RTRIM(u.NombreCompleto)),N''''),N''Cliente''),NULLIF(LTRIM(RTRIM(u.Correo)),N''''),
     CONVERT(bit,CASE WHEN NULLIF(LTRIM(RTRIM(u.Correo)),N'''') IS NOT NULL AND LTRIM(RTRIM(u.Correo)) LIKE N''%_@_%._%'' THEN 1 ELSE 0 END)
   FROM @Selected s JOIN dbo.RutaPedidos rp WITH(UPDLOCK,HOLDLOCK) ON rp.RutaPedidoId=s.RutaPedidoId AND rp.RutaId=@RutaId AND rp.EstadoEntrega=N''EnRuta''
   JOIN dbo.Pedidos p WITH(HOLDLOCK) ON p.PedidoId=rp.PedidoId JOIN dbo.Usuarios u WITH(HOLDLOCK) ON u.UsuarioId=p.UsuarioId
   JOIN @Recipients r ON r.PedidoId=rp.PedidoId;
  IF @@ROWCOUNT<>(SELECT COUNT(*) FROM @Selected) THROW 59505,N''Dispatch item snapshot incomplete.'',1;
  IF EXISTS(SELECT 1 FROM RouteDispatch.Items WHERE DispatchId=@DispatchId AND EventGuid=@DispatchId) THROW 59505,N''Dispatch and event identifiers must differ.'',1;
  INSERT dbo.AuditoriaSistema(UsuarioId,UsuarioNombre,UsuarioCorreo,Rol,Accion,Modulo,Descripcion)
   SELECT @UsuarioId,u.NombreCompleto,u.Correo,p.Nombre,N''Dispatch'',N''Rutas'',CONCAT(N''Ruta #'',@RutaId,N'' despachada; DispatchId '',CONVERT(nvarchar(36),@DispatchId),N''; sin acreditar entrega fisica.'') FROM dbo.Usuarios u JOIN dbo.Perfiles p ON p.PerfilId=u.PerfilId WHERE u.UsuarioId=@UsuarioId;
  EXEC dbo.sp_Stage2_RequireOriginalLogin @LoginId,@UsuarioId,@PerfilId,@SecurityStamp,@BindingHash,@IssuedUtc,@ExpiresUtc,N''RUTAS_GESTIONAR'';
  SELECT @DispatchId AS DispatchId,i.EventId,i.EventGuid,i.RutaPedidoId,i.PedidoId,i.NotifyEligible FROM RouteDispatch.Items i WHERE i.DispatchId=@DispatchId ORDER BY i.EventId;
 END TRY BEGIN CATCH THROW; END CATCH;
 END;');

 EXEC(N'CREATE PROCEDURE RouteDispatch.GetOwned
 @LoginId uniqueidentifier,@UsuarioId int,@PerfilId int,@SecurityStamp uniqueidentifier,@BindingHash binary(32),@IssuedUtc datetime2(7),@ExpiresUtc datetime2(7),@RutaId int,@DispatchId uniqueidentifier
 AS BEGIN SET NOCOUNT ON;SET XACT_ABORT ON;
 BEGIN TRY BEGIN TRAN;
 EXEC dbo.sp_Stage2_RequireOriginalLogin @LoginId,@UsuarioId,@PerfilId,@SecurityStamp,@BindingHash,@IssuedUtc,@ExpiresUtc,N''RUTAS_GESTIONAR'';
 IF NOT EXISTS(SELECT 1 FROM RouteDispatch.Dispatches WITH(UPDLOCK,HOLDLOCK) WHERE DispatchId=@DispatchId AND RutaId=@RutaId AND OriginalLoginId=@LoginId AND Actor=@UsuarioId) THROW 59502,N''Unavailable.'',1;
 SELECT @DispatchId AS DispatchId,i.EventId,i.EventGuid,i.RutaPedidoId,i.PedidoId,i.NotifyEligible FROM RouteDispatch.Items i WHERE i.DispatchId=@DispatchId ORDER BY i.EventId;
 COMMIT; END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH; END;');

 EXEC(N'CREATE PROCEDURE RouteDispatch.SnapshotHash @EventId bigint,@Hash binary(32) OUTPUT AS BEGIN SET NOCOUNT ON;
 DECLARE @Json nvarchar(max)=(SELECT i.EventId,i.EventGuid,i.DispatchId,i.RutaPedidoId,i.PedidoId,i.CustomerId,i.CustomerName,i.CustomerEmail,i.NotifyEligible,i.RecordedUtc FROM RouteDispatch.Items i WITH(HOLDLOCK) WHERE i.EventId=@EventId FOR JSON PATH,WITHOUT_ARRAY_WRAPPER,INCLUDE_NULL_VALUES);
 IF @Json IS NULL THROW 59502,N''Unavailable.'',1; SET @Hash=HASHBYTES(''SHA2_256'',CONVERT(varbinary(max),@Json)); END;');

 EXEC(N'CREATE PROCEDURE RouteDispatch.Request
 @LoginId uniqueidentifier,@UsuarioId int,@PerfilId int,@SecurityStamp uniqueidentifier,@BindingHash binary(32),@IssuedUtc datetime2(7),@ExpiresUtc datetime2(7),@RutaId int,@DispatchId uniqueidentifier,@EventId bigint,@EventGuid uniqueidentifier,@OperationId uniqueidentifier
 AS BEGIN SET NOCOUNT ON;SET XACT_ABORT ON;
 IF @OperationId IS NULL OR @OperationId=''00000000-0000-0000-0000-000000000000'' OR @EventGuid IS NULL OR @EventGuid=''00000000-0000-0000-0000-000000000000'' OR @OperationId=@EventGuid OR @OperationId=@DispatchId THROW 59501,N''Invalid notification selector.'',1;
 BEGIN TRY BEGIN TRAN;
 EXEC dbo.sp_Stage2_RequireOriginalLogin @LoginId,@UsuarioId,@PerfilId,@SecurityStamp,@BindingHash,@IssuedUtc,@ExpiresUtc,N''RUTAS_GESTIONAR'';
 IF NOT EXISTS(SELECT 1 FROM RouteDispatch.Dispatches WITH(UPDLOCK,HOLDLOCK) WHERE DispatchId=@DispatchId AND RutaId=@RutaId AND OriginalLoginId=@LoginId AND Actor=@UsuarioId) THROW 59502,N''Unavailable.'',1;
 DECLARE @Eligible bit,@StoredGuid uniqueidentifier,@Source binary(32);
 SELECT @Eligible=NotifyEligible,@StoredGuid=EventGuid FROM RouteDispatch.Items WITH(UPDLOCK,HOLDLOCK) WHERE EventId=@EventId AND DispatchId=@DispatchId;
 IF @StoredGuid IS NULL OR @StoredGuid<>@EventGuid THROW 59502,N''Unavailable.'',1;
 IF @Eligible=0 BEGIN COMMIT; SELECT CONVERT(uniqueidentifier,''00000000-0000-0000-0000-000000000000'') OperationId,@EventGuid EventGuid,CONVERT(nvarchar(20),N''NoRecipient'') State; RETURN; END;
 EXEC RouteDispatch.SnapshotHash @EventId,@Source OUTPUT;
 IF EXISTS(SELECT 1 FROM RouteDispatch.Requests WITH(UPDLOCK,HOLDLOCK) WHERE OperationId=@OperationId)
 BEGIN
  IF NOT EXISTS(SELECT 1 FROM RouteDispatch.Requests r JOIN RouteDispatch.Items i ON i.EventId=r.EventId WHERE r.OperationId=@OperationId AND r.OriginalLoginId=@LoginId AND r.Actor=@UsuarioId AND r.EventId=@EventId AND i.DispatchId=@DispatchId AND r.SourceHash=@Source) THROW 59502,N''Unavailable.'',1;
 END ELSE BEGIN
  IF EXISTS(SELECT 1 FROM RouteDispatch.Requests WITH(UPDLOCK,HOLDLOCK) WHERE EventId=@EventId) THROW 59502,N''Notification already requested.'',1;
  INSERT RouteDispatch.Requests(OperationId,EventId,OriginalLoginId,Actor,SourceHash) VALUES(@OperationId,@EventId,@LoginId,@UsuarioId,@Source);
  INSERT RouteDispatch.Attempts(OperationId,State) VALUES(@OperationId,N''Pending'');
  INSERT RouteDispatch.Events(OperationId,State) VALUES(@OperationId,N''Pending'');
 END;
 SELECT @OperationId OperationId,@EventGuid EventGuid,a.State FROM RouteDispatch.Attempts a WHERE a.OperationId=@OperationId;
 COMMIT; END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH; END;');

 EXEC(N'CREATE PROCEDURE RouteDispatch.Claim
 @LoginId uniqueidentifier,@UsuarioId int,@PerfilId int,@SecurityStamp uniqueidentifier,@BindingHash binary(32),@IssuedUtc datetime2(7),@ExpiresUtc datetime2(7),@RutaId int,@DispatchId uniqueidentifier,@EventId bigint,@EventGuid uniqueidentifier,@OperationId uniqueidentifier,@ClaimToken binary(32)
 AS BEGIN SET NOCOUNT ON;SET XACT_ABORT ON;
 IF @@TRANCOUNT<>0 THROW 59501,N''Claim requires its own transaction.'',1;
 IF @ClaimToken IS NULL OR @ClaimToken=0x0000000000000000000000000000000000000000000000000000000000000000 THROW 59501,N''Invalid claim.'',1;
 BEGIN TRY BEGIN TRAN;
 EXEC dbo.sp_Stage2_RequireOriginalLogin @LoginId,@UsuarioId,@PerfilId,@SecurityStamp,@BindingHash,@IssuedUtc,@ExpiresUtc,N''RUTAS_GESTIONAR'';
 IF NOT EXISTS(SELECT 1 FROM RouteDispatch.Dispatches WITH(UPDLOCK,HOLDLOCK) WHERE DispatchId=@DispatchId AND RutaId=@RutaId AND OriginalLoginId=@LoginId AND Actor=@UsuarioId) THROW 59502,N''Unavailable.'',1;
 DECLARE @Guid uniqueidentifier,@Eligible bit,@Source binary(32),@Expected binary(32),@State nvarchar(20),@Attempted datetime2(7),@ShouldSend bit=0;
 SELECT @Guid=EventGuid,@Eligible=NotifyEligible FROM RouteDispatch.Items WITH(UPDLOCK,HOLDLOCK) WHERE EventId=@EventId AND DispatchId=@DispatchId;
 IF @Guid IS NULL OR @Guid<>@EventGuid OR @Eligible<>1 THROW 59502,N''Unavailable.'',1;
 SELECT @Expected=SourceHash FROM RouteDispatch.Requests WITH(UPDLOCK,HOLDLOCK) WHERE OperationId=@OperationId AND EventId=@EventId AND OriginalLoginId=@LoginId AND Actor=@UsuarioId;
 IF @Expected IS NULL THROW 59502,N''Unavailable.'',1;
 EXEC RouteDispatch.SnapshotHash @EventId,@Source OUTPUT; IF @Expected<>@Source THROW 59503,N''Snapshot changed.'',1;
 SELECT @State=State,@Attempted=AttemptedUtc FROM RouteDispatch.Attempts WITH(UPDLOCK,HOLDLOCK) WHERE OperationId=@OperationId;
 IF @State IS NULL THROW 59502,N''Unavailable.'',1;
 IF @State=N''Pending'' BEGIN UPDATE RouteDispatch.Attempts SET State=N''Sending'',ClaimHash=HASHBYTES(''SHA2_256'',@ClaimToken),AttemptedUtc=SYSUTCDATETIME() WHERE OperationId=@OperationId AND State=N''Pending''; IF @@ROWCOUNT<>1 THROW 59503,N''Concurrent claim.'',1; INSERT RouteDispatch.Events(OperationId,State) VALUES(@OperationId,N''Sending''); SET @State=N''Sending'';SET @ShouldSend=1; END;
  IF @ShouldSend=0 AND @State=N''Sending'' AND @Attempted<=DATEADD(second,-30,SYSUTCDATETIME())
  BEGIN
   UPDATE RouteDispatch.Attempts SET State=N''Unknown'',CompletedUtc=SYSUTCDATETIME() WHERE OperationId=@OperationId AND State=N''Sending'' AND AttemptedUtc=@Attempted;
   IF @@ROWCOUNT=1 BEGIN INSERT RouteDispatch.Events(OperationId,State) VALUES(@OperationId,N''Unknown'');SET @State=N''Unknown'';END;
  END;
 COMMIT;
 SELECT @OperationId OperationId,@EventGuid EventGuid,@ShouldSend ShouldSend,@State State,CASE WHEN @ShouldSend=1 THEN i.PedidoId END PedidoId,CASE WHEN @ShouldSend=1 THEN i.RecordedUtc END RecordedUtc,CASE WHEN @ShouldSend=1 THEN i.CustomerName END CustomerName,CASE WHEN @ShouldSend=1 THEN i.CustomerEmail END CustomerEmail FROM RouteDispatch.Items i WHERE i.EventId=@EventId;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH; END;');

 EXEC(N'CREATE PROCEDURE RouteDispatch.Complete @OperationId uniqueidentifier,@ClaimToken binary(32),@State nvarchar(20)
 AS BEGIN SET NOCOUNT ON;SET XACT_ABORT ON;
 IF @@TRANCOUNT<>0 THROW 59501,N''Complete requires its own transaction.'',1;
 BEGIN TRY BEGIN TRAN;
 IF @State NOT IN(N''SmtpAccepted'',N''Unknown'') OR @ClaimToken IS NULL THROW 59501,N''Invalid completion.'',1;
 DECLARE @Current nvarchar(20),@EventGuid uniqueidentifier;
 SELECT @Current=a.State,@EventGuid=i.EventGuid FROM RouteDispatch.Attempts a WITH(UPDLOCK,HOLDLOCK) JOIN RouteDispatch.Requests r ON r.OperationId=a.OperationId JOIN RouteDispatch.Items i ON i.EventId=r.EventId WHERE a.OperationId=@OperationId AND a.ClaimHash=HASHBYTES(''SHA2_256'',@ClaimToken);
 IF @Current IS NULL THROW 59502,N''Unavailable.'',1;
 IF @Current=N''Sending'' BEGIN UPDATE RouteDispatch.Attempts SET State=@State,CompletedUtc=SYSUTCDATETIME() WHERE OperationId=@OperationId AND State=N''Sending''; INSERT RouteDispatch.Events(OperationId,State) VALUES(@OperationId,@State); END ELSE IF @Current<>@State THROW 59503,N''Completion is immutable.'',1;
 COMMIT; SELECT @OperationId OperationId,@EventGuid EventGuid,@State State;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH; END;');

 EXEC(N'CREATE PROCEDURE RouteDispatch.Receipt
 @LoginId uniqueidentifier,@UsuarioId int,@PerfilId int,@SecurityStamp uniqueidentifier,@BindingHash binary(32),@IssuedUtc datetime2(7),@ExpiresUtc datetime2(7),@RutaId int,@DispatchId uniqueidentifier,@EventId bigint,@EventGuid uniqueidentifier,@OperationId uniqueidentifier
 AS BEGIN SET NOCOUNT ON;SET XACT_ABORT ON;
 BEGIN TRY BEGIN TRAN;
 EXEC dbo.sp_Stage2_RequireOriginalLogin @LoginId,@UsuarioId,@PerfilId,@SecurityStamp,@BindingHash,@IssuedUtc,@ExpiresUtc,N''RUTAS_GESTIONAR'';
 IF NOT EXISTS(SELECT 1 FROM RouteDispatch.Dispatches WITH(UPDLOCK,HOLDLOCK) WHERE DispatchId=@DispatchId AND RutaId=@RutaId AND OriginalLoginId=@LoginId AND Actor=@UsuarioId) THROW 59502,N''Unavailable.'',1;
 DECLARE @Guid uniqueidentifier,@Eligible bit;SELECT @Guid=EventGuid,@Eligible=NotifyEligible FROM RouteDispatch.Items WITH(UPDLOCK,HOLDLOCK) WHERE EventId=@EventId AND DispatchId=@DispatchId;
 IF @Guid IS NULL OR @Guid<>@EventGuid OR @OperationId=@DispatchId THROW 59502,N''Unavailable.'',1;
  IF @Eligible=0 BEGIN COMMIT;SELECT CONVERT(uniqueidentifier,''00000000-0000-0000-0000-000000000000'') OperationId,@Guid EventGuid,CONVERT(nvarchar(20),N''NoRecipient'') State;RETURN;END;
  IF @OperationId=''00000000-0000-0000-0000-000000000000''
   SELECT @OperationId=r.OperationId FROM RouteDispatch.Requests r WITH(UPDLOCK,HOLDLOCK) WHERE r.EventId=@EventId AND r.OriginalLoginId=@LoginId AND r.Actor=@UsuarioId;
  IF @OperationId IS NULL OR @OperationId=''00000000-0000-0000-0000-000000000000'' BEGIN COMMIT;SELECT CONVERT(uniqueidentifier,''00000000-0000-0000-0000-000000000000'') OperationId,@Guid EventGuid,CONVERT(nvarchar(20),N''None'') State;RETURN;END;
  DECLARE @ReceiptState nvarchar(20),@ReceiptAttempted datetime2(7);
  SELECT @ReceiptState=a.State,@ReceiptAttempted=a.AttemptedUtc FROM RouteDispatch.Requests r WITH(UPDLOCK,HOLDLOCK) JOIN RouteDispatch.Attempts a WITH(UPDLOCK,HOLDLOCK) ON a.OperationId=r.OperationId WHERE r.OperationId=@OperationId AND r.EventId=@EventId AND r.OriginalLoginId=@LoginId AND r.Actor=@UsuarioId;
  IF @ReceiptState IS NULL THROW 59502,N''Unavailable.'',1;
  IF @ReceiptState=N''Sending'' AND @ReceiptAttempted<=DATEADD(second,-30,SYSUTCDATETIME())
  BEGIN
   UPDATE RouteDispatch.Attempts SET State=N''Unknown'',CompletedUtc=SYSUTCDATETIME() WHERE OperationId=@OperationId AND State=N''Sending'' AND AttemptedUtc=@ReceiptAttempted;
   IF @@ROWCOUNT=1 BEGIN INSERT RouteDispatch.Events(OperationId,State) VALUES(@OperationId,N''Unknown'');SET @ReceiptState=N''Unknown'';END;
  END;
  SELECT @OperationId OperationId,@Guid EventGuid,@ReceiptState State;
 COMMIT; END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH; END;');

 INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes)
 VALUES(N'0059_stage2_route_dispatch_notifications',N'0059_stage2_route_dispatch_notifications.sql',UPPER(@Hash),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),N'Route dispatch owns original login and immutable per-order snapshots; notification authority has independent claim state; legacy wrapper dispatch bypass closed. No grants or backfill.');
 COMMIT;
END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
