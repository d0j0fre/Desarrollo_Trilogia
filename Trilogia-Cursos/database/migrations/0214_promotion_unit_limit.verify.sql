SET NOCOUNT ON;
SET XACT_ABORT OFF;

/*  Verificación de 0214. Sólo lectura: la prueba del disparador corre dentro
    de una transacción que siempre se revierte. */

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrationHistory
               WHERE MigrationId = N'0214_promotion_unit_limit'
                 AND Status = N'Applied'
                 AND LEN(FileSha256) = 64)
    THROW 55410, N'0214 no figura aplicada correctamente.', 1;

IF COL_LENGTH(N'dbo.Promociones', N'LimiteUnidades') IS NULL OR COL_LENGTH(N'dbo.Promociones', N'UnidadesConsumidas') IS NULL
    THROW 55411, N'Faltan las columnas LimiteUnidades / UnidadesConsumidas.', 1;

IF NOT EXISTS (SELECT 1 FROM sys.parameters WHERE object_id = OBJECT_ID(N'dbo.sp_Promociones_Create') AND name = N'@LimiteUnidades')
   OR NOT EXISTS (SELECT 1 FROM sys.parameters WHERE object_id = OBJECT_ID(N'dbo.sp_Promociones_Update') AND name = N'@LimiteUnidades')
    THROW 55412, N'Crear o editar promoción no acepta @LimiteUnidades.', 1;

IF OBJECT_ID(N'dbo.TR_PromocionAplicaciones_LimiteUnidades', N'TR') IS NULL
    THROW 55413, N'Falta el disparador TR_PromocionAplicaciones_LimiteUnidades.', 1;

/* Prueba revertida: una regalía de 2 unidades sobre una promoción con límite 2
   debe dejarla Inactiva con 2 unidades consumidas. */
DECLARE @ProductoId INT = (SELECT TOP 1 ProductoId FROM dbo.Productos WHERE Activo = 1 ORDER BY ProductoId);
DECLARE @PedidoId INT = (SELECT TOP 1 PedidoId FROM dbo.Pedidos ORDER BY PedidoId);
DECLARE @Estado NVARCHAR(20), @Consumidas INT;
IF @ProductoId IS NOT NULL AND @PedidoId IS NOT NULL
BEGIN
    BEGIN TRANSACTION;
    INSERT dbo.Promociones (Nombre, Tipo, ProductoId, CantidadMinima, ProductoRegaloId, CantidadRegalo,
                            SegmentoCliente, FechaInicio, FechaFin, Prioridad, LimiteUnidades)
    VALUES (N'0214 verify', N'RegaliaPorVolumen', @ProductoId, 1, @ProductoId, 1,
            N'Todos', '2099-01-01', '2099-01-31', 0, 2);
    DECLARE @PromocionId INT = CAST(SCOPE_IDENTITY() AS INT);

    INSERT dbo.PromocionAplicaciones (PromocionId, PedidoId, ProductoId, TipoBeneficio, MontoDescontado, UnidadesRegalo)
    VALUES (@PromocionId, @PedidoId, @ProductoId, N'Regalia', NULL, 2);

    SELECT @Estado = Estado, @Consumidas = UnidadesConsumidas FROM dbo.Promociones WHERE PromocionId = @PromocionId;
    ROLLBACK TRANSACTION;

    IF @Estado <> N'Inactiva' OR @Consumidas <> 2
        THROW 55414, N'El disparador no inactivó la promoción al alcanzar el límite.', 1;
END;

SELECT ledger.MigrationId, ledger.Status, ledger.AppliedAtUtc,
       @Estado AS EstadoPrueba, @Consumidas AS ConsumidasPrueba
FROM dbo.SchemaMigrationHistory ledger
WHERE ledger.MigrationId = N'0214_promotion_unit_limit';
