SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL OR OBJECT_ID(N'dbo.AuditoriaSistema',N'U') IS NULL OR OBJECT_ID(N'dbo.RRHHAuditoria',N'U') IS NULL OR OBJECT_ID(N'dbo.PlanillaAuditoria',N'U') IS NULL THROW 57730,N'Faltan las tablas de auditoría requeridas.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0273_auditoria_unificada' AND Status=N'Applied') THROW 57731,N'La migración 0273 ya fue aplicada.',1;
BEGIN TRANSACTION;
GO
-- G2-29: la pantalla Auditoría solo leía dbo.AuditoriaSistema y sus listas estaban fijas.
-- sp_Admin_GetAuditLogsUnificado junta, en solo lectura y con columnas comunes, el registro general,
-- la bitácora de RR. HH. y la de planilla. sp_Admin_GetAuditLogs (usado por el panel móvil) no se toca.
CREATE OR ALTER PROCEDURE dbo.sp_Admin_GetAuditLogsUnificado
    @Origen NVARCHAR(20) = NULL,   -- todos | general | rrhh | planilla
    @Modulo NVARCHAR(80) = NULL,
    @Accion NVARCHAR(100) = NULL,
    @Buscar NVARCHAR(200) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET @Origen = LOWER(COALESCE(NULLIF(LTRIM(RTRIM(@Origen)),N''),N'todos'));
    IF @Origen NOT IN (N'todos',N'general',N'rrhh',N'planilla') THROW 57732,N'El origen de la auditoría no es válido.',1;
    SET @Modulo = NULLIF(LTRIM(RTRIM(@Modulo)),N'');
    SET @Accion = NULLIF(LTRIM(RTRIM(@Accion)),N'');
    SET @Buscar = NULLIF(LTRIM(RTRIM(@Buscar)),N'');

    ;WITH Unificado AS
    (
        SELECT N'General' AS Origen, a.AuditoriaId, a.UsuarioId, a.UsuarioNombre, a.UsuarioCorreo, a.Rol,
               a.Accion, a.Modulo, a.Descripcion, a.DireccionIp, a.UserAgent, CAST(a.FechaRegistro AS DATETIME2(7)) AS FechaRegistro
        FROM dbo.AuditoriaSistema a
        WHERE @Origen IN (N'todos',N'general')
        UNION ALL
        SELECT N'RR. HH.', r.AuditoriaId, r.ActorUsuarioId, r.ActorNombre, COALESCE(u.Correo,N'No disponible'), COALESCE(p.Nombre,N'No disponible'),
               r.Accion, N'RR. HH.', LEFT(CONCAT(r.Entidad,N' #',r.EntidadId,N': ',r.Detalle),2000), NULL, NULL, CAST(r.FechaUtc AS DATETIME2(7))
        FROM dbo.RRHHAuditoria r
        LEFT JOIN dbo.Usuarios u ON u.UsuarioId = r.ActorUsuarioId
        LEFT JOIN dbo.Perfiles p ON p.PerfilId = u.PerfilId
        WHERE @Origen IN (N'todos',N'rrhh')
        UNION ALL
        SELECT N'Planilla', x.AuditoriaId, x.ActorUsuarioId, x.ActorNombre, COALESCE(u.Correo,N'No disponible'), COALESCE(p.Nombre,N'No disponible'),
               x.Accion, N'Planilla',
               LEFT(CONCAT(x.Entidad,N' #',x.EntidadId,
                           CASE WHEN x.CalculoId IS NOT NULL AND x.Entidad <> N'Cálculo' THEN CONCAT(N'; cálculo #',x.CalculoId) ELSE N'' END,
                           CASE WHEN x.EstadoAnterior IS NOT NULL OR x.EstadoNuevo IS NOT NULL THEN CONCAT(N'; ',COALESCE(x.EstadoAnterior,N'—'),N' -> ',COALESCE(x.EstadoNuevo,N'—')) ELSE N'' END,
                           CASE WHEN x.Detalle IS NOT NULL AND x.Accion <> N'CALCULAR' THEN CONCAT(N'. ',x.Detalle) ELSE N'' END),2000),
               NULL, NULL, CAST(x.FechaUtc AS DATETIME2(7))
        FROM dbo.PlanillaAuditoria x
        LEFT JOIN dbo.Usuarios u ON u.UsuarioId = x.ActorUsuarioId
        LEFT JOIN dbo.Perfiles p ON p.PerfilId = u.PerfilId
        WHERE @Origen = N'planilla'
           -- En «todos» no se repite lo que el trigger de 0261 ya copió al registro general.
           OR (@Origen = N'todos' AND x.Accion NOT IN (N'CAMBIAR_ESTADO',N'ENVIO_EXITOSO',N'ENVIO_FALLIDO'))
    )
    SELECT TOP 200 Origen, AuditoriaId, UsuarioId, UsuarioNombre, UsuarioCorreo, Rol, Accion, Modulo, Descripcion, DireccionIp, UserAgent, FechaRegistro
    FROM Unificado
    WHERE (@Modulo IS NULL OR Modulo = @Modulo)
      AND (@Accion IS NULL OR Accion = @Accion)
      AND (@Buscar IS NULL
           OR UsuarioNombre LIKE N'%' + @Buscar + N'%'
           OR UsuarioCorreo LIKE N'%' + @Buscar + N'%'
           OR Rol LIKE N'%' + @Buscar + N'%'
           OR Accion LIKE N'%' + @Buscar + N'%'
           OR Modulo LIKE N'%' + @Buscar + N'%'
           OR Descripcion LIKE N'%' + @Buscar + N'%')
    ORDER BY FechaRegistro DESC;
END;
GO
-- Valores reales de los registros para llenar las listas Módulo y Acción.
CREATE OR ALTER PROCEDURE dbo.sp_Admin_GetAuditFilterOptions
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Tipo, Valor FROM
    (
        SELECT N'modulo' AS Tipo, Modulo AS Valor FROM dbo.AuditoriaSistema
        UNION SELECT N'modulo', N'RR. HH.' WHERE EXISTS(SELECT 1 FROM dbo.RRHHAuditoria)
        UNION SELECT N'modulo', N'Planilla' WHERE EXISTS(SELECT 1 FROM dbo.PlanillaAuditoria)
        UNION SELECT N'accion', Accion FROM dbo.AuditoriaSistema
        UNION SELECT N'accion', Accion FROM dbo.RRHHAuditoria
        UNION SELECT N'accion', Accion FROM dbo.PlanillaAuditoria
    ) opciones
    WHERE Valor IS NOT NULL AND LTRIM(RTRIM(Valor)) <> N''
    ORDER BY Tipo, Valor;
END;
GO
DECLARE @sha NVARCHAR(128)=N'$(MigrationSha256)';
IF LEN(@sha)<>64 OR @sha LIKE N'%[^0-9A-Fa-f]%' THROW 57733,N'SHA-256 inválido.',1;
INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes)
VALUES(N'0273_auditoria_unificada',N'0273_auditoria_unificada.sql',UPPER(@sha),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),N'G2-29: Auditoría unificada (general, RR. HH. y planilla) y listas de filtro con valores reales. Solo lectura.');
COMMIT TRANSACTION;
GO
