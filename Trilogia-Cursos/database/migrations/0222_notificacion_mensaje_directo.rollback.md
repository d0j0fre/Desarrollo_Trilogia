# Rollback de 0222

## Qué introduce

- Disparador `dbo.TR_ChatMensajes_NotificarReceptor` (AFTER INSERT en
  `dbo.ChatMensajes`): deja un aviso `ChatMensaje` en
  `dbo.NotificacionesUsuario` (0221) para el receptor de un mensaje directo
  (BUG-23-05). No cambia ningún dato del chat.

Ningún procedimiento existente cambia. `sp_Chat_SendMessage` usa
`SCOPE_IDENTITY()`, que no se ve afectado por el disparador.

## Revertir

```sql
DROP TRIGGER IF EXISTS dbo.TR_ChatMensajes_NotificarReceptor;
DELETE FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0222_notificacion_mensaje_directo';
```

Opcional, para quitar los avisos ya generados:

```sql
DELETE FROM dbo.NotificacionesUsuario WHERE Tipo = N'ChatMensaje';
```
