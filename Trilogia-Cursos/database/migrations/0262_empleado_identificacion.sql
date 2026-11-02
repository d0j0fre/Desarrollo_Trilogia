SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL OR OBJECT_ID(N'dbo.Empleados',N'U') IS NULL THROW 56630,N'Faltan dependencias de empleados.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0262_empleado_identificacion' AND Status=N'Applied') THROW 56631,N'La migración ya fue aplicada.',1;
BEGIN TRANSACTION;
IF COL_LENGTH(N'dbo.Empleados',N'Identificacion') IS NULL ALTER TABLE dbo.Empleados ADD Identificacion NVARCHAR(12) NULL;
GO
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.Empleados') AND name=N'UX_Empleados_Identificacion')
 CREATE UNIQUE INDEX UX_Empleados_Identificacion ON dbo.Empleados(Identificacion) WHERE Identificacion IS NOT NULL;
DECLARE @sha NVARCHAR(128)=N'$(MigrationSha256)';
IF LEN(@sha)<>64 OR @sha LIKE N'%[^0-9A-Fa-f]%' THROW 56632,N'SHA-256 inválido.',1;
INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes)
VALUES(N'0262_empleado_identificacion',N'0262_empleado_identificacion.sql',UPPER(@sha),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),N'Identificación única; expedientes históricos permiten valor pendiente.');
COMMIT TRANSACTION;
GO
