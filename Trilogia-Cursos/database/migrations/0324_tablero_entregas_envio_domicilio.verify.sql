SET NOCOUNT ON;

/* Verificacion de 0324 (POST-qa2-07). Corre en una transaccion que se revierte. */
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0324_tablero_entregas_envio_domicilio' AND Status = N'Applied')
    THROW 58460, N'0324 no figura aplicada en el ledger.', 1;

DECLARE @Chofer INT, @Vehiculo INT, @Activo INT, @RutaP INT;
DECLARE @PEnvioSinRuta INT, @PEnvioPlan INT, @PEntregaSinRuta INT, @PRetiro INT, @PVendedor INT;
DECLARE @T TABLE (Grupo NVARCHAR(10), PedidoId INT, Cliente NVARCHAR(150), Direccion NVARCHAR(300), Total DECIMAL(18,2), FechaPedido DATETIME2,
    RutaId INT, RutaCodigo NVARCHAR(30), RutaEstado NVARCHAR(20), Chofer NVARCHAR(150), Secuencia INT, Prioridad TINYINT);

SELECT TOP 1 @Chofer = u.UsuarioId FROM dbo.Usuarios u INNER JOIN dbo.Perfiles pf ON pf.PerfilId = u.PerfilId
WHERE u.Activo = 1 AND pf.Nombre = N'Chofer' ORDER BY u.UsuarioId;
SELECT TOP 1 @Vehiculo = VehiculoId FROM dbo.Vehiculos WHERE Activo = 1 ORDER BY VehiculoId;
IF @Chofer IS NULL OR @Vehiculo IS NULL THROW 58461, N'La verificacion necesita un chofer y un vehiculo activos.', 1;

BEGIN TRANSACTION;

INSERT dbo.Usuarios (PerfilId, NombreCompleto, Correo, Contrasena, Telefono, Direccion, Activo)
SELECT TOP 1 pf.PerfilId, N'VERIFY-0324 Activo', N'verify0324a@example.invalid', N'x', N'00000000', N'Direccion de prueba', 1 FROM dbo.Perfiles pf WHERE pf.Nombre = N'Cliente';
SET @Activo = SCOPE_IDENTITY();

INSERT dbo.Pedidos (UsuarioId, Estado, TipoEntrega, DireccionEntrega, Total) VALUES (@Activo, N'Pendiente', N'Envío a domicilio', N'Direccion de prueba', 1000);
SET @PEnvioSinRuta = SCOPE_IDENTITY();
INSERT dbo.Pedidos (UsuarioId, Estado, TipoEntrega, DireccionEntrega, Total) VALUES (@Activo, N'Pendiente', N'Envío a domicilio', N'Direccion de prueba', 1000);
SET @PEnvioPlan = SCOPE_IDENTITY();
INSERT dbo.Pedidos (UsuarioId, Estado, TipoEntrega, DireccionEntrega, Total) VALUES (@Activo, N'Pendiente', N'Entrega a domicilio', N'Direccion de prueba', 1000);
SET @PEntregaSinRuta = SCOPE_IDENTITY();
INSERT dbo.Pedidos (UsuarioId, Estado, TipoEntrega, DireccionEntrega, Total) VALUES (@Activo, N'Pendiente', N'Retiro en local', N'Direccion de prueba', 1000);
SET @PRetiro = SCOPE_IDENTITY();
INSERT dbo.Pedidos (UsuarioId, Estado, TipoEntrega, DireccionEntrega, Total) VALUES (@Activo, N'Pendiente', N'Entrega por vendedor', N'Direccion de prueba', 1000);
SET @PVendedor = SCOPE_IDENTITY();

INSERT dbo.Rutas (Codigo, Zona, ChoferUsuarioId, VehiculoId, Estado) VALUES (N'VERIFY-0324P', N'VERIFY-0324', @Chofer, @Vehiculo, N'Planificada');
SET @RutaP = SCOPE_IDENTITY();
INSERT dbo.RutaPedidos (RutaId, PedidoId, Secuencia, EstadoEntrega) VALUES (@RutaP, @PEnvioPlan, 1, N'Pendiente');

INSERT @T EXEC dbo.sp_Entregas_PedidosTablero;

IF NOT EXISTS (SELECT 1 FROM @T WHERE Grupo = N'Pendiente' AND PedidoId = @PEnvioSinRuta AND RutaId IS NULL)
BEGIN ROLLBACK TRANSACTION; THROW 58462, N'Un pedido «Envío a domicilio» sin ruta no sale como pendiente.', 1; END;
IF NOT EXISTS (SELECT 1 FROM @T WHERE Grupo = N'Pendiente' AND PedidoId = @PEnvioPlan AND RutaCodigo = N'VERIFY-0324P')
BEGIN ROLLBACK TRANSACTION; THROW 58463, N'Un pedido «Envío a domicilio» de una ruta planificada no sale como pendiente.', 1; END;
IF NOT EXISTS (SELECT 1 FROM @T WHERE Grupo = N'Pendiente' AND PedidoId = @PEntregaSinRuta)
BEGIN ROLLBACK TRANSACTION; THROW 58464, N'Un pedido «Entrega a domicilio» (datos de ejemplo) dejo de salir como pendiente.', 1; END;
IF EXISTS (SELECT 1 FROM @T WHERE PedidoId IN (@PRetiro, @PVendedor))
BEGIN ROLLBACK TRANSACTION; THROW 58465, N'Salio un pedido de retiro en local o de entrega por vendedor.', 1; END;

ROLLBACK TRANSACTION;
PRINT N'Verificacion 0324 completada.';
