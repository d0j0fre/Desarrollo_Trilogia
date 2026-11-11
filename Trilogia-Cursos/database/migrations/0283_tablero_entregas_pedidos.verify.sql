SET NOCOUNT ON;

/* Verificacion de 0283 (BUG-10-02). Corre en una transaccion que se revierte. */
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0283_tablero_entregas_pedidos' AND Status = N'Applied')
    THROW 56880, N'0283 no figura aplicada en el ledger.', 1;

DECLARE @Chofer INT, @Vehiculo INT, @Activo INT, @Inactivo INT, @RutaD INT, @RutaP INT;
DECLARE @PEnRuta INT, @PPlan INT, @PSinRuta INT, @PInactivo INT, @PEntregado INT, @PRetiro INT;
DECLARE @T TABLE (Grupo NVARCHAR(10), PedidoId INT, Cliente NVARCHAR(150), Direccion NVARCHAR(300), Total DECIMAL(18,2), FechaPedido DATETIME2,
    RutaId INT, RutaCodigo NVARCHAR(30), RutaEstado NVARCHAR(20), Chofer NVARCHAR(150), Secuencia INT, Prioridad TINYINT);

SELECT TOP 1 @Chofer = u.UsuarioId FROM dbo.Usuarios u INNER JOIN dbo.Perfiles pf ON pf.PerfilId = u.PerfilId
WHERE u.Activo = 1 AND pf.Nombre = N'Chofer' ORDER BY u.UsuarioId;
SELECT TOP 1 @Vehiculo = VehiculoId FROM dbo.Vehiculos WHERE Activo = 1 ORDER BY VehiculoId;
IF @Chofer IS NULL OR @Vehiculo IS NULL THROW 56881, N'La verificacion necesita un chofer y un vehiculo activos.', 1;

BEGIN TRANSACTION;

INSERT dbo.Usuarios (PerfilId, NombreCompleto, Correo, Contrasena, Telefono, Direccion, Activo)
SELECT TOP 1 pf.PerfilId, N'VERIFY-0283 Activo', N'verify0283a@example.invalid', N'x', N'00000000', N'Direccion de prueba', 1 FROM dbo.Perfiles pf WHERE pf.Nombre = N'Cliente';
SET @Activo = SCOPE_IDENTITY();
INSERT dbo.Usuarios (PerfilId, NombreCompleto, Correo, Contrasena, Telefono, Direccion, Activo)
SELECT TOP 1 pf.PerfilId, N'VERIFY-0283 Inactivo', N'verify0283b@example.invalid', N'x', N'00000000', N'Direccion de prueba', 0 FROM dbo.Perfiles pf WHERE pf.Nombre = N'Cliente';
SET @Inactivo = SCOPE_IDENTITY();

INSERT dbo.Pedidos (UsuarioId, Estado, TipoEntrega, DireccionEntrega, Total) VALUES (@Activo, N'Pendiente', N'Entrega a domicilio', N'Direccion de prueba', 1000);
SET @PEnRuta = SCOPE_IDENTITY();
INSERT dbo.Pedidos (UsuarioId, Estado, TipoEntrega, DireccionEntrega, Total) VALUES (@Activo, N'Pendiente', N'Entrega a domicilio', N'Direccion de prueba', 1000);
SET @PPlan = SCOPE_IDENTITY();
INSERT dbo.Pedidos (UsuarioId, Estado, TipoEntrega, DireccionEntrega, Total) VALUES (@Activo, N'Pendiente', N'Entrega a domicilio', N'Direccion de prueba', 1000);
SET @PSinRuta = SCOPE_IDENTITY();
INSERT dbo.Pedidos (UsuarioId, Estado, TipoEntrega, DireccionEntrega, Total) VALUES (@Inactivo, N'Pendiente', N'Entrega a domicilio', N'Direccion de prueba', 1000);
SET @PInactivo = SCOPE_IDENTITY();
INSERT dbo.Pedidos (UsuarioId, Estado, TipoEntrega, DireccionEntrega, Total) VALUES (@Activo, N'Pendiente', N'Entrega a domicilio', N'Direccion de prueba', 1000);
SET @PEntregado = SCOPE_IDENTITY();
INSERT dbo.Pedidos (UsuarioId, Estado, TipoEntrega, DireccionEntrega, Total) VALUES (@Activo, N'Pendiente', N'Retiro en tienda', N'Direccion de prueba', 1000);
SET @PRetiro = SCOPE_IDENTITY();

INSERT dbo.Rutas (Codigo, Zona, ChoferUsuarioId, VehiculoId, Estado) VALUES (N'VERIFY-0283D', N'VERIFY-0283', @Chofer, @Vehiculo, N'Despachada');
SET @RutaD = SCOPE_IDENTITY();
INSERT dbo.Rutas (Codigo, Zona, ChoferUsuarioId, VehiculoId, Estado) VALUES (N'VERIFY-0283P', N'VERIFY-0283', @Chofer, @Vehiculo, N'Planificada');
SET @RutaP = SCOPE_IDENTITY();
INSERT dbo.RutaPedidos (RutaId, PedidoId, Secuencia, EstadoEntrega) VALUES (@RutaD, @PEnRuta, 1, N'EnRuta');
INSERT dbo.RutaPedidos (RutaId, PedidoId, Secuencia, EstadoEntrega) VALUES (@RutaD, @PEntregado, 2, N'Entregado');
INSERT dbo.RutaPedidos (RutaId, PedidoId, Secuencia, EstadoEntrega) VALUES (@RutaP, @PPlan, 1, N'Pendiente');

INSERT @T EXEC dbo.sp_Entregas_PedidosTablero;

IF NOT EXISTS (SELECT 1 FROM @T WHERE Grupo = N'EnRuta' AND PedidoId = @PEnRuta AND RutaCodigo = N'VERIFY-0283D' AND Chofer <> N'')
BEGIN ROLLBACK TRANSACTION; THROW 56882, N'El pedido de una ruta despachada no sale como En ruta.', 1; END;
IF NOT EXISTS (SELECT 1 FROM @T WHERE Grupo = N'Pendiente' AND PedidoId = @PPlan AND RutaCodigo = N'VERIFY-0283P')
BEGIN ROLLBACK TRANSACTION; THROW 56883, N'El pedido de una ruta planificada no sale como pendiente.', 1; END;
IF NOT EXISTS (SELECT 1 FROM @T WHERE Grupo = N'Pendiente' AND PedidoId = @PSinRuta AND RutaId IS NULL)
BEGIN ROLLBACK TRANSACTION; THROW 56884, N'El pedido sin ruta no sale como pendiente.', 1; END;
IF EXISTS (SELECT 1 FROM @T WHERE PedidoId IN (@PInactivo, @PEntregado, @PRetiro))
BEGIN ROLLBACK TRANSACTION; THROW 56885, N'Salio un pedido de cliente inactivo, ya entregado o de retiro en tienda.', 1; END;
IF EXISTS (SELECT 1 FROM @T WHERE Grupo = N'Pendiente' AND PedidoId = @PEnRuta)
BEGIN ROLLBACK TRANSACTION; THROW 56886, N'Un pedido En ruta tambien sale como pendiente.', 1; END;

DELETE @T;
INSERT @T EXEC dbo.sp_Entregas_PedidosTablero @Maximo = 1;
IF (SELECT COUNT(*) FROM @T WHERE Grupo = N'EnRuta') > 1 OR (SELECT COUNT(*) FROM @T WHERE Grupo = N'Pendiente') > 1
BEGIN ROLLBACK TRANSACTION; THROW 56887, N'@Maximo no limita cada grupo.', 1; END;

ROLLBACK TRANSACTION;
PRINT N'Verificacion 0283 completada.';
