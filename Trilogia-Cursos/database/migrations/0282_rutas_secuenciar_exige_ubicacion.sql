SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

/* BUG-25-01 · CU-251 escenario 2: secuenciar una ruta con clientes sin coordenadas
   la procesa en lugar de pedir que se fije el punto antes.

   sp_Rutas_Secuenciar reordenaba la ruta aunque faltaran coordenadas (dejaba esas
   paradas al final) y solo avisaba despues. Ahora, si alguna parada pendiente no
   tiene latitud y longitud, el procedimiento NO modifica nada y rechaza con 54012
   diciendo cuantas faltan, para que se ubiquen en el mapa (Guardar orden y
   ubicaciones) y se vuelva a secuenciar.

   Es la definicion de la 0281 (prioridad y luego cercania) con esa sola validacion
   antes de ordenar. No cambia datos ni tablas. sp_Rutas_Recalcular no cambia: una
   ruta ya despachada se recalcula con lo que tenga, para no frenar al chofer en la calle.
   Rollback en 0282_rutas_secuenciar_exige_ubicacion.rollback.md. */
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL THROW 56820,N'Falta el ledger 0001.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0282_rutas_secuenciar_exige_ubicacion' AND Status=N'Applied')
    THROW 56821,N'0282 ya figura aplicada.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0281_rutas_prioridad_secuenciacion' AND Status=N'Applied')
    THROW 56822,N'0282 parte de la versión de sp_Rutas_Secuenciar de la 0281. Aplicar 0281 primero.',1;

BEGIN TRANSACTION;
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

    -- 0282 (BUG-25-01): sin coordenada no se secuencia; primero se ubica la parada en el mapa.
    DECLARE @SinCoord INT = (SELECT COUNT(*) FROM @Stops WHERE TieneCoord = 0);
    IF @SinCoord > 0
    BEGIN
        DECLARE @Mensaje NVARCHAR(400) = CONCAT(
            @SinCoord, N' de ', @Total,
            N' punto(s) de la ruta no tienen coordenada. Ubíquelos en el mapa y guarde las ubicaciones antes de secuenciar la ruta.');
        THROW 54012, @Mensaje, 1;
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

    BEGIN TRANSACTION;
    UPDATE rp
    SET rp.Secuencia = s.NuevaSecuencia, rp.FechaActualizacion = SYSDATETIME()
    FROM dbo.RutaPedidos rp
    INNER JOIN @Stops s ON s.RutaPedidoId = rp.RutaPedidoId;
    UPDATE dbo.Rutas SET FechaActualizacion = SYSDATETIME() WHERE RutaId = @RutaId;
    COMMIT TRANSACTION;

    SELECT @Total AS Secuenciados, 0 AS SinCoordenadas;   -- E1
END;
GO

IF XACT_STATE()<>1 THROW 56823,N'La transacción 0282 no está disponible.',1;
DECLARE @MigrationSha256 NVARCHAR(128)=N'$(MigrationSha256)';
IF LEN(@MigrationSha256)<>64 OR @MigrationSha256 LIKE N'%[^0-9A-Fa-f]%' THROW 56824,N'SHA-256 inválido para 0282.',1;
INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes)
VALUES(N'0282_rutas_secuenciar_exige_ubicacion',N'0282_rutas_secuenciar_exige_ubicacion.sql',UPPER(@MigrationSha256),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),
       N'BUG-25-01: sp_Rutas_Secuenciar rechaza (54012) una ruta con paradas sin coordenada, sin modificarla. No cambia datos.');
COMMIT TRANSACTION;
GO
