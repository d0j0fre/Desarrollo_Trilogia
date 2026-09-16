SET NOCOUNT ON;

IF COL_LENGTH(N'dbo.VehiculoKilometraje', N'SyncGuid') IS NULL
    THROW 54970, N'Falta la columna SyncGuid en dbo.VehiculoKilometraje.', 1;
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'UQ_VehiculoKilometraje_SyncGuid'
                 AND object_id = OBJECT_ID(N'dbo.VehiculoKilometraje'))
    THROW 54971, N'Falta el índice único filtrado sobre SyncGuid.', 1;

IF OBJECT_ID(N'dbo.sp_Chofer_GetVehiculosDisponibles', N'P') IS NULL
    THROW 54972, N'Falta sp_Chofer_GetVehiculosDisponibles.', 1;
IF OBJECT_ID(N'dbo.sp_Chofer_Kilometraje_Abrir', N'P') IS NULL
    THROW 54973, N'Falta sp_Chofer_Kilometraje_Abrir.', 1;
IF OBJECT_ID(N'dbo.sp_Chofer_Kilometraje_Cerrar', N'P') IS NULL
    THROW 54974, N'Falta sp_Chofer_Kilometraje_Cerrar.', 1;
IF OBJECT_ID(N'dbo.sp_Chofer_Kilometraje_Abierto', N'P') IS NULL
    THROW 54975, N'Falta sp_Chofer_Kilometraje_Abierto.', 1;
IF OBJECT_ID(N'dbo.sp_Chofer_ResumenDia', N'P') IS NULL
    THROW 54976, N'Falta sp_Chofer_ResumenDia.', 1;

/* Los procedimientos del módulo web de flota siguen intactos. */
IF OBJECT_ID(N'dbo.sp_Kilometraje_Abrir', N'P') IS NULL
    THROW 54977, N'sp_Kilometraje_Abrir desapareció: la migración no debía tocarlo.', 1;
IF OBJECT_ID(N'dbo.sp_Kilometraje_Cerrar', N'P') IS NULL
    THROW 54978, N'sp_Kilometraje_Cerrar desapareció: la migración no debía tocarlo.', 1;

IF NOT EXISTS (SELECT 1 FROM dbo.Permisos WHERE Codigo = N'MOVIL_ACCESO' AND Activo = 1)
    THROW 54979, N'Falta el permiso MOVIL_ACCESO.', 1;
IF NOT EXISTS (SELECT 1 FROM dbo.Permisos WHERE Codigo = N'FLOTA_KILOMETRAJE_PROPIO' AND Activo = 1)
    THROW 54980, N'Falta el permiso FLOTA_KILOMETRAJE_PROPIO.', 1;

IF NOT EXISTS (
    SELECT 1 FROM dbo.SchemaMigrationHistory
    WHERE MigrationId = N'0025_mobile_driver_surface' AND Status = N'Applied')
    THROW 54981, N'0025 no figura aplicada en el ledger.', 1;
GO

/* Perfiles que quedaron habilitados para la aplicación móvil. */
SELECT profile.Nombre AS Perfil, permission.Codigo AS Permiso
FROM dbo.PerfilPermisos assigned
INNER JOIN dbo.Perfiles profile ON profile.PerfilId = assigned.PerfilId
INNER JOIN dbo.Permisos permission ON permission.PermisoId = assigned.PermisoId
WHERE permission.Codigo IN (N'MOVIL_ACCESO', N'FLOTA_KILOMETRAJE_PROPIO')
ORDER BY permission.Codigo, profile.Nombre;
GO

/* Ninguna jornada existente debió quedar con SyncGuid: la columna es nueva y
   las jornadas del módulo web no lo usan. */
SELECT COUNT(*) AS JornadasConSyncGuid
FROM dbo.VehiculoKilometraje
WHERE SyncGuid IS NOT NULL;
GO

SELECT MigrationId, FileName, Status, AppliedAtUtc
FROM dbo.SchemaMigrationHistory
WHERE MigrationId = N'0025_mobile_driver_surface';
GO
