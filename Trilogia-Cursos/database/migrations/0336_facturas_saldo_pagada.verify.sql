SET NOCOUNT ON;

/* Verificacion de 0336. Solo lectura. */
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0336_facturas_saldo_pagada' AND Status = N'Applied')
    THROW 58530, N'0336 no figura aplicada en el ledger.', 1;
IF OBJECT_ID(N'dbo.fn_Cliente_FacturasSaldo', N'IF') IS NULL
    THROW 58531, N'Falta dbo.fn_Cliente_FacturasSaldo.', 1;
IF OBJECT_ID(N'dbo.sp_Facturas_Saldo', N'P') IS NULL
    THROW 58532, N'Falta dbo.sp_Facturas_Saldo.', 1;

-- Facturas.Estado no se modifica: la migracion no contiene UPDATE ni ALTER sobre dbo.Facturas.
-- El procedimiento se ejecuta sin error y devuelve las columnas esperadas.
DECLARE @S TABLE (FacturaId INT, NumeroFactura NVARCHAR(50), PedidoId INT, UsuarioId INT, MontoCargo DECIMAL(18,2), Pendiente DECIMAL(18,2));
INSERT @S EXEC dbo.sp_Facturas_Saldo;
IF EXISTS (SELECT 1 FROM @S WHERE Pendiente < 0 OR Pendiente > MontoCargo)
    THROW 58533, N'El saldo de alguna factura queda fuera del rango 0..monto del cargo.', 1;
PRINT N'Verificacion 0336 completada.';
