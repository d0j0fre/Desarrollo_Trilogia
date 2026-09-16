SET NOCOUNT ON;

IF COL_LENGTH(N'dbo.AppMovilVersiones', N'ArchivoAlmacenado') IS NULL
    THROW 55120, N'Falta la columna ArchivoAlmacenado.', 1;
IF COL_LENGTH(N'dbo.AppMovilVersiones', N'ArchivoNombre') IS NULL
    THROW 55121, N'Falta la columna ArchivoNombre.', 1;

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_AppMovilVersiones_Origen')
    THROW 55122, N'Falta la restriccion que exige archivo o direccion HTTPS.', 1;
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_AppMovilVersiones_Url')
    THROW 55123, N'La restriccion antigua de URL obligatoria sigue presente.', 1;

IF EXISTS (SELECT 1 FROM sys.columns
           WHERE object_id = OBJECT_ID(N'dbo.AppMovilVersiones')
             AND name = N'UrlDescarga' AND is_nullable = 0)
    THROW 55124, N'UrlDescarga deberia admitir nulos.', 1;

IF OBJECT_ID(N'dbo.sp_AppMovil_GetArchivo', N'P') IS NULL
    THROW 55125, N'Falta sp_AppMovil_GetArchivo.', 1;

IF NOT EXISTS (
    SELECT 1 FROM dbo.SchemaMigrationHistory
    WHERE MigrationId = N'0027_mobile_app_selfhosted_package' AND Status = N'Applied')
    THROW 55126, N'0027 no figura aplicada en el ledger.', 1;
GO

/* Ninguna version existente debe haber quedado invalida. */
SELECT COUNT(*) AS VersionesSinOrigen
FROM dbo.AppMovilVersiones
WHERE ArchivoAlmacenado IS NULL
  AND (UrlDescarga IS NULL OR UrlDescarga NOT LIKE N'https://%');
GO

SELECT MigrationId, FileName, Status, AppliedAtUtc
FROM dbo.SchemaMigrationHistory
WHERE MigrationId = N'0027_mobile_app_selfhosted_package';
GO
