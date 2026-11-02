SET NOCOUNT ON;

/* Verificacion de 0256. La prueba de guardado corre en una transaccion que se revierte. */
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0256_pedido_datos_cliente' AND Status = N'Applied')
    THROW 56610, N'0256 no figura aplicada en el ledger.', 1;
IF OBJECT_ID(N'dbo.PedidoDatosCliente', N'U') IS NULL
   OR OBJECT_ID(N'dbo.sp_Pedido_GuardarDatosCliente', N'P') IS NULL
   OR OBJECT_ID(N'dbo.sp_Pedido_GetDatosCliente', N'P') IS NULL
    THROW 56611, N'Falta la tabla o un procedimiento de la 0256.', 1;

DECLARE @PedidoId INT, @UsuarioId INT;
SELECT TOP 1 @PedidoId = p.PedidoId, @UsuarioId = p.UsuarioId
FROM dbo.Pedidos p
WHERE NOT EXISTS (SELECT 1 FROM dbo.PedidoDatosCliente d WHERE d.PedidoId = p.PedidoId)
ORDER BY p.PedidoId;
IF @PedidoId IS NOT NULL
BEGIN
    BEGIN TRANSACTION;
    EXEC dbo.sp_Pedido_GuardarDatosCliente @PedidoId, @UsuarioId, N'Cliente Jurídico', 1, N'facturas@ejemplo.cr', N'2222-2222', NULL;
    EXEC dbo.sp_Pedido_GuardarDatosCliente @PedidoId, @UsuarioId, N'Cliente Jurídico', 1, N'facturas@ejemplo.cr', N'2222-2222', NULL;  -- idempotente
    IF (SELECT COUNT(*) FROM dbo.PedidoDatosCliente WHERE PedidoId = @PedidoId AND FacturaElectronica = 1) <> 1
    BEGIN ROLLBACK TRANSACTION; THROW 56612, N'No se guardaron los datos del cliente del pedido.', 1; END;

    BEGIN TRY
        DECLARE @Otro INT = @UsuarioId + 100000;
        EXEC dbo.sp_Pedido_GuardarDatosCliente @PedidoId, @Otro, N'Cliente Físico', 0, NULL, NULL, NULL;
        ROLLBACK TRANSACTION;
        THROW 56613, N'Se aceptaron datos de cliente para un pedido de otro usuario.', 1;
    END TRY
    BEGIN CATCH
        IF ERROR_NUMBER() = 56613 THROW;
        IF ERROR_NUMBER() <> 53140 BEGIN IF XACT_STATE() <> 0 ROLLBACK TRANSACTION; THROW; END;
    END CATCH;
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
END;

SELECT COUNT(*) AS PedidosConDatosCliente FROM dbo.PedidoDatosCliente;
PRINT N'Verificación 0256 completada.';
