SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

/* fin-mejoras M-09: «Recalcular ruta» dejaba la numeración repetida (1, 1, 2, 2, 3).

   sp_Rutas_Recalcular (0281) volvia a numerar solo las paradas por visitar empezando en 1, mientras las paradas ya
   resueltas (Entregado o Fallido) conservaban su numero anterior: ambas listas compartian los numeros 1, 2...

   Ahora, al recalcular:
     1. Las paradas resueltas conservan su orden relativo y quedan numeradas 1..R (sin huecos ni repetidos).
     2. Las paradas por visitar siguen despues: R+1, R+2... con la misma regla de 0281 (prioridad y luego cercania,
        las paradas sin coordenada al final).

   Solo reemplaza dbo.sp_Rutas_Recalcular; no cambia tablas ni datos existentes. sp_Rutas_Secuenciar no cambia
   (solo corre con la ruta Planificada, cuando todas las paradas estan pendientes).
   Rollback en 0337_rutas_recalcular_numeracion.rollback.md. */
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL THROW 58540,N'Falta el ledger 0001.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0337_rutas_recalcular_numeracion' AND Status=N'Applied')
    THROW 58541,N'0337 ya figura aplicada.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0281_rutas_prioridad_secuenciacion' AND Status=N'Applied')
    THROW 58542,N'0337 parte de la versión de sp_Rutas_Recalcular de la 0281. Aplicar 0281 primero.',1;

BEGIN TRANSACTION;
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

    -- 0337 (M-09): las paradas ya resueltas conservan su orden y quedan numeradas 1..R.
    UPDATE rp
    SET rp.Secuencia = r.rn
    FROM dbo.RutaPedidos rp
    INNER JOIN (
        SELECT x.RutaPedidoId, ROW_NUMBER() OVER (ORDER BY x.Secuencia, x.RutaPedidoId) AS rn
        FROM dbo.RutaPedidos x
        WHERE x.RutaId = @RutaId AND x.EstadoEntrega NOT IN (N'Pendiente', N'EnRuta')
    ) r ON r.RutaPedidoId = rp.RutaPedidoId
    WHERE rp.Secuencia <> r.rn;
    DECLARE @Resueltas INT = (SELECT COUNT(*) FROM dbo.RutaPedidos WHERE RutaId = @RutaId AND EstadoEntrega NOT IN (N'Pendiente', N'EnRuta'));

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

    -- Las paradas por visitar siguen a las resueltas: la numeración empieza en @Resueltas + 1.
    DECLARE @Seq INT = @Resueltas, @CurLat DECIMAL(9,6) = @OrigenLat, @CurLng DECIMAL(9,6) = @OrigenLng,
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

IF XACT_STATE()<>1 THROW 58543,N'La transacción 0337 no está disponible.',1;
DECLARE @MigrationSha256 NVARCHAR(128)=N'$(MigrationSha256)';
IF LEN(@MigrationSha256)<>64 OR @MigrationSha256 LIKE N'%[^0-9A-Fa-f]%' THROW 58544,N'SHA-256 inválido para 0337.',1;
INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes)
VALUES(N'0337_rutas_recalcular_numeracion',N'0337_rutas_recalcular_numeracion.sql',UPPER(@MigrationSha256),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),
       N'fin-mejoras M-09: sp_Rutas_Recalcular numera 1..R las paradas resueltas y R+1.. las pendientes (antes la numeracion se repetia: 1,1,2,2,3). Reemplaza un procedimiento; sin cambios de tablas.');
COMMIT TRANSACTION;
GO
