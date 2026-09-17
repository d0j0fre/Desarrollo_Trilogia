SET NOCOUNT ON;

IF OBJECT_ID(N'dbo.AppMovilVersiones', N'U') IS NULL
    THROW 55020, N'Falta la tabla dbo.AppMovilVersiones.', 1;
IF OBJECT_ID(N'dbo.sp_AppMovil_GetVigente', N'P') IS NULL
    THROW 55021, N'Falta sp_AppMovil_GetVigente.', 1;
IF OBJECT_ID(N'dbo.sp_AppMovil_List', N'P') IS NULL
    THROW 55022, N'Falta sp_AppMovil_List.', 1;
IF OBJECT_ID(N'dbo.sp_AppMovil_Publicar', N'P') IS NULL
    THROW 55023, N'Falta sp_AppMovil_Publicar.', 1;
IF OBJECT_ID(N'dbo.sp_AppMovil_Despublicar', N'P') IS NULL
    THROW 55024, N'Falta sp_AppMovil_Despublicar.', 1;

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints
               WHERE name = N'CK_AppMovilVersiones_Url'
                 AND parent_object_id = OBJECT_ID(N'dbo.AppMovilVersiones'))
    THROW 55025, N'Falta la restricción que obliga a que la descarga sea HTTPS.', 1;

IF NOT EXISTS (SELECT 1 FROM dbo.Permisos WHERE Codigo = N'MOVIL_APP_PUBLICAR' AND Activo = 1)
    THROW 55026, N'Falta el permiso MOVIL_APP_PUBLICAR.', 1;

IF NOT EXISTS (
    SELECT 1 FROM dbo.SchemaMigrationHistory
    WHERE MigrationId = N'0026_mobile_app_distribution' AND Status = N'Applied')
    THROW 55027, N'0026 no figura aplicada en el ledger.', 1;
GO

/* Recien aplicada no hay ninguna version publicada: es lo esperado. */
SELECT COUNT(*) AS VersionesPublicadas
FROM dbo.AppMovilVersiones
WHERE Publicada = 1;
GO

SELECT MigrationId, FileName, Status, AppliedAtUtc
FROM dbo.SchemaMigrationHistory
WHERE MigrationId = N'0026_mobile_app_distribution';
GO
