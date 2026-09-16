SET NOCOUNT ON;

IF OBJECT_ID(N'dbo.UsuarioTokensRefresco', N'U') IS NULL
    THROW 54930, N'Falta la tabla dbo.UsuarioTokensRefresco.', 1;
IF OBJECT_ID(N'dbo.sp_Auth_GetPermisosPorPerfil', N'P') IS NULL
    THROW 54931, N'Falta sp_Auth_GetPermisosPorPerfil.', 1;
IF OBJECT_ID(N'dbo.sp_Auth_GetUsuarioActivo', N'P') IS NULL
    THROW 54932, N'Falta sp_Auth_GetUsuarioActivo.', 1;
IF OBJECT_ID(N'dbo.sp_Auth_EmitirTokenRefresco', N'P') IS NULL
    THROW 54933, N'Falta sp_Auth_EmitirTokenRefresco.', 1;
IF OBJECT_ID(N'dbo.sp_Auth_CanjearTokenRefresco', N'P') IS NULL
    THROW 54934, N'Falta sp_Auth_CanjearTokenRefresco.', 1;
IF OBJECT_ID(N'dbo.sp_Auth_RevocarCadenaTokenRefresco', N'P') IS NULL
    THROW 54935, N'Falta sp_Auth_RevocarCadenaTokenRefresco.', 1;
IF OBJECT_ID(N'dbo.sp_Auth_RevocarTokensUsuario', N'P') IS NULL
    THROW 54936, N'Falta sp_Auth_RevocarTokensUsuario.', 1;
IF OBJECT_ID(N'dbo.sp_Auth_PurgarTokensRefresco', N'P') IS NULL
    THROW 54937, N'Falta sp_Auth_PurgarTokensRefresco.', 1;

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'IX_UsuarioTokensRefresco_Cadena'
                 AND object_id = OBJECT_ID(N'dbo.UsuarioTokensRefresco'))
    THROW 54938, N'Falta el índice IX_UsuarioTokensRefresco_Cadena.', 1;

IF NOT EXISTS (SELECT 1 FROM sys.key_constraints
               WHERE name = N'UQ_UsuarioTokensRefresco_TokenHash'
                 AND parent_object_id = OBJECT_ID(N'dbo.UsuarioTokensRefresco'))
    THROW 54939, N'Falta la restricción única sobre TokenHash.', 1;

IF NOT EXISTS (
    SELECT 1 FROM dbo.SchemaMigrationHistory
    WHERE MigrationId = N'0024_mobile_auth_jwt' AND Status = N'Applied')
    THROW 54940, N'0024 no figura aplicada en el ledger.', 1;
GO

/* La autenticación existente no se tocó: el procedimiento de login sigue en pie. */
IF OBJECT_ID(N'dbo.sp_Auth_GetLoginCredential', N'P') IS NULL
    THROW 54941, N'sp_Auth_GetLoginCredential desapareció: la migración no debía tocarlo.', 1;
GO

/* Ningún perfil debería quedarse sin permisos por efecto de esta migración:
   es de solo lectura sobre PerfilPermisos. */
SELECT p.Nombre AS Perfil, COUNT(pp.PermisoId) AS PermisosAsignados
FROM dbo.Perfiles p
LEFT JOIN dbo.PerfilPermisos pp ON pp.PerfilId = p.PerfilId
GROUP BY p.Nombre
ORDER BY p.Nombre;
GO

SELECT MigrationId, FileName, Status, AppliedAtUtc
FROM dbo.SchemaMigrationHistory
WHERE MigrationId = N'0024_mobile_auth_jwt';
GO
