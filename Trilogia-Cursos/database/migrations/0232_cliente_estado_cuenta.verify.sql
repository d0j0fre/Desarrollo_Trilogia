SET NOCOUNT ON;

/* Verificación de 0232. Solo lectura. */
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrationHistory
               WHERE MigrationId = N'0232_cliente_estado_cuenta' AND Status = N'Applied' AND LEN(FileSha256) = 64)
    THROW 56350, N'0232 no figura aplicada correctamente.', 1;

IF OBJECT_ID(N'dbo.sp_Cliente_EstadoCuenta', N'P') IS NULL
    THROW 56351, N'Falta sp_Cliente_EstadoCuenta.', 1;

/* El saldo del estado de cuenta es el de Créditos: el saldo acumulado del
   último movimiento, sin bajar de cero, coincide con fn_Cliente_EstadoDeuda. */
IF EXISTS (
    SELECT 1
    FROM dbo.Usuarios u
    CROSS APPLY dbo.fn_Cliente_EstadoDeuda(u.UsuarioId) d
    OUTER APPLY (SELECT SUM(CASE WHEN m.TipoMovimiento IN (N'Cargo', N'AjustePositivo') THEN m.Monto ELSE -m.Monto END) AS Neto
                 FROM dbo.ClienteCreditoMovimientos m WHERE m.UsuarioId = u.UsuarioId) t
    WHERE d.DeudaActual <> CASE WHEN ISNULL(t.Neto, 0) < 0 THEN 0 ELSE ISNULL(t.Neto, 0) END)
    THROW 56352, N'El saldo del estado de cuenta no coincide con Créditos.', 1;

DECLARE @UsuarioId INT = (SELECT TOP 1 f.UsuarioId FROM dbo.Facturas f ORDER BY f.FacturaId DESC);
IF @UsuarioId IS NOT NULL
    EXEC dbo.sp_Cliente_EstadoCuenta @UsuarioId = @UsuarioId;

PRINT N'0232 verificada.';
