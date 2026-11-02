SET NOCOUNT ON;

/* Verificacion de 0257. La prueba corre en una transaccion que se revierte. */
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0257_promociones_vencidas_en_lista' AND Status = N'Applied')
    THROW 56620, N'0257 no figura aplicada en el ledger.', 1;

DECLARE @Lista TABLE (PromocionId INT, Nombre NVARCHAR(150), Tipo NVARCHAR(30), SegmentoCliente NVARCHAR(20), Estado NVARCHAR(20),
    FechaInicio DATE, FechaFin DATE, CantidadMinima INT, PorcentajeDescuento DECIMAL(5,2), CantidadRegalo INT, Prioridad INT,
    ProductoNombre NVARCHAR(150), ProductoRegaloNombre NVARCHAR(150), Vigente BIT, LimiteUnidades INT, UnidadesConsumidas INT);
DECLARE @ProductoId INT = (SELECT TOP 1 ProductoId FROM dbo.Productos ORDER BY ProductoId), @Id INT;

BEGIN TRANSACTION;
INSERT dbo.Promociones (Nombre, Tipo, SegmentoCliente, Estado, ProductoId, CantidadMinima, PorcentajeDescuento, FechaInicio, FechaFin, Prioridad)
VALUES (N'VERIFY-0257', N'DescuentoPorcentual', N'Todos', N'Activa', @ProductoId, 1, 5,
        DATEADD(DAY, -30, CAST(SYSDATETIME() AS DATE)), DATEADD(DAY, -1, CAST(SYSDATETIME() AS DATE)), 1);
SET @Id = SCOPE_IDENTITY();

INSERT @Lista EXEC dbo.sp_Promociones_List @Estado = N'Vencida', @Buscar = N'VERIFY-0257';
IF NOT EXISTS (SELECT 1 FROM @Lista WHERE PromocionId = @Id AND Estado = N'Vencida' AND Vigente = 0)
BEGIN ROLLBACK TRANSACTION; THROW 56621, N'La promoción activa vencida no aparece como Vencida.', 1; END;

DELETE @Lista;
INSERT @Lista EXEC dbo.sp_Promociones_List @Estado = N'Activa', @Buscar = N'VERIFY-0257';
IF EXISTS (SELECT 1 FROM @Lista WHERE PromocionId = @Id)
BEGIN ROLLBACK TRANSACTION; THROW 56622, N'La promoción vencida sigue en el filtro Activa.', 1; END;

IF (SELECT Estado FROM dbo.Promociones WHERE PromocionId = @Id) <> N'Activa'
BEGIN ROLLBACK TRANSACTION; THROW 56623, N'La lista cambió el estado guardado.', 1; END;
ROLLBACK TRANSACTION;
PRINT N'Verificación 0257 completada.';
