# Rollback de 0221

## Qué introduce

- Tabla `dbo.NotificacionesUsuario`: avisos por usuario (BUG-08-01, BUG-10-08).
- Disparador `dbo.TR_Rutas_NotificarChofer` (AFTER INSERT, UPDATE en
  `dbo.Rutas`): avisa al chofer cuando una ruta Planificada o Despachada se le
  asigna o se le reasigna. No cambia ningún dato de `dbo.Rutas`.
- Procedimientos `sp_Notificaciones_Listar`, `sp_Notificaciones_ContarNoLeidas`
  y `sp_Notificaciones_MarcarLeidas`.

Ningún procedimiento existente cambia.

## Revertir

Primero desplegar MVC y API sin la campana ni los endpoints de notificaciones
(si no, responderían 503 al no encontrar los procedimientos). Luego:

```sql
DROP TRIGGER IF EXISTS dbo.TR_Rutas_NotificarChofer;
DROP PROCEDURE IF EXISTS dbo.sp_Notificaciones_MarcarLeidas;
DROP PROCEDURE IF EXISTS dbo.sp_Notificaciones_ContarNoLeidas;
DROP PROCEDURE IF EXISTS dbo.sp_Notificaciones_Listar;
DROP TABLE IF EXISTS dbo.NotificacionesUsuario;
DELETE FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0221_notificaciones_usuario';
```

Si solo se quiere dejar de generar avisos sin perder los existentes:

```sql
DISABLE TRIGGER dbo.TR_Rutas_NotificarChofer ON dbo.Rutas;
```
