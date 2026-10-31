SET NOCOUNT ON;
SET XACT_ABORT OFF;

/* Verificacion de 0230. Solo lectura: la prueba de la regla FIFO corre dentro
   de una transaccion que siempre se revierte. */
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrationHistory
               WHERE MigrationId = N'0230_cliente_estado_deuda' AND Status = N'Applied' AND LEN(FileSha256) = 64)
    THROW 56310, N'0230 no figura aplicada correctamente.', 1;

IF COL_LENGTH(N'dbo.ClienteCreditos', N'PlazoCreditoDias') IS NULL
   OR COL_LENGTH(N'dbo.ClienteCreditoMovimientos', N'PedidoId') IS NULL
    THROW 56311, N'Faltan PlazoCreditoDias o ClienteCreditoMovimientos.PedidoId.', 1;

IF OBJECT_ID(N'dbo.fn_Cliente_EstadoDeuda', N'IF') IS NULL
   OR OBJECT_ID(N'dbo.sp_Cliente_EstadoDeuda', N'P') IS NULL
   OR OBJECT_ID(N'dbo.sp_Credito_CargarPedido', N'P') IS NULL
    THROW 56312, N'Faltan la función o los procedimientos de deuda.', 1;

IF OBJECT_ID(N'dbo.TR_Pedidos_AnularCargoCredito', N'TR') IS NULL
   OR EXISTS (SELECT 1 FROM sys.triggers WHERE name = N'TR_Pedidos_AnularCargoCredito' AND is_disabled = 1)
    THROW 56313, N'Falta o está deshabilitado TR_Pedidos_AnularCargoCredito.', 1;

IF NOT EXISTS (SELECT 1 FROM sys.parameters WHERE object_id = OBJECT_ID(N'dbo.sp_Seller_CreateOrder') AND name = N'@MetodoPago')
    THROW 56314, N'sp_Seller_CreateOrder no acepta @MetodoPago.', 1;

/* La deuda actual de la función coincide con la de Créditos para todos los clientes. */
IF EXISTS (
    SELECT 1
    FROM dbo.Usuarios u
    CROSS APPLY dbo.fn_Cliente_EstadoDeuda(u.UsuarioId) d
    OUTER APPLY (SELECT SUM(CASE WHEN m.TipoMovimiento IN (N'Cargo', N'AjustePositivo') THEN m.Monto ELSE -m.Monto END) AS Neto
                 FROM dbo.ClienteCreditoMovimientos m WHERE m.UsuarioId = u.UsuarioId) t
    WHERE d.DeudaActual <> CASE WHEN ISNULL(t.Neto, 0) < 0 THEN 0 ELSE ISNULL(t.Neto, 0) END)
    THROW 56315, N'La deuda de fn_Cliente_EstadoDeuda no coincide con Créditos.', 1;

/* Un procedimiento que use OUTPUT sin INTO o @@IDENTITY sobre dbo.Pedidos
   fallaría o leería mal con el disparador. Debe salir vacío. */
SELECT p.name AS ProcedimientoARevisar
FROM sys.procedures p
INNER JOIN sys.sql_modules m ON m.object_id = p.object_id
WHERE m.definition LIKE N'%dbo.Pedidos%'
  AND (m.definition LIKE N'%@@IDENTITY%' OR m.definition LIKE N'%UPDATE dbo.Pedidos%OUTPUT inserted%');

/* Prueba revertida: cargo de 100 000 hace 40 días, cargo de 50 000 hace 10 días
   y abono de 60 000: deuda 90 000, vencida 40 000. */
DECLARE @UsuarioId INT = (SELECT TOP 1 u.UsuarioId FROM dbo.Usuarios u
                          INNER JOIN dbo.Perfiles p ON p.PerfilId = u.PerfilId
                          WHERE p.Nombre = N'Cliente' ORDER BY u.UsuarioId);
IF @UsuarioId IS NOT NULL
BEGIN
    DECLARE @Base DECIMAL(18,2) = (SELECT DeudaActual FROM dbo.fn_Cliente_EstadoDeuda(@UsuarioId));
    DECLARE @Pagos DECIMAL(18,2) = @Base;
    DECLARE @Actual DECIMAL(18,2), @Vencida DECIMAL(18,2);
    BEGIN TRANSACTION;
        -- Se salda la deuda existente para aislar la prueba.
        IF @Pagos > 0
            INSERT dbo.ClienteCreditoMovimientos (UsuarioId, TipoMovimiento, Monto, Descripcion, FechaMovimiento)
            VALUES (@UsuarioId, N'Abono', @Pagos, N'verify 0230', DATEADD(DAY, -60, SYSDATETIME()));
        INSERT dbo.ClienteCreditoMovimientos (UsuarioId, TipoMovimiento, Monto, Descripcion, FechaMovimiento)
        VALUES (@UsuarioId, N'Cargo', 100000, N'verify 0230', DATEADD(DAY, -40, SYSDATETIME())),
               (@UsuarioId, N'Cargo', 50000, N'verify 0230', DATEADD(DAY, -10, SYSDATETIME())),
               (@UsuarioId, N'Abono', 60000, N'verify 0230', DATEADD(DAY, -5, SYSDATETIME()));
        UPDATE dbo.ClienteCreditos SET PlazoCreditoDias = 30 WHERE UsuarioId = @UsuarioId;
        SELECT @Actual = DeudaActual, @Vencida = DeudaVencida FROM dbo.fn_Cliente_EstadoDeuda(@UsuarioId);
    ROLLBACK TRANSACTION;

    IF @Actual IS NULL OR @Vencida IS NULL OR @Actual <> 90000 OR @Vencida <> 40000
        THROW 56316, N'La regla FIFO de deuda vencida no dio el resultado esperado.', 1;
END;

PRINT N'0230 verificada.';
