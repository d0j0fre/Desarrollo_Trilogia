SET NOCOUNT ON;

/* Verificacion de 0281 (BUG-10-01). Corre en una transaccion que se revierte. */
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0281_rutas_prioridad_secuenciacion' AND Status = N'Applied')
    THROW 56860, N'0281 no figura aplicada en el ledger.', 1;
IF COL_LENGTH(N'dbo.RutaPedidos', N'Prioridad') IS NULL
    THROW 56861, N'Falta dbo.RutaPedidos.Prioridad.', 1;

DECLARE @Chofer INT, @Vehiculo INT, @Cliente INT, @Ruta INT, @Json NVARCHAR(MAX), @Orden NVARCHAR(200), @Rechazo INT;
DECLARE @P1 INT, @P2 INT, @P3 INT, @P4 INT;
DECLARE @Res TABLE (Secuenciados INT, SinCoordenadas INT);
DECLARE @Creada TABLE (RutaId INT, Codigo NVARCHAR(30));

SELECT TOP 1 @Chofer = u.UsuarioId FROM dbo.Usuarios u INNER JOIN dbo.Perfiles pf ON pf.PerfilId = u.PerfilId
WHERE u.Activo = 1 AND pf.Nombre = N'Chofer' ORDER BY u.UsuarioId;
SELECT TOP 1 @Vehiculo = VehiculoId FROM dbo.Vehiculos WHERE Activo = 1 ORDER BY VehiculoId;
IF @Chofer IS NULL OR @Vehiculo IS NULL THROW 56862, N'La verificacion necesita un chofer y un vehiculo activos.', 1;

BEGIN TRANSACTION;

INSERT dbo.Usuarios (PerfilId, NombreCompleto, Correo, Contrasena, Telefono, Direccion, Activo)
SELECT TOP 1 pf.PerfilId, N'VERIFY-0281 Cliente', N'verify0281@example.invalid', N'x', N'00000000', N'Direccion de prueba', 1
FROM dbo.Perfiles pf WHERE pf.Nombre = N'Cliente';
SET @Cliente = SCOPE_IDENTITY();

/* Cuatro pedidos. Cercania al origen (9.9333, -84.0833): P1 < P2 < P3 < P4. */
INSERT dbo.Pedidos (UsuarioId, Estado, TipoEntrega, DireccionEntrega, Total) VALUES (@Cliente, N'Pendiente', N'Domicilio', N'Direccion de prueba', 1000);
SET @P1 = SCOPE_IDENTITY();
INSERT dbo.Pedidos (UsuarioId, Estado, TipoEntrega, DireccionEntrega, Total) VALUES (@Cliente, N'Pendiente', N'Domicilio', N'Direccion de prueba', 1000);
SET @P2 = SCOPE_IDENTITY();
INSERT dbo.Pedidos (UsuarioId, Estado, TipoEntrega, DireccionEntrega, Total) VALUES (@Cliente, N'Pendiente', N'Domicilio', N'Direccion de prueba', 1000);
SET @P3 = SCOPE_IDENTITY();
INSERT dbo.Pedidos (UsuarioId, Estado, TipoEntrega, DireccionEntrega, Total) VALUES (@Cliente, N'Pendiente', N'Domicilio', N'Direccion de prueba', 1000);
SET @P4 = SCOPE_IDENTITY();

/* Create guarda la prioridad indicada y un pedido sin dato queda en 3 (Normal). */
SET @Json = N'[{"pedidoId":' + CAST(@P1 AS NVARCHAR(20)) + N',"prioridad":3},'
          + N'{"pedidoId":' + CAST(@P2 AS NVARCHAR(20)) + N',"prioridad":2},'
          + N'{"pedidoId":' + CAST(@P3 AS NVARCHAR(20)) + N',"prioridad":1},'
          + N'{"pedidoId":' + CAST(@P4 AS NVARCHAR(20)) + N'}]';
INSERT @Creada EXEC dbo.sp_Rutas_Create N'VERIFY-0281', @Chofer, @Vehiculo, NULL, @Json, NULL, NULL;
SELECT @Ruta = RutaId FROM @Creada;
IF @Ruta IS NULL BEGIN ROLLBACK TRANSACTION; THROW 56863, N'No se creo la ruta de prueba.', 1; END;
IF (SELECT Prioridad FROM dbo.RutaPedidos WHERE RutaId = @Ruta AND PedidoId = @P3) <> 1
   OR (SELECT Prioridad FROM dbo.RutaPedidos WHERE RutaId = @Ruta AND PedidoId = @P4) <> 3
BEGIN ROLLBACK TRANSACTION; THROW 56864, N'sp_Rutas_Create no guardo la prioridad indicada.', 1; END;

/* Coordenadas: P1 la mas cercana a la bodega y P4 la mas lejana. */
UPDATE dbo.RutaPedidos SET Latitud = 9.940000, Longitud = -84.080000 WHERE RutaId = @Ruta AND PedidoId = @P1;
UPDATE dbo.RutaPedidos SET Latitud = 10.000000, Longitud = -84.100000 WHERE RutaId = @Ruta AND PedidoId = @P2;
UPDATE dbo.RutaPedidos SET Latitud = 10.200000, Longitud = -84.200000 WHERE RutaId = @Ruta AND PedidoId = @P3;
UPDATE dbo.RutaPedidos SET Latitud = 10.500000, Longitud = -84.500000 WHERE RutaId = @Ruta AND PedidoId = @P4;

/* Prioridades 3, 2, 1, 3: el orden por prioridad es P3, P2 y luego P1 y P4 (cercania). */
INSERT @Res EXEC dbo.sp_Rutas_Secuenciar @Ruta;
SELECT @Orden = STRING_AGG(CAST(PedidoId AS NVARCHAR(20)), N',') WITHIN GROUP (ORDER BY Secuencia) FROM dbo.RutaPedidos WHERE RutaId = @Ruta;
IF @Orden <> CONCAT(@P3, N',', @P2, N',', @P1, N',', @P4)
BEGIN ROLLBACK TRANSACTION; THROW 56865, N'sp_Rutas_Secuenciar no ordeno por prioridad y luego por cercania.', 1; END;

/* Con la misma prioridad en todas, vuelve a ordenar solo por cercania. */
UPDATE dbo.RutaPedidos SET Prioridad = 3 WHERE RutaId = @Ruta;
DELETE @Res;
INSERT @Res EXEC dbo.sp_Rutas_Secuenciar @Ruta;
SELECT @Orden = STRING_AGG(CAST(PedidoId AS NVARCHAR(20)), N',') WITHIN GROUP (ORDER BY Secuencia) FROM dbo.RutaPedidos WHERE RutaId = @Ruta;
IF @Orden <> CONCAT(@P1, N',', @P2, N',', @P3, N',', @P4)
BEGIN ROLLBACK TRANSACTION; THROW 56866, N'Con prioridades iguales no se ordeno por cercania.', 1; END;

/* GuardarSecuencia cambia la prioridad y rechaza un valor fuera de 1 a 3 (54002). */
DECLARE @RP4 INT = (SELECT RutaPedidoId FROM dbo.RutaPedidos WHERE RutaId = @Ruta AND PedidoId = @P4);
SET @Json = N'[{"rutaPedidoId":' + CAST(@RP4 AS NVARCHAR(20)) + N',"secuencia":4,"lat":10.5,"lng":-84.5,"prioridad":1}]';
EXEC dbo.sp_Rutas_GuardarSecuencia @Ruta, @Json;
IF (SELECT Prioridad FROM dbo.RutaPedidos WHERE RutaPedidoId = @RP4) <> 1
BEGIN ROLLBACK TRANSACTION; THROW 56867, N'sp_Rutas_GuardarSecuencia no cambio la prioridad.', 1; END;

SET @Json = N'[{"rutaPedidoId":' + CAST(@RP4 AS NVARCHAR(20)) + N',"secuencia":4,"prioridad":9}]';
SET @Rechazo = 0;
BEGIN TRY
    EXEC dbo.sp_Rutas_GuardarSecuencia @Ruta, @Json;
END TRY
BEGIN CATCH
    SET @Rechazo = ERROR_NUMBER();
END CATCH;
IF @Rechazo <> 54002
BEGIN IF XACT_STATE() <> 0 ROLLBACK TRANSACTION; THROW 56868, N'Una prioridad fuera de 1 a 3 no se rechazo.', 1; END;

/* GetOrders devuelve Prioridad como ultima columna. */
DECLARE @Ultima NVARCHAR(128);
SELECT TOP 1 @Ultima = name FROM sys.dm_exec_describe_first_result_set_for_object(OBJECT_ID(N'dbo.sp_Rutas_GetOrders'), NULL) ORDER BY column_ordinal DESC;
IF @Ultima <> N'Prioridad'
BEGIN IF XACT_STATE() <> 0 ROLLBACK TRANSACTION; THROW 56869, N'sp_Rutas_GetOrders no devuelve Prioridad al final.', 1; END;

IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
PRINT N'Verificacion 0281 completada.';
