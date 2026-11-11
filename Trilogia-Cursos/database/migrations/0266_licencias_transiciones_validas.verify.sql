SET NOCOUNT ON;
IF OBJECT_ID(N'dbo.sp_Admin_UpdateEmployeeLeaveRequestStatus',N'P') IS NULL THROW 58663,N'Falta sp_Admin_UpdateEmployeeLeaveRequestStatus.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0266_licencias_transiciones_validas' AND Status=N'Applied') THROW 58664,N'La migracion 0266 no esta registrada.',1;
IF OBJECT_DEFINITION(OBJECT_ID(N'dbo.sp_Admin_UpdateEmployeeLeaveRequestStatus')) NOT LIKE N'%no vuelve a Pendiente%' THROW 58665,N'El procedimiento no valida las transiciones.',1;
-- Prueba funcional deshecha sobre una solicitud sintetica.
DECLARE @emp INT=(SELECT TOP (1) EmpleadoId FROM dbo.Empleados ORDER BY EmpleadoId);
IF @emp IS NOT NULL
BEGIN
    BEGIN TRANSACTION;
    DECLARE @id INT,@ok BIT;
    INSERT dbo.EmpleadoSolicitudesTiempoLibre(EmpleadoId,FechaInicio,FechaFin,CantidadDias,TipoSolicitud,Motivo,Estado)
    VALUES(@emp,DATEADD(DAY,30,CONVERT(DATE,GETDATE())),DATEADD(DAY,31,CONVERT(DATE,GETDATE())),2,N'Con goce salarial',N'verificacion 0266',N'Pendiente');
    SET @id=SCOPE_IDENTITY();
    -- Pendiente -> Cancelada no esta permitido.
    SET @ok=1; BEGIN TRY EXEC dbo.sp_Admin_UpdateEmployeeLeaveRequestStatus @SolicitudId=@id,@Estado=N'Cancelada'; END TRY BEGIN CATCH SET @ok=0; END CATCH;
    IF @ok=1 THROW 58666,N'Se cancelo una solicitud pendiente.',1;
    EXEC dbo.sp_Admin_UpdateEmployeeLeaveRequestStatus @SolicitudId=@id,@Estado=N'Aprobada';
    -- Aprobada -> Pendiente / Rechazada no esta permitido.
    SET @ok=1; BEGIN TRY EXEC dbo.sp_Admin_UpdateEmployeeLeaveRequestStatus @SolicitudId=@id,@Estado=N'Pendiente'; END TRY BEGIN CATCH SET @ok=0; END CATCH;
    IF @ok=1 THROW 58667,N'Una aprobada volvio a Pendiente.',1;
    SET @ok=1; BEGIN TRY EXEC dbo.sp_Admin_UpdateEmployeeLeaveRequestStatus @SolicitudId=@id,@Estado=N'Rechazada'; END TRY BEGIN CATCH SET @ok=0; END CATCH;
    IF @ok=1 THROW 58668,N'Una aprobada paso a Rechazada.',1;
    -- Aprobada que aun no empieza -> Cancelada si esta permitido.
    EXEC dbo.sp_Admin_UpdateEmployeeLeaveRequestStatus @SolicitudId=@id,@Estado=N'Cancelada';
    IF (SELECT Estado FROM dbo.EmpleadoSolicitudesTiempoLibre WHERE SolicitudId=@id)<>N'Cancelada' THROW 58669,N'No se cancelo la aprobada futura.',1;
    ROLLBACK TRANSACTION;
END;
PRINT N'Verificacion 0266 completada.';
