SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

/* BUG-23-05 · CU-231-02: al recibir un mensaje directo, el receptor que no
   esta conectado al chat no recibe ningun aviso en su celular.

   TR_ChatMensajes_NotificarReceptor (AFTER INSERT en dbo.ChatMensajes) deja
   un aviso en dbo.NotificacionesUsuario (0221) para el otro participante de
   la conversacion privada. La aplicacion movil lo muestra como notificacion
   del sistema y en su bandeja; el sitio web, en la campana.

   Va en un disparador para no modificar sp_Chat_SendMessage (chat interno,
   otro bloque). Ese procedimiento toma el identificador con SCOPE_IDENTITY(),
   que no se ve afectado por el disparador.

   Depende de 0221. Aditiva. Rollback en 0222_notificacion_mensaje_directo.rollback.md. */
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL THROW 56230,N'Falta el ledger 0001.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0222_notificacion_mensaje_directo' AND Status=N'Applied')
    THROW 56231,N'0222 ya figura aplicada.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0221_notificaciones_usuario' AND Status=N'Applied')
    THROW 56232,N'0222 depende de 0221 (NotificacionesUsuario). Aplicar 0221 primero.',1;
IF OBJECT_ID(N'dbo.ChatMensajes',N'U') IS NULL OR OBJECT_ID(N'dbo.ChatConversaciones',N'U') IS NULL
    THROW 56233,N'Faltan las tablas del chat privado (migración 0002).',1;

BEGIN TRANSACTION;
GO

CREATE OR ALTER TRIGGER dbo.TR_ChatMensajes_NotificarReceptor
ON dbo.ChatMensajes
AFTER INSERT
AS
BEGIN
    SET NOCOUNT ON;

    INSERT dbo.NotificacionesUsuario (UsuarioId, Tipo, Titulo, Mensaje, EntidadTipo, EntidadId)
    SELECT CASE WHEN c.UsuarioMenorId = i.RemitenteId THEN c.UsuarioMayorId ELSE c.UsuarioMenorId END,
           N'ChatMensaje',
           LEFT(CONCAT(N'Mensaje de ', ISNULL(r.NombreCompleto, N'un compañero')), 150),
           CASE WHEN LEN(i.Contenido) > 140 THEN CONCAT(LEFT(i.Contenido, 140), N'…') ELSE i.Contenido END,
           N'Chat',
           i.ConversacionId
    FROM inserted i
    INNER JOIN dbo.ChatConversaciones c ON c.ConversacionId = i.ConversacionId
    LEFT JOIN dbo.Usuarios r ON r.UsuarioId = i.RemitenteId
    WHERE c.UsuarioMenorId <> c.UsuarioMayorId;
END;
GO

IF XACT_STATE()<>1 THROW 56234,N'La transacción 0222 no está disponible.',1;
DECLARE @MigrationSha256 NVARCHAR(128)=N'$(MigrationSha256)';
IF LEN(@MigrationSha256)<>64 OR @MigrationSha256 LIKE N'%[^0-9A-Fa-f]%' THROW 56235,N'SHA-256 inválido para 0222.',1;
INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes)
VALUES(N'0222_notificacion_mensaje_directo',N'0222_notificacion_mensaje_directo.sql',UPPER(@MigrationSha256),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),
       N'BUG-23-05: TR_ChatMensajes_NotificarReceptor avisa al receptor de un mensaje directo. Aditiva.');
COMMIT TRANSACTION;
GO
