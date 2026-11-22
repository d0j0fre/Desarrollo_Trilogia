SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

/* POST-qa2-07 · CU-104-02: el tablero de entregas no lista los pedidos pendientes de entrega.

   sp_Entregas_PedidosTablero (0283) filtraba los pendientes con TipoEntrega = 'Entrega a domicilio',
   que es el texto de los datos de ejemplo. Los pedidos que crea el sitio (Tienda y Venta movil) guardan
   'Envío a domicilio', asi que ningun pedido real salia en «Pendientes de entrega» (la lista y el
   contador quedaban en 0 aunque hubiera pedidos a domicilio por entregar).

   Esta migracion vuelve a crear el procedimiento con el mismo resultado y las mismas columnas, y acepta
   las dos escrituras: 'Entrega a domicilio' y 'Envío a domicilio'. Los grupos En ruta y Pendiente, el
   orden y el limite no cambian. Solo lectura.
   Rollback en 0324_tablero_entregas_envio_domicilio.rollback.md. */
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL THROW 58450,N'Falta el ledger 0001.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0324_tablero_entregas_envio_domicilio' AND Status=N'Applied')
    THROW 58451,N'0324 ya figura aplicada.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0283_tablero_entregas_pedidos' AND Status=N'Applied')
    THROW 58452,N'0324 reemplaza sp_Entregas_PedidosTablero, que crea la 0283. Aplicar 0283 primero.',1;

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
        WHERE p.TipoEntrega IN (N'Entrega a domicilio', N'Envío a domicilio')
          AND p.Estado NOT IN (N'Cancelado', N'Rechazado', N'Entregado', N'Retenido', N'Fallido')
          AND (
                (rp.RutaPedidoId IS NOT NULL AND rp.EstadoEntrega = N'Pendiente' AND r.Estado IN (N'Planificada', N'Despachada'))
             OR (rp.RutaPedidoId IS NULL AND u.Activo = 1)
          )
        ORDER BY p.FechaPedido, p.PedidoId
    ) pendientes;
END;
GO

IF XACT_STATE()<>1 THROW 58453,N'La transacción 0324 no está disponible.',1;
DECLARE @MigrationSha256 NVARCHAR(128)=N'$(MigrationSha256)';
IF LEN(@MigrationSha256)<>64 OR @MigrationSha256 LIKE N'%[^0-9A-Fa-f]%' THROW 58454,N'SHA-256 inválido para 0324.',1;
INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes)
VALUES(N'0324_tablero_entregas_envio_domicilio',N'0324_tablero_entregas_envio_domicilio.sql',UPPER(@MigrationSha256),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),
       N'POST-qa2-07 (CU-104-02): sp_Entregas_PedidosTablero acepta TipoEntrega Envío a domicilio (el que guardan Tienda y Venta móvil) además de Entrega a domicilio. Solo lectura.');
COMMIT TRANSACTION;
GO
