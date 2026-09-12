SET NOCOUNT ON;
SET XACT_ABORT ON;

/*  0023 — sp_Admin_ToggleProductStatus devuelve el estado resultante.

    Problema corregido
    ------------------
    InventoryController.ToggleStatus usa el valor devuelto por el procedimiento
    para decidir DOS cosas: el texto de TempData y, sobre todo, el registro de
    auditoría ("Activar" contra "Inactivar"). Las definiciones históricas del
    procedimiento en los baselines (sp_admin_modulo.sql y 00_todo_en_uno.sql)
    hacen el UPDATE pero no devuelven fila alguna, por lo que ExecuteScalar
    entrega NULL, el booleano queda en false y CADA reactivación se audita como
    si hubiera sido una inactivación.

    Esta migración deja una única definición canónica que devuelve el nuevo
    valor con la cláusula OUTPUT del propio UPDATE: es atómico y no depende de
    una segunda lectura que otra sesión podría alterar.

    Compatibilidad
    --------------
    - Se usa CREATE OR ALTER (no DROP) para conservar object_id y los permisos
      GRANT EXECUTE ya otorgados a los perfiles.
    - Normaliza cualquier firma previa, incluida la variante de dos parámetros
      (@ProductoId, @Activo) que existe en database_Esteban/Fase3_1-3.sql y que
      la aplicación nunca podría invocar, porque sólo envía @ProductoId.
    - Ningún otro objeto SQL ejecuta este procedimiento; el único consumidor es
      AdminDbService.ToggleProductStatusAsync.
    - No modifica datos de negocio: sólo reemplaza una definición de código.
*/

IF OBJECT_ID(N'dbo.SchemaMigrationHistory', N'U') IS NULL
    THROW 55400, N'Falta el ledger 0001.', 1;

IF EXISTS (SELECT 1 FROM dbo.SchemaMigrationHistory
           WHERE MigrationId = N'0023_toggle_product_status_returns_state'
             AND Status = N'Applied')
    THROW 55401, N'0023 ya figura aplicada.', 1;

IF OBJECT_ID(N'dbo.Productos', N'U') IS NULL
    THROW 55402, N'Falta la tabla dbo.Productos.', 1;

IF NOT EXISTS (SELECT 1
               FROM sys.columns columna
               JOIN sys.types tipo ON tipo.user_type_id = columna.user_type_id
               WHERE columna.object_id = OBJECT_ID(N'dbo.Productos')
                 AND columna.name = N'Activo'
                 AND tipo.name = N'bit')
    THROW 55403, N'dbo.Productos.Activo no existe o no es BIT.', 1;

BEGIN TRANSACTION;

DECLARE @definicion NVARCHAR(MAX) = N'
CREATE OR ALTER PROCEDURE dbo.sp_Admin_ToggleProductStatus
    @ProductoId INT
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.Productos WHERE ProductoId = @ProductoId)
        THROW 51005, N''El producto indicado no existe.'', 1;

    /* El OUTPUT devuelve el estado ya invertido dentro de la misma sentencia,
       sin una segunda lectura que pudiera leer el valor de otra sesión. */
    UPDATE dbo.Productos
    SET Activo = CASE WHEN Activo = 1 THEN 0 ELSE 1 END
    OUTPUT INSERTED.Activo
    WHERE ProductoId = @ProductoId;
END';

EXEC sys.sp_executesql @definicion;

/* Validación posterior: la migración se revierte si la definición no quedó
   con la forma esperada. */
IF OBJECT_ID(N'dbo.sp_Admin_ToggleProductStatus', N'P') IS NULL
    THROW 55406, N'El procedimiento no quedó creado.', 1;

IF (SELECT COUNT(*) FROM sys.parameters
    WHERE object_id = OBJECT_ID(N'dbo.sp_Admin_ToggleProductStatus')) <> 1
    THROW 55406, N'El procedimiento debe exponer exactamente un parámetro.', 1;

IF NOT EXISTS (SELECT 1 FROM sys.parameters
               WHERE object_id = OBJECT_ID(N'dbo.sp_Admin_ToggleProductStatus')
                 AND name = N'@ProductoId')
    THROW 55406, N'El único parámetro debe llamarse @ProductoId.', 1;

IF NOT EXISTS (SELECT 1 FROM sys.sql_modules
               WHERE object_id = OBJECT_ID(N'dbo.sp_Admin_ToggleProductStatus')
                 AND definition LIKE N'%OUTPUT INSERTED.Activo%')
    THROW 55406, N'La definición no devuelve el estado resultante.', 1;

IF XACT_STATE() <> 1
    THROW 55404, N'La transacción 0023 no está disponible.', 1;

DECLARE @MigrationSha256 NVARCHAR(128) = N'$(MigrationSha256)';
IF LEN(@MigrationSha256) <> 64 OR @MigrationSha256 LIKE N'%[^0-9A-Fa-f]%'
    THROW 55405, N'SHA-256 inválido para 0023.', 1;

INSERT dbo.SchemaMigrationHistory
    (MigrationId, FileName, FileSha256, Status, AppliedBy, EnvironmentName, Notes)
VALUES
    (N'0023_toggle_product_status_returns_state',
     N'0023_toggle_product_status_returns_state.sql',
     UPPER(@MigrationSha256),
     N'Applied',
     ORIGINAL_LOGIN(),
     DB_NAME(),
     N'sp_Admin_ToggleProductStatus devuelve el estado resultante para que la auditoría distinga activar de inactivar.');

COMMIT TRANSACTION;
