SET NOCOUNT ON;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0312_kilometraje_propiedad' AND Status=N'Applied') THROW 58310,N'La migracion 0312 no esta registrada.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.Permisos WHERE Codigo=N'FLOTA_KILOMETRAJE_ADMIN' AND Activo=1) THROW 58311,N'Falta el permiso FLOTA_KILOMETRAJE_ADMIN.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.PerfilPermisos pp JOIN dbo.Perfiles p ON p.PerfilId=pp.PerfilId JOIN dbo.Permisos pe ON pe.PermisoId=pp.PermisoId WHERE p.Nombre=N'Administrador' AND pe.Codigo=N'FLOTA_KILOMETRAJE_ADMIN') THROW 58312,N'El Administrador no tiene FLOTA_KILOMETRAJE_ADMIN.',1;

/* Prueba funcional con datos propios que se borran al final. No usa una transaccion externa porque el
   procedimiento revierte la suya al rechazar (XACT_STATE) y eso desharia tambien las filas de prueba.
   El km final es igual al inicial, asi que el odometro del vehiculo no cambia. */
DECLARE @choferA INT = (SELECT TOP (1) u.UsuarioId FROM dbo.Usuarios u JOIN dbo.Perfiles p ON p.PerfilId=u.PerfilId WHERE p.Nombre=N'Chofer' AND u.Activo=1 ORDER BY u.UsuarioId);
DECLARE @choferB INT = (SELECT TOP (1) u.UsuarioId FROM dbo.Usuarios u JOIN dbo.Perfiles p ON p.PerfilId=u.PerfilId WHERE p.Nombre=N'Chofer' AND u.Activo=1 AND u.UsuarioId<>@choferA ORDER BY u.UsuarioId);
DECLARE @vehiculo INT = (SELECT TOP (1) VehiculoId FROM dbo.Vehiculos WHERE Activo=1 ORDER BY VehiculoId);
IF @choferA IS NULL OR @choferB IS NULL OR @vehiculo IS NULL THROW 58313,N'La base no tiene dos choferes activos y un vehiculo para la prueba.',1;
DECLARE @odometro INT = (SELECT ISNULL(KilometrajeActual,0) FROM dbo.Vehiculos WHERE VehiculoId=@vehiculo);
DECLARE @id INT, @id2 INT, @err NVARCHAR(200), @fallo NVARCHAR(400) = N'';

INSERT dbo.VehiculoKilometraje(VehiculoId,ChoferUsuarioId,ChoferNombre,Fecha,KmInicial,Observaciones)
VALUES(@vehiculo,@choferA,N'Chofer A',CONVERT(DATE,SYSDATETIME()),@odometro,N'verificacion 0312');
SET @id = CONVERT(INT, SCOPE_IDENTITY());
INSERT dbo.VehiculoKilometraje(VehiculoId,ChoferUsuarioId,ChoferNombre,Fecha,KmInicial,Observaciones)
VALUES(@vehiculo,@choferA,N'Chofer A',CONVERT(DATE,SYSDATETIME()),@odometro,N'verificacion 0312 admin');
SET @id2 = CONVERT(INT, SCOPE_IDENTITY());

-- Sin actor: se rechaza.
SET @err = N''; BEGIN TRY EXEC dbo.sp_Kilometraje_Cerrar @KilometrajeId=@id, @KmFinal=@odometro; END TRY BEGIN CATCH SET @err = CONVERT(NVARCHAR(10),ERROR_NUMBER()); END CATCH;
IF @err <> N'53047' SET @fallo = N'Un cierre sin actor no se rechazo con 53047 (' + @err + N').';
-- Otro chofer: «no encontrada», y la jornada sigue abierta.
SET @err = N''; BEGIN TRY EXEC dbo.sp_Kilometraje_Cerrar @KilometrajeId=@id, @KmFinal=@odometro, @ActorUsuarioId=@choferB, @PuedeAdministrar=0; END TRY BEGIN CATCH SET @err = CONVERT(NVARCHAR(10),ERROR_NUMBER()); END CATCH;
IF @err <> N'53043' SET @fallo = ISNULL(NULLIF(@fallo,N''),N'Un chofer ajeno no recibio 53043 (' + @err + N').');
IF EXISTS(SELECT 1 FROM dbo.VehiculoKilometraje WHERE KilometrajeId=@id AND KmFinal IS NOT NULL) SET @fallo = ISNULL(NULLIF(@fallo,N''),N'Un chofer ajeno cerro la jornada.');
-- Km final menor al inicial.
SET @err = N''; BEGIN TRY EXEC dbo.sp_Kilometraje_Cerrar @KilometrajeId=@id, @KmFinal=-1, @ActorUsuarioId=@choferA; END TRY BEGIN CATCH SET @err = CONVERT(NVARCHAR(10),ERROR_NUMBER()); END CATCH;
IF @err <> N'53045' SET @fallo = ISNULL(NULLIF(@fallo,N''),N'Un km final menor no recibio 53045 (' + @err + N').');
-- El dueño cierra.
SET @err = N''; BEGIN TRY EXEC dbo.sp_Kilometraje_Cerrar @KilometrajeId=@id, @KmFinal=@odometro, @ActorUsuarioId=@choferA, @PuedeAdministrar=0; END TRY BEGIN CATCH SET @err = CONVERT(NVARCHAR(10),ERROR_NUMBER()); END CATCH;
IF @err <> N'' OR NOT EXISTS(SELECT 1 FROM dbo.VehiculoKilometraje WHERE KilometrajeId=@id AND KmFinal=@odometro AND FechaCierre IS NOT NULL) SET @fallo = ISNULL(NULLIF(@fallo,N''),N'El dueño no pudo cerrar su jornada (' + @err + N').');
-- Segundo cierre: 53044, tambien para el administrador.
SET @err = N''; BEGIN TRY EXEC dbo.sp_Kilometraje_Cerrar @KilometrajeId=@id, @KmFinal=@odometro, @ActorUsuarioId=@choferA; END TRY BEGIN CATCH SET @err = CONVERT(NVARCHAR(10),ERROR_NUMBER()); END CATCH;
IF @err <> N'53044' SET @fallo = ISNULL(NULLIF(@fallo,N''),N'El segundo cierre no recibio 53044 (' + @err + N').');
SET @err = N''; BEGIN TRY EXEC dbo.sp_Kilometraje_Cerrar @KilometrajeId=@id, @KmFinal=@odometro, @ActorUsuarioId=@choferB, @PuedeAdministrar=1; END TRY BEGIN CATCH SET @err = CONVERT(NVARCHAR(10),ERROR_NUMBER()); END CATCH;
IF @err <> N'53044' SET @fallo = ISNULL(NULLIF(@fallo,N''),N'El segundo cierre administrativo no recibio 53044 (' + @err + N').');
-- Un administrador cierra la jornada abierta de otro.
SET @err = N''; BEGIN TRY EXEC dbo.sp_Kilometraje_Cerrar @KilometrajeId=@id2, @KmFinal=@odometro, @ActorUsuarioId=@choferB, @PuedeAdministrar=1; END TRY BEGIN CATCH SET @err = CONVERT(NVARCHAR(10),ERROR_NUMBER()); END CATCH;
IF @err <> N'' OR NOT EXISTS(SELECT 1 FROM dbo.VehiculoKilometraje WHERE KilometrajeId=@id2 AND KmFinal IS NOT NULL) SET @fallo = ISNULL(NULLIF(@fallo,N''),N'El administrador no pudo cerrar una jornada ajena (' + @err + N').');

DELETE dbo.VehiculoKilometraje WHERE KilometrajeId IN (@id, @id2);
IF @fallo <> N'' THROW 58314, @fallo, 1;
PRINT N'Verificacion 0312 completada.';
