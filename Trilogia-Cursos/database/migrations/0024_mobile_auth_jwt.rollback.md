# Rollback de 0024

## Qué introduce la migración

Objetos **nuevos** únicamente. No altera ni elimina tablas, columnas,
procedimientos, perfiles ni permisos existentes:

- Tabla `dbo.UsuarioTokensRefresco` (vacía al aplicarse) con dos índices.
- `dbo.sp_Auth_GetPermisosPorPerfil` y `dbo.sp_Auth_GetUsuarioActivo` — solo lectura.
- `dbo.sp_Auth_EmitirTokenRefresco`, `dbo.sp_Auth_CanjearTokenRefresco`,
  `dbo.sp_Auth_RevocarCadenaTokenRefresco`, `dbo.sp_Auth_RevocarTokensUsuario`,
  `dbo.sp_Auth_PurgarTokensRefresco` — escriben **solo** sobre la tabla nueva.

`dbo.sp_Auth_GetLoginCredential` no se modifica, así que el login del MVC y el del
API siguen funcionando exactamente igual con la migración aplicada o sin ella.

## Rollback ordinario

Retirar la configuración `Jwt` del App Service del API. Sin clave de firma la API
no emite ni valida tokens y los endpoints móviles quedan inaccesibles, mientras el
resto del sistema sigue intacto. No requiere tocar la base.

## Rollback completo

Solo si hay que dejar la base como antes. Revoca toda sesión móvil activa: los
usuarios tendrán que volver a iniciar sesión cuando se reinstale.

```sql
SET XACT_ABORT ON;
BEGIN TRANSACTION;

DROP PROCEDURE IF EXISTS dbo.sp_Auth_PurgarTokensRefresco;
DROP PROCEDURE IF EXISTS dbo.sp_Auth_RevocarTokensUsuario;
DROP PROCEDURE IF EXISTS dbo.sp_Auth_RevocarCadenaTokenRefresco;
DROP PROCEDURE IF EXISTS dbo.sp_Auth_CanjearTokenRefresco;
DROP PROCEDURE IF EXISTS dbo.sp_Auth_EmitirTokenRefresco;
DROP PROCEDURE IF EXISTS dbo.sp_Auth_GetUsuarioActivo;
DROP PROCEDURE IF EXISTS dbo.sp_Auth_GetPermisosPorPerfil;
DROP TABLE IF EXISTS dbo.UsuarioTokensRefresco;

UPDATE dbo.SchemaMigrationHistory
SET Status = N'RolledBack'
WHERE MigrationId = N'0024_mobile_auth_jwt';

COMMIT TRANSACTION;
```

`DROP TABLE` sobre `UsuarioTokensRefresco` solo elimina hashes de tokens; no hay
datos de negocio en ella. La clave de firma JWT vive fuera de la base, en
App Service Configuration, y debe rotarse aparte si el rollback responde a un
incidente de seguridad.

## Restaurar BACPAC

Último recurso, con aprobación expresa. Esta migración no lo justifica por sí sola:
es aditiva y su rollback completo es determinista.
