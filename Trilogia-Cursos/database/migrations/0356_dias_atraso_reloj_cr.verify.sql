SET NOCOUNT ON;

/* Verificacion de 0356. Solo lectura. */
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0356_dias_atraso_reloj_cr' AND Status = N'Applied')
    THROW 58970, N'0356 no figura aplicada en el ledger.', 1;
IF EXISTS (SELECT 1 FROM (VALUES (N'fn_Cliente_EstadoDeuda'),(N'fn_Cliente_FacturasVencidas')) o(n)
           WHERE OBJECT_ID(N'dbo.' + o.n) IS NULL
              OR CHARINDEX(N'dbo.fn_HoyCR()', OBJECT_DEFINITION(OBJECT_ID(N'dbo.' + o.n))) = 0
              OR OBJECT_DEFINITION(OBJECT_ID(N'dbo.' + o.n)) LIKE N'%DATEDIFF(DAY, % SYSDATETIME())%')
    THROW 58971, N'Una funcion de deuda vencida todavia cuenta los dias de atraso con SYSDATETIME().', 1;

-- Los dias de atraso de cada factura vencida coinciden con la diferencia de dias de calendario de Costa Rica.
DECLARE @Malos INT = 0;
SELECT @Malos = COUNT(*)
FROM dbo.Usuarios u
CROSS APPLY dbo.fn_Cliente_FacturasVencidas(u.UsuarioId) v
WHERE v.DiasAtraso <> DATEDIFF(DAY, CAST(dbo.fn_FechaHoraCR(v.Vencimiento) AS DATE), dbo.fn_HoyCR());
IF @Malos > 0
    THROW 58972, N'Hay facturas vencidas cuyo atraso no es el de dias de calendario de Costa Rica.', 1;
PRINT N'Verificacion 0356 completada.';
