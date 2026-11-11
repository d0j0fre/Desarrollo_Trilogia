SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

/* BUG-10-01 · CU-101: la secuenciacion automatica ordena solo por cercania y no
   considera la prioridad de los pedidos.

   Ni los pedidos ni las paradas de una ruta tenian prioridad. Esta migracion la
   agrega en la parada (dbo.RutaPedidos.Prioridad) y la usa al secuenciar:

       1 = Urgente   2 = Alta   3 = Normal (valor por omision)

   Regla de secuenciacion (sp_Rutas_Secuenciar y sp_Rutas_Recalcular):
     1. Se visitan primero las paradas de prioridad 1, luego las de 2 y al final
        las de 3.
     2. Dentro de cada nivel se sigue el vecino mas cercano, empezando desde el
        punto donde termino el nivel anterior (o desde el origen, la bodega, en
        el primer nivel).
     3. Las paradas sin coordenada van al final, por prioridad y luego por el
        orden que ya tenian.

   La prioridad se fija al crear la ruta (sp_Rutas_Create acepta "prioridad" en
   cada pedido del JSON) y se cambia mientras la ruta esta Planificada
   (sp_Rutas_GuardarSecuencia acepta "prioridad" por parada). Una prioridad fuera
   de 1 a 3 se rechaza con 54002.

   sp_Rutas_GetOrders devuelve Prioridad como ULTIMA columna: sus lectores
   (sitio web y API) leen por posicion o por nombre y no se alteran.

   Aditiva: una columna con valor por omision (las paradas existentes quedan en
   3 = Normal y se secuencian como antes) y cuatro procedimientos reemplazados.
   Rollback en 0281_rutas_prioridad_secuenciacion.rollback.md. */
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL THROW 56810,N'Falta el ledger 0001.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0281_rutas_prioridad_secuenciacion' AND Status=N'Applied')
    THROW 56811,N'0281 ya figura aplicada.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0280_rutas_cliente_inactivo' AND Status=N'Applied')
    THROW 56812,N'0281 parte de la versión de sp_Rutas_Create de la 0280. Aplicar 0280 primero.',1;
IF OBJECT_ID(N'dbo.RutaPedidos',N'U') IS NULL OR OBJECT_ID(N'dbo.Rutas',N'U') IS NULL
    THROW 56813,N'Faltan las tablas de rutas (CU-081).',1;
IF OBJECT_ID(N'dbo.sp_Rutas_Secuenciar',N'P') IS NULL OR OBJECT_ID(N'dbo.sp_Rutas_Recalcular',N'P') IS NULL
   OR OBJECT_ID(N'dbo.sp_Rutas_GuardarSecuencia',N'P') IS NULL OR OBJECT_ID(N'dbo.sp_Rutas_GetOrders',N'P') IS NULL
    THROW 56814,N'Faltan los procedimientos de secuenciación de rutas (cu251).',1;

BEGIN TRANSACTION;

IF COL_LENGTH(N'dbo.RutaPedidos', N'Prioridad') IS NULL
    ALTER TABLE dbo.RutaPedidos ADD Prioridad TINYINT NOT NULL
        CONSTRAINT DF_RutaPedidos_Prioridad DEFAULT (3)
        CONSTRAINT CK_RutaPedidos_Prioridad CHECK (Prioridad BETWEEN 1 AND 3);
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

    -- 0281: cada pedido puede traer "prioridad" (1 Urgente, 2 Alta, 3 Normal; sin dato = 3).
    DECLARE @Items TABLE (PedidoId INT NOT NULL PRIMARY KEY, Prioridad TINYINT NOT NULL);
    INSERT INTO @Items (PedidoId, Prioridad)
    SELECT PedidoId, ISNULL(MIN(Prioridad), 3)
    FROM OPENJSON(ISNULL(@PedidosJson, N''))
    WITH (PedidoId INT '$.pedidoId', Prioridad INT '$.prioridad')
    WHERE PedidoId IS NOT NULL
    GROUP BY PedidoId;

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

    IF EXISTS (
        SELECT 1 FROM @Items i
        INNER JOIN dbo.Pedidos p  ON p.PedidoId  = i.PedidoId
        INNER JOIN dbo.Usuarios u ON u.UsuarioId = p.UsuarioId
        WHERE u.Activo = 0
    )
    BEGIN ROLLBACK TRANSACTION; THROW 52006, 'Uno o más pedidos son de un cliente inactivo: no se pueden asignar a una ruta.', 1; END;

    IF EXISTS (SELECT 1 FROM @Items WHERE Prioridad NOT BETWEEN 1 AND 3)
    BEGIN ROLLBACK TRANSACTION; THROW 52007, 'La prioridad de un pedido debe ser 1 (Urgente), 2 (Alta) o 3 (Normal).', 1; END;

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

    -- Siembra la coordenada guardada del cliente (si existe) y la prioridad elegida
    INSERT INTO dbo.RutaPedidos (RutaId, PedidoId, Secuencia, EstadoEntrega, Latitud, Longitud, Prioridad, FechaActualizacion)
    SELECT @RutaId, i.PedidoId,
           ROW_NUMBER() OVER (ORDER BY i.Prioridad, i.PedidoId),
           N'Pendiente', u.Latitud, u.Longitud, i.Prioridad, SYSDATETIME()
    FROM @Items i
    INNER JOIN dbo.Pedidos  p ON p.PedidoId  = i.PedidoId
    INNER JOIN dbo.Usuarios u ON u.UsuarioId = p.UsuarioId;

    COMMIT TRANSACTION;

    SELECT @RutaId AS RutaId, @Codigo AS Codigo;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Rutas_GetOrders
    @RutaId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        rp.RutaPedidoId, rp.PedidoId, rp.Secuencia, rp.EstadoEntrega,
        ISNULL(rp.MotivoFallo, N'') AS MotivoFallo, rp.FechaEntrega,
        u.NombreCompleto AS Cliente, u.Correo AS ClienteCorreo,
        ISNULL(NULLIF(p.DireccionEntrega, N''), u.Direccion) AS DireccionEntrega,
        p.Total, p.Estado AS EstadoPedido,
        (SELECT COUNT(*) FROM dbo.EntregaEvidencias e WHERE e.PedidoId = rp.PedidoId) AS TotalEvidencias,
        rp.Latitud, rp.Longitud,
        rp.Prioridad   -- 0281: siempre la última columna
    FROM dbo.RutaPedidos rp
    INNER JOIN dbo.Pedidos  p ON p.PedidoId  = rp.PedidoId
    INNER JOIN dbo.Usuarios u ON u.UsuarioId = p.UsuarioId
    WHERE rp.RutaId = @RutaId
    ORDER BY rp.Secuencia ASC, rp.RutaPedidoId ASC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Rutas_GuardarSecuencia
    @RutaId    INT,
    @ItemsJson NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @EstadoRuta NVARCHAR(20) = (SELECT Estado FROM dbo.Rutas WHERE RutaId = @RutaId);
    IF @EstadoRuta IS NULL       THROW 54000, 'No se encontró la ruta indicada.', 1;
    IF @EstadoRuta <> N'Planificada'
        THROW 54001, 'Solo se puede reordenar la ruta antes de despacharla (estado Planificada).', 1;

    -- 0281: "prioridad" es opcional; sin dato la parada conserva la que tiene.
    DECLARE @Items TABLE (RutaPedidoId INT, Secuencia INT, Lat DECIMAL(9,6), Lng DECIMAL(9,6), Prioridad INT NULL);
    INSERT INTO @Items (RutaPedidoId, Secuencia, Lat, Lng, Prioridad)
    SELECT RutaPedidoId, Secuencia, Lat, Lng, Prioridad
    FROM OPENJSON(ISNULL(@ItemsJson, N''))
    WITH (RutaPedidoId INT '$.rutaPedidoId', Secuencia INT '$.secuencia',
          Lat DECIMAL(9,6) '$.lat', Lng DECIMAL(9,6) '$.lng', Prioridad INT '$.prioridad');

    IF EXISTS (SELECT 1 FROM @Items WHERE Prioridad IS NOT NULL AND Prioridad NOT BETWEEN 1 AND 3)
        THROW 54002, 'La prioridad de una parada debe ser 1 (Urgente), 2 (Alta) o 3 (Normal).', 1;

    BEGIN TRANSACTION;

    UPDATE rp
    SET rp.Secuencia = i.Secuencia,
        rp.Latitud   = i.Lat,
        rp.Longitud  = i.Lng,
        rp.Prioridad = ISNULL(CAST(i.Prioridad AS TINYINT), rp.Prioridad),
        rp.FechaActualizacion = SYSDATETIME()
    FROM dbo.RutaPedidos rp
    INNER JOIN @Items i ON i.RutaPedidoId = rp.RutaPedidoId
    WHERE rp.RutaId = @RutaId;

    -- Persistir la ubicación capturada en el cliente (reuso futuro)
    UPDATE u
    SET u.Latitud = i.Lat, u.Longitud = i.Lng
    FROM dbo.Usuarios u
    INNER JOIN dbo.Pedidos     p  ON p.UsuarioId = u.UsuarioId
    INNER JOIN dbo.RutaPedidos rp ON rp.PedidoId = p.PedidoId AND rp.RutaId = @RutaId
    INNER JOIN @Items i ON i.RutaPedidoId = rp.RutaPedidoId
    WHERE i.Lat IS NOT NULL AND i.Lng IS NOT NULL;

    UPDATE dbo.Rutas SET FechaActualizacion = SYSDATETIME() WHERE RutaId = @RutaId;

    COMMIT TRANSACTION;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Rutas_Secuenciar
    @RutaId    INT,
    @OrigenLat DECIMAL(9,6) = 9.933300,
    @OrigenLng DECIMAL(9,6) = -84.083300
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @EstadoRuta NVARCHAR(20) = (SELECT Estado FROM dbo.Rutas WHERE RutaId = @RutaId);
    IF @EstadoRuta IS NULL       THROW 54010, 'No se encontró la ruta indicada.', 1;
    IF @EstadoRuta <> N'Planificada'
        THROW 54011, 'Solo se puede secuenciar la ruta antes de despacharla (estado Planificada).', 1;

    DECLARE @Stops TABLE (
        RutaPedidoId INT PRIMARY KEY, Lat DECIMAL(9,6), Lng DECIMAL(9,6), Prioridad TINYINT NOT NULL,
        TieneCoord BIT, Visitado BIT DEFAULT 0, NuevaSecuencia INT NULL);
    INSERT INTO @Stops (RutaPedidoId, Lat, Lng, Prioridad, TieneCoord)
    SELECT rp.RutaPedidoId, rp.Latitud, rp.Longitud, rp.Prioridad,
           CASE WHEN rp.Latitud IS NOT NULL AND rp.Longitud IS NOT NULL THEN 1 ELSE 0 END
    FROM dbo.RutaPedidos rp
    WHERE rp.RutaId = @RutaId AND rp.EstadoEntrega = N'Pendiente';

    DECLARE @Total INT = (SELECT COUNT(*) FROM @Stops);
    IF @Total = 0
    BEGIN
        SELECT 0 AS Secuenciados, 0 AS SinCoordenadas;   -- E2
        RETURN;
    END;

    DECLARE @Seq INT = 0, @CurLat DECIMAL(9,6) = @OrigenLat, @CurLng DECIMAL(9,6) = @OrigenLng,
            @NextId INT, @Nivel TINYINT;

    -- 0281 (BUG-10-01): primero el nivel de prioridad más alto (1) y, dentro de cada
    -- nivel, el vecino más cercano al punto donde se quedó el recorrido.
    WHILE EXISTS (SELECT 1 FROM @Stops WHERE Visitado = 0 AND TieneCoord = 1)
    BEGIN
        SELECT @Nivel = MIN(Prioridad) FROM @Stops WHERE Visitado = 0 AND TieneCoord = 1;

        SELECT TOP 1 @NextId = RutaPedidoId
        FROM @Stops
        WHERE Visitado = 0 AND TieneCoord = 1 AND Prioridad = @Nivel
        ORDER BY (POWER(Lat - @CurLat, 2) + POWER((Lng - @CurLng) * COS(RADIANS(@CurLat)), 2)) ASC, RutaPedidoId ASC;

        SET @Seq += 1;
        UPDATE @Stops SET Visitado = 1, NuevaSecuencia = @Seq WHERE RutaPedidoId = @NextId;
        SELECT @CurLat = Lat, @CurLng = Lng FROM @Stops WHERE RutaPedidoId = @NextId;
    END;

    UPDATE s
    SET NuevaSecuencia = @Seq + orden.rn
    FROM @Stops s
    INNER JOIN (
        SELECT st.RutaPedidoId, ROW_NUMBER() OVER (ORDER BY st.Prioridad, rp.Secuencia, st.RutaPedidoId) AS rn
        FROM @Stops st INNER JOIN dbo.RutaPedidos rp ON rp.RutaPedidoId = st.RutaPedidoId
        WHERE st.TieneCoord = 0
    ) orden ON orden.RutaPedidoId = s.RutaPedidoId;

    BEGIN TRANSACTION;
    UPDATE rp
    SET rp.Secuencia = s.NuevaSecuencia, rp.FechaActualizacion = SYSDATETIME()
    FROM dbo.RutaPedidos rp
    INNER JOIN @Stops s ON s.RutaPedidoId = rp.RutaPedidoId;
    UPDATE dbo.Rutas SET FechaActualizacion = SYSDATETIME() WHERE RutaId = @RutaId;
    COMMIT TRANSACTION;

    SELECT @Total AS Secuenciados,
           (SELECT COUNT(*) FROM @Stops WHERE TieneCoord = 0) AS SinCoordenadas;   -- E1
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Rutas_Recalcular
    @RutaId              INT,
    @ExcluirRutaPedidoId INT           = NULL,
    @MotivoFallo         NVARCHAR(300)  = NULL,
    @OrigenLat           DECIMAL(9,6)   = 9.933300,
    @OrigenLng           DECIMAL(9,6)   = -84.083300
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @EstadoRuta NVARCHAR(20) = (SELECT Estado FROM dbo.Rutas WHERE RutaId = @RutaId);
    IF @EstadoRuta IS NULL       THROW 54020, 'No se encontró la ruta indicada.', 1;
    IF @EstadoRuta <> N'Despachada'
        THROW 54021, 'Solo se puede recalcular una ruta despachada (en curso).', 1;

    BEGIN TRANSACTION;

    -- Marca el punto afectado como Fallido (queda fuera del recorrido)
    IF @ExcluirRutaPedidoId IS NOT NULL
        UPDATE dbo.RutaPedidos
        SET EstadoEntrega = N'Fallido',
            MotivoFallo = ISNULL(NULLIF(LTRIM(RTRIM(@MotivoFallo)), N''), N'Imprevisto - recalculado'),
            FechaActualizacion = SYSDATETIME()
        WHERE RutaPedidoId = @ExcluirRutaPedidoId AND RutaId = @RutaId AND EstadoEntrega IN (N'Pendiente', N'EnRuta');

    -- Puntos restantes por visitar
    DECLARE @Stops TABLE (
        RutaPedidoId INT PRIMARY KEY, Lat DECIMAL(9,6), Lng DECIMAL(9,6), Prioridad TINYINT NOT NULL,
        TieneCoord BIT, Visitado BIT DEFAULT 0, NuevaSecuencia INT NULL);
    INSERT INTO @Stops (RutaPedidoId, Lat, Lng, Prioridad, TieneCoord)
    SELECT rp.RutaPedidoId, rp.Latitud, rp.Longitud, rp.Prioridad,
           CASE WHEN rp.Latitud IS NOT NULL AND rp.Longitud IS NOT NULL THEN 1 ELSE 0 END
    FROM dbo.RutaPedidos rp
    WHERE rp.RutaId = @RutaId AND rp.EstadoEntrega IN (N'Pendiente', N'EnRuta');

    DECLARE @Total INT = (SELECT COUNT(*) FROM @Stops);
    IF @Total = 0
    BEGIN
        COMMIT TRANSACTION;
        SELECT 0 AS Resecuenciados;  -- E2: no hay puntos por recalcular
        RETURN;
    END;

    DECLARE @Seq INT = 0, @CurLat DECIMAL(9,6) = @OrigenLat, @CurLng DECIMAL(9,6) = @OrigenLng,
            @NextId INT, @Nivel TINYINT;

    -- 0281 (BUG-10-01): misma regla que sp_Rutas_Secuenciar (prioridad y luego cercanía).
    WHILE EXISTS (SELECT 1 FROM @Stops WHERE Visitado = 0 AND TieneCoord = 1)
    BEGIN
        SELECT @Nivel = MIN(Prioridad) FROM @Stops WHERE Visitado = 0 AND TieneCoord = 1;

        SELECT TOP 1 @NextId = RutaPedidoId
        FROM @Stops
        WHERE Visitado = 0 AND TieneCoord = 1 AND Prioridad = @Nivel
        ORDER BY (POWER(Lat - @CurLat, 2) + POWER((Lng - @CurLng) * COS(RADIANS(@CurLat)), 2)) ASC, RutaPedidoId ASC;

        SET @Seq += 1;
        UPDATE @Stops SET Visitado = 1, NuevaSecuencia = @Seq WHERE RutaPedidoId = @NextId;
        SELECT @CurLat = Lat, @CurLng = Lng FROM @Stops WHERE RutaPedidoId = @NextId;
    END;

    UPDATE s
    SET NuevaSecuencia = @Seq + orden.rn
    FROM @Stops s
    INNER JOIN (
        SELECT st.RutaPedidoId, ROW_NUMBER() OVER (ORDER BY st.Prioridad, rp.Secuencia, st.RutaPedidoId) AS rn
        FROM @Stops st INNER JOIN dbo.RutaPedidos rp ON rp.RutaPedidoId = st.RutaPedidoId
        WHERE st.TieneCoord = 0
    ) orden ON orden.RutaPedidoId = s.RutaPedidoId;

    UPDATE rp
    SET rp.Secuencia = s.NuevaSecuencia, rp.FechaActualizacion = SYSDATETIME()
    FROM dbo.RutaPedidos rp INNER JOIN @Stops s ON s.RutaPedidoId = rp.RutaPedidoId;
    UPDATE dbo.Rutas SET FechaActualizacion = SYSDATETIME() WHERE RutaId = @RutaId;

    COMMIT TRANSACTION;
    SELECT @Total AS Resecuenciados;  -- E1
END;
GO

IF XACT_STATE()<>1 THROW 56815,N'La transacción 0281 no está disponible.',1;
DECLARE @MigrationSha256 NVARCHAR(128)=N'$(MigrationSha256)';
IF LEN(@MigrationSha256)<>64 OR @MigrationSha256 LIKE N'%[^0-9A-Fa-f]%' THROW 56816,N'SHA-256 inválido para 0281.',1;
INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes)
VALUES(N'0281_rutas_prioridad_secuenciacion',N'0281_rutas_prioridad_secuenciacion.sql',UPPER(@MigrationSha256),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),
       N'BUG-10-01: RutaPedidos.Prioridad (1 Urgente, 2 Alta, 3 Normal) y secuenciación por prioridad y luego cercanía. Aditiva.');
COMMIT TRANSACTION;
GO
