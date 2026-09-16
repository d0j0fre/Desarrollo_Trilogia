SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

/* Prueba funcional de la superficie movil sobre la replica local.
   NO se ejecuta en Azure: es solo para la base de ensayo.

   Comprueba que los procedimientos hacen lo que dicen, no solo que existen.
   Cada bloque imprime OK o FALLA con el motivo. */

DECLARE @errores INT = 0;
DECLARE @msg NVARCHAR(400);

/* ── Datos minimos ───────────────────────────────────────────────────────── */

IF NOT EXISTS(SELECT 1 FROM dbo.Perfiles WHERE Nombre = N'Chofer')
    INSERT dbo.Perfiles(Nombre) VALUES (N'Chofer');
IF NOT EXISTS(SELECT 1 FROM dbo.Perfiles WHERE Nombre = N'Administrador')
    INSERT dbo.Perfiles(Nombre) VALUES (N'Administrador');

DECLARE @perfilChofer INT = (SELECT PerfilId FROM dbo.Perfiles WHERE Nombre = N'Chofer');

IF NOT EXISTS(SELECT 1 FROM dbo.Usuarios WHERE Correo = N'chofer.prueba@local.test')
    INSERT dbo.Usuarios(PerfilId, NombreCompleto, Correo, Contrasena)
    VALUES (@perfilChofer, N'Chofer de Prueba', N'chofer.prueba@local.test', N'x');

DECLARE @chofer INT = (SELECT UsuarioId FROM dbo.Usuarios WHERE Correo = N'chofer.prueba@local.test');

IF NOT EXISTS(SELECT 1 FROM dbo.Vehiculos WHERE Placa = N'PRB-001')
    INSERT dbo.Vehiculos(Placa, Descripcion, KilometrajeActual) VALUES (N'PRB-001', N'Camion de prueba', 10000);

DECLARE @vehiculo INT = (SELECT VehiculoId FROM dbo.Vehiculos WHERE Placa = N'PRB-001');

IF NOT EXISTS(SELECT 1 FROM dbo.Rutas WHERE Codigo = N'RUTA-PRB-001')
    INSERT dbo.Rutas(Codigo, Zona, ChoferUsuarioId, VehiculoId, Estado)
    VALUES (N'RUTA-PRB-001', N'Zona de prueba', @chofer, @vehiculo, N'Despachada');

/* Un segundo chofer, para probar que no ve lo ajeno */
IF NOT EXISTS(SELECT 1 FROM dbo.Usuarios WHERE Correo = N'otro.chofer@local.test')
    INSERT dbo.Usuarios(PerfilId, NombreCompleto, Correo, Contrasena)
    VALUES (@perfilChofer, N'Otro Chofer', N'otro.chofer@local.test', N'x');

DECLARE @otroChofer INT = (SELECT UsuarioId FROM dbo.Usuarios WHERE Correo = N'otro.chofer@local.test');

PRINT '=== PRUEBAS FUNCIONALES ===';

/* ── 1. Permisos efectivos ───────────────────────────────────────────────── */

-- La migracion 0025 corrio antes de que existieran los perfiles, asi que
-- aqui se asignan igual que lo haria ella.
INSERT dbo.PerfilPermisos(PerfilId, PermisoId, UsuarioAsignacionId, UsuarioAsignacionNombre)
SELECT pf.PerfilId, pe.PermisoId, NULL, N'Prueba funcional'
FROM dbo.Perfiles pf CROSS JOIN dbo.Permisos pe
WHERE pf.Nombre = N'Chofer' AND pe.Codigo IN (N'MOVIL_ACCESO', N'FLOTA_KILOMETRAJE_PROPIO')
  AND NOT EXISTS(SELECT 1 FROM dbo.PerfilPermisos x WHERE x.PerfilId = pf.PerfilId AND x.PermisoId = pe.PermisoId);

DECLARE @permisos TABLE (Codigo NVARCHAR(100));
INSERT @permisos EXEC dbo.sp_Auth_GetPermisosPorPerfil @NombreRol = N'Chofer';

IF (SELECT COUNT(*) FROM @permisos WHERE Codigo IN (N'MOVIL_ACCESO', N'FLOTA_KILOMETRAJE_PROPIO')) = 2
    PRINT '  OK   sp_Auth_GetPermisosPorPerfil devuelve los dos permisos del chofer';
ELSE BEGIN SET @errores += 1; PRINT '  FALLA sp_Auth_GetPermisosPorPerfil'; END

/* ── 2. Usuario activo ───────────────────────────────────────────────────── */

DECLARE @usuarios TABLE (UsuarioId INT, NombreCompleto NVARCHAR(200), Correo NVARCHAR(200),
                         PerfilNombre NVARCHAR(100), Activo BIT);
INSERT @usuarios EXEC dbo.sp_Auth_GetUsuarioActivo @UsuarioId = @chofer;

IF EXISTS(SELECT 1 FROM @usuarios WHERE UsuarioId = @chofer AND PerfilNombre = N'Chofer')
    PRINT '  OK   sp_Auth_GetUsuarioActivo devuelve el usuario con su perfil';
ELSE BEGIN SET @errores += 1; PRINT '  FALLA sp_Auth_GetUsuarioActivo'; END

/* ── 3. Ciclo completo de tokens de refresco ─────────────────────────────── */

DECLARE @h1 CHAR(64) = REPLICATE('A', 64);
DECLARE @h2 CHAR(64) = REPLICATE('B', 64);
DECLARE @h3 CHAR(64) = REPLICATE('C', 64);
DECLARE @h4 CHAR(64) = REPLICATE('D', 64);
DECLARE @shaA CHAR(64) = REPLICATE('F', 64);
DECLARE @shaB CHAR(64) = REPLICATE('E', 64);
DECLARE @shaC CHAR(64) = REPLICATE('9', 64);
DECLARE @syncAjeno UNIQUEIDENTIFIER;
DECLARE @syncD UNIQUEIDENTIFIER;
DECLARE @emitido TABLE (TokenId BIGINT, CadenaId UNIQUEIDENTIFIER);

INSERT @emitido EXEC dbo.sp_Auth_EmitirTokenRefresco
    @UsuarioId = @chofer, @TokenHash = @h1,
    @ExpiraUtc = '2099-01-01', @DispositivoId = N'telefono-prueba';

DECLARE @cadena UNIQUEIDENTIFIER = (SELECT TOP 1 CadenaId FROM @emitido);

IF @cadena IS NOT NULL PRINT '  OK   sp_Auth_EmitirTokenRefresco emite y devuelve la cadena';
ELSE BEGIN SET @errores += 1; PRINT '  FALLA sp_Auth_EmitirTokenRefresco'; END

-- Canje legitimo
DECLARE @canje TABLE (Resultado NVARCHAR(20), UsuarioId INT, CadenaId UNIQUEIDENTIFIER);
INSERT @canje EXEC dbo.sp_Auth_CanjearTokenRefresco
    @TokenHash = @h1, @NuevoTokenHash = @h2, @NuevaExpiraUtc = '2099-01-01';

IF EXISTS(SELECT 1 FROM @canje WHERE Resultado = N'Ok' AND UsuarioId = @chofer AND CadenaId = @cadena)
    PRINT '  OK   el canje rota el token y conserva la cadena';
ELSE BEGIN SET @errores += 1; PRINT '  FALLA el canje legitimo'; END

-- Reutilizacion del token ya canjeado: debe cortar la cadena entera
DELETE @canje;
INSERT @canje EXEC dbo.sp_Auth_CanjearTokenRefresco
    @TokenHash = @h1, @NuevoTokenHash = @h3, @NuevaExpiraUtc = '2099-01-01';

IF EXISTS(SELECT 1 FROM @canje WHERE Resultado = N'Reutilizado')
    PRINT '  OK   reutilizar un token ya canjeado se detecta';
ELSE BEGIN SET @errores += 1; PRINT '  FALLA no se detecto la reutilizacion'; END

IF NOT EXISTS(SELECT 1 FROM dbo.UsuarioTokensRefresco WHERE CadenaId = @cadena AND RevocadoUtc IS NULL)
    PRINT '  OK   la cadena completa quedo revocada tras la reutilizacion';
ELSE BEGIN SET @errores += 1; PRINT '  FALLA quedaron tokens vivos en la cadena robada'; END

-- El token rotado tampoco sirve ya
DELETE @canje;
INSERT @canje EXEC dbo.sp_Auth_CanjearTokenRefresco
    @TokenHash = @h2, @NuevoTokenHash = @h4, @NuevaExpiraUtc = '2099-01-01';

IF EXISTS(SELECT 1 FROM @canje WHERE Resultado = N'Invalido')
    PRINT '  OK   el token de una cadena revocada ya no sirve';
ELSE BEGIN SET @errores += 1; PRINT '  FALLA un token de cadena revocada todavia sirve'; END

/* ── 4. Kilometraje con alcance de chofer ────────────────────────────────── */

DECLARE @vehiculos TABLE (VehiculoId INT, Placa NVARCHAR(50), Descripcion NVARCHAR(200),
                          KilometrajeActual INT, JornadaAbierta BIT);
INSERT @vehiculos EXEC dbo.sp_Chofer_GetVehiculosDisponibles @ChoferUsuarioId = @chofer;

IF EXISTS(SELECT 1 FROM @vehiculos WHERE VehiculoId = @vehiculo)
    PRINT '  OK   el chofer ve el vehiculo de su ruta activa';
ELSE BEGIN SET @errores += 1; PRINT '  FALLA el chofer no ve su vehiculo'; END

DELETE @vehiculos;
INSERT @vehiculos EXEC dbo.sp_Chofer_GetVehiculosDisponibles @ChoferUsuarioId = @otroChofer;

IF NOT EXISTS(SELECT 1 FROM @vehiculos)
    PRINT '  OK   otro chofer NO ve ese vehiculo';
ELSE BEGIN SET @errores += 1; PRINT '  FALLA un chofer ajeno ve el vehiculo'; END

-- Apertura de jornada
DECLARE @sync UNIQUEIDENTIFIER = NEWID();
DECLARE @apertura TABLE (KilometrajeId INT, Duplicado BIT);

INSERT @apertura EXEC dbo.sp_Chofer_Kilometraje_Abrir
    @ChoferUsuarioId = @chofer, @VehiculoId = @vehiculo, @KmInicial = 10000, @SyncGuid = @sync;

DECLARE @jornada INT = (SELECT TOP 1 KilometrajeId FROM @apertura);

IF @jornada IS NOT NULL AND EXISTS(SELECT 1 FROM @apertura WHERE Duplicado = 0)
    PRINT '  OK   el chofer abre jornada en el vehiculo de su ruta';
ELSE BEGIN SET @errores += 1; PRINT '  FALLA no se pudo abrir la jornada'; END

-- Idempotencia: el mismo envio no crea otra jornada
DELETE @apertura;
INSERT @apertura EXEC dbo.sp_Chofer_Kilometraje_Abrir
    @ChoferUsuarioId = @chofer, @VehiculoId = @vehiculo, @KmInicial = 10000, @SyncGuid = @sync;

IF EXISTS(SELECT 1 FROM @apertura WHERE KilometrajeId = @jornada AND Duplicado = 1)
    PRINT '  OK   reenviar la misma apertura devuelve Duplicado y no crea otra';
ELSE BEGIN SET @errores += 1; PRINT '  FALLA la apertura se duplico'; END

IF (SELECT COUNT(*) FROM dbo.VehiculoKilometraje WHERE SyncGuid = @sync) = 1
    PRINT '  OK   hay exactamente una jornada para ese identificador';
ELSE BEGIN SET @errores += 1; PRINT '  FALLA hay mas de una jornada con el mismo identificador'; END

-- Un chofer ajeno no puede cerrar la jornada
BEGIN TRY
    DECLARE @cierreAjeno TABLE (KilometrajeId INT, KmFinal INT, KmRecorridos INT, Duplicado BIT);
    SET @syncAjeno = NEWID();
    INSERT @cierreAjeno EXEC dbo.sp_Chofer_Kilometraje_Cerrar
        @ChoferUsuarioId = @otroChofer, @KilometrajeId = @jornada,
        @KmFinal = 10500, @SyncGuid = @syncAjeno;
    SET @errores += 1; PRINT '  FALLA un chofer ajeno cerro la jornada';
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() = 54967 PRINT '  OK   un chofer ajeno no puede cerrar la jornada';
    ELSE BEGIN SET @errores += 1; SET @msg = '  FALLA error inesperado: ' + CAST(ERROR_NUMBER() AS NVARCHAR(10)); PRINT @msg; END
END CATCH

-- Cierre legitimo
DECLARE @syncCierre UNIQUEIDENTIFIER = NEWID();
DECLARE @cierre TABLE (KilometrajeId INT, KmFinal INT, KmRecorridos INT, Duplicado BIT);

INSERT @cierre EXEC dbo.sp_Chofer_Kilometraje_Cerrar
    @ChoferUsuarioId = @chofer, @KilometrajeId = @jornada, @KmFinal = 10500, @SyncGuid = @syncCierre;

IF EXISTS(SELECT 1 FROM @cierre WHERE KmRecorridos = 500 AND Duplicado = 0)
    PRINT '  OK   el cierre calcula 500 km recorridos';
ELSE BEGIN SET @errores += 1; PRINT '  FALLA el cierre no calculo bien'; END

IF (SELECT KilometrajeActual FROM dbo.Vehiculos WHERE VehiculoId = @vehiculo) = 10500
    PRINT '  OK   el odometro del vehiculo avanzo a 10500';
ELSE BEGIN SET @errores += 1; PRINT '  FALLA el odometro no avanzo'; END

-- Reenvio del cierre
DELETE @cierre;
INSERT @cierre EXEC dbo.sp_Chofer_Kilometraje_Cerrar
    @ChoferUsuarioId = @chofer, @KilometrajeId = @jornada, @KmFinal = 10500, @SyncGuid = @syncCierre;

IF EXISTS(SELECT 1 FROM @cierre WHERE Duplicado = 1)
    PRINT '  OK   reenviar el mismo cierre devuelve Duplicado';
ELSE BEGIN SET @errores += 1; PRINT '  FALLA el reenvio del cierre no se detecto'; END

/* ── 5. Resumen del dia ──────────────────────────────────────────────────── */

DECLARE @resumen TABLE (RutasActivas INT, EntregasPendientes INT, EntregasCompletadasHoy INT,
                        EntregasFallidasHoy INT, JornadaAbierta BIT);
INSERT @resumen EXEC dbo.sp_Chofer_ResumenDia @ChoferUsuarioId = @chofer;

IF EXISTS(SELECT 1 FROM @resumen WHERE RutasActivas = 1 AND JornadaAbierta = 0)
    PRINT '  OK   el resumen ve 1 ruta activa y ninguna jornada abierta';
ELSE BEGIN SET @errores += 1; PRINT '  FALLA el resumen del dia'; END

/* ── 6. Catalogo de versiones ────────────────────────────────────────────── */

DECLARE @version TABLE (VersionId INT);
INSERT @version EXEC dbo.sp_AppMovil_Publicar
    @VersionNombre = N'1.0.0', @BuildNumero = 1, @MinBuildSoportado = 1,
    @UrlDescarga = N'https://example.test/app.apk', @Sha256 = @shaA;

IF EXISTS(SELECT 1 FROM @version) PRINT '  OK   se publica una version';
ELSE BEGIN SET @errores += 1; PRINT '  FALLA no se pudo publicar la version'; END

BEGIN TRY
    DECLARE @v2 TABLE (VersionId INT);
    INSERT @v2 EXEC dbo.sp_AppMovil_Publicar
        @VersionNombre = N'0.9.0', @BuildNumero = 1, @MinBuildSoportado = 1,
        @UrlDescarga = N'https://example.test/viejo.apk', @Sha256 = @shaB;
    SET @errores += 1; PRINT '  FALLA acepto un build que no supera al anterior';
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() = 55014 PRINT '  OK   rechaza un build que no supera al anterior';
    ELSE BEGIN SET @errores += 1; SET @msg = '  FALLA error inesperado: ' + CAST(ERROR_NUMBER() AS NVARCHAR(10)); PRINT @msg; END
END CATCH

BEGIN TRY
    DECLARE @v3 TABLE (VersionId INT);
    INSERT @v3 EXEC dbo.sp_AppMovil_Publicar
        @VersionNombre = N'1.1.0', @BuildNumero = 2, @MinBuildSoportado = 1,
        @UrlDescarga = N'http://inseguro.test/app.apk', @Sha256 = @shaC;
    SET @errores += 1; PRINT '  FALLA acepto una direccion sin HTTPS';
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() = 55011 PRINT '  OK   rechaza una direccion de descarga sin HTTPS';
    ELSE BEGIN SET @errores += 1; SET @msg = '  FALLA error inesperado: ' + CAST(ERROR_NUMBER() AS NVARCHAR(10)); PRINT @msg; END
END CATCH

/* ── 7. Lo existente sigue en pie ────────────────────────────────────────── */

IF OBJECT_ID(N'dbo.sp_Auth_GetLoginCredential', N'P') IS NOT NULL
   AND OBJECT_ID(N'dbo.sp_Kilometraje_Abrir', N'P') IS NOT NULL
   AND OBJECT_ID(N'dbo.sp_Kilometraje_Cerrar', N'P') IS NOT NULL
    PRINT '  OK   el login y el kilometraje del modulo web siguen intactos';
ELSE BEGIN SET @errores += 1; PRINT '  FALLA se toco algo del modulo web'; END

/* ── Resultado ───────────────────────────────────────────────────────────── */

PRINT '';
IF @errores = 0 PRINT '=== RESULTADO: todas las pruebas funcionales pasaron ===';
ELSE BEGIN
    SET @msg = '=== RESULTADO: ' + CAST(@errores AS NVARCHAR(10)) + ' PRUEBAS FALLARON ===';
    PRINT @msg;
    THROW 60000, N'Hay pruebas funcionales fallidas.', 1;
END
