SET NOCOUNT ON;

/* Verificacion de 0326 (POST-qa2-03). Corre en una transaccion que se revierte. */
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0326_facturas_vencidas_avisos' AND Status = N'Applied')
    THROW 58500, N'0326 no figura aplicada en el ledger.', 1;

DECLARE @Cliente INT, @PedVencida INT, @PedAlDia INT, @FacVencida INT, @FacAlDia INT, @Admin INT;
DECLARE @V TABLE (FacturaId INT, NumeroFactura NVARCHAR(30), PedidoId INT, UsuarioId INT, MontoCargo DECIMAL(18,2), Pendiente DECIMAL(18,2), Vencimiento DATETIME2, DiasAtraso INT);
DECLARE @N TABLE (Nuevos INT);

SELECT TOP 1 @Admin = u.UsuarioId FROM dbo.Usuarios u INNER JOIN dbo.Perfiles p ON p.PerfilId = u.PerfilId WHERE p.Nombre = N'Administrador' AND ISNULL(u.Activo, 1) = 1 ORDER BY u.UsuarioId;
IF @Admin IS NULL THROW 58501, N'La verificacion necesita un Administrador activo.', 1;

BEGIN TRANSACTION;

INSERT dbo.Usuarios (PerfilId, NombreCompleto, Correo, Contrasena, Telefono, Direccion, Activo)
SELECT TOP 1 pf.PerfilId, N'VERIFY-0326 Cliente', N'verify0326@example.invalid', N'x', N'00000000', N'Direccion de prueba', 1 FROM dbo.Perfiles pf WHERE pf.Nombre = N'Cliente';
SET @Cliente = SCOPE_IDENTITY();

-- Dos facturas a credito: una con cargo de hace 40 dias (vencida con plazo de 30) y otra de hace 5 dias (al dia).
INSERT dbo.Pedidos (UsuarioId, Estado, TipoEntrega, DireccionEntrega, Total) VALUES (@Cliente, N'Pendiente', N'Retiro en local', N'Direccion de prueba', 1000);
SET @PedVencida = SCOPE_IDENTITY();
INSERT dbo.Pedidos (UsuarioId, Estado, TipoEntrega, DireccionEntrega, Total) VALUES (@Cliente, N'Pendiente', N'Retiro en local', N'Direccion de prueba', 2000);
SET @PedAlDia = SCOPE_IDENTITY();
INSERT dbo.Facturas (PedidoId, NumeroFactura, UsuarioId, ClienteNombre, ClienteCorreo, Subtotal, Impuesto, Total, Estado)
VALUES (@PedVencida, N'VERIFY-0326-V', @Cliente, N'VERIFY-0326 Cliente', N'verify0326@example.invalid', 1000, 130, 1130, N'Generada');
SET @FacVencida = SCOPE_IDENTITY();
INSERT dbo.Facturas (PedidoId, NumeroFactura, UsuarioId, ClienteNombre, ClienteCorreo, Subtotal, Impuesto, Total, Estado)
VALUES (@PedAlDia, N'VERIFY-0326-D', @Cliente, N'VERIFY-0326 Cliente', N'verify0326@example.invalid', 2000, 260, 2260, N'Generada');
SET @FacAlDia = SCOPE_IDENTITY();

INSERT dbo.ClienteCreditoMovimientos (UsuarioId, TipoMovimiento, Monto, Descripcion, Referencia, PedidoId, FechaMovimiento)
VALUES (@Cliente, N'Cargo', 1130, N'VERIFY-0326 cargo vencido', N'VERIFY-0326-V', @PedVencida, DATEADD(DAY, -40, SYSDATETIME()));
INSERT dbo.ClienteCreditoMovimientos (UsuarioId, TipoMovimiento, Monto, Descripcion, Referencia, PedidoId, FechaMovimiento)
VALUES (@Cliente, N'Cargo', 2260, N'VERIFY-0326 cargo al dia', N'VERIFY-0326-D', @PedAlDia, DATEADD(DAY, -5, SYSDATETIME()));

INSERT @V EXEC dbo.sp_Facturas_Vencidas @UsuarioId = @Cliente;
IF NOT EXISTS (SELECT 1 FROM @V WHERE FacturaId = @FacVencida AND Pendiente = 1130 AND DiasAtraso >= 10)
BEGIN ROLLBACK TRANSACTION; THROW 58502, N'La factura con cargo de hace 40 dias no sale como vencida.', 1; END;
IF EXISTS (SELECT 1 FROM @V WHERE FacturaId = @FacAlDia)
BEGIN ROLLBACK TRANSACTION; THROW 58503, N'Una factura dentro del plazo sale como vencida.', 1; END;

-- Avisos: uno por destinatario y por cliente; la segunda corrida del dia no repite.
INSERT @N EXEC dbo.sp_FacturasVencidas_GenerarAvisos;
IF NOT EXISTS (SELECT 1 FROM dbo.NotificacionesUsuario WHERE Tipo = N'FacturaVencida' AND EntidadTipo = N'Cliente' AND EntidadId = @Cliente AND UsuarioId = @Admin)
BEGIN ROLLBACK TRANSACTION; THROW 58504, N'No se creo el aviso FacturaVencida para el Administrador.', 1; END;
DECLARE @Despues1 INT = (SELECT COUNT(*) FROM dbo.NotificacionesUsuario WHERE Tipo = N'FacturaVencida' AND EntidadId = @Cliente);
DELETE @N;
INSERT @N EXEC dbo.sp_FacturasVencidas_GenerarAvisos;
IF (SELECT COUNT(*) FROM dbo.NotificacionesUsuario WHERE Tipo = N'FacturaVencida' AND EntidadId = @Cliente) <> @Despues1
BEGIN ROLLBACK TRANSACTION; THROW 58505, N'El aviso se repitio el mismo dia.', 1; END;

-- Un abono que cubre el cargo vencido: la factura deja de salir como vencida.
INSERT dbo.ClienteCreditoMovimientos (UsuarioId, TipoMovimiento, Monto, Descripcion, Referencia)
VALUES (@Cliente, N'Abono', 1130, N'VERIFY-0326 abono', N'VERIFY-0326-A');
DELETE @V;
INSERT @V EXEC dbo.sp_Facturas_Vencidas @UsuarioId = @Cliente;
IF EXISTS (SELECT 1 FROM @V)
BEGIN ROLLBACK TRANSACTION; THROW 58506, N'Con el abono sigue saliendo una factura vencida.', 1; END;

ROLLBACK TRANSACTION;
PRINT N'Verificacion 0326 completada.';
