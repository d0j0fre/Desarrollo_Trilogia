SET NOCOUNT ON;

/* Verificacion de 0243. Solo lectura. */
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0243_comodato_alerta_averia' AND Status = N'Applied')
    THROW 56446, N'0243 no figura aplicada en el ledger.', 1;
IF NOT EXISTS (SELECT 1 FROM sys.sql_modules
               WHERE object_id = OBJECT_ID(N'dbo.sp_Comodato_Devolver')
                 AND definition LIKE N'%ComodatoAveria%')
    THROW 56447, N'sp_Comodato_Devolver no genera el aviso ComodatoAveria.', 1;

/* Quiénes recibirían hoy el aviso de avería (debe haber al menos uno). */
SELECT u.UsuarioId, u.Correo, p.Nombre AS Perfil
FROM dbo.Usuarios u
INNER JOIN dbo.Perfiles p ON p.PerfilId = u.PerfilId
WHERE ISNULL(u.Activo, 1) = 1
  AND (EXISTS (SELECT 1 FROM dbo.PerfilPermisos pp INNER JOIN dbo.Permisos pe ON pe.PermisoId = pp.PermisoId
               WHERE pp.PerfilId = u.PerfilId AND pe.Activo = 1
                 AND pe.Codigo IN (N'FLOTA_MANTENIMIENTO', N'ACTIVOS_GESTIONAR', N'CREDITOS_MOVIMIENTOS'))
       OR p.Nombre = N'Crédito y Cobro')
ORDER BY p.Nombre, u.UsuarioId;

SELECT TOP 10 NotificacionId, UsuarioId, EntidadId AS ActivoId, Leida, FechaCreacion
FROM dbo.NotificacionesUsuario WHERE Tipo = N'ComodatoAveria' ORDER BY NotificacionId DESC;

PRINT N'0243 verificada.';
