SET NOCOUNT ON;

/* Verificacion de 0280 (BUG-06-04).
   La lista se prueba en una transaccion que se revierte. sp_Rutas_Create y
   sp_Rutas_AddOrder hacen ROLLBACK al rechazar, y eso desharia una transaccion
   externa: esas dos pruebas usan datos de prueba (VERIFY-0280) que se crean y se
   borran al final, tambien si la prueba falla. */
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0280_rutas_cliente_inactivo' AND Status = N'Applied')
    THROW 56850, N'0280 no figura aplicada en el ledger.', 1;

DECLARE @Cliente INT, @Pedido INT, @Ruta INT, @Chofer INT, @Vehiculo INT, @Json NVARCHAR(MAX), @Rechazo INT, @Falla NVARCHAR(200);
DECLARE @Lista TABLE (PedidoId INT, Cliente NVARCHAR(150), Correo NVARCHAR(150), FechaPedido DATETIME2, Estado NVARCHAR(30),
    TipoEntrega NVARCHAR(60), DireccionEntrega NVARCHAR(300), Total DECIMAL(18,2), TotalLineas INT);

SELECT TOP 1 @Chofer = u.UsuarioId FROM dbo.Usuarios u INNER JOIN dbo.Perfiles pf ON pf.PerfilId = u.PerfilId
WHERE u.Activo = 1 AND pf.Nombre = N'Chofer' ORDER BY u.UsuarioId;
SELECT TOP 1 @Vehiculo = VehiculoId FROM dbo.Vehiculos WHERE Activo = 1 ORDER BY VehiculoId;
IF @Chofer IS NULL OR @Vehiculo IS NULL THROW 56851, N'La verificacion necesita un chofer y un vehiculo activos.', 1;

/* 1) La lista de pedidos asignables. */
BEGIN TRANSACTION;
INSERT dbo.Usuarios (PerfilId, NombreCompleto, Correo, Contrasena, Telefono, Direccion, Activo)
SELECT TOP 1 pf.PerfilId, N'VERIFY-0280 Cliente', N'verify0280@example.invalid', N'x', N'00000000', N'Direccion de prueba', 1
FROM dbo.Perfiles pf WHERE pf.Nombre = N'Cliente';
SET @Cliente = SCOPE_IDENTITY();
INSERT dbo.Pedidos (UsuarioId, Estado, TipoEntrega, DireccionEntrega, Total)
VALUES (@Cliente, N'Pendiente', N'Domicilio', N'Direccion de prueba', 1000);
SET @Pedido = SCOPE_IDENTITY();

INSERT @Lista EXEC dbo.sp_Rutas_GetAssignableOrders @Buscar = N'VERIFY-0280';
IF NOT EXISTS (SELECT 1 FROM @Lista WHERE PedidoId = @Pedido)
BEGIN ROLLBACK TRANSACTION; THROW 56852, N'El pedido de un cliente activo no se ofrece para rutas.', 1; END;

UPDATE dbo.Usuarios SET Activo = 0 WHERE UsuarioId = @Cliente;
DELETE @Lista;
INSERT @Lista EXEC dbo.sp_Rutas_GetAssignableOrders @Buscar = N'VERIFY-0280';
IF EXISTS (SELECT 1 FROM @Lista WHERE PedidoId = @Pedido)
BEGIN ROLLBACK TRANSACTION; THROW 56853, N'El pedido de un cliente inactivo sigue ofrecido para rutas.', 1; END;
ROLLBACK TRANSACTION;

/* 2) Create y AddOrder rechazan al cliente inactivo. */
BEGIN TRY
    INSERT dbo.Usuarios (PerfilId, NombreCompleto, Correo, Contrasena, Telefono, Direccion, Activo)
    SELECT TOP 1 pf.PerfilId, N'VERIFY-0280 Cliente', N'verify0280@example.invalid', N'x', N'00000000', N'Direccion de prueba', 0
    FROM dbo.Perfiles pf WHERE pf.Nombre = N'Cliente';
    SET @Cliente = SCOPE_IDENTITY();
    INSERT dbo.Pedidos (UsuarioId, Estado, TipoEntrega, DireccionEntrega, Total)
    VALUES (@Cliente, N'Pendiente', N'Domicilio', N'Direccion de prueba', 1000);
    SET @Pedido = SCOPE_IDENTITY();
    SET @Json = N'[{"pedidoId":' + CAST(@Pedido AS NVARCHAR(20)) + N'}]';

    SET @Rechazo = 0;
    BEGIN TRY
        EXEC dbo.sp_Rutas_Create N'VERIFY-0280', @Chofer, @Vehiculo, NULL, @Json, NULL, NULL;
    END TRY
    BEGIN CATCH
        SET @Rechazo = ERROR_NUMBER();
    END CATCH;
    IF @Rechazo <> 52006 THROW 56854, N'sp_Rutas_Create no rechazo el pedido de un cliente inactivo.', 1;

    INSERT dbo.Rutas (Codigo, Zona, ChoferUsuarioId, VehiculoId, Estado)
    VALUES (N'VERIFY-0280', N'VERIFY-0280', @Chofer, @Vehiculo, N'Planificada');
    SET @Ruta = SCOPE_IDENTITY();

    SET @Rechazo = 0;
    BEGIN TRY
        EXEC dbo.sp_Rutas_AddOrder @Ruta, @Pedido;
    END TRY
    BEGIN CATCH
        SET @Rechazo = ERROR_NUMBER();
    END CATCH;
    IF @Rechazo <> 52014 THROW 56856, N'sp_Rutas_AddOrder no rechazo el pedido de un cliente inactivo.', 1;
END TRY
BEGIN CATCH
    SET @Falla = ERROR_MESSAGE();
END CATCH;

/* Limpieza de los datos de prueba (siempre). */
IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
DELETE rp FROM dbo.RutaPedidos rp INNER JOIN dbo.Rutas r ON r.RutaId = rp.RutaId WHERE r.Codigo = N'VERIFY-0280';
DELETE dbo.Rutas WHERE Codigo = N'VERIFY-0280';
DELETE p FROM dbo.Pedidos p INNER JOIN dbo.Usuarios u ON u.UsuarioId = p.UsuarioId WHERE u.NombreCompleto = N'VERIFY-0280 Cliente';
DELETE dbo.Usuarios WHERE NombreCompleto = N'VERIFY-0280 Cliente';

IF @Falla IS NOT NULL THROW 56857, @Falla, 1;
PRINT N'Verificacion 0280 completada.';
