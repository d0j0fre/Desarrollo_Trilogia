SET NOCOUNT ON;
SET XACT_ABORT ON;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0310_credenciales_solo_hash' AND Status=N'Applied') THROW 58120,N'La migracion 0310 no esta registrada.',1;
IF OBJECT_ID(N'dbo.fn_Auth_HashTransicion',N'FN') IS NULL THROW 58121,N'Falta fn_Auth_HashTransicion.',1;
IF NOT EXISTS(SELECT 1 FROM sys.triggers WHERE name=N'tr_Usuarios_SinTextoPlano' AND parent_id=OBJECT_ID(N'dbo.Usuarios')) THROW 58122,N'Falta el disparador tr_Usuarios_SinTextoPlano.',1;
IF EXISTS(SELECT 1 FROM dbo.Usuarios WHERE NULLIF(LTRIM(RTRIM(Contrasena)),N'') IS NOT NULL) THROW 58123,N'Quedan contrasenas en texto plano.',1;
IF EXISTS(SELECT 1 FROM dbo.Usuarios WHERE NULLIF(LTRIM(RTRIM(ContrasenaHash)),N'') IS NULL AND Activo=1) THROW 58124,N'Hay usuarios activos sin hash.',1;
IF OBJECT_DEFINITION(OBJECT_ID(N'dbo.sp_Auth_GetLoginCredential')) NOT LIKE N'%@Contrasena NVARCHAR(255) = NULL%' THROW 58125,N'El inicio de sesion aun exige la contrasena como parametro obligatorio.',1;
IF OBJECT_DEFINITION(OBJECT_ID(N'dbo.sp_Auth_GetLoginCredential')) LIKE N'%u.Contrasena = @Contrasena%' THROW 58126,N'El inicio de sesion aun compara texto plano.',1;

-- Prueba funcional deshecha: el texto plano escrito por cualquier via no sobrevive a la sentencia.
DECLARE @perfil INT = (SELECT TOP (1) PerfilId FROM dbo.Perfiles WHERE Nombre = N'Cliente');
IF @perfil IS NULL THROW 58127,N'No existe el perfil Cliente.',1;
DECLARE @sufijo NVARCHAR(36) = CONVERT(NVARCHAR(36), NEWID());
BEGIN TRANSACTION;
-- Una sola sentencia con tres filas: cada fila recibe su propia sal.
INSERT dbo.Usuarios(PerfilId,NombreCompleto,Correo,Contrasena,Activo)
VALUES(@perfil,N'Verificacion 0310 A',N'v0310a-'+@sufijo+N'@prueba.invalid',N'clave-verificacion',1),
      (@perfil,N'Verificacion 0310 B',N'v0310b-'+@sufijo+N'@prueba.invalid',N'clave-verificacion',1),
      (@perfil,N'Verificacion 0310 C',N'v0310c-'+@sufijo+N'@prueba.invalid',N'otra-clave',1);
IF EXISTS(SELECT 1 FROM dbo.Usuarios WHERE Correo LIKE N'v0310_-'+@sufijo+N'@prueba.invalid' AND (Contrasena<>N'' OR ContrasenaHash IS NULL)) THROW 58128,N'Un insert con texto plano guardo la clave sin convertirla.',1;
IF (SELECT COUNT(DISTINCT ContrasenaHash) FROM dbo.Usuarios WHERE Correo LIKE N'v0310_-'+@sufijo+N'@prueba.invalid') <> 3 THROW 58129,N'Las filas comparten sal.',1;
DECLARE @h NVARCHAR(512)=(SELECT ContrasenaHash FROM dbo.Usuarios WHERE Correo=N'v0310a-'+@sufijo+N'@prueba.invalid');
IF @h NOT LIKE N'legacy-sha256$%' THROW 58130,N'El hash no tiene el formato de transicion.',1;
IF dbo.fn_Auth_HashTransicion(N'clave-verificacion', CONVERT(VARBINARY(16), N'0x'+SUBSTRING(@h,15,32), 1)) <> @h THROW 58131,N'El hash no corresponde a la contrasena escrita.',1;

-- Un UPDATE que escribe texto plano tambien se convierte; si fija un hash a la vez, el hash manda.
UPDATE dbo.Usuarios SET Contrasena=N'cambio-por-update' WHERE Correo=N'v0310a-'+@sufijo+N'@prueba.invalid';
IF EXISTS(SELECT 1 FROM dbo.Usuarios WHERE Correo=N'v0310a-'+@sufijo+N'@prueba.invalid' AND (Contrasena<>N'' OR ContrasenaHash=@h)) THROW 58132,N'El UPDATE con texto plano no se convirtio.',1;
UPDATE dbo.Usuarios SET Contrasena=N'se-ignora', ContrasenaHash=N'pbkdf2-sha256$210000$c2Fs$aGFzaA==', PasswordVersion=1 WHERE Correo=N'v0310b-'+@sufijo+N'@prueba.invalid';
IF NOT EXISTS(SELECT 1 FROM dbo.Usuarios WHERE Correo=N'v0310b-'+@sufijo+N'@prueba.invalid' AND Contrasena=N'' AND ContrasenaHash=N'pbkdf2-sha256$210000$c2Fs$aGFzaA==') THROW 58133,N'El hash explicito no se conservo.',1;
-- Un UPDATE que no toca la clave deja el hash como esta.
DECLARE @antes NVARCHAR(512)=(SELECT ContrasenaHash FROM dbo.Usuarios WHERE Correo=N'v0310c-'+@sufijo+N'@prueba.invalid');
UPDATE dbo.Usuarios SET Telefono=N'0000' WHERE Correo=N'v0310c-'+@sufijo+N'@prueba.invalid';
IF (SELECT ContrasenaHash FROM dbo.Usuarios WHERE Correo=N'v0310c-'+@sufijo+N'@prueba.invalid') <> @antes THROW 58134,N'Un UPDATE ajeno a la clave cambio el hash.',1;

-- Procedimientos que reciben la clave: con hash, y el texto plano heredado tambien termina convertido.
EXEC dbo.sp_Admin_CreateClient @NombreCompleto=N'Verificacion 0310 D', @Correo=N'v0310d-@prueba.invalid', @ContrasenaHash=N'pbkdf2-sha256$210000$c2Fs$aGFzaA==';
IF NOT EXISTS(SELECT 1 FROM dbo.Usuarios WHERE Correo=N'v0310d-@prueba.invalid' AND Contrasena=N'' AND ContrasenaHash LIKE N'pbkdf2-sha256$%') THROW 58135,N'sp_Admin_CreateClient no guardo el hash.',1;
EXEC dbo.sp_Admin_CreateClient @NombreCompleto=N'Verificacion 0310 E', @Correo=N'v0310e-@prueba.invalid', @Contrasena=N'clave-heredada';
IF NOT EXISTS(SELECT 1 FROM dbo.Usuarios WHERE Correo=N'v0310e-@prueba.invalid' AND Contrasena=N'' AND ContrasenaHash LIKE N'legacy-sha256$%') THROW 58136,N'sp_Admin_CreateClient dejo texto plano.',1;
DECLARE @err NVARCHAR(200)=N'';
BEGIN TRY EXEC dbo.sp_Auth_RegisterClient @NombreCompleto=N'Verificacion 0310 F', @Correo=N'v0310f-@prueba.invalid', @Contrasena=N'solo-texto'; END TRY BEGIN CATCH SET @err=ERROR_MESSAGE(); END CATCH;
IF @err=N'' OR EXISTS(SELECT 1 FROM dbo.Usuarios WHERE Correo=N'v0310f-@prueba.invalid') THROW 58137,N'sp_Auth_RegisterClient acepto texto plano sin hash.',1;
ROLLBACK TRANSACTION;
PRINT N'Verificacion 0310 completada.';
