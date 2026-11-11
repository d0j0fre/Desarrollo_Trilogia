SET NOCOUNT ON;
SET XACT_ABORT ON;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0311_revocacion_sesiones' AND Status=N'Applied') THROW 58220,N'La migracion 0311 no esta registrada.',1;
IF COL_LENGTH(N'dbo.Usuarios',N'SecurityStamp') IS NULL THROW 58221,N'Falta Usuarios.SecurityStamp.',1;
IF NOT EXISTS(SELECT 1 FROM sys.triggers WHERE name=N'tr_Usuarios_RevocarSesiones' AND parent_id=OBJECT_ID(N'dbo.Usuarios')) THROW 58222,N'Falta tr_Usuarios_RevocarSesiones.',1;
IF OBJECT_ID(N'dbo.sp_Auth_GetSessionState',N'P') IS NULL OR OBJECT_ID(N'dbo.sp_Auth_ActualizarHash',N'P') IS NULL THROW 58223,N'Faltan los procedimientos de sesion.',1;
IF EXISTS(SELECT 1 FROM dbo.Usuarios GROUP BY SecurityStamp HAVING COUNT(*)>1) THROW 58224,N'Hay sellos de seguridad repetidos.',1;

-- Prueba funcional deshecha con un usuario y un token propios.
DECLARE @perfil INT = (SELECT TOP (1) PerfilId FROM dbo.Perfiles WHERE Nombre = N'Cliente');
DECLARE @otro INT = (SELECT TOP (1) PerfilId FROM dbo.Perfiles WHERE Nombre <> N'Cliente' AND Activo = 1 ORDER BY PerfilId);
DECLARE @correo NVARCHAR(150) = N'v0311-' + CONVERT(NVARCHAR(36), NEWID()) + N'@prueba.invalid';
DECLARE @u INT, @s UNIQUEIDENTIFIER, @s2 UNIQUEIDENTIFIER;
BEGIN TRANSACTION;
INSERT dbo.Usuarios(PerfilId,NombreCompleto,Correo,Contrasena,ContrasenaHash,PasswordVersion,Activo)
VALUES(@perfil,N'Verificacion 0311',@correo,N'',N'pbkdf2-sha256$210000$c2Fs$aGFzaA==',1,1);
SET @u = CONVERT(INT, SCOPE_IDENTITY());
INSERT dbo.UsuarioTokensRefresco(UsuarioId,TokenHash,CadenaId,ExpiraUtc) VALUES(@u,REPLICATE(N'A',64),NEWID(),DATEADD(DAY,1,SYSUTCDATETIME()));
SELECT @s = SecurityStamp FROM dbo.Usuarios WHERE UsuarioId=@u;

-- Un cambio ajeno al acceso no toca el sello ni los tokens.
UPDATE dbo.Usuarios SET Telefono=N'0000' WHERE UsuarioId=@u;
IF (SELECT SecurityStamp FROM dbo.Usuarios WHERE UsuarioId=@u) <> @s THROW 58225,N'Un cambio de telefono renovo el sello.',1;
IF EXISTS(SELECT 1 FROM dbo.UsuarioTokensRefresco WHERE UsuarioId=@u AND RevocadoUtc IS NOT NULL) THROW 58226,N'Un cambio de telefono revoco tokens.',1;

-- Actualizar el algoritmo del hash en el inicio de sesion no revoca nada; con un hash anterior viejo no escribe.
DECLARE @r TABLE(Actualizadas INT);
INSERT @r EXEC dbo.sp_Auth_ActualizarHash @UsuarioId=@u, @HashAnterior=N'pbkdf2-sha256$210000$c2Fs$bm9lcw==', @HashNuevo=N'pbkdf2-sha256$210000$c2Fs$eHh4eA==';
IF (SELECT Actualizadas FROM @r)<>0 THROW 58227,N'Se actualizo un hash que ya habia cambiado.',1;
DELETE @r;
INSERT @r EXEC dbo.sp_Auth_ActualizarHash @UsuarioId=@u, @HashAnterior=N'pbkdf2-sha256$210000$c2Fs$aGFzaA==', @HashNuevo=N'pbkdf2-sha256$210000$c2Fs$eHh4eA==';
IF (SELECT Actualizadas FROM @r)<>1 THROW 58228,N'No se actualizo el hash vigente.',1;
IF (SELECT SecurityStamp FROM dbo.Usuarios WHERE UsuarioId=@u) <> @s OR EXISTS(SELECT 1 FROM dbo.UsuarioTokensRefresco WHERE UsuarioId=@u AND RevocadoUtc IS NOT NULL) THROW 58229,N'El cambio de algoritmo revoco la sesion.',1;
IF CONVERT(INT, ISNULL(SESSION_CONTEXT(N'sin_revocar_sesiones'),0)) <> 0 THROW 58230,N'La marca de no revocar quedo activa.',1;

-- Cambiar la contrasena renueva el sello y revoca los tokens.
UPDATE dbo.Usuarios SET ContrasenaHash=N'pbkdf2-sha256$210000$c2Fs$bnVldmE=' WHERE UsuarioId=@u;
SELECT @s2 = SecurityStamp FROM dbo.Usuarios WHERE UsuarioId=@u;
IF @s2 = @s THROW 58231,N'Cambiar la contrasena no renovo el sello.',1;
IF EXISTS(SELECT 1 FROM dbo.UsuarioTokensRefresco WHERE UsuarioId=@u AND RevocadoUtc IS NULL) THROW 58232,N'Cambiar la contrasena no revoco los tokens.',1;

-- Cambiar de perfil e inactivar tambien.
UPDATE dbo.UsuarioTokensRefresco SET RevocadoUtc=NULL WHERE UsuarioId=@u;
UPDATE dbo.Usuarios SET PerfilId=@otro WHERE UsuarioId=@u;
IF (SELECT SecurityStamp FROM dbo.Usuarios WHERE UsuarioId=@u) = @s2 THROW 58233,N'Cambiar de perfil no renovo el sello.',1;
IF EXISTS(SELECT 1 FROM dbo.UsuarioTokensRefresco WHERE UsuarioId=@u AND RevocadoUtc IS NULL) THROW 58234,N'Cambiar de perfil no revoco los tokens.',1;
SELECT @s2 = SecurityStamp FROM dbo.Usuarios WHERE UsuarioId=@u;
UPDATE dbo.UsuarioTokensRefresco SET RevocadoUtc=NULL WHERE UsuarioId=@u;
UPDATE dbo.Usuarios SET Activo=0, MotivoInactivacion=N'verificacion' WHERE UsuarioId=@u;
IF (SELECT SecurityStamp FROM dbo.Usuarios WHERE UsuarioId=@u) = @s2 THROW 58235,N'Inactivar no renovo el sello.',1;
IF EXISTS(SELECT 1 FROM dbo.UsuarioTokensRefresco WHERE UsuarioId=@u AND RevocadoUtc IS NULL) THROW 58236,N'Inactivar no revoco los tokens.',1;
DECLARE @estado TABLE(Activo BIT, PerfilNombre NVARCHAR(100), SecurityStamp UNIQUEIDENTIFIER);
INSERT @estado EXEC dbo.sp_Auth_GetSessionState @UsuarioId=@u;
IF (SELECT Activo FROM @estado)<>0 THROW 58237,N'El estado de sesion de un inactivo no es inactivo.',1;
ROLLBACK TRANSACTION;
PRINT N'Verificacion 0311 completada.';
