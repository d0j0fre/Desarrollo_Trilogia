-- Purpose-bound invoice egress. No SMTP credentials, recipients, profiles or fixtures are installed here.
-- SMTP and SQL cannot commit atomically: an unconfirmed attempt is never automatically sent again.
SET NOCOUNT ON; SET XACT_ABORT ON;
IF SESSIONPROPERTY('QUOTED_IDENTIFIER')<>1 OR SESSIONPROPERTY('ANSI_NULLS')<>1 THROW 59310,N'SQL options unavailable.',1;
DECLARE @Hash nvarchar(128)=N'$(MigrationSha256)';
IF LEN(@Hash)<>64 OR @Hash LIKE N'%[^0-9a-fA-F]%' THROW 59310,N'Invalid migration hash.',1;
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL THROW 59310,N'Ledger unavailable.',1;
BEGIN TRY
BEGIN TRAN;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WITH(UPDLOCK,HOLDLOCK) WHERE MigrationId=N'0057_stage2_invoice_notifications')
BEGIN
 IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0057_stage2_invoice_notifications' AND FileSha256=UPPER(@Hash) AND Status=N'Applied') THROW 59310,N'Ledger conflict.',1;
 COMMIT; RETURN;
END;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId LIKE N'0057%') OR SCHEMA_ID(N'InvoiceNotifications') IS NOT NULL THROW 59310,N'Migration identifier or namespace collision.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0051_stage2_commercial_orders' AND Status=N'Applied') THROW 59310,N'Invoice workflow prerequisite unavailable.',1;
IF ISNULL(HASHBYTES('SHA2_256',CONVERT(varbinary(max),OBJECT_DEFINITION(OBJECT_ID(N'dbo.sp_Stage2_RequireOriginalLogin')))),0x)<>0x8C8515B2D2DD516A737A548EE7A03AB3E12D101BB8D6B27A1FBDC8846E31479C THROW 59310,N'Original login guard differs.',1;
IF OBJECT_ID(N'dbo.Facturas',N'U') IS NULL OR COL_LENGTH(N'dbo.Facturas',N'ClienteCorreo')<>300 OR COL_LENGTH(N'dbo.Facturas',N'ClienteNombre')<>300 OR COL_LENGTH(N'dbo.Facturas',N'NumeroFactura')<>60 THROW 59310,N'Invoice snapshot contract unavailable.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.Facturas') AND name=N'Total' AND TYPE_NAME(user_type_id)=N'decimal' AND precision=18 AND scale=2 AND is_nullable=0) THROW 59310,N'Invoice money contract unavailable.',1;
EXEC(N'CREATE SCHEMA InvoiceNotifications AUTHORIZATION dbo;');
EXEC(N'
CREATE TABLE InvoiceNotifications.Requests(
 OperationId uniqueidentifier NOT NULL PRIMARY KEY,
 InvoiceId int NOT NULL REFERENCES dbo.Facturas(FacturaId),
 OriginalLoginId uniqueidentifier NOT NULL REFERENCES dbo.Stage2OriginalLogins(LoginId),
 Actor int NOT NULL REFERENCES dbo.Usuarios(UsuarioId),
 IsInitial bit NOT NULL,
 SourceHash binary(32) NOT NULL,
 CreatedUtc datetime2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
 CONSTRAINT CK_InvoiceNotification_Operation CHECK(OperationId<>''00000000-0000-0000-0000-000000000000'')
);
CREATE UNIQUE INDEX UX_InvoiceNotification_Initial ON InvoiceNotifications.Requests(InvoiceId) WHERE IsInitial=1;
CREATE INDEX IX_InvoiceNotification_Owner ON InvoiceNotifications.Requests(OriginalLoginId,InvoiceId,CreatedUtc);
CREATE TABLE InvoiceNotifications.Attempts(
 OperationId uniqueidentifier NOT NULL PRIMARY KEY REFERENCES InvoiceNotifications.Requests(OperationId),
 State nvarchar(20) NOT NULL,
 ClaimHash binary(32) NULL,
 AttemptedUtc datetime2(7) NULL,
 CompletedUtc datetime2(7) NULL,
 CONSTRAINT CK_InvoiceNotification_State CHECK(
  (State=N''Pending'' AND ClaimHash IS NULL AND AttemptedUtc IS NULL AND CompletedUtc IS NULL) OR
  (State=N''Sending'' AND ClaimHash IS NOT NULL AND AttemptedUtc IS NOT NULL AND CompletedUtc IS NULL) OR
  (State IN(N''SmtpAccepted'',N''Unknown'') AND ClaimHash IS NOT NULL AND AttemptedUtc IS NOT NULL AND CompletedUtc IS NOT NULL))
);
CREATE TABLE InvoiceNotifications.Events(
 EventId bigint IDENTITY NOT NULL PRIMARY KEY,
 OperationId uniqueidentifier NOT NULL REFERENCES InvoiceNotifications.Requests(OperationId),
 State nvarchar(20) NOT NULL,
 CreatedUtc datetime2(7) NOT NULL DEFAULT SYSUTCDATETIME()
);
');
EXEC(N'CREATE TRIGGER InvoiceNotifications.ImmutableRequests ON InvoiceNotifications.Requests AFTER UPDATE,DELETE AS
BEGIN SET NOCOUNT ON; THROW 59303,N''Immutable original notification request.'',1; END;');
EXEC(N'CREATE TRIGGER InvoiceNotifications.ImmutableEvents ON InvoiceNotifications.Events AFTER UPDATE,DELETE AS
BEGIN SET NOCOUNT ON; THROW 59303,N''Immutable notification audit.'',1; END;');
EXEC(N'CREATE TRIGGER InvoiceNotifications.AttemptTransitions ON InvoiceNotifications.Attempts AFTER UPDATE,DELETE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.OperationId=d.OperationId WHERE i.OperationId IS NULL
  OR NOT((d.State=N''Pending'' AND i.State=N''Sending'') OR
         (d.State=N''Sending'' AND i.State IN(N''SmtpAccepted'',N''Unknown'') AND d.ClaimHash=i.ClaimHash AND d.AttemptedUtc=i.AttemptedUtc)))
  THROW 59303,N''Invalid notification state transition.'',1;
END;');
EXEC(N'
CREATE PROCEDURE InvoiceNotifications.RequireAccess
 @LoginId uniqueidentifier,@UsuarioId int,@PerfilId int,@SecurityStamp uniqueidentifier,@BindingHash binary(32),
 @IssuedUtc datetime2(7),@ExpiresUtc datetime2(7),@InvoiceId int
AS
BEGIN
 SET NOCOUNT ON;
 -- Resolve the capability branch while holding the profile first, then invoke the
 -- canonical guard once. Preserve its Profile -> Permission -> User -> Login order.
 DECLARE @Permission nvarchar(100)=N''FACTURACION_GENERAR'',@Client bit=0;
 IF EXISTS(SELECT 1 FROM dbo.Perfiles WITH(UPDLOCK,HOLDLOCK) WHERE PerfilId=@PerfilId AND Nombre=N''Cliente'' AND Activo=1)
 BEGIN SET @Permission=NULL; SET @Client=1; END;
 EXEC dbo.sp_Stage2_RequireOriginalLogin @LoginId,@UsuarioId,@PerfilId,@SecurityStamp,@BindingHash,@IssuedUtc,@ExpiresUtc,@Permission;
 IF @InvoiceId<=0 OR NOT EXISTS(SELECT 1 FROM dbo.Facturas WITH(UPDLOCK,HOLDLOCK) WHERE FacturaId=@InvoiceId AND Estado=N''Generada'')
  THROW 59304,N''Unavailable.'',1;
 IF @Client=1 AND NOT EXISTS(SELECT 1 FROM dbo.Facturas WHERE FacturaId=@InvoiceId AND UsuarioId=@UsuarioId)
  THROW 59304,N''Unavailable.'',1;
END;
');
EXEC(N'
CREATE PROCEDURE InvoiceNotifications.SnapshotHash @InvoiceId int,@Hash binary(32) OUTPUT
AS
BEGIN
 SET NOCOUNT ON;
 DECLARE @Json nvarchar(max)=(SELECT FacturaId,PedidoId,NumeroFactura,UsuarioId,ClienteNombre,ClienteCorreo,FechaFactura,Total
  FROM dbo.Facturas WITH(HOLDLOCK) WHERE FacturaId=@InvoiceId FOR JSON PATH,WITHOUT_ARRAY_WRAPPER,INCLUDE_NULL_VALUES);
 IF @Json IS NULL OR DATALENGTH(@Json)=0 THROW 59304,N''Unavailable.'',1;
 SET @Hash=HASHBYTES(''SHA2_256'',CONVERT(varbinary(max),@Json));
END;
');
EXEC(N'
CREATE PROCEDURE InvoiceNotifications.Request
 @LoginId uniqueidentifier,@UsuarioId int,@PerfilId int,@SecurityStamp uniqueidentifier,@BindingHash binary(32),
 @IssuedUtc datetime2(7),@ExpiresUtc datetime2(7),@InvoiceId int,@OperationId uniqueidentifier,@IsInitial bit
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 BEGIN TRY BEGIN TRAN;
 EXEC InvoiceNotifications.RequireAccess @LoginId,@UsuarioId,@PerfilId,@SecurityStamp,@BindingHash,@IssuedUtc,@ExpiresUtc,@InvoiceId;
 IF @OperationId IS NULL OR @OperationId=''00000000-0000-0000-0000-000000000000'' OR @IsInitial IS NULL THROW 59301,N''Invalid request.'',1;
 DECLARE @Source binary(32); EXEC InvoiceNotifications.SnapshotHash @InvoiceId,@Source OUTPUT;
 IF EXISTS(SELECT 1 FROM InvoiceNotifications.Requests WITH(UPDLOCK,HOLDLOCK) WHERE OperationId=@OperationId)
 BEGIN
  IF NOT EXISTS(SELECT 1 FROM InvoiceNotifications.Requests WHERE OperationId=@OperationId AND OriginalLoginId=@LoginId AND Actor=@UsuarioId)
   THROW 59304,N''Unavailable.'',1;
  IF NOT EXISTS(SELECT 1 FROM InvoiceNotifications.Requests WHERE OperationId=@OperationId AND InvoiceId=@InvoiceId AND IsInitial=@IsInitial AND SourceHash=@Source)
   THROW 59303,N''Request differs.'',1;
 END
 ELSE
 BEGIN
  IF @IsInitial=1 AND EXISTS(SELECT 1 FROM InvoiceNotifications.Requests WITH(UPDLOCK,HOLDLOCK) WHERE InvoiceId=@InvoiceId AND IsInitial=1)
  BEGIN SELECT @OperationId AS OperationId,CONVERT(nvarchar(20),N''AlreadyRequested'') AS State; COMMIT; RETURN; END;
  INSERT InvoiceNotifications.Requests(OperationId,InvoiceId,OriginalLoginId,Actor,IsInitial,SourceHash) VALUES(@OperationId,@InvoiceId,@LoginId,@UsuarioId,@IsInitial,@Source);
  INSERT InvoiceNotifications.Attempts(OperationId,State) VALUES(@OperationId,N''Pending'');
  INSERT InvoiceNotifications.Events(OperationId,State) VALUES(@OperationId,N''Pending'');
 END;
 SELECT @OperationId AS OperationId,State FROM InvoiceNotifications.Attempts WHERE OperationId=@OperationId;
 COMMIT;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
END;
');
EXEC(N'
CREATE PROCEDURE InvoiceNotifications.Claim
 @LoginId uniqueidentifier,@UsuarioId int,@PerfilId int,@SecurityStamp uniqueidentifier,@BindingHash binary(32),
 @IssuedUtc datetime2(7),@ExpiresUtc datetime2(7),@InvoiceId int,@OperationId uniqueidentifier,@ClaimToken binary(32)
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 IF @@TRANCOUNT<>0 THROW 59301,N''Claim requires its own durable transaction.'',1;
 BEGIN TRY BEGIN TRAN;
 EXEC InvoiceNotifications.RequireAccess @LoginId,@UsuarioId,@PerfilId,@SecurityStamp,@BindingHash,@IssuedUtc,@ExpiresUtc,@InvoiceId;
 IF @ClaimToken IS NULL OR @ClaimToken=0x0000000000000000000000000000000000000000000000000000000000000000 THROW 59301,N''Invalid claim.'',1;
 DECLARE @Expected binary(32),@Source binary(32),@State nvarchar(20),@Attempted datetime2(7);
 SELECT @Expected=SourceHash FROM InvoiceNotifications.Requests WITH(UPDLOCK,HOLDLOCK)
  WHERE OperationId=@OperationId AND InvoiceId=@InvoiceId AND OriginalLoginId=@LoginId AND Actor=@UsuarioId;
 IF @Expected IS NULL THROW 59304,N''Unavailable.'',1;
 EXEC InvoiceNotifications.SnapshotHash @InvoiceId,@Source OUTPUT;
 IF @Expected<>@Source THROW 59303,N''Invoice snapshot changed.'',1;
 SELECT @State=State,@Attempted=AttemptedUtc FROM InvoiceNotifications.Attempts WITH(UPDLOCK,HOLDLOCK) WHERE OperationId=@OperationId;
 IF @State IS NULL THROW 59304,N''Unavailable.'',1;
 DECLARE @ShouldSend bit=0;
 IF @State=N''Pending''
 BEGIN
  UPDATE InvoiceNotifications.Attempts SET State=N''Sending'',ClaimHash=HASHBYTES(''SHA2_256'',@ClaimToken),AttemptedUtc=SYSUTCDATETIME() WHERE OperationId=@OperationId AND State=N''Pending'';
  IF @@ROWCOUNT<>1 THROW 59303,N''Concurrent claim.'',1;
  INSERT InvoiceNotifications.Events(OperationId,State) VALUES(@OperationId,N''Sending'');
  SET @ShouldSend=1; SET @State=N''Sending'';
 END;
 -- Do not expose a sendable snapshot until its one-time claim has committed.
 DECLARE @Number nvarchar(30),@Order int,@Name nvarchar(150),@Email nvarchar(150),@Date datetime2(7),@Total decimal(18,2);
 IF @ShouldSend=1
  SELECT @Number=NumeroFactura,@Order=PedidoId,@Name=ClienteNombre,@Email=ClienteCorreo,@Date=FechaFactura,@Total=Total
   FROM dbo.Facturas WHERE FacturaId=@InvoiceId;
 SET @State=CASE WHEN @ShouldSend=0 AND @State=N''Sending'' AND @Attempted<=DATEADD(second,-30,SYSUTCDATETIME()) THEN N''Unknown'' ELSE @State END;
 COMMIT;
 SELECT @OperationId AS OperationId,@ShouldSend AS ShouldSend,@State AS State,@Number AS NumeroFactura,@Order AS PedidoId,
  @Name AS ClienteNombre,@Email AS ClienteCorreo,@Date AS FechaFactura,@Total AS Total;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
END;
');
EXEC(N'
-- Only the notification authority receives EXECUTE. The claim token is generated internally and never returned over HTTP.
CREATE PROCEDURE InvoiceNotifications.Complete @OperationId uniqueidentifier,@ClaimToken binary(32),@State nvarchar(20)
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 BEGIN TRY BEGIN TRAN;
 IF @State NOT IN(N''SmtpAccepted'',N''Unknown'') OR @State IS NULL OR @ClaimToken IS NULL THROW 59301,N''Invalid result.'',1;
 DECLARE @Current nvarchar(20);
 SELECT @Current=State FROM InvoiceNotifications.Attempts WITH(UPDLOCK,HOLDLOCK)
  WHERE OperationId=@OperationId AND ClaimHash=HASHBYTES(''SHA2_256'',@ClaimToken);
 IF @Current IS NULL THROW 59304,N''Unavailable.'',1;
 IF @Current=N''Sending''
 BEGIN
  UPDATE InvoiceNotifications.Attempts SET State=@State,CompletedUtc=SYSUTCDATETIME() WHERE OperationId=@OperationId AND State=N''Sending'';
  INSERT InvoiceNotifications.Events(OperationId,State) VALUES(@OperationId,@State);
 END
 ELSE IF @Current<>@State THROW 59303,N''Immutable completion.'',1;
 SELECT @OperationId AS OperationId,@State AS State;
 COMMIT;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
END;
');
EXEC(N'
CREATE PROCEDURE InvoiceNotifications.Receipt
 @LoginId uniqueidentifier,@UsuarioId int,@PerfilId int,@SecurityStamp uniqueidentifier,@BindingHash binary(32),
 @IssuedUtc datetime2(7),@ExpiresUtc datetime2(7),@InvoiceId int,@OperationId uniqueidentifier
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 BEGIN TRY BEGIN TRAN;
 EXEC InvoiceNotifications.RequireAccess @LoginId,@UsuarioId,@PerfilId,@SecurityStamp,@BindingHash,@IssuedUtc,@ExpiresUtc,@InvoiceId;
 -- An empty selector discovers only the latest attempt of this original session.
 -- This restores an acknowledgement lost across an MVC restart without adopting
 -- the UUID of another session or exposing recipient/content.
 IF @OperationId=''00000000-0000-0000-0000-000000000000''
 BEGIN
  SET @OperationId=NULL;
  SELECT TOP(1) @OperationId=OperationId FROM InvoiceNotifications.Requests WITH(HOLDLOCK)
   WHERE InvoiceId=@InvoiceId AND OriginalLoginId=@LoginId AND Actor=@UsuarioId ORDER BY CreatedUtc DESC,OperationId DESC;
  IF @OperationId IS NULL
  BEGIN COMMIT; SELECT CONVERT(uniqueidentifier,''00000000-0000-0000-0000-000000000000'') AS OperationId,CONVERT(nvarchar(20),N''None'') AS State; RETURN; END;
 END;
 IF NOT EXISTS(SELECT 1 FROM InvoiceNotifications.Requests WITH(HOLDLOCK) WHERE OperationId=@OperationId AND InvoiceId=@InvoiceId AND OriginalLoginId=@LoginId AND Actor=@UsuarioId)
  THROW 59304,N''Unavailable.'',1;
 SELECT @OperationId AS OperationId,CONVERT(nvarchar(20),CASE WHEN State=N''Sending'' AND AttemptedUtc<=DATEADD(second,-30,SYSUTCDATETIME()) THEN N''Unknown'' ELSE State END) AS State
  FROM InvoiceNotifications.Attempts WITH(HOLDLOCK) WHERE OperationId=@OperationId;
 COMMIT;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
END;
');
INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes)
 VALUES(N'0057_stage2_invoice_notifications',N'0057_stage2_invoice_notifications.sql',UPPER(@Hash),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),
 N'Purpose-bound invoice notifications; canonical original login and invoice snapshot; immutable session/UUID ownership; one initial request per invoice; one SMTP claim; uncertain attempts never auto-retry; append-only state audit. No grants, credentials, commercial data rewrite or SMTP execution.');
COMMIT;
END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
