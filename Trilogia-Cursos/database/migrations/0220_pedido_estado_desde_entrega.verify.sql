SET NOCOUNT ON;

/* Verificacion de 0220. Solo lectura. */
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0220_pedido_estado_desde_entrega' AND Status = N'Applied')
    THROW 56250, N'0220 no figura aplicada en el ledger.', 1;
IF OBJECT_ID(N'dbo.sp_Pedidos_SincronizarEstadoEntrega', N'P') IS NULL
    THROW 56251, N'Falta dbo.sp_Pedidos_SincronizarEstadoEntrega.', 1;

/* Los nombres que el procedimiento copia son los del CHECK de RutaPedidos. */
IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID(N'dbo.RutaPedidos')
      AND definition LIKE N'%EnRuta%' AND definition LIKE N'%Fallido%' AND definition LIKE N'%Entregado%')
    THROW 56252, N'El CHECK de RutaPedidos.EstadoEntrega ya no tiene EnRuta/Entregado/Fallido.', 1;

/* Pedidos cuya entrega esta en curso o cerrada y el pedido no la refleja.
   Despues de usar la app deberia salir vacio (los anteriores a 0220 o los
   marcados desde el sitio web pueden aparecer aqui). */
SELECT p.PedidoId, p.Estado AS EstadoPedido, rp.RutaId, rp.EstadoEntrega
FROM dbo.Pedidos p
INNER JOIN dbo.RutaPedidos rp ON rp.PedidoId = p.PedidoId
WHERE rp.EstadoEntrega IN (N'EnRuta', N'Fallido')
  AND p.Estado NOT IN (N'Cancelado', N'Rechazado', N'Entregado')
  AND p.Estado <> rp.EstadoEntrega;

PRINT N'0220 verificada.';
