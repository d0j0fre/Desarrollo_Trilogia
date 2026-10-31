SET NOCOUNT ON;

/* Verificacion de 0222. Solo lectura. */
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0222_notificacion_mensaje_directo' AND Status = N'Applied')
    THROW 56280, N'0222 no figura aplicada en el ledger.', 1;
IF OBJECT_ID(N'dbo.TR_ChatMensajes_NotificarReceptor', N'TR') IS NULL
    THROW 56281, N'Falta el disparador dbo.TR_ChatMensajes_NotificarReceptor.', 1;
IF EXISTS (SELECT 1 FROM sys.triggers WHERE name = N'TR_ChatMensajes_NotificarReceptor' AND is_disabled = 1)
    THROW 56282, N'El disparador TR_ChatMensajes_NotificarReceptor está deshabilitado.', 1;

/* Un procedimiento que use OUTPUT sin INTO o @@IDENTITY al insertar en
   dbo.ChatMensajes fallaria o leeria mal con el disparador. Debe salir vacio. */
SELECT p.name AS ProcedimientoARevisar
FROM sys.procedures p
INNER JOIN sys.sql_modules m ON m.object_id = p.object_id
WHERE m.definition LIKE N'%INSERT%ChatMensajes%'
  AND (m.definition LIKE N'%@@IDENTITY%' OR m.definition LIKE N'%OUTPUT inserted%');

SELECT TOP 10 n.NotificacionId, n.UsuarioId, n.Titulo, n.EntidadId AS ConversacionId, n.Leida, n.FechaCreacion
FROM dbo.NotificacionesUsuario n
WHERE n.Tipo = N'ChatMensaje'
ORDER BY n.NotificacionId DESC;

PRINT N'0222 verificada.';
