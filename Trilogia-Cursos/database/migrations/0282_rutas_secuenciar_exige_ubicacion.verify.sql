SET NOCOUNT ON;

/* Verificacion de 0282 (BUG-25-01). Cada prueba corre en su propia transaccion, que se revierte. */
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0282_rutas_secuenciar_exige_ubicacion' AND Status = N'Applied')
    THROW 56870, N'0282 no figura aplicada en el ledger.', 1;

DECLARE @Chofer INT, @Vehiculo INT, @Cliente INT, @Ruta INT, @P1 INT, @P2 INT, @Orden NVARCHAR(100), @Rechazo INT, @Mensaje NVARCHAR(400);
DECLARE @Res TABLE (Secuenciados INT, SinCoordenadas INT);

SELECT TOP 1 @Chofer = u.UsuarioId FROM dbo.Usuarios u INNER JOIN dbo.Perfiles pf ON pf.PerfilId = u.PerfilId
WHERE u.Activo = 1 AND pf.Nombre = N'Chofer' ORDER BY u.UsuarioId;
SELECT TOP 1 @Vehiculo = VehiculoId FROM dbo.Vehiculos WHERE Activo = 1 ORDER BY VehiculoId;
IF @Chofer IS NULL OR @Vehiculo IS NULL THROW 56871, N'La verificacion necesita un chofer y un vehiculo activos.', 1;

/* 1) Con una parada sin coordenada se rechaza (54012) y el orden no cambia. El THROW
      deja la transaccion sin poder continuar: se captura y se revierte. */
BEGIN TRANSACTION;
INSERT dbo.Usuarios (PerfilId, NombreCompleto, Correo, Contrasena, Telefono, Direccion, Activo)
SELECT TOP 1 pf.PerfilId, N'VERIFY-0282 Cliente', N'verify0282@example.invalid', N'x', N'00000000', N'Direccion de prueba', 1
FROM dbo.Perfiles pf WHERE pf.Nombre = N'Cliente';
SET @Cliente = SCOPE_IDENTITY();
INSERT dbo.Pedidos (UsuarioId, Estado, TipoEntrega, DireccionEntrega, Total) VALUES (@Cliente, N'Pendiente', N'Domicilio', N'Direccion de prueba', 1000);
SET @P1 = SCOPE_IDENTITY();
INSERT dbo.Pedidos (UsuarioId, Estado, TipoEntrega, DireccionEntrega, Total) VALUES (@Cliente, N'Pendiente', N'Domicilio', N'Direccion de prueba', 1000);
SET @P2 = SCOPE_IDENTITY();
INSERT dbo.Rutas (Codigo, Zona, ChoferUsuarioId, VehiculoId, Estado) VALUES (N'VERIFY-0282', N'VERIFY-0282', @Chofer, @Vehiculo, N'Planificada');
SET @Ruta = SCOPE_IDENTITY();
INSERT dbo.RutaPedidos (RutaId, PedidoId, Secuencia, EstadoEntrega, Latitud, Longitud) VALUES (@Ruta, @P1, 2, N'Pendiente', 10.0, -84.1);
INSERT dbo.RutaPedidos (RutaId, PedidoId, Secuencia, EstadoEntrega, Latitud, Longitud) VALUES (@Ruta, @P2, 1, N'Pendiente', NULL, NULL);

SET @Rechazo = 0;
BEGIN TRY
    INSERT @Res EXEC dbo.sp_Rutas_Secuenciar @Ruta;
END TRY
BEGIN CATCH
    SET @Rechazo = ERROR_NUMBER();
    SET @Mensaje = ERROR_MESSAGE();
END CATCH;
IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
IF @Rechazo <> 54012
    THROW 56872, N'Secuenciar una ruta con paradas sin coordenada no se rechazo.', 1;
IF @Mensaje NOT LIKE N'1 de 2 punto(s)%'
    THROW 56873, N'El rechazo no dice cuantas paradas faltan.', 1;

/* 2) Con todas las coordenadas se secuencia (el rechazo anterior no dejo nada). */
BEGIN TRANSACTION;
INSERT dbo.Usuarios (PerfilId, NombreCompleto, Correo, Contrasena, Telefono, Direccion, Activo)
SELECT TOP 1 pf.PerfilId, N'VERIFY-0282 Cliente', N'verify0282@example.invalid', N'x', N'00000000', N'Direccion de prueba', 1
FROM dbo.Perfiles pf WHERE pf.Nombre = N'Cliente';
SET @Cliente = SCOPE_IDENTITY();
INSERT dbo.Pedidos (UsuarioId, Estado, TipoEntrega, DireccionEntrega, Total) VALUES (@Cliente, N'Pendiente', N'Domicilio', N'Direccion de prueba', 1000);
SET @P1 = SCOPE_IDENTITY();
INSERT dbo.Pedidos (UsuarioId, Estado, TipoEntrega, DireccionEntrega, Total) VALUES (@Cliente, N'Pendiente', N'Domicilio', N'Direccion de prueba', 1000);
SET @P2 = SCOPE_IDENTITY();
INSERT dbo.Rutas (Codigo, Zona, ChoferUsuarioId, VehiculoId, Estado) VALUES (N'VERIFY-0282', N'VERIFY-0282', @Chofer, @Vehiculo, N'Planificada');
SET @Ruta = SCOPE_IDENTITY();
INSERT dbo.RutaPedidos (RutaId, PedidoId, Secuencia, EstadoEntrega, Latitud, Longitud) VALUES (@Ruta, @P1, 1, N'Pendiente', 10.5, -84.5);
INSERT dbo.RutaPedidos (RutaId, PedidoId, Secuencia, EstadoEntrega, Latitud, Longitud) VALUES (@Ruta, @P2, 2, N'Pendiente', 9.94, -84.08);

INSERT @Res EXEC dbo.sp_Rutas_Secuenciar @Ruta;
IF NOT EXISTS (SELECT 1 FROM @Res WHERE Secuenciados = 2 AND SinCoordenadas = 0)
BEGIN ROLLBACK TRANSACTION; THROW 56874, N'Secuenciar no devolvio 2 puntos y 0 sin coordenada.', 1; END;
SELECT @Orden = STRING_AGG(CAST(PedidoId AS NVARCHAR(20)), N',') WITHIN GROUP (ORDER BY Secuencia) FROM dbo.RutaPedidos WHERE RutaId = @Ruta;
IF @Orden <> CONCAT(@P2, N',', @P1)
BEGIN ROLLBACK TRANSACTION; THROW 56875, N'Secuenciar no ordeno por cercania.', 1; END;
ROLLBACK TRANSACTION;

IF EXISTS (SELECT 1 FROM dbo.Rutas WHERE Codigo = N'VERIFY-0282') OR EXISTS (SELECT 1 FROM dbo.Usuarios WHERE NombreCompleto = N'VERIFY-0282 Cliente')
    THROW 56876, N'La verificacion dejo datos de prueba.', 1;
PRINT N'Verificacion 0282 completada.';
