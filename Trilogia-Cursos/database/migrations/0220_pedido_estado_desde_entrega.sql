SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

/* BUG-10-06 · CU-102: el pedido sigue "Pendiente" cuando el chofer lo marca
   En ruta o No entregado desde la aplicacion.

   sp_Chofer_UpdateDeliveryStatus solo copiaba a dbo.Pedidos el estado
   Entregado, y sp_Rutas_Dispatch no copia nada. La entrega (RutaPedidos)
   cambiaba, el pedido no.

   Esta migracion agrega dbo.sp_Pedidos_SincronizarEstadoEntrega, que lleva al
   pedido el estado de su entrega con los MISMOS nombres que ya usa la base
   (CHECK de dbo.RutaPedidos.EstadoEntrega), sin inventar ni renombrar:

       RutaPedidos.EstadoEntrega   ->   Pedidos.Estado
       EnRuta                           EnRuta       (se muestra "En ruta")
       Entregado                        Entregado
       Fallido                          Fallido      (se muestra "No entregado";
                                                      requiere retorno a bodega)

   No toca pedidos Cancelado, Rechazado ni Entregado. Es idempotente: deriva
   el estado de la entrega actual, asi que volver a llamarlo no cambia nada.

   La API movil lo invoca despues de sp_Chofer_UpdateDeliveryStatus y de
   sp_Rutas_Dispatch. Ningun procedimiento existente se modifica: esos dos
   tambien los usa el sitio web (rutas y entregas, otro bloque), que puede
   invocar este mismo procedimiento para quedar igual.

   Aditiva. Rollback en 0220_pedido_estado_desde_entrega.rollback.md. */
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL THROW 56200,N'Falta el ledger 0001.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0220_pedido_estado_desde_entrega' AND Status=N'Applied')
    THROW 56201,N'0220 ya figura aplicada.',1;
IF OBJECT_ID(N'dbo.Pedidos',N'U') IS NULL OR OBJECT_ID(N'dbo.RutaPedidos',N'U') IS NULL OR OBJECT_ID(N'dbo.Rutas',N'U') IS NULL
    THROW 56202,N'Faltan las tablas de pedidos o rutas (CU-081).',1;

BEGIN TRANSACTION;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Pedidos_SincronizarEstadoEntrega
    @RutaId       INT = NULL,
    @RutaPedidoId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @RutaId IS NULL AND @RutaPedidoId IS NULL
        THROW 56210, N'Indique la ruta o la entrega a sincronizar.', 1;

    UPDATE p
    SET Estado = rp.EstadoEntrega,
        FechaActualizacion = SYSDATETIME()
    FROM dbo.Pedidos p
    INNER JOIN dbo.RutaPedidos rp ON rp.PedidoId = p.PedidoId
    WHERE (rp.RutaPedidoId = @RutaPedidoId OR rp.RutaId = @RutaId)
      AND rp.EstadoEntrega IN (N'EnRuta', N'Entregado', N'Fallido')
      AND p.Estado NOT IN (N'Cancelado', N'Rechazado', N'Entregado')
      AND p.Estado <> rp.EstadoEntrega;

    SELECT @@ROWCOUNT AS PedidosActualizados;
END;
GO

IF XACT_STATE()<>1 THROW 56203,N'La transacción 0220 no está disponible.',1;
DECLARE @MigrationSha256 NVARCHAR(128)=N'$(MigrationSha256)';
IF LEN(@MigrationSha256)<>64 OR @MigrationSha256 LIKE N'%[^0-9A-Fa-f]%' THROW 56204,N'SHA-256 inválido para 0220.',1;
INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes)
VALUES(N'0220_pedido_estado_desde_entrega',N'0220_pedido_estado_desde_entrega.sql',UPPER(@MigrationSha256),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),
       N'BUG-10-06: sp_Pedidos_SincronizarEstadoEntrega lleva a Pedidos.Estado el estado de la entrega (EnRuta, Entregado, Fallido). Aditiva.');
COMMIT TRANSACTION;
GO
