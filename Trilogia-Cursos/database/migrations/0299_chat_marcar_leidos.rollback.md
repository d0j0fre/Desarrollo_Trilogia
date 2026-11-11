# Reversión de 0299

`DROP PROCEDURE dbo.sp_Chat_MarcarLeidos;` No cambia tablas ni datos. Sin el procedimiento el método `MarkConversationRead` del hub falla (se registra y no se avisa al emisor) y los mensajes enviados dejan de mostrar el doble check al instante; el estado `Leido` se sigue actualizando cuando el destinatario carga la conversación (`sp_Chat_GetMessages`). Marcar la fila `0299_chat_marcar_leidos` como `RolledBack` en `dbo.SchemaMigrationHistory`.
