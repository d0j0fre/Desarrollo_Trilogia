SET NOCOUNT ON;

/* Verificacion de 0245. Solo lectura. */
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0245_comodato_bloqueo_morosidad' AND Status = N'Applied')
    THROW 56467, N'0245 no figura aplicada en el ledger.', 1;
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0230_cliente_estado_deuda' AND Status = N'Applied')
    THROW 56468, N'0230 (regla de deuda vencida) no figura aplicada.', 1;
IF OBJECT_ID(N'dbo.sp_Comodato_ClientesParaAsignar', N'P') IS NULL
    THROW 56469, N'Falta dbo.sp_Comodato_ClientesParaAsignar.', 1;
IF NOT EXISTS (SELECT 1 FROM sys.sql_modules WHERE object_id = OBJECT_ID(N'dbo.sp_Comodato_Asignar')
               AND definition LIKE N'%fn_Cliente_EstadoDeuda%' AND definition LIKE N'%53086%')
    THROW 56470, N'sp_Comodato_Asignar no bloquea por deuda vencida con fn_Cliente_EstadoDeuda.', 1;

/* Clientes que hoy quedarían bloqueados. */
SELECT c.UsuarioId, u.NombreCompleto, d.DeudaActual, d.DeudaVencida, d.VencimientoMasAntiguo, d.DiasAtraso
FROM (SELECT DISTINCT UsuarioId FROM dbo.ClienteCreditoMovimientos) c
INNER JOIN dbo.Usuarios u ON u.UsuarioId = c.UsuarioId
CROSS APPLY dbo.fn_Cliente_EstadoDeuda(c.UsuarioId) d
WHERE d.TieneDeudaVencida = 1;

PRINT N'0245 verificada.';
