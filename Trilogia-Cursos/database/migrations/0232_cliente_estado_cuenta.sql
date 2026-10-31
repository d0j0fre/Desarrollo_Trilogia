SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

/* BUG-06-03 · CU-061-07, BUG-12-01 · CU-123-02 / CU-124-01 y BUG-12-02 ·
   CU-123-03 / CU-124-02: el detalle del cliente no mostraba facturas ni
   saldo, y el portal no tenía lista de facturas ni mostraba facturas y pagos
   en el estado de cuenta.

   dbo.sp_Cliente_EstadoCuenta @UsuarioId es la única consulta del estado de
   cuenta. La usan el detalle del cliente (administración), el portal del
   cliente (Estado de cuenta y Mis facturas) y los PDF descargables, así que
   el saldo es el mismo en todos lados y coincide con Créditos: sale de
   dbo.fn_Cliente_EstadoDeuda (0230).

   Resultados
   ----------
   1. Resumen: datos del cliente, límite, plazo, saldo (deuda actual), deuda
      vencida, disponible, total facturado, pagos registrados y fecha de corte.
   2. Facturas del cliente (más reciente primero) con el método de pago del
      pedido.
   3. Movimientos de la cuenta (cargos, pagos y ajustes) en orden cronológico
      con el saldo acumulado después de cada uno.

   Solo lectura. Rollback en 0232_cliente_estado_cuenta.rollback.md. */
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL THROW 56340,N'Falta el ledger 0001.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0232_cliente_estado_cuenta' AND Status=N'Applied')
    THROW 56341,N'0232 ya figura aplicada.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0230_cliente_estado_deuda' AND Status=N'Applied')
    THROW 56342,N'0232 requiere 0230 aplicada.',1;
IF OBJECT_ID(N'dbo.Facturas',N'U') IS NULL
    THROW 56343,N'Falta dbo.Facturas.',1;

BEGIN TRANSACTION;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Cliente_EstadoCuenta
    @UsuarioId INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @ClientePerfilId INT = (SELECT TOP 1 PerfilId FROM dbo.Perfiles WHERE Nombre = N'Cliente');

    IF NOT EXISTS (SELECT 1 FROM dbo.Usuarios WHERE UsuarioId = @UsuarioId AND PerfilId = @ClientePerfilId)
        THROW 52030, 'El cliente seleccionado no existe.', 1;

    /* 1. Resumen */
    SELECT
        u.UsuarioId,
        u.NombreCompleto,
        u.Correo,
        u.Telefono,
        u.Direccion,
        deuda.LimiteCredito,
        CAST(ISNULL(cc.CreditoActivo, 0) AS BIT) AS CreditoActivo,
        CAST(ISNULL(cc.CreditoBloqueado, 0) AS BIT) AS CreditoBloqueado,
        deuda.PlazoCreditoDias,
        deuda.DeudaActual AS Saldo,
        deuda.DeudaVencida,
        deuda.VencimientoMasAntiguo,
        CAST(deuda.LimiteCredito - deuda.DeudaActual AS DECIMAL(18,2)) AS CreditoDisponible,
        ISNULL(f.CantidadFacturas, 0) AS CantidadFacturas,
        CAST(ISNULL(f.TotalFacturado, 0) AS DECIMAL(18,2)) AS TotalFacturado,
        CAST(ISNULL(p.TotalPagado, 0) AS DECIMAL(18,2)) AS TotalPagado,
        SYSDATETIME() AS FechaCorte
    FROM dbo.Usuarios u
    CROSS APPLY dbo.fn_Cliente_EstadoDeuda(u.UsuarioId) deuda
    LEFT JOIN dbo.ClienteCreditos cc ON cc.UsuarioId = u.UsuarioId
    OUTER APPLY (SELECT COUNT(*) AS CantidadFacturas, SUM(fa.Total) AS TotalFacturado
                 FROM dbo.Facturas fa WHERE fa.UsuarioId = u.UsuarioId) f
    OUTER APPLY (SELECT SUM(m.Monto) AS TotalPagado
                 FROM dbo.ClienteCreditoMovimientos m
                 WHERE m.UsuarioId = u.UsuarioId AND m.TipoMovimiento = N'Abono') p
    WHERE u.UsuarioId = @UsuarioId;

    /* 2. Facturas */
    SELECT
        fa.FacturaId,
        fa.NumeroFactura,
        fa.PedidoId,
        fa.FechaFactura,
        fa.Subtotal,
        fa.Impuesto,
        fa.Total,
        fa.Estado,
        pe.MetodoPago
    FROM dbo.Facturas fa
    LEFT JOIN dbo.Pedidos pe ON pe.PedidoId = fa.PedidoId
    WHERE fa.UsuarioId = @UsuarioId
    ORDER BY fa.FechaFactura DESC, fa.FacturaId DESC;

    /* 3. Movimientos con saldo acumulado */
    SELECT
        m.CreditoMovimientoId,
        m.FechaMovimiento,
        m.TipoMovimiento,
        m.Descripcion,
        m.Referencia,
        m.PedidoId,
        CAST(CASE WHEN m.TipoMovimiento IN (N'Cargo', N'AjustePositivo') THEN m.Monto ELSE 0 END AS DECIMAL(18,2)) AS Cargo,
        CAST(CASE WHEN m.TipoMovimiento IN (N'Abono', N'AjusteNegativo') THEN m.Monto ELSE 0 END AS DECIMAL(18,2)) AS Abono,
        CAST(SUM(CASE WHEN m.TipoMovimiento IN (N'Cargo', N'AjustePositivo') THEN m.Monto ELSE -m.Monto END)
             OVER (ORDER BY m.FechaMovimiento, m.CreditoMovimientoId ROWS UNBOUNDED PRECEDING) AS DECIMAL(18,2)) AS SaldoAcumulado
    FROM dbo.ClienteCreditoMovimientos m
    WHERE m.UsuarioId = @UsuarioId
    ORDER BY m.FechaMovimiento, m.CreditoMovimientoId;
END;
GO

IF OBJECT_ID(N'dbo.sp_Cliente_EstadoCuenta', N'P') IS NULL
    THROW 56344, N'No quedó creado sp_Cliente_EstadoCuenta.', 1;

IF XACT_STATE() <> 1
    THROW 56345, N'La transacción 0232 no está disponible.', 1;

DECLARE @MigrationSha256 NVARCHAR(128) = N'$(MigrationSha256)';
IF LEN(@MigrationSha256) <> 64 OR @MigrationSha256 LIKE N'%[^0-9A-Fa-f]%'
    THROW 56346, N'SHA-256 inválido para 0232.', 1;

INSERT dbo.SchemaMigrationHistory
    (MigrationId, FileName, FileSha256, Status, AppliedBy, EnvironmentName, Notes)
VALUES
    (N'0232_cliente_estado_cuenta',
     N'0232_cliente_estado_cuenta.sql',
     UPPER(@MigrationSha256),
     N'Applied',
     ORIGINAL_LOGIN(),
     DB_NAME(),
     N'BUG-06-03, BUG-12-01, BUG-12-02: estado de cuenta único (saldo, facturas y movimientos).');

COMMIT TRANSACTION;
GO
