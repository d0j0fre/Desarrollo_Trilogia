SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

/* Pruebas post instalación obs. 1 (total del pedido con IVA), parte de la app móvil: el detalle de un pedido
   (sp_Movil_Pedido_Detalle: gestión, bodega, gerencia y chofer) devuelve además Subtotal e Impuesto, para mostrar
   subtotal, IVA y total con IVA. Se agregan al final del primer conjunto de resultados (Total ya es con IVA desde
   0330); el lector por nombre de la API no cambia con ellas. Sin cambios de tablas ni de datos.
   Rollback en 0333_movil_detalle_pedido_iva.rollback.md. */
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL THROW 59330,N'Falta el ledger 0001.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0333_movil_detalle_pedido_iva' AND Status=N'Applied')
    THROW 59331,N'0333 ya figura aplicada.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0330_pedido_total_con_iva' AND Status=N'Applied')
    THROW 59332,N'0333 requiere 0330 aplicada.',1;

BEGIN TRANSACTION;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Movil_Pedido_Detalle
    @PedidoId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        p.PedidoId, u.NombreCompleto AS Cliente, u.Correo AS ClienteCorreo, ISNULL(u.Telefono, N'') AS ClienteTelefono,
        p.FechaPedido, p.Estado, ISNULL(p.TipoEntrega, N'') AS TipoEntrega,
        ISNULL(NULLIF(p.DireccionEntrega, N''), ISNULL(u.Direccion, N'')) AS DireccionEntrega,
        p.Total, ISNULL(p.Observaciones, N'') AS Observaciones,
        ISNULL(p.Subtotal, p.Total) AS Subtotal, ISNULL(p.Impuesto, 0) AS Impuesto,   -- 0333
        ISNULL(p.VendedorNombre, N'') AS VendedorNombre, ISNULL(p.CanalPedido, N'') AS CanalPedido,
        ISNULL(p.MotivoRechazo, N'') AS MotivoRechazo,
        ISNULL(f.NumeroFactura, N'') AS NumeroFactura,
        CAST(IIF(f.FacturaId IS NULL, 0, 1) AS BIT) AS TieneFactura,
        ISNULL(pp.PreparadoPorNombre, N'') AS PreparadoPorNombre, pp.FechaPreparacion,
        ISNULL(ruta.Codigo, N'') AS RutaCodigo, ISNULL(ruta.EstadoEntrega, N'') AS EstadoEntrega
    FROM dbo.Pedidos p
    INNER JOIN dbo.Usuarios u ON u.UsuarioId = p.UsuarioId
    OUTER APPLY (SELECT TOP 1 fac.FacturaId, fac.NumeroFactura FROM dbo.Facturas fac
                 WHERE fac.PedidoId = p.PedidoId ORDER BY fac.FacturaId DESC) f
    LEFT JOIN dbo.PedidoPreparacion pp ON pp.PedidoId = p.PedidoId
    OUTER APPLY (SELECT TOP 1 r.Codigo, rp.EstadoEntrega FROM dbo.RutaPedidos rp
                 INNER JOIN dbo.Rutas r ON r.RutaId = rp.RutaId
                 WHERE rp.PedidoId = p.PedidoId AND r.Estado <> N'Cancelada'
                 ORDER BY r.FechaCreacion DESC) ruta
    WHERE p.PedidoId = @PedidoId;

    IF @@ROWCOUNT > 0
        EXEC dbo.sp_Admin_GetOrderDetailLines @PedidoId = @PedidoId;
END;

GO

IF XACT_STATE()<>1 THROW 59333,N'La transacción 0333 no está disponible.',1;
DECLARE @MigrationSha256 NVARCHAR(128)=N'$(MigrationSha256)';
IF LEN(@MigrationSha256)<>64 OR @MigrationSha256 LIKE N'%[^0-9A-Fa-f]%' THROW 59334,N'SHA-256 inválido para 0333.',1;
INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes)
VALUES(N'0333_movil_detalle_pedido_iva',N'0333_movil_detalle_pedido_iva.sql',UPPER(@MigrationSha256),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),
       N'Pruebas post instalación obs. 1: sp_Movil_Pedido_Detalle devuelve Subtotal e Impuesto (la app muestra subtotal, IVA y total con IVA). Aditiva.');
COMMIT TRANSACTION;
GO
