SET NOCOUNT ON;
SET XACT_ABORT ON;
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL OR OBJECT_ID(N'dbo.Empleados',N'U') IS NULL THROW 56640,N'Faltan dependencias.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0263_vigencia_salarial' AND Status=N'Applied') THROW 56641,N'La migración ya fue aplicada.',1;
BEGIN TRANSACTION;
CREATE TABLE dbo.EmpleadoSalarioVigencias
(
 EmpleadoId INT NOT NULL REFERENCES dbo.Empleados(EmpleadoId), FechaVigencia DATE NOT NULL, Salario DECIMAL(18,2) NOT NULL,
 Motivo NVARCHAR(255) NULL, ActorUsuarioId INT NULL, ActorNombre NVARCHAR(150) NULL,
 CONSTRAINT PK_EmpleadoSalarioVigencias PRIMARY KEY(EmpleadoId,FechaVigencia), CONSTRAINT CK_EmpleadoSalarioVigencias_Salario CHECK(Salario>=0)
);
INSERT dbo.EmpleadoSalarioVigencias(EmpleadoId,FechaVigencia,Salario,Motivo)
SELECT e.EmpleadoId,CONVERT(date,'19000101',112),COALESCE((SELECT TOP 1 h.SalarioAnterior FROM dbo.EmpleadoHistorialSalarios h WHERE h.EmpleadoId=e.EmpleadoId AND h.SalarioAnterior IS NOT NULL ORDER BY h.FechaCambio,h.HistorialSalarioId),e.Salario,0),N'Saldo inicial migrado' FROM dbo.Empleados e;
INSERT dbo.EmpleadoSalarioVigencias(EmpleadoId,FechaVigencia,Salario,Motivo,ActorUsuarioId,ActorNombre)
SELECT h.EmpleadoId,CONVERT(date,h.FechaCambio),h.SalarioNuevo,h.Motivo,h.UsuarioCambioId,h.UsuarioCambioNombre
FROM dbo.EmpleadoHistorialSalarios h WHERE h.HistorialSalarioId=(SELECT TOP 1 h2.HistorialSalarioId FROM dbo.EmpleadoHistorialSalarios h2 WHERE h2.EmpleadoId=h.EmpleadoId AND CONVERT(date,h2.FechaCambio)=CONVERT(date,h.FechaCambio) ORDER BY h2.FechaCambio DESC,h2.HistorialSalarioId DESC)
AND CONVERT(date,h.FechaCambio)>CONVERT(date,'19000101',112);
DECLARE @sha NVARCHAR(128)=N'$(MigrationSha256)';
IF LEN(@sha)<>64 OR @sha LIKE N'%[^0-9A-Fa-f]%' THROW 56642,N'SHA-256 inválido.',1;
INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes)
VALUES(N'0263_vigencia_salarial',N'0263_vigencia_salarial.sql',UPPER(@sha),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),N'Historial por fecha efectiva; historia anterior usa fecha registrada.');
COMMIT TRANSACTION;
GO
