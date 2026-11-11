SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

/* BUG-15-01, G2-18 y apoyo de BUG-15-02: órdenes de mantenimiento de la flota (CU-153).

   BUG-15-01: una orden correctiva En proceso no sacaba el vehículo de circulación.
     Ahora el vehículo pasa a inactivo mientras tenga una orden Correctiva EnProceso y
     vuelve a activo al completarla. Como Activo ya lo leen la lista de vehículos de una
     ruta nueva, la jornada del chofer y Vehículos, no hay que tocar esas pantallas.
     Vehiculos.InactivoPorMantenimiento distingue «inactivo por reparación» de un
     vehículo que alguien inactivó a mano: solo el primero se reactiva solo.
     Igual que al inactivar a mano (52093), un vehículo con rutas abiertas no pasa a
     reparación (53057): hay que finalizar o reasignar esas rutas antes.
   G2-18: una orden registrada como Completada guarda su fecha de realización (la
     indicada o la del día) y sp_Mantenimiento_MarcarCompletada ya recibe el costo final.
     sp_Mantenimiento_Iniciar pasa una orden Programada a EnProceso (con lo anterior).
     Validan: estado válido, costo y kilometraje no negativos, fecha de realización no
     futura y que una orden Completada o Cancelada no se complete otra vez.
   Vehículos: no se reactiva a mano un vehículo que está en reparación (52094) y
     sp_Vehiculos_List devuelve EnMantenimiento al final.

   El historial (BUG-15-02) ya devolvía FechaProgramada, FechaRealizada y KilometrajeProximo:
   lo corrige la pantalla. Rollback en 0287_mantenimiento_flota_correcciones.rollback.md. */
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL THROW 56870,N'Falta el ledger 0001.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0287_mantenimiento_flota_correcciones' AND Status=N'Applied')
    THROW 56871,N'0287 ya figura aplicada.',1;
IF OBJECT_ID(N'dbo.Vehiculos',N'U') IS NULL OR OBJECT_ID(N'dbo.OrdenesMantenimiento',N'U') IS NULL OR OBJECT_ID(N'dbo.Rutas',N'U') IS NULL
    THROW 56872,N'Faltan las tablas de vehículos, mantenimiento o rutas (cu15x).',1;

BEGIN TRANSACTION;
GO

IF COL_LENGTH(N'dbo.Vehiculos', N'InactivoPorMantenimiento') IS NULL
    ALTER TABLE dbo.Vehiculos ADD InactivoPorMantenimiento BIT NOT NULL CONSTRAINT DF_Vehiculos_InactivoMant DEFAULT 0;
GO

/* Interno: deja el vehículo en el estado que corresponde a sus órdenes correctivas En proceso. */
CREATE OR ALTER PROCEDURE dbo.sp_Vehiculos_SincronizarMantenimiento
    @VehiculoId INT
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM dbo.OrdenesMantenimiento WHERE VehiculoId = @VehiculoId AND Tipo = N'Correctivo' AND Estado = N'EnProceso')
        UPDATE dbo.Vehiculos SET Activo = 0, InactivoPorMantenimiento = 1
        WHERE VehiculoId = @VehiculoId AND Activo = 1;
    ELSE
        UPDATE dbo.Vehiculos SET Activo = 1, InactivoPorMantenimiento = 0
        WHERE VehiculoId = @VehiculoId AND InactivoPorMantenimiento = 1;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Mantenimiento_Create
    @VehiculoId INT,
    @Tipo       NVARCHAR(20),
    @Descripcion NVARCHAR(300),
    @Taller     NVARCHAR(150)  = NULL,
    @Costo      DECIMAL(18,2)  = 0,
    @Estado     NVARCHAR(20)   = N'Programada',
    @FechaProgramada DATE      = NULL,
    @FechaRealizada  DATE      = NULL,
    @KilometrajeProximo INT    = NULL,
    @UsuarioId  INT            = NULL,
    @Nombre     NVARCHAR(150)  = NULL
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Hoy DATE = CAST(SYSDATETIME() AS DATE);

    IF NOT EXISTS (SELECT 1 FROM dbo.Vehiculos WHERE VehiculoId = @VehiculoId)
        THROW 53050, N'El vehículo indicado no existe.', 1;
    SET @Descripcion = NULLIF(LTRIM(RTRIM(@Descripcion)), N'');
    IF @Descripcion IS NULL THROW 53051, N'Debe describir el mantenimiento.', 1;
    IF @Tipo NOT IN (N'Preventivo', N'Correctivo') THROW 53052, N'Tipo de mantenimiento no válido.', 1;
    SET @Estado = ISNULL(NULLIF(LTRIM(RTRIM(@Estado)), N''), N'Programada');
    IF @Estado NOT IN (N'Programada', N'EnProceso', N'Completada') THROW 53056, N'Estado de la orden no válido.', 1;
    IF ISNULL(@Costo, 0) < 0 THROW 53059, N'El costo no puede ser negativo.', 1;
    IF @KilometrajeProximo IS NOT NULL AND @KilometrajeProximo < 0 THROW 53425, N'El kilometraje próximo no puede ser negativo.', 1;

    IF @Estado = N'Completada'
        SET @FechaRealizada = ISNULL(@FechaRealizada, @Hoy);
    ELSE
        SET @FechaRealizada = NULL;
    IF @FechaRealizada > @Hoy THROW 53424, N'La fecha de realización no puede ser futura.', 1;

    IF @Tipo = N'Correctivo' AND @Estado = N'EnProceso'
       AND EXISTS (SELECT 1 FROM dbo.Rutas WHERE VehiculoId = @VehiculoId AND Estado IN (N'Planificada', N'Despachada'))
        THROW 53057, N'El vehículo tiene rutas abiertas: finalice o reasigne esas rutas antes de enviarlo a reparación.', 1;

    BEGIN TRANSACTION;
    BEGIN TRY
        INSERT INTO dbo.OrdenesMantenimiento
            (VehiculoId, Tipo, Descripcion, Taller, Costo, Estado, FechaProgramada, FechaRealizada,
             KilometrajeProximo, RegistradoPorUsuarioId, RegistradoPorNombre)
        VALUES
            (@VehiculoId, @Tipo, @Descripcion, NULLIF(LTRIM(RTRIM(@Taller)), N''), ISNULL(@Costo, 0),
             @Estado, @FechaProgramada, @FechaRealizada, @KilometrajeProximo, @UsuarioId, @Nombre);
        DECLARE @Id INT = CAST(SCOPE_IDENTITY() AS INT);

        EXEC dbo.sp_Vehiculos_SincronizarMantenimiento @VehiculoId = @VehiculoId;
        COMMIT;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK;
        THROW;
    END CATCH;

    SELECT @Id AS OrdenMantenimientoId,
           CAST(CASE WHEN EXISTS (SELECT 1 FROM dbo.Vehiculos WHERE VehiculoId = @VehiculoId AND InactivoPorMantenimiento = 1) THEN 1 ELSE 0 END AS BIT) AS VehiculoFueraDeCirculacion;
END;
GO

/* Programada -> EnProceso. Una correctiva En proceso saca el vehículo de circulación. */
CREATE OR ALTER PROCEDURE dbo.sp_Mantenimiento_Iniciar
    @OrdenMantenimientoId INT
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Estado NVARCHAR(40), @Tipo NVARCHAR(40), @VehiculoId INT;
    SELECT @Estado = Estado, @Tipo = Tipo, @VehiculoId = VehiculoId
    FROM dbo.OrdenesMantenimiento WHERE OrdenMantenimientoId = @OrdenMantenimientoId;

    IF @Estado IS NULL THROW 53053, N'No se encontró la orden de mantenimiento.', 1;
    IF @Estado <> N'Programada' THROW 53058, N'Solo se puede iniciar una orden programada.', 1;
    IF @Tipo = N'Correctivo'
       AND EXISTS (SELECT 1 FROM dbo.Rutas WHERE VehiculoId = @VehiculoId AND Estado IN (N'Planificada', N'Despachada'))
        THROW 53057, N'El vehículo tiene rutas abiertas: finalice o reasigne esas rutas antes de enviarlo a reparación.', 1;

    BEGIN TRANSACTION;
    BEGIN TRY
        UPDATE dbo.OrdenesMantenimiento SET Estado = N'EnProceso' WHERE OrdenMantenimientoId = @OrdenMantenimientoId;
        EXEC dbo.sp_Vehiculos_SincronizarMantenimiento @VehiculoId = @VehiculoId;
        COMMIT;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK;
        THROW;
    END CATCH;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Mantenimiento_MarcarCompletada
    @OrdenMantenimientoId INT,
    @FechaRealizada DATE = NULL,
    @Costo DECIMAL(18,2) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Estado NVARCHAR(40), @VehiculoId INT, @Hoy DATE = CAST(SYSDATETIME() AS DATE);
    SELECT @Estado = Estado, @VehiculoId = VehiculoId
    FROM dbo.OrdenesMantenimiento WHERE OrdenMantenimientoId = @OrdenMantenimientoId;

    IF @Estado IS NULL THROW 53053, N'No se encontró la orden de mantenimiento.', 1;
    IF @Estado IN (N'Completada', N'Cancelada') THROW 53058, N'La orden ya está cerrada.', 1;
    IF @Costo IS NOT NULL AND @Costo < 0 THROW 53059, N'El costo no puede ser negativo.', 1;
    IF @FechaRealizada > @Hoy THROW 53424, N'La fecha de realización no puede ser futura.', 1;

    BEGIN TRANSACTION;
    BEGIN TRY
        UPDATE dbo.OrdenesMantenimiento
        SET Estado = N'Completada',
            FechaRealizada = ISNULL(@FechaRealizada, @Hoy),
            Costo = ISNULL(@Costo, Costo)
        WHERE OrdenMantenimientoId = @OrdenMantenimientoId;
        EXEC dbo.sp_Vehiculos_SincronizarMantenimiento @VehiculoId = @VehiculoId;
        COMMIT;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK;
        THROW;
    END CATCH;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Vehiculos_List
    @Buscar NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET @Buscar = NULLIF(LTRIM(RTRIM(@Buscar)), N'');

    SELECT
        v.VehiculoId,
        v.Placa,
        v.Descripcion,
        ISNULL(v.Capacidad, 0) AS Capacidad,
        v.Activo,
        (SELECT COUNT(*) FROM dbo.Rutas r
          WHERE r.VehiculoId = v.VehiculoId
            AND r.Estado IN (N'Planificada', N'Despachada')) AS RutasAbiertas,
        v.Marca,
        v.KilometrajeActual,
        v.InactivoPorMantenimiento AS EnMantenimiento
    FROM dbo.Vehiculos v
    WHERE (@Buscar IS NULL
           OR v.Placa       LIKE N'%' + @Buscar + N'%'
           OR v.Descripcion LIKE N'%' + @Buscar + N'%'
           OR v.Marca       LIKE N'%' + @Buscar + N'%')
    ORDER BY v.Activo DESC, v.Placa ASC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Vehiculos_ToggleStatus
    @VehiculoId INT
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.Vehiculos WHERE VehiculoId = @VehiculoId)
        THROW 52092, N'No se encontró el vehículo indicado.', 1;

    -- En reparación no se reactiva a mano: se reactiva al completar la orden correctiva.
    IF EXISTS (SELECT 1 FROM dbo.Vehiculos WHERE VehiculoId = @VehiculoId AND InactivoPorMantenimiento = 1)
        THROW 52094, N'El vehículo está en reparación (orden correctiva en proceso): complete la orden en Mantenimiento para volver a activarlo.', 1;

    -- No inactivar un vehículo con rutas abiertas
    IF EXISTS (SELECT 1 FROM dbo.Vehiculos WHERE VehiculoId = @VehiculoId AND Activo = 1)
       AND EXISTS (SELECT 1 FROM dbo.Rutas WHERE VehiculoId = @VehiculoId AND Estado IN (N'Planificada', N'Despachada'))
        THROW 52093, N'No se puede inactivar un vehículo con rutas abiertas.', 1;

    UPDATE dbo.Vehiculos SET Activo = CASE WHEN Activo = 1 THEN 0 ELSE 1 END
    WHERE VehiculoId = @VehiculoId;

    SELECT Activo FROM dbo.Vehiculos WHERE VehiculoId = @VehiculoId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Vehiculos_Update
    @VehiculoId  INT,
    @Placa       NVARCHAR(20),
    @Descripcion NVARCHAR(150),
    @Capacidad   INT = NULL,
    @Activo      BIT = 1,
    @Marca       NVARCHAR(60) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SET @Placa       = NULLIF(LTRIM(RTRIM(@Placa)), N'');
    SET @Descripcion = NULLIF(LTRIM(RTRIM(@Descripcion)), N'');
    SET @Marca       = NULLIF(LTRIM(RTRIM(@Marca)), N'');

    IF @Placa IS NULL OR @Descripcion IS NULL
        THROW 52090, N'La placa y la descripción son obligatorias.', 1;

    IF NOT EXISTS (SELECT 1 FROM dbo.Vehiculos WHERE VehiculoId = @VehiculoId)
        THROW 52092, N'No se encontró el vehículo indicado.', 1;

    IF EXISTS (SELECT 1 FROM dbo.Vehiculos WHERE Placa = @Placa AND VehiculoId <> @VehiculoId)
        THROW 52091, N'Ya existe un vehículo con esa placa.', 1;

    IF ISNULL(@Activo, 1) = 1
       AND EXISTS (SELECT 1 FROM dbo.Vehiculos WHERE VehiculoId = @VehiculoId AND InactivoPorMantenimiento = 1)
        THROW 52094, N'El vehículo está en reparación (orden correctiva en proceso): complete la orden en Mantenimiento para volver a activarlo.', 1;

    UPDATE dbo.Vehiculos
    SET Placa       = @Placa,
        Descripcion = @Descripcion,
        Capacidad   = @Capacidad,
        Activo      = ISNULL(@Activo, 1),
        Marca       = @Marca
    WHERE VehiculoId = @VehiculoId;
END;
GO

/* Órdenes correctivas En proceso que ya existían: el vehículo sale de circulación si no tiene rutas abiertas. */
UPDATE v SET Activo = 0, InactivoPorMantenimiento = 1
FROM dbo.Vehiculos v
WHERE v.Activo = 1
  AND EXISTS (SELECT 1 FROM dbo.OrdenesMantenimiento m WHERE m.VehiculoId = v.VehiculoId AND m.Tipo = N'Correctivo' AND m.Estado = N'EnProceso')
  AND NOT EXISTS (SELECT 1 FROM dbo.Rutas r WHERE r.VehiculoId = v.VehiculoId AND r.Estado IN (N'Planificada', N'Despachada'));
GO

IF XACT_STATE()<>1 THROW 56873,N'La transacción 0287 no está disponible.',1;
DECLARE @MigrationSha256 NVARCHAR(128)=N'$(MigrationSha256)';
IF LEN(@MigrationSha256)<>64 OR @MigrationSha256 LIKE N'%[^0-9A-Fa-f]%' THROW 56874,N'SHA-256 inválido para 0287.',1;
INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes)
VALUES(N'0287_mantenimiento_flota_correcciones',N'0287_mantenimiento_flota_correcciones.sql',UPPER(@MigrationSha256),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),
       N'BUG-15-01, G2-18: la orden correctiva En proceso saca el vehículo de circulación; fecha de realización y costo final; iniciar orden.');
COMMIT TRANSACTION;
GO
