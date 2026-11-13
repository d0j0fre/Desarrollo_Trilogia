SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

/* BUG-10-02 · CU-104: el tablero de entregas lista rutas y contadores, pero no los
   pedidos En ruta ni los pendientes de entrega.

   dbo.sp_Entregas_PedidosTablero devuelve, en un solo resultado, los pedidos que
   el tablero debe listar:

     Grupo = 'EnRuta'     paradas En ruta de rutas Despachadas (ya salieron).
     Grupo = 'Pendiente'  pedidos a domicilio que todavia no salen:
                            - parada Pendiente de una ruta Planificada o Despachada, o
                            - sin ruta activa (los que Nueva ruta ofrece asignar; el
                              pedido de un cliente inactivo no se cuenta, igual que alli).
                          No incluye Cancelado, Rechazado, Entregado, Retenido ni Fallido
                          (la entrega fallida vuelve a bodega, no esta pendiente).

   Cada grupo se limita a @Maximo filas (200 por omision) ordenadas por ruta y secuencia
   (En ruta) o por antiguedad del pedido (Pendiente). Solo lectura.
   Rollback en 0283_tablero_entregas_pedidos.rollback.md. */
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL THROW 56830,N'Falta el ledger 0001.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0283_tablero_entregas_pedidos' AND Status=N'Applied')
    THROW 56831,N'0283 ya figura aplicada.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0281_rutas_prioridad_secuenciacion' AND Status=N'Applied')
    THROW 56832,N'0283 lee RutaPedidos.Prioridad, que crea la 0281. Aplicar 0281 primero.',1;

BEGIN TRANSACTION;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Entregas_PedidosTablero
    @Maximo INT = 200
AS
BEGIN
    SET NOCOUNT ON;
    SET @Maximo = CASE WHEN @Maximo IS NULL OR @Maximo < 1 THEN 200 WHEN @Maximo > 1000 THEN 1000 ELSE @Maximo END;

    -- En ruta: la parada salió con el chofer.
    SELECT * FROM (
        SELECT TOP (@Maximo)
            CAST(N'EnRuta' AS NVARCHAR(10)) AS Grupo,
            p.PedidoId,
            u.NombreCompleto AS Cliente,
            ISNULL(NULLIF(p.DireccionEntrega, N''), u.Direccion) AS Direccion,
            p.Total,
            p.FechaPedido,
            r.RutaId,
            r.Codigo AS RutaCodigo,
            r.Estado AS RutaEstado,
            ISNULL(ch.NombreCompleto, N'') AS Chofer,
            rp.Secuencia,
            rp.Prioridad
        FROM dbo.RutaPedidos rp
        INNER JOIN dbo.Rutas    r  ON r.RutaId    = rp.RutaId AND r.Estado = N'Despachada'
        INNER JOIN dbo.Pedidos  p  ON p.PedidoId  = rp.PedidoId
        INNER JOIN dbo.Usuarios u  ON u.UsuarioId = p.UsuarioId
        LEFT  JOIN dbo.Usuarios ch ON ch.UsuarioId = r.ChoferUsuarioId
        WHERE rp.EstadoEntrega = N'EnRuta'
        ORDER BY r.Codigo, rp.Secuencia, rp.RutaPedidoId
    ) enruta

    UNION ALL

    -- Pendientes de entrega: con ruta todavía sin salir, o sin ruta.
    SELECT * FROM (
        SELECT TOP (@Maximo)
            CAST(N'Pendiente' AS NVARCHAR(10)) AS Grupo,
            p.PedidoId,
            u.NombreCompleto AS Cliente,
            ISNULL(NULLIF(p.DireccionEntrega, N''), u.Direccion) AS Direccion,
            p.Total,
            p.FechaPedido,
            r.RutaId,
            r.Codigo AS RutaCodigo,
            r.Estado AS RutaEstado,
            ISNULL(ch.NombreCompleto, N'') AS Chofer,
            rp.Secuencia,
            rp.Prioridad
        FROM dbo.Pedidos p
        INNER JOIN dbo.Usuarios u ON u.UsuarioId = p.UsuarioId
        LEFT  JOIN dbo.RutaPedidos rp ON rp.PedidoId = p.PedidoId
                  AND EXISTS (SELECT 1 FROM dbo.Rutas rr WHERE rr.RutaId = rp.RutaId AND rr.Estado <> N'Cancelada')
        LEFT  JOIN dbo.Rutas    r  ON r.RutaId = rp.RutaId
        LEFT  JOIN dbo.Usuarios ch ON ch.UsuarioId = r.ChoferUsuarioId
        WHERE p.TipoEntrega = N'Entrega a domicilio'
          AND p.Estado NOT IN (N'Cancelado', N'Rechazado', N'Entregado', N'Retenido', N'Fallido')
          AND (
                (rp.RutaPedidoId IS NOT NULL AND rp.EstadoEntrega = N'Pendiente' AND r.Estado IN (N'Planificada', N'Despachada'))
             OR (rp.RutaPedidoId IS NULL AND u.Activo = 1)
          )
        ORDER BY p.FechaPedido, p.PedidoId
    ) pendientes;
END;
GO

IF XACT_STATE()<>1 THROW 56833,N'La transacción 0283 no está disponible.',1;
DECLARE @MigrationSha256 NVARCHAR(128)=N'$(MigrationSha256)';
IF LEN(@MigrationSha256)<>64 OR @MigrationSha256 LIKE N'%[^0-9A-Fa-f]%' THROW 56834,N'SHA-256 inválido para 0283.',1;
INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes)
VALUES(N'0283_tablero_entregas_pedidos',N'0283_tablero_entregas_pedidos.sql',UPPER(@MigrationSha256),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),
       N'BUG-10-02: sp_Entregas_PedidosTablero lista los pedidos En ruta y los pendientes de entrega para el tablero. Solo lectura.');
COMMIT TRANSACTION;
GO
