SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

/* BUG-06-04 · CU-063: un cliente inactivo sigue apareciendo con sus pedidos
   pendientes al crear rutas.

   Al inactivar un cliente (CU-063) desaparece de Venta movil, pero
   sp_Rutas_GetAssignableOrders, sp_Rutas_AddOrder y sp_Rutas_Create no miraban
   Usuarios.Activo, asi que sus pedidos pendientes seguian ofrecidos y se podian
   asignar a una ruta.

   Se reemplazan esos tres procedimientos (definiciones vigentes de
   cu081/cu302) con un solo cambio cada uno:
     - GetAssignableOrders: no devuelve pedidos de clientes inactivos.
     - AddOrder: rechaza el pedido de un cliente inactivo (52014).
     - Create: rechaza la ruta si alguno de sus pedidos es de un cliente
       inactivo (52006).
   Solo afecta lo que se asigna a partir de ahora: un pedido de un cliente que
   se inactiva cuando su ruta ya esta armada o despachada se entrega igual.

   No cambia datos ni tablas. Rollback en 0280_rutas_cliente_inactivo.rollback.md. */
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL THROW 56800,N'Falta el ledger 0001.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0280_rutas_cliente_inactivo' AND Status=N'Applied')
    THROW 56801,N'0280 ya figura aplicada.',1;
IF OBJECT_ID(N'dbo.Rutas',N'U') IS NULL OR OBJECT_ID(N'dbo.RutaPedidos',N'U') IS NULL OR COL_LENGTH(N'dbo.Usuarios',N'Activo') IS NULL
    THROW 56802,N'Faltan las tablas de rutas (CU-081) o Usuarios.Activo.',1;
IF OBJECT_ID(N'dbo.sp_Rutas_GetAssignableOrders',N'P') IS NULL OR OBJECT_ID(N'dbo.sp_Rutas_AddOrder',N'P') IS NULL OR OBJECT_ID(N'dbo.sp_Rutas_Create',N'P') IS NULL
    THROW 56803,N'Faltan los procedimientos de rutas (cu081/cu302).',1;

BEGIN TRANSACTION;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Rutas_GetAssignableOrders
    @Buscar NVARCHAR(150) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SET @Buscar = NULLIF(LTRIM(RTRIM(@Buscar)), N'');

    SELECT
        p.PedidoId,
        u.NombreCompleto                         AS Cliente,
        u.Correo,
        p.FechaPedido,
        p.Estado,
        ISNULL(p.TipoEntrega, N'')               AS TipoEntrega,
        ISNULL(NULLIF(p.DireccionEntrega, N''), u.Direccion) AS DireccionEntrega,
        p.Total,
        (SELECT COUNT(*) FROM dbo.PedidoDetalle d WHERE d.PedidoId = p.PedidoId) AS TotalLineas
    FROM dbo.Pedidos p
    INNER JOIN dbo.Usuarios u ON u.UsuarioId = p.UsuarioId
    WHERE p.Estado NOT IN (N'Cancelado', N'Rechazado', N'Entregado', N'Retenido')
      AND u.Activo = 1   -- 0280 (BUG-06-04): el cliente inactivo no se ofrece en las rutas
      AND ISNULL(NULLIF(p.DireccionEntrega, N''), u.Direccion) IS NOT NULL
      AND NOT EXISTS (
          SELECT 1
          FROM dbo.RutaPedidos rp
          INNER JOIN dbo.Rutas r ON r.RutaId = rp.RutaId
          WHERE rp.PedidoId = p.PedidoId
            AND r.Estado <> N'Cancelada'
      )
      AND (
          @Buscar IS NULL
          OR u.NombreCompleto LIKE N'%' + @Buscar + N'%'
          OR u.Correo         LIKE N'%' + @Buscar + N'%'
          OR CAST(p.PedidoId AS NVARCHAR(20)) = @Buscar
          OR ISNULL(p.DireccionEntrega, N'')  LIKE N'%' + @Buscar + N'%'
      )
    ORDER BY p.FechaPedido ASC, p.PedidoId ASC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Rutas_AddOrder
    @RutaId   INT,
    @PedidoId INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @EstadoRuta NVARCHAR(20), @Secuencia INT, @Lat DECIMAL(9,6), @Lng DECIMAL(9,6);

    BEGIN TRANSACTION;

    SELECT @EstadoRuta = Estado FROM dbo.Rutas WITH (UPDLOCK, HOLDLOCK) WHERE RutaId = @RutaId;

    IF @EstadoRuta IS NULL
    BEGIN ROLLBACK TRANSACTION; THROW 52010, 'No se encontró la ruta indicada.', 1; END;

    IF @EstadoRuta <> N'Planificada'
    BEGIN ROLLBACK TRANSACTION; THROW 52011, 'Solo se pueden modificar rutas en estado Planificada (antes del despacho).', 1; END;

    IF NOT EXISTS (
        SELECT 1 FROM dbo.Pedidos
        WHERE PedidoId = @PedidoId
          AND Estado NOT IN (N'Cancelado', N'Rechazado', N'Entregado', N'Retenido')
    )
    BEGIN ROLLBACK TRANSACTION; THROW 52012, 'El pedido no existe o no se puede asignar por su estado.', 1; END;

    -- 0280 (BUG-06-04): el pedido de un cliente inactivo no se asigna a una ruta.
    IF EXISTS (
        SELECT 1 FROM dbo.Pedidos p
        INNER JOIN dbo.Usuarios u ON u.UsuarioId = p.UsuarioId
        WHERE p.PedidoId = @PedidoId AND u.Activo = 0
    )
    BEGIN ROLLBACK TRANSACTION; THROW 52014, 'El cliente de este pedido está inactivo: no se puede asignar a una ruta.', 1; END;

    IF EXISTS (
        SELECT 1 FROM dbo.RutaPedidos rp
        INNER JOIN dbo.Rutas r ON r.RutaId = rp.RutaId AND r.Estado <> N'Cancelada'
        WHERE rp.PedidoId = @PedidoId
    )
    BEGIN ROLLBACK TRANSACTION; THROW 52013, 'El pedido ya está asignado a una ruta activa.', 1; END;

    SELECT @Secuencia = ISNULL(MAX(Secuencia), 0) + 1 FROM dbo.RutaPedidos WHERE RutaId = @RutaId;

    SELECT @Lat = u.Latitud, @Lng = u.Longitud
    FROM dbo.Pedidos p INNER JOIN dbo.Usuarios u ON u.UsuarioId = p.UsuarioId
    WHERE p.PedidoId = @PedidoId;

    INSERT INTO dbo.RutaPedidos (RutaId, PedidoId, Secuencia, EstadoEntrega, Latitud, Longitud, FechaActualizacion)
    VALUES (@RutaId, @PedidoId, @Secuencia, N'Pendiente', @Lat, @Lng, SYSDATETIME());

    UPDATE dbo.Rutas SET FechaActualizacion = SYSDATETIME() WHERE RutaId = @RutaId;

    COMMIT TRANSACTION;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Rutas_Create
    @Zona               NVARCHAR(120),
    @ChoferUsuarioId    INT,
    @VehiculoId         INT,
    @Observaciones      NVARCHAR(300)   = NULL,
    @PedidosJson        NVARCHAR(MAX),
    @CreadaPorUsuarioId INT             = NULL,
    @CreadaPorNombre    NVARCHAR(150)   = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @RutaId INT, @Codigo NVARCHAR(30);

    SET @Zona = NULLIF(LTRIM(RTRIM(@Zona)), N'');
    IF @Zona IS NULL
        THROW 52000, 'Debe indicar la zona de la ruta.', 1;

    DECLARE @Items TABLE (PedidoId INT NOT NULL PRIMARY KEY);
    INSERT INTO @Items (PedidoId)
    SELECT DISTINCT PedidoId
    FROM OPENJSON(ISNULL(@PedidosJson, N''))
    WITH (PedidoId INT '$.pedidoId')
    WHERE PedidoId IS NOT NULL;

    IF NOT EXISTS (SELECT 1 FROM @Items)
        THROW 52003, 'Debe seleccionar al menos un pedido para la ruta.', 1;

    BEGIN TRANSACTION;

    IF NOT EXISTS (
        SELECT 1 FROM dbo.Usuarios u
        INNER JOIN dbo.Perfiles pf ON pf.PerfilId = u.PerfilId
        WHERE u.UsuarioId = @ChoferUsuarioId AND u.Activo = 1 AND pf.Nombre = N'Chofer'
    )
    BEGIN ROLLBACK TRANSACTION; THROW 52001, 'No hay chofer disponible o el chofer seleccionado no es válido.', 1; END;

    IF NOT EXISTS (SELECT 1 FROM dbo.Vehiculos v WHERE v.VehiculoId = @VehiculoId AND v.Activo = 1)
    BEGIN ROLLBACK TRANSACTION; THROW 52002, 'No hay vehículo disponible o el vehículo seleccionado no es válido.', 1; END;

    IF EXISTS (
        SELECT 1 FROM @Items i
        LEFT JOIN dbo.Pedidos p ON p.PedidoId = i.PedidoId
        WHERE p.PedidoId IS NULL
           OR p.Estado IN (N'Cancelado', N'Rechazado', N'Entregado', N'Retenido')
    )
    BEGIN ROLLBACK TRANSACTION; THROW 52004, 'Uno o más pedidos ya no se pueden asignar (estado no válido).', 1; END;

    -- 0280 (BUG-06-04): ningún pedido de un cliente inactivo entra a una ruta nueva.
    IF EXISTS (
        SELECT 1 FROM @Items i
        INNER JOIN dbo.Pedidos p  ON p.PedidoId  = i.PedidoId
        INNER JOIN dbo.Usuarios u ON u.UsuarioId = p.UsuarioId
        WHERE u.Activo = 0
    )
    BEGIN ROLLBACK TRANSACTION; THROW 52006, 'Uno o más pedidos son de un cliente inactivo: no se pueden asignar a una ruta.', 1; END;

    IF EXISTS (
        SELECT 1 FROM @Items i
        INNER JOIN dbo.RutaPedidos rp ON rp.PedidoId = i.PedidoId
        INNER JOIN dbo.Rutas r        ON r.RutaId    = rp.RutaId AND r.Estado <> N'Cancelada'
    )
    BEGIN ROLLBACK TRANSACTION; THROW 52005, 'Uno o más pedidos ya están asignados a otra ruta activa.', 1; END;

    SET @Codigo = CONCAT(N'RUT-', FORMAT(SYSDATETIME(), 'yyyyMMddHHmmss'));

    INSERT INTO dbo.Rutas (Codigo, Zona, ChoferUsuarioId, VehiculoId, Estado, Observaciones,
                           CreadaPorUsuarioId, CreadaPorNombre, FechaCreacion, FechaActualizacion)
    VALUES (@Codigo, @Zona, @ChoferUsuarioId, @VehiculoId, N'Planificada',
            NULLIF(LTRIM(RTRIM(@Observaciones)), N''),
            @CreadaPorUsuarioId, @CreadaPorNombre, SYSDATETIME(), SYSDATETIME());

    SET @RutaId = CAST(SCOPE_IDENTITY() AS INT);

    -- Siembra la coordenada guardada del cliente (si existe)
    INSERT INTO dbo.RutaPedidos (RutaId, PedidoId, Secuencia, EstadoEntrega, Latitud, Longitud, FechaActualizacion)
    SELECT @RutaId, i.PedidoId,
           ROW_NUMBER() OVER (ORDER BY i.PedidoId),
           N'Pendiente', u.Latitud, u.Longitud, SYSDATETIME()
    FROM @Items i
    INNER JOIN dbo.Pedidos  p ON p.PedidoId  = i.PedidoId
    INNER JOIN dbo.Usuarios u ON u.UsuarioId = p.UsuarioId;

    COMMIT TRANSACTION;

    SELECT @RutaId AS RutaId, @Codigo AS Codigo;
END;
GO

IF XACT_STATE()<>1 THROW 56804,N'La transacción 0280 no está disponible.',1;
DECLARE @MigrationSha256 NVARCHAR(128)=N'$(MigrationSha256)';
IF LEN(@MigrationSha256)<>64 OR @MigrationSha256 LIKE N'%[^0-9A-Fa-f]%' THROW 56805,N'SHA-256 inválido para 0280.',1;
INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes)
VALUES(N'0280_rutas_cliente_inactivo',N'0280_rutas_cliente_inactivo.sql',UPPER(@MigrationSha256),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),
       N'BUG-06-04: GetAssignableOrders, AddOrder y Create excluyen pedidos de clientes inactivos. Solo reemplaza procedimientos; no cambia datos.');
COMMIT TRANSACTION;
GO
