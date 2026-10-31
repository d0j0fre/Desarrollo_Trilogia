SET NOCOUNT ON;

/* Verificacion de 0221. Solo lectura. */
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0221_notificaciones_usuario' AND Status = N'Applied')
    THROW 56270, N'0221 no figura aplicada en el ledger.', 1;
IF OBJECT_ID(N'dbo.NotificacionesUsuario', N'U') IS NULL
    THROW 56271, N'Falta dbo.NotificacionesUsuario.', 1;
IF OBJECT_ID(N'dbo.TR_Rutas_NotificarChofer', N'TR') IS NULL
    THROW 56272, N'Falta el disparador dbo.TR_Rutas_NotificarChofer.', 1;
IF EXISTS (SELECT 1 FROM sys.triggers WHERE name = N'TR_Rutas_NotificarChofer' AND is_disabled = 1)
    THROW 56273, N'El disparador TR_Rutas_NotificarChofer está deshabilitado.', 1;
IF OBJECT_ID(N'dbo.sp_Notificaciones_Listar', N'P') IS NULL
   OR OBJECT_ID(N'dbo.sp_Notificaciones_ContarNoLeidas', N'P') IS NULL
   OR OBJECT_ID(N'dbo.sp_Notificaciones_MarcarLeidas', N'P') IS NULL
    THROW 56274, N'Falta al menos un procedimiento sp_Notificaciones_*.', 1;

/* Un procedimiento que use OUTPUT sin INTO o @@IDENTITY sobre dbo.Rutas
   fallaria o leeria mal con el disparador. Debe salir vacio. */
SELECT p.name AS ProcedimientoARevisar
FROM sys.procedures p
INNER JOIN sys.sql_modules m ON m.object_id = p.object_id
WHERE m.definition LIKE N'%dbo.Rutas%'
  AND (m.definition LIKE N'%@@IDENTITY%' OR m.definition LIKE N'%OUTPUT inserted%');

SELECT TOP 10 n.NotificacionId, n.UsuarioId, n.Tipo, n.Titulo, n.Leida, n.FechaCreacion
FROM dbo.NotificacionesUsuario n
ORDER BY n.NotificacionId DESC;

PRINT N'0221 verificada.';
