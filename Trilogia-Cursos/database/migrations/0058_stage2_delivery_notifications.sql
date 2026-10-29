-- Purpose-bound notification of an already committed delivery event.
-- SyncGuid selects the business event; OperationId is a DISTINCT notification UUID.
-- No delivery, invoice, stock, assignment, credential or grant is modified here.
SET NOCOUNT ON; SET XACT_ABORT ON;
IF SESSIONPROPERTY('QUOTED_IDENTIFIER')<>1 OR SESSIONPROPERTY('ANSI_NULLS')<>1 THROW 59410,N'SQL options unavailable.',1;
DECLARE @Hash nvarchar(128)=N'$(MigrationSha256)';
IF LEN(@Hash)<>64 OR @Hash LIKE N'%[^0-9a-fA-F]%' THROW 59410,N'Invalid migration hash.',1;
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL THROW 59410,N'Ledger unavailable.',1;
BEGIN TRY BEGIN TRAN;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WITH(UPDLOCK,HOLDLOCK) WHERE MigrationId=N'0058_stage2_delivery_notifications')
BEGIN
 IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0058_stage2_delivery_notifications' AND FileSha256=UPPER(@Hash) AND Status=N'Applied') THROW 59410,N'Ledger conflict.',1;
 COMMIT; RETURN;
END;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId LIKE N'0058%') OR SCHEMA_ID(N'DeliveryNotifications') IS NOT NULL THROW 59410,N'Migration identifier or namespace collision.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0031_stage2_physical_delivery' AND FileSha256=N'9A1E93B5A8F58375D901E2889D58E5C8E3060C604A64736891FEAE5EF6AA8F78' AND Status=N'Applied') THROW 59410,N'Physical delivery prerequisite differs.',1;
IF ISNULL(HASHBYTES('SHA2_256',CONVERT(varbinary(max),OBJECT_DEFINITION(OBJECT_ID(N'dbo.sp_Stage2_RequireOriginalLogin')))),0x)<>0x8C8515B2D2DD516A737A548EE7A03AB3E12D101BB8D6B27A1FBDC8846E31479C THROW 59410,N'Original login guard differs.',1;
IF ISNULL(HASHBYTES('SHA2_256',CONVERT(varbinary(max),OBJECT_DEFINITION(OBJECT_ID(N'dbo.sp_Stage2_ConfirmDelivery')))),0x)<>0x7E8763B2CA8D87462FE2A1D109C07BCD28175E779C1F39E5E752DC5673227866 THROW 59410,N'Committed delivery contract differs.',1;
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.EntregaSyncLog') AND name=N'UQ_EntregaSyncLog_Guid' AND is_unique=1 AND is_disabled=0)
 OR COL_LENGTH(N'dbo.EntregaSyncLog',N'SyncGuid')<>16 OR COL_LENGTH(N'dbo.EntregaSyncLog',N'EstadoNuevo')<>40
 OR COL_LENGTH(N'dbo.RutaPedidos',N'RutaPedidoId')<>4 OR COL_LENGTH(N'dbo.Rutas',N'ChoferUsuarioId')<>4 THROW 59410,N'Delivery event schema unavailable.',1;
EXEC(N'CREATE SCHEMA DeliveryNotifications AUTHORIZATION dbo;');
EXEC(N'
CREATE TABLE DeliveryNotifications.Requests(
 OperationId uniqueidentifier NOT NULL PRIMARY KEY,
 EventId int NOT NULL REFERENCES dbo.EntregaSyncLog(EntregaSyncLogId),
 RouteOrderId int NOT NULL REFERENCES dbo.RutaPedidos(RutaPedidoId),
 OriginalLoginId uniqueidentifier NOT NULL REFERENCES dbo.Stage2OriginalLogins(LoginId),
 Actor int NOT NULL REFERENCES dbo.Usuarios(UsuarioId),
 SourceHash binary(32) NOT NULL,
 CreatedUtc datetime2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
 CONSTRAINT CK_DeliveryNotification_Operation CHECK(OperationId<>''00000000-0000-0000-0000-000000000000'')
);
CREATE UNIQUE INDEX UX_DeliveryNotification_Event ON DeliveryNotifications.Requests(EventId);
CREATE INDEX IX_DeliveryNotification_Owner ON DeliveryNotifications.Requests(OriginalLoginId,EventId,CreatedUtc);
CREATE TABLE DeliveryNotifications.Attempts(
 OperationId uniqueidentifier NOT NULL PRIMARY KEY REFERENCES DeliveryNotifications.Requests(OperationId),
 State nvarchar(20) NOT NULL,ClaimHash binary(32) NULL,AttemptedUtc datetime2(7) NULL,CompletedUtc datetime2(7) NULL,
 CONSTRAINT CK_DeliveryNotification_State CHECK(
  (State=N''Pending'' AND ClaimHash IS NULL AND AttemptedUtc IS NULL AND CompletedUtc IS NULL) OR
  (State=N''Sending'' AND ClaimHash IS NOT NULL AND AttemptedUtc IS NOT NULL AND CompletedUtc IS NULL) OR
  (State IN(N''SmtpAccepted'',N''Unknown'') AND ClaimHash IS NOT NULL AND AttemptedUtc IS NOT NULL AND CompletedUtc IS NOT NULL))
);
CREATE TABLE DeliveryNotifications.Events(
 EventId bigint IDENTITY NOT NULL PRIMARY KEY,
 OperationId uniqueidentifier NOT NULL REFERENCES DeliveryNotifications.Requests(OperationId),
 State nvarchar(20) NOT NULL,CreatedUtc datetime2(7) NOT NULL DEFAULT SYSUTCDATETIME()
);
');
EXEC(N'CREATE TRIGGER DeliveryNotifications.ImmutableRequests ON DeliveryNotifications.Requests AFTER UPDATE,DELETE AS
BEGIN SET NOCOUNT ON; THROW 59403,N''Immutable original notification request.'',1; END;');
EXEC(N'CREATE TRIGGER DeliveryNotifications.ImmutableEvents ON DeliveryNotifications.Events AFTER UPDATE,DELETE AS
BEGIN SET NOCOUNT ON; THROW 59403,N''Immutable notification audit.'',1; END;');
EXEC(N'CREATE TRIGGER DeliveryNotifications.AttemptTransitions ON DeliveryNotifications.Attempts AFTER UPDATE,DELETE AS
BEGIN SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.OperationId=d.OperationId WHERE i.OperationId IS NULL
  OR NOT((d.State=N''Pending'' AND i.State=N''Sending'') OR
   (d.State=N''Sending'' AND i.State IN(N''SmtpAccepted'',N''Unknown'') AND d.ClaimHash=i.ClaimHash AND d.AttemptedUtc=i.AttemptedUtc)))
  THROW 59403,N''Invalid notification state transition.'',1;
END;');
EXEC(N'
CREATE PROCEDURE DeliveryNotifications.RequireAccess
 @LoginId uniqueidentifier,@UsuarioId int,@PerfilId int,@SecurityStamp uniqueidentifier,@BindingHash binary(32),
 @IssuedUtc datetime2(7),@ExpiresUtc datetime2(7),@RouteOrderId int,@SyncGuid uniqueidentifier OUTPUT,@EventId int OUTPUT
AS BEGIN SET NOCOUNT ON;
 EXEC dbo.sp_Stage2_RequireOriginalLogin @LoginId,@UsuarioId,@PerfilId,@SecurityStamp,@BindingHash,@IssuedUtc,@ExpiresUtc,NULL;
 -- Match admission of the existing driver workflow. Neither profile bypasses route/event ownership.
 IF @RouteOrderId<=0 OR @SyncGuid IS NULL OR NOT EXISTS(SELECT 1 FROM dbo.Perfiles WHERE PerfilId=@PerfilId AND Nombre IN(N''Chofer'',N''Administrador'') AND Activo=1) THROW 59404,N''Unavailable.'',1;
 DECLARE @Route int;
 SELECT @Route=RutaId FROM dbo.RutaPedidos WITH(UPDLOCK,HOLDLOCK) WHERE RutaPedidoId=@RouteOrderId;
 IF @Route IS NULL OR NOT EXISTS(SELECT 1 FROM dbo.Rutas WITH(UPDLOCK,HOLDLOCK) WHERE RutaId=@Route AND ChoferUsuarioId=@UsuarioId) THROW 59404,N''Unavailable.'',1;
 -- Route/order precedes the sync row, as in the physical delivery writer.
 IF @SyncGuid=''00000000-0000-0000-0000-000000000000''
 BEGIN
  SET @SyncGuid=NULL;
  SELECT TOP(1) @SyncGuid=SyncGuid FROM dbo.EntregaSyncLog WITH(UPDLOCK,HOLDLOCK)
   WHERE RutaPedidoId=@RouteOrderId AND ChoferUsuarioId=@UsuarioId AND EstadoNuevo IN(N''EnRuta'',N''Entregado'') ORDER BY EntregaSyncLogId DESC;
  IF @SyncGuid IS NULL BEGIN SET @SyncGuid=''00000000-0000-0000-0000-000000000000''; SET @EventId=NULL; RETURN; END;
 END;
 SET @EventId=NULL;
 SELECT @EventId=EntregaSyncLogId FROM dbo.EntregaSyncLog WITH(UPDLOCK,HOLDLOCK)
  WHERE SyncGuid=@SyncGuid AND RutaPedidoId=@RouteOrderId AND ChoferUsuarioId=@UsuarioId AND EstadoNuevo IN(N''EnRuta'',N''Entregado'');
 IF @EventId IS NULL THROW 59404,N''Unavailable.'',1;
END;');
EXEC(N'
CREATE PROCEDURE DeliveryNotifications.SnapshotHash @EventId int,@Hash binary(32) OUTPUT
AS BEGIN SET NOCOUNT ON;
 DECLARE @Json nvarchar(max)=(SELECT s.EntregaSyncLogId,s.SyncGuid,s.RutaPedidoId,s.EstadoNuevo,s.FechaSync,
  rp.PedidoId,u.UsuarioId,u.NombreCompleto,u.Correo
  FROM dbo.EntregaSyncLog s WITH(HOLDLOCK) JOIN dbo.RutaPedidos rp WITH(HOLDLOCK) ON rp.RutaPedidoId=s.RutaPedidoId
  JOIN dbo.Pedidos p WITH(HOLDLOCK) ON p.PedidoId=rp.PedidoId JOIN dbo.Usuarios u WITH(HOLDLOCK) ON u.UsuarioId=p.UsuarioId AND u.Activo=1
  WHERE s.EntregaSyncLogId=@EventId FOR JSON PATH,WITHOUT_ARRAY_WRAPPER,INCLUDE_NULL_VALUES);
 IF @Json IS NULL OR DATALENGTH(@Json)=0 THROW 59404,N''Unavailable.'',1;
 SET @Hash=HASHBYTES(''SHA2_256'',CONVERT(varbinary(max),@Json));
END;');
EXEC(N'
CREATE PROCEDURE DeliveryNotifications.Request
 @LoginId uniqueidentifier,@UsuarioId int,@PerfilId int,@SecurityStamp uniqueidentifier,@BindingHash binary(32),
 @IssuedUtc datetime2(7),@ExpiresUtc datetime2(7),@RouteOrderId int,@SyncGuid uniqueidentifier,@OperationId uniqueidentifier
AS BEGIN SET NOCOUNT ON; SET XACT_ABORT ON;
 IF @OperationId IS NULL OR @OperationId=''00000000-0000-0000-0000-000000000000'' OR @SyncGuid IS NULL OR @SyncGuid=''00000000-0000-0000-0000-000000000000'' OR @OperationId=@SyncGuid THROW 59401,N''Invalid request.'',1;
 BEGIN TRY BEGIN TRAN;
 DECLARE @EventId int; EXEC DeliveryNotifications.RequireAccess @LoginId,@UsuarioId,@PerfilId,@SecurityStamp,@BindingHash,@IssuedUtc,@ExpiresUtc,@RouteOrderId,@SyncGuid OUTPUT,@EventId OUTPUT;
 DECLARE @Source binary(32); EXEC DeliveryNotifications.SnapshotHash @EventId,@Source OUTPUT;
 IF EXISTS(SELECT 1 FROM DeliveryNotifications.Requests WITH(UPDLOCK,HOLDLOCK) WHERE OperationId=@OperationId)
 BEGIN
  IF NOT EXISTS(SELECT 1 FROM DeliveryNotifications.Requests WHERE OperationId=@OperationId AND OriginalLoginId=@LoginId AND Actor=@UsuarioId) THROW 59404,N''Unavailable.'',1;
  IF NOT EXISTS(SELECT 1 FROM DeliveryNotifications.Requests WHERE OperationId=@OperationId AND EventId=@EventId AND RouteOrderId=@RouteOrderId AND SourceHash=@Source) THROW 59403,N''Request differs.'',1;
 END
 ELSE
 BEGIN
  IF EXISTS(SELECT 1 FROM DeliveryNotifications.Requests WITH(UPDLOCK,HOLDLOCK) WHERE EventId=@EventId)
  BEGIN COMMIT; SELECT @OperationId AS OperationId,@SyncGuid AS SyncGuid,CONVERT(nvarchar(20),N''AlreadyRequested'') AS State; RETURN; END;
  INSERT DeliveryNotifications.Requests(OperationId,EventId,RouteOrderId,OriginalLoginId,Actor,SourceHash) VALUES(@OperationId,@EventId,@RouteOrderId,@LoginId,@UsuarioId,@Source);
  INSERT DeliveryNotifications.Attempts(OperationId,State) VALUES(@OperationId,N''Pending'');
  INSERT DeliveryNotifications.Events(OperationId,State) VALUES(@OperationId,N''Pending'');
 END;
 SELECT @OperationId AS OperationId,@SyncGuid AS SyncGuid,State FROM DeliveryNotifications.Attempts WHERE OperationId=@OperationId;
 COMMIT;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
END;');
EXEC(N'
CREATE PROCEDURE DeliveryNotifications.Claim
 @LoginId uniqueidentifier,@UsuarioId int,@PerfilId int,@SecurityStamp uniqueidentifier,@BindingHash binary(32),
 @IssuedUtc datetime2(7),@ExpiresUtc datetime2(7),@RouteOrderId int,@SyncGuid uniqueidentifier,@OperationId uniqueidentifier,@ClaimToken binary(32)
AS BEGIN SET NOCOUNT ON; SET XACT_ABORT ON;
 IF @@TRANCOUNT<>0 THROW 59401,N''Claim requires its own durable transaction.'',1;
 IF @OperationId IS NULL OR @OperationId=''00000000-0000-0000-0000-000000000000'' OR @OperationId=@SyncGuid OR @SyncGuid IS NULL OR @SyncGuid=''00000000-0000-0000-0000-000000000000'' OR @ClaimToken IS NULL OR @ClaimToken=0x0000000000000000000000000000000000000000000000000000000000000000 THROW 59401,N''Invalid claim.'',1;
 BEGIN TRY BEGIN TRAN;
 DECLARE @EventId int; EXEC DeliveryNotifications.RequireAccess @LoginId,@UsuarioId,@PerfilId,@SecurityStamp,@BindingHash,@IssuedUtc,@ExpiresUtc,@RouteOrderId,@SyncGuid OUTPUT,@EventId OUTPUT;
 DECLARE @Expected binary(32),@Source binary(32),@State nvarchar(20),@Attempted datetime2(7),@ShouldSend bit=0;
 SELECT @Expected=SourceHash FROM DeliveryNotifications.Requests WITH(UPDLOCK,HOLDLOCK) WHERE OperationId=@OperationId AND EventId=@EventId AND RouteOrderId=@RouteOrderId AND OriginalLoginId=@LoginId AND Actor=@UsuarioId;
 IF @Expected IS NULL THROW 59404,N''Unavailable.'',1;
 EXEC DeliveryNotifications.SnapshotHash @EventId,@Source OUTPUT;
 IF @Expected<>@Source THROW 59403,N''Event snapshot changed.'',1;
 SELECT @State=State,@Attempted=AttemptedUtc FROM DeliveryNotifications.Attempts WITH(UPDLOCK,HOLDLOCK) WHERE OperationId=@OperationId;
 IF @State IS NULL THROW 59404,N''Unavailable.'',1;
 IF @State=N''Pending''
 BEGIN
  UPDATE DeliveryNotifications.Attempts SET State=N''Sending'',ClaimHash=HASHBYTES(''SHA2_256'',@ClaimToken),AttemptedUtc=SYSUTCDATETIME() WHERE OperationId=@OperationId AND State=N''Pending'';
  IF @@ROWCOUNT<>1 THROW 59403,N''Concurrent claim.'',1;
  INSERT DeliveryNotifications.Events(OperationId,State) VALUES(@OperationId,N''Sending'');
  SET @State=N''Sending''; SET @ShouldSend=1;
 END;
 DECLARE @Order int,@Status nvarchar(20),@Date datetime2(7),@Name nvarchar(150),@Email nvarchar(256);
 IF @ShouldSend=1
  SELECT @Order=rp.PedidoId,@Status=s.EstadoNuevo,@Date=s.FechaSync,@Name=u.NombreCompleto,@Email=u.Correo
   FROM dbo.EntregaSyncLog s JOIN dbo.RutaPedidos rp ON rp.RutaPedidoId=s.RutaPedidoId JOIN dbo.Pedidos p ON p.PedidoId=rp.PedidoId JOIN dbo.Usuarios u ON u.UsuarioId=p.UsuarioId WHERE s.EntregaSyncLogId=@EventId;
 SET @State=CASE WHEN @ShouldSend=0 AND @State=N''Sending'' AND @Attempted<=DATEADD(second,-30,SYSUTCDATETIME()) THEN N''Unknown'' ELSE @State END;
 COMMIT;
 SELECT @OperationId AS OperationId,@SyncGuid AS SyncGuid,@ShouldSend AS ShouldSend,@State AS State,
  @Order AS PedidoId,@Status AS EstadoNuevo,@Date AS FechaSync,@Name AS ClienteNombre,@Email AS ClienteCorreo;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
END;');
EXEC(N'
CREATE PROCEDURE DeliveryNotifications.Complete @OperationId uniqueidentifier,@ClaimToken binary(32),@State nvarchar(20)
AS BEGIN SET NOCOUNT ON; SET XACT_ABORT ON;
 IF @@TRANCOUNT<>0 THROW 59401,N''Completion requires its own durable transaction.'',1;
 BEGIN TRY BEGIN TRAN;
 IF @State IS NULL OR @State NOT IN(N''SmtpAccepted'',N''Unknown'') OR @ClaimToken IS NULL THROW 59401,N''Invalid result.'',1;
 DECLARE @Current nvarchar(20);
 SELECT @Current=a.State FROM DeliveryNotifications.Attempts a WITH(UPDLOCK,HOLDLOCK)
  WHERE a.OperationId=@OperationId AND a.ClaimHash=HASHBYTES(''SHA2_256'',@ClaimToken);
 IF @Current IS NULL THROW 59404,N''Unavailable.'',1;
 IF @Current=N''Sending''
 BEGIN
  UPDATE DeliveryNotifications.Attempts SET State=@State,CompletedUtc=SYSUTCDATETIME() WHERE OperationId=@OperationId AND State=N''Sending'';
  INSERT DeliveryNotifications.Events(OperationId,State) VALUES(@OperationId,@State);
 END
 ELSE IF @Current<>@State THROW 59403,N''Immutable completion.'',1;
 COMMIT;
 -- Read event identity only after releasing the Attempt lock. A concurrent Claim
 -- locks the business event before the Attempt; joining it above would invert that order.
 SELECT @OperationId AS OperationId,s.SyncGuid AS SyncGuid,@State AS State
  FROM DeliveryNotifications.Requests r JOIN dbo.EntregaSyncLog s ON s.EntregaSyncLogId=r.EventId WHERE r.OperationId=@OperationId;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
END;');
EXEC(N'
CREATE PROCEDURE DeliveryNotifications.Receipt
 @LoginId uniqueidentifier,@UsuarioId int,@PerfilId int,@SecurityStamp uniqueidentifier,@BindingHash binary(32),
 @IssuedUtc datetime2(7),@ExpiresUtc datetime2(7),@RouteOrderId int,@SyncGuid uniqueidentifier,@OperationId uniqueidentifier
AS BEGIN SET NOCOUNT ON; SET XACT_ABORT ON;
 IF @SyncGuid IS NULL OR @OperationId IS NULL OR (@OperationId<>''00000000-0000-0000-0000-000000000000'' AND @SyncGuid=''00000000-0000-0000-0000-000000000000'') THROW 59401,N''Invalid receipt selector.'',1;
 BEGIN TRY BEGIN TRAN;
 DECLARE @EventId int; EXEC DeliveryNotifications.RequireAccess @LoginId,@UsuarioId,@PerfilId,@SecurityStamp,@BindingHash,@IssuedUtc,@ExpiresUtc,@RouteOrderId,@SyncGuid OUTPUT,@EventId OUTPUT;
 IF @EventId IS NULL
 BEGIN COMMIT; SELECT CONVERT(uniqueidentifier,''00000000-0000-0000-0000-000000000000'') AS OperationId,@SyncGuid AS SyncGuid,CONVERT(nvarchar(20),N''NoEvent'') AS State; RETURN; END;
 IF @OperationId=''00000000-0000-0000-0000-000000000000''
 BEGIN
  SET @OperationId=NULL;
  SELECT TOP(1) @OperationId=OperationId FROM DeliveryNotifications.Requests WITH(HOLDLOCK) WHERE EventId=@EventId AND RouteOrderId=@RouteOrderId AND OriginalLoginId=@LoginId AND Actor=@UsuarioId ORDER BY CreatedUtc DESC,OperationId DESC;
  IF @OperationId IS NULL
  BEGIN COMMIT; SELECT CONVERT(uniqueidentifier,''00000000-0000-0000-0000-000000000000'') AS OperationId,@SyncGuid AS SyncGuid,CONVERT(nvarchar(20),N''None'') AS State; RETURN; END;
 END;
 IF NOT EXISTS(SELECT 1 FROM DeliveryNotifications.Requests WITH(HOLDLOCK) WHERE OperationId=@OperationId AND EventId=@EventId AND RouteOrderId=@RouteOrderId AND OriginalLoginId=@LoginId AND Actor=@UsuarioId) THROW 59404,N''Unavailable.'',1;
 SELECT @OperationId AS OperationId,@SyncGuid AS SyncGuid,CONVERT(nvarchar(20),CASE WHEN State=N''Sending'' AND AttemptedUtc<=DATEADD(second,-30,SYSUTCDATETIME()) THEN N''Unknown'' ELSE State END) AS State
  FROM DeliveryNotifications.Attempts WITH(HOLDLOCK) WHERE OperationId=@OperationId;
 COMMIT;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
END;');
INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes)
 VALUES(N'0058_stage2_delivery_notifications',N'0058_stage2_delivery_notifications.sql',UPPER(@Hash),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),
 N'Closed delivery-event notification authority; distinct session-owned notification UUID; one request/claim per committed event, canonical actor/route/event checks, opaque receipts and irreversible uncertain SMTP state. No dispatch, delivery, financial, historical, credential or grant changes.');
COMMIT;
END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
