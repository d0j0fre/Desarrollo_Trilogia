# Reversión de 0311

Reversión compensatoria, sin pérdida de datos:

1. `DROP TRIGGER dbo.tr_Usuarios_RevocarSesiones;` — las sesiones dejan de revocarse por cambio de contraseña o de perfil. La sesión web conserva la revocación por usuario inactivo o perfil renombrado (BUG-11-06, middleware `SessionStatusMiddleware`), pero la web nueva exige el sello: antes de este paso hay que volver a desplegar el MVC y la API anteriores.
2. `DROP PROCEDURE dbo.sp_Auth_GetSessionState;` y `dbo.sp_Auth_ActualizarHash;`.
3. Volver a definir `dbo.sp_Auth_GetLoginCredential` y `dbo.sp_Auth_GetUsuarioActivo` como en 0310 y 0024 (sin `SecurityStamp`).
4. La columna `Usuarios.SecurityStamp` puede conservarse (es inocua); si se retira: `ALTER TABLE dbo.Usuarios DROP CONSTRAINT DF_Usuarios_SecurityStamp; ALTER TABLE dbo.Usuarios DROP COLUMN SecurityStamp;`.
5. Marcar la fila `0311_revocacion_sesiones` como `RolledBack` en `dbo.SchemaMigrationHistory`.

Orden de despliegue (adelante): migraciones 0310 y 0311, luego la API y por último el MVC. Las sesiones web abiertas antes del despliegue no tienen sello y se cierran una vez: los usuarios vuelven a iniciar sesión.
