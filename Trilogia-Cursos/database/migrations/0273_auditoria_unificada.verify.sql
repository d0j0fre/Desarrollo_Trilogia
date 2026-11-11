SET NOCOUNT ON;
IF OBJECT_ID(N'dbo.sp_Admin_GetAuditLogsUnificado',N'P') IS NULL THROW 57734,N'Falta sp_Admin_GetAuditLogsUnificado.',1;
IF OBJECT_ID(N'dbo.sp_Admin_GetAuditFilterOptions',N'P') IS NULL THROW 57735,N'Falta sp_Admin_GetAuditFilterOptions.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0273_auditoria_unificada' AND Status=N'Applied') THROW 57736,N'La migración 0273 no está registrada.',1;
-- Las tres vistas ejecutan sin error y un origen inválido se rechaza.
DECLARE @t TABLE(Origen NVARCHAR(20),AuditoriaId BIGINT,UsuarioId INT,UsuarioNombre NVARCHAR(300),UsuarioCorreo NVARCHAR(300),Rol NVARCHAR(100),Accion NVARCHAR(160),Modulo NVARCHAR(160),Descripcion NVARCHAR(2000),DireccionIp NVARCHAR(160),UserAgent NVARCHAR(600),FechaRegistro DATETIME2(7));
INSERT @t EXEC dbo.sp_Admin_GetAuditLogsUnificado @Origen=N'todos';
DELETE @t; INSERT @t EXEC dbo.sp_Admin_GetAuditLogsUnificado @Origen=N'general';
DELETE @t; INSERT @t EXEC dbo.sp_Admin_GetAuditLogsUnificado @Origen=N'rrhh';
DELETE @t; INSERT @t EXEC dbo.sp_Admin_GetAuditLogsUnificado @Origen=N'planilla';
DECLARE @ok BIT=0;
BEGIN TRY EXEC dbo.sp_Admin_GetAuditLogsUnificado @Origen=N'otro'; END TRY BEGIN CATCH IF ERROR_NUMBER()=57732 SET @ok=1; END CATCH;
IF @ok=0 THROW 57737,N'Un origen inválido debe rechazarse.',1;
DECLARE @o TABLE(Tipo NVARCHAR(10),Valor NVARCHAR(200));
INSERT @o EXEC dbo.sp_Admin_GetAuditFilterOptions;
IF EXISTS(SELECT 1 FROM dbo.AuditoriaSistema a WHERE NOT EXISTS(SELECT 1 FROM @o x WHERE x.Tipo=N'modulo' AND x.Valor=a.Modulo)) THROW 57738,N'Faltan módulos del registro general en la lista.',1;
PRINT N'Verificación 0273 completada.';
