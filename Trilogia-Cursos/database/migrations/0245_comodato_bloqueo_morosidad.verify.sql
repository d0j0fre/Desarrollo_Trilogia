SET NOCOUNT ON;

/* Verificacion de 0245. Solo lectura. */
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0245_comodato_bloqueo_morosidad' AND Status = N'Applied')
    THROW 56466, N'0245 no figura aplicada en el ledger.', 1;
IF OBJECT_ID(N'dbo.fn_Comodato_DeudaVencida', N'IF') IS NULL
   OR OBJECT_ID(N'dbo.sp_Comodato_ClientesParaAsignar', N'P') IS NULL
   OR OBJECT_ID(N'dbo.sp_Comodato_EvaluarMorosidad', N'P') IS NULL
    THROW 56467, N'Falta fn_Comodato_DeudaVencida, sp_Comodato_ClientesParaAsignar o sp_Comodato_EvaluarMorosidad.', 1;
IF NOT EXISTS (SELECT 1 FROM sys.sql_modules WHERE object_id = OBJECT_ID(N'dbo.sp_Comodato_Asignar') AND definition LIKE N'%53086%')
    THROW 56468, N'sp_Comodato_Asignar no bloquea por morosidad.', 1;

/* El saldo actual debe coincidir con DeudaActual del módulo de créditos
   (cargos + ajustes positivos − abonos − ajustes negativos, mínimo 0). */
IF EXISTS (
    SELECT 1
    FROM dbo.fn_Comodato_DeudaVencida(30) d
    CROSS APPLY (SELECT CASE WHEN x.Neto < 0 THEN 0 ELSE x.Neto END AS DeudaActual
                 FROM (SELECT SUM(CASE WHEN TipoMovimiento IN (N'Cargo', N'AjustePositivo') THEN Monto ELSE -Monto END) AS Neto
                       FROM dbo.ClienteCreditoMovimientos m WHERE m.UsuarioId = d.UsuarioId) x) c
    WHERE d.SaldoActual <> c.DeudaActual OR d.SaldoVencido > d.SaldoActual)
    THROW 56469, N'fn_Comodato_DeudaVencida no cuadra con el saldo del módulo de créditos.', 1;

SELECT d.UsuarioId, u.NombreCompleto, d.SaldoActual, d.SaldoVencido, d.CargoVencidoMasAntiguo
FROM dbo.fn_Comodato_DeudaVencida(30) d
INNER JOIN dbo.Usuarios u ON u.UsuarioId = d.UsuarioId
ORDER BY d.SaldoVencido DESC, d.SaldoActual DESC;

PRINT N'0245 verificada.';
