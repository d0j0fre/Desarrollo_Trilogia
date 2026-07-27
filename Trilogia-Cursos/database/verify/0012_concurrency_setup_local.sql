/* LocalDB-only fixture for the parallel checkout test. Do not run in shared environments. */
SET NOCOUNT ON;

UPDATE dbo.Productos SET Stock = 1 WHERE ProductoId = 1;

DECLARE @ComboId INT;
EXEC dbo.sp_Admin_CreateCombo
    @Nombre = N'QA concurrency 0012',
    @Descripcion = N'Prueba de dos sesiones',
    @Precio = 1000.00,
    @ComponentesJson = N'[{"productoId":1,"cantidad":1}]',
    @UsuarioId = 1,
    @UsuarioNombre = N'QA Local',
    @NuevoComboId = @ComboId OUTPUT;

SELECT @ComboId AS ComboId;
