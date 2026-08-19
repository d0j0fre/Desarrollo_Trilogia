SET NOCOUNT ON;

IF OBJECT_ID(N'dbo.sp_Kilometraje_Cerrar', N'P') IS NULL
    THROW 55244, N'No existe sp_Kilometraje_Cerrar.', 1;
IF NOT EXISTS (SELECT 1 FROM sys.parameters WHERE object_id = OBJECT_ID(N'dbo.sp_Kilometraje_Cerrar') AND name = N'@ActorUsuarioId')
    THROW 55245, N'Falta el actor en sp_Kilometraje_Cerrar.', 1;
IF NOT EXISTS (SELECT 1 FROM sys.parameters WHERE object_id = OBJECT_ID(N'dbo.sp_Kilometraje_Cerrar') AND name = N'@PuedeAdministrar')
    THROW 55246, N'Falta la capacidad administrativa en sp_Kilometraje_Cerrar.', 1;
IF NOT EXISTS (SELECT 1 FROM dbo.Permisos WHERE Codigo = N'FLOTA_KILOMETRAJE_ADMIN' AND Activo = 1)
    THROW 55247, N'Falta el permiso administrativo de kilometraje.', 1;

SELECT MigrationId, FileName, FileSha256, Status, AppliedAtUtc
FROM dbo.SchemaMigrationHistory
WHERE MigrationId = N'0024_stage1_ownership_security';
