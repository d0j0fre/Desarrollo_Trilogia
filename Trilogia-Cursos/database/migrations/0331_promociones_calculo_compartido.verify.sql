SET NOCOUNT ON;

/* Verificacion de 0331. Crea una promocion de prueba dentro de una transaccion y la revierte. */
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0331_promociones_calculo_compartido' AND Status = N'Applied')
    THROW 59390, N'0331 no figura aplicada en el ledger.', 1;
IF OBJECT_ID(N'dbo.fn_Promociones_Aplicar', N'IF') IS NULL OR OBJECT_ID(N'dbo.fn_Venta_Lineas', N'IF') IS NULL
    THROW 59391, N'Faltan dbo.fn_Promociones_Aplicar o dbo.fn_Venta_Lineas.', 1;

DECLARE @Cliente INT, @A INT, @B INT, @Hoy DATE = CAST(SYSDATETIME() AS DATE), @Items NVARCHAR(MAX), @Aplic INT, @Desc DECIMAL(18,2), @Regalo INT;
SELECT TOP 1 @Cliente = u.UsuarioId FROM dbo.Usuarios u INNER JOIN dbo.Perfiles p ON p.PerfilId = u.PerfilId WHERE p.Nombre = N'Cliente' AND u.Activo = 1 ORDER BY u.UsuarioId;
SELECT TOP 1 @A = ProductoId FROM dbo.Productos WHERE Activo = 1 AND Stock >= 20 AND Precio > 0 ORDER BY ProductoId;
SELECT TOP 1 @B = ProductoId FROM dbo.Productos WHERE Activo = 1 AND Stock >= 20 AND Precio > 0 AND ProductoId <> @A ORDER BY ProductoId;
IF @Cliente IS NULL OR @A IS NULL OR @B IS NULL THROW 59392, N'La verificacion necesita un cliente y dos productos con stock.', 1;
SET @Items = CONCAT(N'[{"productoId":', @A, N',"cantidad":3},{"productoId":', @A, N',"cantidad":1}]');

BEGIN TRANSACTION;
UPDATE dbo.Promociones SET Estado = N'Inactiva' WHERE ProductoId = @A AND Estado = N'Activa';
INSERT dbo.Promociones (Nombre, Tipo, ProductoId, CantidadMinima, PorcentajeDescuento, SegmentoCliente, FechaInicio, FechaFin, Estado, Prioridad)
VALUES (N'VERIFY 0331 descuento', N'DescuentoPorcentual', @A, 4, 10.00, N'Todos', DATEADD(DAY, -1, @Hoy), DATEADD(DAY, 1, @Hoy), N'Activa', 1);

/* Las cantidades repetidas se suman (3 + 1 = 4 = cantidad minima) y el 10 % cae solo sobre esa linea. */
SELECT @Aplic = COUNT(*), @Desc = MAX(MontoDescontado) FROM dbo.fn_Promociones_Aplicar(@Cliente, @Items) WHERE TipoBeneficio = N'Descuento' AND ProductoId = @A;
IF @Aplic <> 1 THROW 59393, N'La promocion de descuento no se aplico una sola vez.', 1;
IF @Desc <> (SELECT ROUND(4 * Precio * 10.00 / 100.0, 2) FROM dbo.Productos WHERE ProductoId = @A) THROW 59394, N'El monto del descuento no es el 10 % de la linea.', 1;
IF NOT EXISTS (SELECT 1 FROM dbo.fn_Venta_Lineas(@Cliente, @Items) WHERE ProductoId = @A AND Cantidad = 4 AND EsRegalo = 0 AND MontoDescuento = @Desc
               AND PrecioUnitario = CONVERT(DECIMAL(18,2), ((SELECT Precio FROM dbo.Productos WHERE ProductoId = @A) * 4 - @Desc) / 4))
    THROW 59395, N'La linea de venta no trae el precio unitario con descuento.', 1;

/* Con menos de la cantidad minima no aplica. */
IF EXISTS (SELECT 1 FROM dbo.fn_Promociones_Aplicar(@Cliente, CONCAT(N'[{"productoId":', @A, N',"cantidad":3}]')))
    THROW 59396, N'La promocion aplico por debajo de la cantidad minima.', 1;

/* Regalia por volumen: 1 de B por cada 2 comprados de A; se agrega la linea de regalia a precio 0. */
UPDATE dbo.Promociones SET Estado = N'Inactiva' WHERE ProductoId = @A AND Estado = N'Activa';
INSERT dbo.Promociones (Nombre, Tipo, ProductoId, CantidadMinima, ProductoRegaloId, CantidadRegalo, SegmentoCliente, FechaInicio, FechaFin, Estado, Prioridad)
VALUES (N'VERIFY 0331 regalia', N'RegaliaPorVolumen', @A, 2, @B, 1, N'Todos', DATEADD(DAY, -1, @Hoy), DATEADD(DAY, 1, @Hoy), N'Activa', 1);
SELECT @Regalo = SUM(Cantidad) FROM dbo.fn_Venta_Lineas(@Cliente, CONCAT(N'[{"productoId":', @A, N',"cantidad":5}]')) WHERE EsRegalo = 1 AND ProductoId = @B AND PrecioUnitario = 0;
IF ISNULL(@Regalo, 0) <> 2 THROW 59397, N'La regalia por volumen no dio 2 unidades por 5 comprados (minimo 2, 1 de regalo).', 1;
ROLLBACK TRANSACTION;
PRINT N'Verificacion 0331 completada.';
