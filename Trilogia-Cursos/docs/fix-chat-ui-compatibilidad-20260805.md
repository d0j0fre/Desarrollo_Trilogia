# Corrección de compatibilidad visual del chat

## Problema

La reescritura de `wwwroot/js/chat.js` cambió la estructura generada para mensajes, formulario, búsqueda y avisos, pero `wwwroot/css/chat.css` todavía estaba orientado principalmente al marcado anterior. Esto causaba pérdida de contención, mensajes sin burbuja y controles desalineados.

## Corrección

Se consolidaron los estilos para soportar tanto las clases anteriores como las nuevas:

- `.chat-message.sent` y `.chat-message.received`
- `.chat-input-area`
- `.chat-inline-notice`
- `.chat-search-result`
- `.chat-load-older` y `.chat-search-more`

También se eliminó CSS duplicado y reglas administrativas que no pertenecían al archivo del chat.

## Validación recomendada

1. Iniciar sesión con dos usuarios autorizados.
2. Abrir chat privado y comprobar lista, búsqueda y conversación.
3. Enviar mensajes desde ambos usuarios y validar SignalR.
4. Abrir un departamento con permiso de publicación y otro de solo lectura.
5. Probar la vista en escritorio y móvil.
6. Confirmar que las migraciones `0002_chat_private_security.sql` y `0003_chat_departments_and_search.sql` estén aplicadas en la base utilizada.
