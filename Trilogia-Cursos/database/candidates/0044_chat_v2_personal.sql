-- Candidate 0044. NOT an applied migration. Install batches in ONE transaction.
-- Requires canonical 0030/0034/0036 identity contracts. No identity/session import.
-- Ledger reconciliation and backup/restore gates are mandatory before promotion.
SET XACT_ABORT ON;
SET NOCOUNT ON;
IF OBJECT_ID(N'dbo.sp_Stage2_RequireOriginalLogin',N'P') IS NULL
 OR OBJECT_ID(N'dbo.Stage2OriginalLogins',N'U') IS NULL
 OR COL_LENGTH(N'dbo.Usuarios',N'SecurityStamp') IS NULL
 THROW 58400,N'Canonical identity contract missing.',1;
IF SCHEMA_ID(N'ChatV2P') IS NOT NULL OR COL_LENGTH(N'dbo.Usuarios',N'ChatV2Revision') IS NOT NULL
 THROW 58400,N'Existing candidate requires explicit reconciliation.',1;
ALTER TABLE dbo.Usuarios ADD ChatV2Revision rowversion;
EXEC(N'CREATE SCHEMA ChatV2P AUTHORIZATION dbo');
GO
CREATE TABLE ChatV2P.Conversations(
 ConversationId int IDENTITY PRIMARY KEY,
 FirstUser int NOT NULL REFERENCES dbo.Usuarios(UsuarioId),
 SecondUser int NOT NULL REFERENCES dbo.Usuarios(UsuarioId),
 Active bit NOT NULL DEFAULT 1,
 AccessVersion bigint NOT NULL DEFAULT 1 CHECK(AccessVersion>0),
 LastSequence bigint NOT NULL DEFAULT 0 CHECK(LastSequence>=0),
 CONSTRAINT CK_ChatV2P_Pair CHECK(FirstUser<SecondUser),
 CONSTRAINT UQ_ChatV2P_Pair UNIQUE(FirstUser,SecondUser)
);
CREATE TABLE ChatV2P.Intents(
 RequestId uniqueidentifier NOT NULL PRIMARY KEY,
 LoginId uniqueidentifier NOT NULL REFERENCES dbo.Stage2OriginalLogins(LoginId),
 Actor int NOT NULL REFERENCES dbo.Usuarios(UsuarioId),
 ConversationId int NOT NULL REFERENCES ChatV2P.Conversations(ConversationId),
 ContentHash binary(32) NOT NULL,
 ActorEpoch bigint NOT NULL CHECK(ActorEpoch>0),
 ResourceEpoch bigint NOT NULL CHECK(ResourceEpoch>0),
 IssuedUtc datetime2(7) NOT NULL,
 ExpiresUtc datetime2(7) NOT NULL,
 CONSTRAINT CK_ChatV2P_IntentLifetime CHECK(ExpiresUtc>IssuedUtc AND ExpiresUtc<=DATEADD(minute,15,IssuedUtc)),
 CONSTRAINT CK_ChatV2P_Request CHECK(RequestId<>'00000000-0000-0000-0000-000000000000')
);
CREATE TABLE ChatV2P.Messages(
 MessageId bigint IDENTITY PRIMARY KEY,
 RequestId uniqueidentifier NOT NULL UNIQUE REFERENCES ChatV2P.Intents(RequestId),
 ConversationId int NOT NULL REFERENCES ChatV2P.Conversations(ConversationId),
 StreamSequence bigint NOT NULL CHECK(StreamSequence>0),
 Sender int NOT NULL REFERENCES dbo.Usuarios(UsuarioId),
 Content nvarchar(1000) NOT NULL CHECK(DATALENGTH(Content)>0),
 TimestampUtc datetime2(7) NOT NULL,
 UNIQUE(ConversationId,StreamSequence)
);
CREATE TABLE ChatV2P.Requests(
 RequestId uniqueidentifier NOT NULL PRIMARY KEY REFERENCES ChatV2P.Intents(RequestId),
 MessageId bigint NOT NULL UNIQUE REFERENCES ChatV2P.Messages(MessageId)
);
CREATE TABLE ChatV2P.Outbox(
 EventId bigint IDENTITY PRIMARY KEY,
 RequestId uniqueidentifier NOT NULL UNIQUE REFERENCES ChatV2P.Requests(RequestId),
 ConversationId int NOT NULL REFERENCES ChatV2P.Conversations(ConversationId),
 CreatedUtc datetime2(7) NOT NULL
);
GO
CREATE TRIGGER ChatV2P.ImmutableIntent ON ChatV2P.Intents AFTER UPDATE,DELETE AS
BEGIN SET NOCOUNT ON; THROW 58403,N'Immutable original intent.',1; END;
GO
CREATE TRIGGER ChatV2P.ImmutableMessages ON ChatV2P.Messages AFTER UPDATE,DELETE AS
BEGIN SET NOCOUNT ON; THROW 58403,N'Immutable message.',1; END;
GO
CREATE TRIGGER ChatV2P.ImmutableRequests ON ChatV2P.Requests AFTER UPDATE,DELETE AS
BEGIN SET NOCOUNT ON; THROW 58403,N'Immutable receipt.',1; END;
GO
-- Internal helper. APP gets no direct EXECUTE permission on this procedure.
CREATE PROCEDURE ChatV2P.RequireAccess
 @LoginId uniqueidentifier,@UsuarioId int,@PerfilId int,@SecurityStamp uniqueidentifier,@BindingHash binary(32),
 @IssuedUtc datetime2(7),@ExpiresUtc datetime2(7),@ConversationId int=NULL,
 @ExpectedActor bigint=NULL,@ExpectedResource bigint=NULL
AS
BEGIN
 SET NOCOUNT ON;
 EXEC dbo.sp_Stage2_RequireOriginalLogin @LoginId,@UsuarioId,@PerfilId,@SecurityStamp,@BindingHash,@IssuedUtc,@ExpiresUtc,N'CHAT_USAR';
 IF EXISTS(SELECT 1 FROM dbo.Perfiles WHERE PerfilId=@PerfilId AND Nombre=N'Cliente')
  THROW 58402,N'Unavailable.',1;
 IF @ExpectedActor IS NOT NULL AND NOT EXISTS(SELECT 1 FROM dbo.Usuarios WHERE UsuarioId=@UsuarioId AND CONVERT(bigint,ChatV2Revision)=@ExpectedActor)
  THROW 58403,N'Authorization changed.',1;
 IF @ConversationId IS NOT NULL AND NOT EXISTS(
  SELECT 1 FROM ChatV2P.Conversations WITH(UPDLOCK,HOLDLOCK)
  WHERE ConversationId=@ConversationId AND Active=1 AND @UsuarioId IN(FirstUser,SecondUser)
   AND (@ExpectedResource IS NULL OR AccessVersion=@ExpectedResource))
  THROW 58404,N'Unavailable.',1;
END;
GO
CREATE PROCEDURE ChatV2P.Resolve
 @LoginId uniqueidentifier,@UsuarioId int,@PerfilId int,@SecurityStamp uniqueidentifier,@BindingHash binary(32),
 @IssuedUtc datetime2(7),@ExpiresUtc datetime2(7)
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 BEGIN TRY BEGIN TRANSACTION;
 EXEC ChatV2P.RequireAccess @LoginId,@UsuarioId,@PerfilId,@SecurityStamp,@BindingHash,@IssuedUtc,@ExpiresUtc;
 SELECT CONVERT(bigint,u.ChatV2Revision) AS ActorEpoch,
  CONVERT(bit,CASE WHEN p.Nombre=N'Cliente' THEN 1 ELSE 0 END) AS IsClient,
  CONVERT(bit,CASE WHEN p.Nombre<>N'Cliente' AND EXISTS(SELECT 1 FROM dbo.PerfilPermisos pp JOIN dbo.Permisos pe ON pe.PermisoId=pp.PermisoId WHERE pp.PerfilId=@PerfilId AND pe.Codigo=N'CHAT_USAR' AND pe.Activo=1) THEN 1 ELSE 0 END) AS InternalUse
 FROM dbo.Usuarios u JOIN dbo.Perfiles p ON p.PerfilId=u.PerfilId WHERE u.UsuarioId=@UsuarioId;
 COMMIT; END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
END;
GO
CREATE PROCEDURE ChatV2P.Directory
 @LoginId uniqueidentifier,@UsuarioId int,@PerfilId int,@SecurityStamp uniqueidentifier,@BindingHash binary(32),
 @IssuedUtc datetime2(7),@ExpiresUtc datetime2(7)
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 BEGIN TRY BEGIN TRANSACTION;
 EXEC ChatV2P.RequireAccess @LoginId,@UsuarioId,@PerfilId,@SecurityStamp,@BindingHash,@IssuedUtc,@ExpiresUtc;
 SELECT TOP(100) u.UsuarioId FROM dbo.Usuarios u JOIN dbo.Perfiles p ON p.PerfilId=u.PerfilId
 WHERE u.UsuarioId<>@UsuarioId AND u.Activo=1 AND p.Activo=1 AND p.Nombre<>N'Cliente'
 AND EXISTS(SELECT 1 FROM dbo.PerfilPermisos pp JOIN dbo.Permisos pe ON pe.PermisoId=pp.PermisoId WHERE pp.PerfilId=u.PerfilId AND pe.Codigo=N'CHAT_USAR' AND pe.Activo=1)
 ORDER BY u.UsuarioId;
 COMMIT; END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
END;
GO
CREATE PROCEDURE ChatV2P.OpenPrivate
 @LoginId uniqueidentifier,@UsuarioId int,@PerfilId int,@SecurityStamp uniqueidentifier,@BindingHash binary(32),
 @IssuedUtc datetime2(7),@ExpiresUtc datetime2(7),@OtherUser int
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 DECLARE @First int=CASE WHEN @UsuarioId<@OtherUser THEN @UsuarioId ELSE @OtherUser END,
 @Second int=CASE WHEN @UsuarioId<@OtherUser THEN @OtherUser ELSE @UsuarioId END,@Id int;
 BEGIN TRY BEGIN TRANSACTION;
 EXEC ChatV2P.RequireAccess @LoginId,@UsuarioId,@PerfilId,@SecurityStamp,@BindingHash,@IssuedUtc,@ExpiresUtc;
 IF @OtherUser=@UsuarioId OR NOT EXISTS(
  SELECT 1 FROM dbo.Usuarios u JOIN dbo.Perfiles p ON p.PerfilId=u.PerfilId
  JOIN dbo.PerfilPermisos pp ON pp.PerfilId=p.PerfilId JOIN dbo.Permisos pe ON pe.PermisoId=pp.PermisoId
  WHERE u.UsuarioId=@OtherUser AND u.Activo=1 AND p.Activo=1 AND p.Nombre<>N'Cliente' AND pe.Codigo=N'CHAT_USAR' AND pe.Activo=1)
  THROW 58404,N'Unavailable.',1;
 SELECT @Id=ConversationId FROM ChatV2P.Conversations WITH(UPDLOCK,HOLDLOCK) WHERE FirstUser=@First AND SecondUser=@Second;
 IF @Id IS NULL BEGIN
  INSERT ChatV2P.Conversations(FirstUser,SecondUser) VALUES(@First,@Second); SET @Id=CONVERT(int,SCOPE_IDENTITY());
 END;
 EXEC ChatV2P.RequireAccess @LoginId,@UsuarioId,@PerfilId,@SecurityStamp,@BindingHash,@IssuedUtc,@ExpiresUtc,@Id;
 COMMIT; SELECT @Id AS ConversationId;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
END;
GO
CREATE PROCEDURE ChatV2P.Own
 @LoginId uniqueidentifier,@UsuarioId int,@PerfilId int,@SecurityStamp uniqueidentifier,@BindingHash binary(32),
 @IssuedUtc datetime2(7),@ExpiresUtc datetime2(7)
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 BEGIN TRY BEGIN TRANSACTION;
 EXEC ChatV2P.RequireAccess @LoginId,@UsuarioId,@PerfilId,@SecurityStamp,@BindingHash,@IssuedUtc,@ExpiresUtc;
 SELECT ConversationId,AccessVersion FROM ChatV2P.Conversations WHERE Active=1 AND @UsuarioId IN(FirstUser,SecondUser) ORDER BY ConversationId;
 COMMIT; END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
END;
GO
CREATE PROCEDURE ChatV2P.Membership
 @LoginId uniqueidentifier,@UsuarioId int,@PerfilId int,@SecurityStamp uniqueidentifier,@BindingHash binary(32),
 @IssuedUtc datetime2(7),@ExpiresUtc datetime2(7),@ConversationId int
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 BEGIN TRY BEGIN TRANSACTION;
 EXEC ChatV2P.RequireAccess @LoginId,@UsuarioId,@PerfilId,@SecurityStamp,@BindingHash,@IssuedUtc,@ExpiresUtc,@ConversationId;
 SELECT AccessVersion FROM ChatV2P.Conversations WHERE ConversationId=@ConversationId;
 COMMIT; END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
END;
GO
CREATE PROCEDURE ChatV2P.Prepare
 @LoginId uniqueidentifier,@UsuarioId int,@PerfilId int,@SecurityStamp uniqueidentifier,@BindingHash binary(32),
 @IssuedUtc datetime2(7),@ExpiresUtc datetime2(7),@ConversationId int,@RequestId uniqueidentifier,@ContentHash binary(32),@ActorEpoch bigint
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 DECLARE @Now datetime2(7)=SYSUTCDATETIME(),@Until datetime2(7),@ResourceEpoch bigint;
 BEGIN TRY BEGIN TRANSACTION;
 EXEC ChatV2P.RequireAccess @LoginId,@UsuarioId,@PerfilId,@SecurityStamp,@BindingHash,@IssuedUtc,@ExpiresUtc,@ConversationId,@ActorEpoch;
 SELECT @ResourceEpoch=AccessVersion FROM ChatV2P.Conversations WHERE ConversationId=@ConversationId;
 SET @Now=SYSUTCDATETIME();
 IF @RequestId IS NULL OR @RequestId='00000000-0000-0000-0000-000000000000' OR @ContentHash IS NULL THROW 58403,N'Invalid intent.',1;
 IF EXISTS(SELECT 1 FROM ChatV2P.Intents WITH(UPDLOCK,HOLDLOCK) WHERE RequestId=@RequestId)
 BEGIN
  IF NOT EXISTS(SELECT 1 FROM ChatV2P.Intents WHERE RequestId=@RequestId AND LoginId=@LoginId AND Actor=@UsuarioId
   AND ConversationId=@ConversationId AND ContentHash=@ContentHash AND ActorEpoch=@ActorEpoch AND ResourceEpoch=@ResourceEpoch AND ExpiresUtc>@Now)
   THROW 58403,N'Intent unavailable.',1;
 END
 ELSE
 BEGIN
  SET @Until=CASE WHEN DATEADD(minute,15,@Now)<@ExpiresUtc THEN DATEADD(minute,15,@Now) ELSE @ExpiresUtc END;
  INSERT ChatV2P.Intents(RequestId,LoginId,Actor,ConversationId,ContentHash,ActorEpoch,ResourceEpoch,IssuedUtc,ExpiresUtc)
   VALUES(@RequestId,@LoginId,@UsuarioId,@ConversationId,@ContentHash,@ActorEpoch,@ResourceEpoch,@Now,@Until);
 END;
 SELECT ActorEpoch,ResourceEpoch,ExpiresUtc FROM ChatV2P.Intents WHERE RequestId=@RequestId;
 COMMIT; END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
END;
GO
CREATE PROCEDURE ChatV2P.ExecuteIntent
 @LoginId uniqueidentifier,@UsuarioId int,@PerfilId int,@SecurityStamp uniqueidentifier,@BindingHash binary(32),
 @IssuedUtc datetime2(7),@ExpiresUtc datetime2(7),@ConversationId int,@RequestId uniqueidentifier,@ContentHash binary(32),
 @ActorEpoch bigint,@ResourceEpoch bigint,@IntentExpiresUtc datetime2(7),@ReceiptOnly bit,@Content nvarchar(1000)=NULL
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 DECLARE @Now datetime2(7)=SYSUTCDATETIME(),@Sequence bigint,@Message bigint;
 BEGIN TRY BEGIN TRANSACTION;
 EXEC ChatV2P.RequireAccess @LoginId,@UsuarioId,@PerfilId,@SecurityStamp,@BindingHash,@IssuedUtc,@ExpiresUtc,@ConversationId,@ActorEpoch,@ResourceEpoch;
 IF NOT EXISTS(SELECT 1 FROM ChatV2P.Intents WITH(UPDLOCK,HOLDLOCK) WHERE RequestId=@RequestId AND LoginId=@LoginId AND Actor=@UsuarioId
  AND ConversationId=@ConversationId AND ContentHash=@ContentHash AND ActorEpoch=@ActorEpoch AND ResourceEpoch=@ResourceEpoch
  AND ExpiresUtc=@IntentExpiresUtc AND ExpiresUtc>@Now)
 BEGIN
  IF @ReceiptOnly=1 THROW 58404,N'Unavailable.',1;
  THROW 58403,N'Intent unavailable.',1;
 END;
 IF @ReceiptOnly=0 AND (@Content IS NULL OR DATALENGTH(@Content)=0 OR HASHBYTES('SHA2_256',@Content)<>@ContentHash)
  THROW 58403,N'Content changed.',1;
 IF NOT EXISTS(SELECT 1 FROM ChatV2P.Requests WHERE RequestId=@RequestId)
 BEGIN
  IF @ReceiptOnly=1 THROW 58404,N'Unavailable.',1;
  UPDATE ChatV2P.Conversations SET LastSequence=LastSequence+1 WHERE ConversationId=@ConversationId;
  SELECT @Sequence=LastSequence FROM ChatV2P.Conversations WHERE ConversationId=@ConversationId;
  INSERT ChatV2P.Messages(RequestId,ConversationId,StreamSequence,Sender,Content,TimestampUtc)
   VALUES(@RequestId,@ConversationId,@Sequence,@UsuarioId,@Content,@Now);
  SET @Message=CONVERT(bigint,SCOPE_IDENTITY());
  INSERT ChatV2P.Requests(RequestId,MessageId) VALUES(@RequestId,@Message);
  INSERT ChatV2P.Outbox(RequestId,ConversationId,CreatedUtc) VALUES(@RequestId,@ConversationId,@Now);
 END;
 -- Check the absolute session and intent deadlines after all work, before commit.
 EXEC ChatV2P.RequireAccess @LoginId,@UsuarioId,@PerfilId,@SecurityStamp,@BindingHash,@IssuedUtc,@ExpiresUtc,@ConversationId,@ActorEpoch,@ResourceEpoch;
 IF @IntentExpiresUtc<=SYSUTCDATETIME() THROW 58403,N'Intent expired.',1;
 SELECT m.MessageId,m.StreamSequence,m.TimestampUtc,m.Content FROM ChatV2P.Requests r JOIN ChatV2P.Messages m ON m.MessageId=r.MessageId WHERE r.RequestId=@RequestId;
 COMMIT; END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
END;
GO
CREATE PROCEDURE ChatV2P.History
 @LoginId uniqueidentifier,@UsuarioId int,@PerfilId int,@SecurityStamp uniqueidentifier,@BindingHash binary(32),
 @IssuedUtc datetime2(7),@ExpiresUtc datetime2(7),@ConversationId int,@After bigint=0,@Upper bigint=NULL,
 @ActorEpoch bigint,@ResourceEpoch bigint=NULL
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 BEGIN TRY BEGIN TRANSACTION;
 EXEC ChatV2P.RequireAccess @LoginId,@UsuarioId,@PerfilId,@SecurityStamp,@BindingHash,@IssuedUtc,@ExpiresUtc,@ConversationId,@ActorEpoch,@ResourceEpoch;
 IF @After<0 OR @Upper<@After THROW 58403,N'Invalid cursor.',1;
 DECLARE @ActualUpper bigint,@Version bigint;
 SELECT @ActualUpper=LastSequence,@Version=AccessVersion FROM ChatV2P.Conversations WHERE ConversationId=@ConversationId;
 IF @Upper IS NULL SET @Upper=@ActualUpper;
 IF @Upper>@ActualUpper THROW 58403,N'Invalid cursor.',1;
 SELECT TOP(51) MessageId,StreamSequence,Sender,Content,TimestampUtc FROM ChatV2P.Messages
  WHERE ConversationId=@ConversationId AND StreamSequence>@After AND StreamSequence<=@Upper ORDER BY StreamSequence;
 SELECT @Upper AS UpperBound,@Version AS ResourceEpoch;
 COMMIT; END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
END;
GO
CREATE PROCEDURE ChatV2P.Leave
 @LoginId uniqueidentifier,@UsuarioId int,@PerfilId int,@SecurityStamp uniqueidentifier,@BindingHash binary(32),
 @IssuedUtc datetime2(7),@ExpiresUtc datetime2(7),@ConversationId int
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 BEGIN TRY BEGIN TRANSACTION;
 EXEC ChatV2P.RequireAccess @LoginId,@UsuarioId,@PerfilId,@SecurityStamp,@BindingHash,@IssuedUtc,@ExpiresUtc,@ConversationId;
 UPDATE ChatV2P.Conversations SET Active=0,AccessVersion=AccessVersion+1 WHERE ConversationId=@ConversationId;
 -- No reactivation or deletion: history and original intent owners are retained.
 COMMIT; END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
END;
