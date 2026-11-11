SET NOCOUNT ON;
IF OBJECT_ID(N'dbo.sp_Chat_MarcarLeidos',N'P') IS NULL THROW 57993,N'Falta sp_Chat_MarcarLeidos.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0299_chat_marcar_leidos' AND Status=N'Applied') THROW 57994,N'La migracion 0299 no esta registrada.',1;
-- Prueba funcional deshecha: un mensaje del menor se marca recibido solo cuando lo abre el mayor.
DECLARE @c INT,@menor INT,@mayor INT;
SELECT TOP (1) @c=ConversacionId,@menor=UsuarioMenorId,@mayor=UsuarioMayorId FROM dbo.ChatConversaciones ORDER BY ConversacionId;
IF @c IS NOT NULL
BEGIN
    BEGIN TRANSACTION;
    INSERT dbo.ChatMensajes(ConversacionId,RemitenteId,Contenido) VALUES(@c,@menor,N'verificacion 0299');
    DECLARE @r TABLE(MensajesMarcados INT);
    INSERT @r EXEC dbo.sp_Chat_MarcarLeidos @ConversacionId=@c,@UsuarioId=@menor;   -- el propio emisor no lo marca
    IF EXISTS(SELECT 1 FROM dbo.ChatMensajes WHERE ConversacionId=@c AND Contenido=N'verificacion 0299' AND Leido=1) THROW 57995,N'El emisor marco su propio mensaje.',1;
    INSERT @r EXEC dbo.sp_Chat_MarcarLeidos @ConversacionId=@c,@UsuarioId=@mayor;
    IF NOT EXISTS(SELECT 1 FROM dbo.ChatMensajes WHERE ConversacionId=@c AND Contenido=N'verificacion 0299' AND Leido=1) THROW 57996,N'El destinatario no marco el mensaje.',1;
    ROLLBACK TRANSACTION;
END;
PRINT N'Verificacion 0299 completada.';
