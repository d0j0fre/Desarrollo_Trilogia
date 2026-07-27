/*
  Verificación de sólo lectura para 0012.
  Ejecute después de aplicar 0012, contra la base seleccionada explícitamente.
  No inserta, actualiza ni elimina datos.
*/
SET NOCOUNT ON;

IF NOT EXISTS
(
    SELECT 1
    FROM dbo.SchemaMigrationHistory
    WHERE MigrationId = N'0012_inventory_combos_transformations_intelligence'
      AND Status = N'Applied'
)
    THROW 54600, N'El ledger no confirma que 0012 esté aplicada.', 1;

DECLARE @ObjetosEsperados TABLE
(
    ObjectName SYSNAME NOT NULL,
    ObjectType CHAR(2) NOT NULL
);

INSERT INTO @ObjetosEsperados (ObjectName, ObjectType)
VALUES
    (N'dbo.Combos', N'U'),
    (N'dbo.ComboDetalle', N'U'),
    (N'dbo.PedidoCombos', N'U'),
    (N'dbo.PedidoComboDetalle', N'U'),
    (N'dbo.FacturaCombos', N'U'),
    (N'dbo.CheckoutOperaciones', N'U'),
    (N'dbo.InventarioTransformaciones', N'U'),
    (N'dbo.InventarioOperacionAuditoria', N'U'),
    (N'dbo.sp_Admin_GetCombos', N'P'),
    (N'dbo.sp_Admin_CreateCombo', N'P'),
    (N'dbo.sp_Store_GetActiveCombos', N'P'),
    (N'dbo.sp_Store_CreateOrderWithPromotions', N'P'),
    (N'dbo.sp_Inventory_TransformStockAtomic', N'P'),
    (N'dbo.sp_Inventory_RestoreOrderStock', N'P'),
    (N'dbo.sp_Admin_GetPurchaseSuggestions', N'P'),
    (N'dbo.sp_Admin_GetSlowMovingProducts', N'P'),
    (N'dbo.sp_Admin_GetSeasonalSalesTrend', N'P');

IF EXISTS
(
    SELECT 1
    FROM @ObjetosEsperados expected
    WHERE OBJECT_ID(expected.ObjectName, expected.ObjectType) IS NULL
)
    THROW 54601, N'Faltan objetos esperados de 0012.', 1;

IF COL_LENGTH(N'dbo.PedidoDetalle', N'ProductoNombreSnapshot') IS NULL
   OR COL_LENGTH(N'dbo.PedidoDetalle', N'EsRegalo') IS NULL
    THROW 54602, N'Faltan columnas de snapshot en PedidoDetalle.', 1;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.parameters parameter
    WHERE parameter.object_id = OBJECT_ID(N'dbo.sp_Store_CreateOrderWithPromotions', N'P')
      AND parameter.name = N'@TokenOperacion'
      AND TYPE_NAME(parameter.user_type_id) = N'uniqueidentifier'
)
    THROW 54603, N'El contrato idempotente de checkout no está disponible.', 1;

IF (SELECT COUNT(*) FROM dbo.Permisos
    WHERE Codigo IN (N'COMBOS_VER', N'COMBOS_GESTIONAR', N'INVENTARIO_TRANSFORMAR', N'INVENTARIO_INTELIGENCIA_VER')
      AND Activo = 1) <> 4
    THROW 54604, N'Faltan permisos activos de 0012.', 1;

IF (SELECT COUNT(DISTINCT permission.Codigo)
    FROM dbo.Perfiles profile
    INNER JOIN dbo.PerfilPermisos profilePermission ON profilePermission.PerfilId = profile.PerfilId
    INNER JOIN dbo.Permisos permission ON permission.PermisoId = profilePermission.PermisoId
    WHERE profile.Nombre = N'Administrador'
      AND permission.Codigo IN (N'COMBOS_VER', N'COMBOS_GESTIONAR', N'INVENTARIO_TRANSFORMAR', N'INVENTARIO_INTELIGENCIA_VER')) <> 4
    THROW 54605, N'El perfil Administrador no tiene todos los permisos de 0012.', 1;

SELECT N'0012 verificada' AS Resultado,
       DB_NAME() AS BaseDeDatos,
       SYSUTCDATETIME() AS VerificadoEnUtc;
