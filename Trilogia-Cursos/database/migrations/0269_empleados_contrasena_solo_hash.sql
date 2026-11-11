SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL OR OBJECT_ID(N'dbo.Usuarios',N'U') IS NULL OR OBJECT_ID(N'dbo.Empleados',N'U') IS NULL THROW 58690,N'Faltan dependencias de empleados (0017).',1;
IF COL_LENGTH(N'dbo.Usuarios',N'ContrasenaHash') IS NULL OR COL_LENGTH(N'dbo.Usuarios',N'PasswordVersion') IS NULL OR COL_LENGTH(N'dbo.Usuarios',N'PasswordActualizadaUtc') IS NULL THROW 58691,N'Faltan las columnas de hash (0022).',1;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0310_credenciales_solo_hash' AND Status=N'Applied') THROW 58692,N'Falta la migracion 0310 (credenciales solo con hash).',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0269_empleados_contrasena_solo_hash' AND Status=N'Applied') THROW 58693,N'La migracion ya fue aplicada.',1;
BEGIN TRANSACTION;
GO
-- Contrasena de empleados solo con hash: el MVC genera el hash PBKDF2 (PasswordHasher) y los procedimientos
-- ya no reciben la contrasena en texto. Se retira el parametro @Contrasena; ya no depende del disparador de 0310.
CREATE OR ALTER PROCEDURE dbo.sp_Admin_CreateEmployee
    @PerfilId INT,
    @NombreCompleto NVARCHAR(150),
    @Correo NVARCHAR(150),
    @ContrasenaHash NVARCHAR(512),
    @Telefono NVARCHAR(30) = NULL,
    @Direccion NVARCHAR(255) = NULL,
    @Puesto NVARCHAR(100),
    @Departamento NVARCHAR(100) = NULL,
    @Salario DECIMAL(18,2) = 0,
    @FechaContratacion DATE = NULL,
    @Responsabilidades NVARCHAR(MAX) = NULL,
    @ObservacionesInternas NVARCHAR(MAX) = NULL,
    @Activo BIT = 1,
    @UsuarioCambioId INT = NULL,
    @UsuarioCambioNombre NVARCHAR(150) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF NULLIF(LTRIM(RTRIM(@ContrasenaHash)), N'') IS NULL
        THROW 51001, N'Se requiere la credencial en forma de hash.', 1;

    IF EXISTS (SELECT 1 FROM dbo.Usuarios WHERE Correo = @Correo)
    BEGIN
        THROW 51000, 'Ya existe un usuario registrado con ese correo.', 1;
    END;

    DECLARE @UsuarioId INT;
    DECLARE @EmpleadoId INT;

    BEGIN TRANSACTION;

    BEGIN TRY
        INSERT INTO dbo.Usuarios (PerfilId, NombreCompleto, Correo, Contrasena, ContrasenaHash, PasswordVersion, PasswordActualizadaUtc, Telefono, Direccion, Activo)
        VALUES (@PerfilId, @NombreCompleto, @Correo, N'', @ContrasenaHash, 1, SYSUTCDATETIME(), @Telefono, @Direccion, @Activo);

        SET @UsuarioId = SCOPE_IDENTITY();

        INSERT INTO dbo.Empleados (UsuarioId, Puesto, Salario, FechaContratacion, Activo, Departamento, Responsabilidades, ObservacionesInternas, FechaActualizacion)
        VALUES (@UsuarioId, @Puesto, @Salario, @FechaContratacion, @Activo, @Departamento, @Responsabilidades, @ObservacionesInternas, SYSDATETIME());

        SET @EmpleadoId = SCOPE_IDENTITY();

        IF ISNULL(@Salario, 0) > 0
        BEGIN
            INSERT INTO dbo.EmpleadoHistorialSalarios (EmpleadoId, SalarioAnterior, SalarioNuevo, Motivo, UsuarioCambioId, UsuarioCambioNombre)
            VALUES (@EmpleadoId, NULL, @Salario, 'Salario inicial registrado.', @UsuarioCambioId, @UsuarioCambioNombre);
        END;

        COMMIT TRANSACTION;

        SELECT @EmpleadoId AS EmpleadoId;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE dbo.sp_RRHH_UpdateEmployee
    @EmpleadoId INT,@PerfilId INT,@NombreCompleto NVARCHAR(150),@Correo NVARCHAR(150),@ContrasenaHash NVARCHAR(512)=NULL,
    @Telefono NVARCHAR(30)=NULL,@Direccion NVARCHAR(255)=NULL,@Puesto NVARCHAR(100),@Departamento NVARCHAR(100)=NULL,
    @Salario DECIMAL(18,2)=0,@FechaContratacion DATE=NULL,@Responsabilidades NVARCHAR(MAX)=NULL,
    @ObservacionesInternas NVARCHAR(MAX)=NULL,@Activo BIT=1,@MotivoCambioSalario NVARCHAR(255)=NULL,
    @UsuarioCambioId INT=NULL,@UsuarioCambioNombre NVARCHAR(150)=NULL,@VersionFila BINARY(8)
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    IF @Salario<0 THROW 55010,N'El salario no puede ser negativo.',1;
    SET @ContrasenaHash=NULLIF(LTRIM(RTRIM(@ContrasenaHash)),N'');
    BEGIN TRY
        BEGIN TRANSACTION;
        DECLARE @UsuarioId INT,@SalarioAnterior DECIMAL(18,2),@PuestoAnterior NVARCHAR(100),@DepartamentoAnterior NVARCHAR(100);
        SELECT @UsuarioId=e.UsuarioId,@SalarioAnterior=ISNULL(e.Salario,0),@PuestoAnterior=e.Puesto,@DepartamentoAnterior=e.Departamento
        FROM dbo.Empleados e WITH(UPDLOCK,HOLDLOCK) WHERE e.EmpleadoId=@EmpleadoId AND e.VersionFila=@VersionFila;
        IF @UsuarioId IS NULL THROW 55011,N'El expediente cambió o no existe.',1;
        IF EXISTS(SELECT 1 FROM dbo.Usuarios WHERE Correo=@Correo AND UsuarioId<>@UsuarioId) THROW 55012,N'El correo ya está asignado.',1;
        UPDATE dbo.Usuarios SET PerfilId=@PerfilId,NombreCompleto=@NombreCompleto,Correo=@Correo,Telefono=@Telefono,
            Direccion=@Direccion,Activo=@Activo,
            ContrasenaHash=COALESCE(@ContrasenaHash,ContrasenaHash),
            PasswordVersion=CASE WHEN @ContrasenaHash IS NULL THEN PasswordVersion ELSE 1 END,
            PasswordActualizadaUtc=CASE WHEN @ContrasenaHash IS NULL THEN PasswordActualizadaUtc ELSE SYSUTCDATETIME() END
        WHERE UsuarioId=@UsuarioId;
        UPDATE dbo.Empleados SET Puesto=@Puesto,Departamento=@Departamento,Salario=@Salario,FechaContratacion=@FechaContratacion,
            Responsabilidades=@Responsabilidades,ObservacionesInternas=@ObservacionesInternas,Activo=@Activo,FechaActualizacion=SYSUTCDATETIME()
        WHERE EmpleadoId=@EmpleadoId AND VersionFila=@VersionFila;
        IF @@ROWCOUNT<>1 THROW 55011,N'El expediente cambió mientras se editaba.',1;
        IF @SalarioAnterior<>@Salario
            INSERT dbo.EmpleadoHistorialSalarios(EmpleadoId,SalarioAnterior,SalarioNuevo,Motivo,UsuarioCambioId,UsuarioCambioNombre)
            VALUES(@EmpleadoId,@SalarioAnterior,@Salario,NULLIF(LTRIM(RTRIM(@MotivoCambioSalario)),N''),@UsuarioCambioId,@UsuarioCambioNombre);
        INSERT dbo.RRHHAuditoria(Entidad,EntidadId,Accion,Detalle,ActorUsuarioId,ActorNombre)
        VALUES(N'Empleado',@EmpleadoId,N'ACTUALIZAR',CONCAT(N'Puesto: ',COALESCE(@PuestoAnterior,N''),N' -> ',@Puesto,N'; departamento actualizado: ',CASE WHEN ISNULL(@DepartamentoAnterior,N'')<>ISNULL(@Departamento,N'') THEN N'sí' ELSE N'no' END,N'; salario actualizado: ',CASE WHEN @SalarioAnterior<>@Salario THEN N'sí' ELSE N'no' END),@UsuarioCambioId,COALESCE(NULLIF(@UsuarioCambioNombre,N''),N'Sistema'));
        COMMIT TRANSACTION;
    END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK TRANSACTION; THROW; END CATCH;
END;
GO
DECLARE @sha NVARCHAR(128)=N'$(MigrationSha256)';
IF LEN(@sha)<>64 OR @sha LIKE N'%[^0-9A-Fa-f]%' THROW 58694,N'SHA-256 invalido.',1;
INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes) VALUES(N'0269_empleados_contrasena_solo_hash',N'0269_empleados_contrasena_solo_hash.sql',UPPER(@sha),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),N'Seguridad: sp_Admin_CreateEmployee y sp_RRHH_UpdateEmployee reciben @ContrasenaHash (PBKDF2 generado por el MVC) en vez de @Contrasena en texto.');
COMMIT TRANSACTION;
GO
