SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL OR OBJECT_ID(N'dbo.ChatMensajes',N'U') IS NULL OR COL_LENGTH(N'dbo.ChatMensajes',N'Leido') IS NULL THROW 57990,N'Faltan dependencias del chat.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0299_chat_marcar_leidos' AND Status=N'Applied') THROW 57991,N'La migracion ya fue aplicada.',1;
BEGIN TRANSACTION;
GO
-- BUG-23-01: el destinatario que tiene la conversacion abierta marca como recibidos (Leido = 1)
-- los mensajes del otro participante; la pantalla del emisor los muestra con doble check.
-- Devuelve cuantos mensajes cambiaron de estado.
CREATE OR ALTER PROCEDURE dbo.sp_Chat_MarcarLeidos
    @ConversacionId INT,
    @UsuarioId INT
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS
    (
        SELECT 1 FROM dbo.ChatConversaciones
        WHERE ConversacionId = @ConversacionId
          AND @UsuarioId IN (UsuarioMenorId, UsuarioMayorId)
    )
        THROW 51005, N'El usuario no pertenece a la conversacion.', 1;

    UPDATE dbo.ChatMensajes
    SET Leido = 1
    WHERE ConversacionId = @ConversacionId
      AND RemitenteId <> @UsuarioId
      AND Leido = 0;

    SELECT CONVERT(INT, @@ROWCOUNT) AS MensajesMarcados;
END;
GO
DECLARE @sha NVARCHAR(128)=N'$(MigrationSha256)';
IF LEN(@sha)<>64 OR @sha LIKE N'%[^0-9A-Fa-f]%' THROW 57992,N'SHA-256 invalido.',1;
INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes) VALUES(N'0299_chat_marcar_leidos',N'0299_chat_marcar_leidos.sql',UPPER(@sha),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),N'BUG-23-01: marcar mensajes directos como recibidos para el doble check.');
COMMIT TRANSACTION;
GO
