SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL OR OBJECT_ID(N'dbo.PlanillaAuditoria',N'U') IS NULL OR OBJECT_ID(N'dbo.AuditoriaSistema',N'U') IS NULL THROW 56620,N'Faltan las tablas de auditoría requeridas.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0261_auditoria_planilla' AND Status=N'Applied') THROW 56621,N'La migración ya fue aplicada.',1;
BEGIN TRANSACTION;
GO
CREATE OR ALTER TRIGGER dbo.tr_PlanillaAuditoria_AuditoriaSistema ON dbo.PlanillaAuditoria
AFTER INSERT AS
BEGIN
 SET NOCOUNT ON;
 INSERT dbo.AuditoriaSistema(UsuarioId,UsuarioNombre,UsuarioCorreo,Rol,Accion,Modulo,Descripcion,FechaRegistro)
 SELECT i.ActorUsuarioId,i.ActorNombre,COALESCE(u.Correo,N'No disponible'),COALESCE(p.Nombre,N'No disponible'),
        LEFT(i.Accion,80),N'Planilla',LEFT(CONCAT(N'Cálculo #',i.CalculoId,N'; ',i.Entidad,N'; ',i.EstadoAnterior,N' -> ',i.EstadoNuevo,N'. ',i.Detalle),500),i.FechaUtc
 FROM inserted i LEFT JOIN dbo.Usuarios u ON u.UsuarioId=i.ActorUsuarioId LEFT JOIN dbo.Perfiles p ON p.PerfilId=u.PerfilId
 WHERE i.Accion IN(N'CAMBIAR_ESTADO',N'ENVIO_EXITOSO',N'ENVIO_FALLIDO');
END;
GO
DECLARE @sha NVARCHAR(128)=N'$(MigrationSha256)';
IF LEN(@sha)<>64 OR @sha LIKE N'%[^0-9A-Fa-f]%' THROW 56622,N'SHA-256 inválido.',1;
INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes)
VALUES(N'0261_auditoria_planilla',N'0261_auditoria_planilla.sql',UPPER(@sha),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),N'Refleja cambios de estado y envíos de boletas en auditoría general.');
COMMIT TRANSACTION;
GO
