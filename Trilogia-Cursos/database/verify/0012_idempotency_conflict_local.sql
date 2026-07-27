/*
  Prueba negativa local para el conflicto de token idempotente de 0012.
  Debe terminar con SQL error 54609. El rollback interno del procedimiento
  elimina todos los datos de prueba de la transacción abierta.
  No ejecutar en Azure DEV, producción ni una base compartida.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

DECLARE @ComboId INT;
DECLARE @Token UNIQUEIDENTIFIER = NEWID();
DECLARE @Items NVARCHAR(MAX);

UPDATE dbo.Productos SET Stock = 100 WHERE ProductoId IN (1, 2);

EXEC dbo.sp_Admin_CreateCombo
    @Nombre = N'QA token conflict 0012',
    @Descripcion = N'Prueba negativa local',
    @Precio = 1000.00,
    @ComponentesJson = N'[{"productoId":1,"cantidad":1},{"productoId":2,"cantidad":1}]',
    @UsuarioId = 1,
    @UsuarioNombre = N'QA Local',
    @NuevoComboId = @ComboId OUTPUT;

SET @Items = CONCAT(N'[{"tipo":"Combo","productoId":null,"comboId":', @ComboId, N',"cantidad":1}]');

EXEC dbo.sp_Store_CreateOrderWithPromotions
    @UsuarioId = 2,
    @TipoEntrega = N'Retiro en tienda',
    @IdentificacionCliente = N'QA-TOKEN-0012',
    @ItemsJson = @Items,
    @TokenOperacion = @Token;

EXEC dbo.sp_Store_CreateOrderWithPromotions
    @UsuarioId = 2,
    @TipoEntrega = N'Retiro en tienda',
    @IdentificacionCliente = N'QA-TOKEN-0012',
    @ItemsJson = N'[{"tipo":"Producto","productoId":1,"comboId":null,"cantidad":1}]',
    @TokenOperacion = @Token;
