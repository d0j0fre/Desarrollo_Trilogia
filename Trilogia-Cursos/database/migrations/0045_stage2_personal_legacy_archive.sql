-- Additive read-only archive for original personal chat; no import, rewrite or grants.
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF SESSIONPROPERTY('QUOTED_IDENTIFIER')<>1 OR SESSIONPROPERTY('ANSI_NULLS')<>1 THROW 58500,N'SQL options not accredited.',1;
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL THROW 58500,N'Migration ledger missing.',1;
DECLARE @Hash nvarchar(128)=N'$(MigrationSha256)';
IF LEN(@Hash)<>64 OR @Hash LIKE N'%[^0-9a-fA-F]%' THROW 58500,N'Invalid migration hash.',1;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @Lock int;
 EXEC @Lock=sys.sp_getapplock @Resource=N'Stage2:0045:install',@LockMode=N'Exclusive',@LockOwner=N'Transaction',@LockTimeout=10000;
 IF @Lock<0 THROW 58500,N'Concurrent installation.',1;
 IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0045_stage2_personal_legacy_archive')
 BEGIN
  IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0045_stage2_personal_legacy_archive' AND Status=N'Applied' AND FileSha256=UPPER(@Hash)) THROW 58500,N'Ledger conflict.',1;
  COMMIT; RETURN;
 END;
 IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId LIKE N'0045%') THROW 58500,N'Migration identifier collision.',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0044_stage2_chat_v2_personal' AND Status=N'Applied' AND FileSha256=N'91D1FCD83BA9E39A903B97CF7C45054C554E079626B21ECD1D84303E21642922') THROW 58500,N'0044 prerequisite not accredited.',1;
 IF SCHEMA_ID(N'ChatV2Archive') IS NOT NULL THROW 58500,N'Existing archive requires reconciliation.',1;
 IF OBJECT_ID(N'dbo.sp_Stage2_RequireOriginalLogin',N'P') IS NULL THROW 58500,N'Original login contract missing.',1;
 IF (SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.ChatConversaciones') AND name IN(N'ConversacionId',N'UsuarioUnoId',N'UsuarioDosId') AND system_type_id=56 AND is_nullable=0)<>3
 OR (SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.ChatMensajes') AND name IN(N'MensajeId',N'ConversacionId',N'RemitenteId') AND system_type_id=56 AND is_nullable=0)<>3
 OR NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.ChatMensajes') AND name=N'Contenido' AND system_type_id=231 AND is_nullable=0)
 OR NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.ChatMensajes') AND name=N'FechaEnvio' AND system_type_id=42 AND is_nullable=0)
 OR NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.ChatMensajes') AND name=N'Leido' AND system_type_id=104 AND is_nullable=0)
 THROW 58500,N'Legacy archive shape not accredited.',1;
 EXEC(N'CREATE SCHEMA ChatV2Archive AUTHORIZATION dbo');
 EXEC(N'CREATE PROCEDURE ChatV2Archive.Own
 @LoginId uniqueidentifier,@UsuarioId int,@PerfilId int,@SecurityStamp uniqueidentifier,@BindingHash binary(32),
 @IssuedUtc datetime2(7),@ExpiresUtc datetime2(7)
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 BEGIN TRY BEGIN TRANSACTION;
 EXEC dbo.sp_Stage2_RequireOriginalLogin @LoginId,@UsuarioId,@PerfilId,@SecurityStamp,@BindingHash,@IssuedUtc,@ExpiresUtc,N''CHAT_USAR'';
 IF EXISTS(SELECT 1 FROM dbo.Perfiles WHERE PerfilId=@PerfilId AND Nombre=N''Cliente'') THROW 56802,N''Unavailable.'',1;
 SELECT TOP(100) ConversacionId FROM dbo.ChatConversaciones WITH(UPDLOCK,HOLDLOCK)
 WHERE @UsuarioId IN(UsuarioUnoId,UsuarioDosId) ORDER BY ConversacionId DESC;
 COMMIT; END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
END;');
 EXEC(N'CREATE PROCEDURE ChatV2Archive.History
 @LoginId uniqueidentifier,@UsuarioId int,@PerfilId int,@SecurityStamp uniqueidentifier,@BindingHash binary(32),
 @IssuedUtc datetime2(7),@ExpiresUtc datetime2(7),@ConversationId int,@Page int=1,@PageSize int=50
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 IF @Page<=0 OR @PageSize<1 OR @PageSize>100 THROW 58504,N''Unavailable.'',1;
 BEGIN TRY BEGIN TRANSACTION;
 EXEC dbo.sp_Stage2_RequireOriginalLogin @LoginId,@UsuarioId,@PerfilId,@SecurityStamp,@BindingHash,@IssuedUtc,@ExpiresUtc,N''CHAT_USAR'';
 IF EXISTS(SELECT 1 FROM dbo.Perfiles WHERE PerfilId=@PerfilId AND Nombre=N''Cliente'') THROW 56802,N''Unavailable.'',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.ChatConversaciones WITH(UPDLOCK,HOLDLOCK)
  WHERE ConversacionId=@ConversationId AND @UsuarioId IN(UsuarioUnoId,UsuarioDosId)) THROW 58504,N''Unavailable.'',1;
 SELECT MensajeId,ConversacionId,RemitenteId,Contenido,FechaEnvio,Leido FROM dbo.ChatMensajes
 WHERE ConversacionId=@ConversationId ORDER BY FechaEnvio DESC,MensajeId DESC
 OFFSET (CONVERT(bigint,@Page)-1)*@PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;
 COMMIT; END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
END;');
 INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes)
 VALUES(N'0045_stage2_personal_legacy_archive',N'0045_stage2_personal_legacy_archive.sql',UPPER(@Hash),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),N'Read-only original personal history. Canonical login and membership; no data rewrite, assignment, grants or legacy fallback.');
 COMMIT;
END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;