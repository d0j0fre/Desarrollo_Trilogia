SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

/* Pruebas post instalación, observación 3 (decisión de Esteban): Venta móvil debe aplicar las promociones
   activas igual que la Tienda. Ejemplo real: promoción de 10 % sobre Yucas Tostadas; por la Tienda ₡2 970
   y por Venta móvil ₡3 300 sin descuento (pedido #92).

   Esta migración NO cambia ningún procedimiento: solo agrega las dos funciones que reúnen la regla de
   promociones para Venta móvil (sitio y app) y para la cotización que ve el vendedor antes de confirmar:

     - dbo.fn_Promociones_Aplicar(@ClienteUsuarioId, @ItemsJson): las promociones que aplican a un carrito
       de productos ([{"productoId":n,"cantidad":n}]). Es la misma regla de dbo.sp_Store_CreateOrderWithPromotions
       (checkout de la Tienda): promoción Activa dentro de su vigencia, segmento «Todos» o el del cliente,
       cantidad mínima, UN beneficio por producto (gana la mayor Prioridad, luego el menor PromocionId),
       descuento porcentual SOLO sobre la línea del producto estratégico (ROUND a 2 decimales), regalía por
       volumen (cantidad comprada / mínima * unidades de regalo) limitada al stock que queda del producto
       regalado, y reparto de ese stock entre promociones por prioridad. El límite de unidades de 0214 sigue
       aplicándose solo, con el disparador TR_PromocionAplicaciones_LimiteUnidades.
     - dbo.fn_Venta_Lineas(@ClienteUsuarioId, @ItemsJson): las líneas del pedido ya con precio final: precio
       de lista, descuento, precio unitario con el descuento repartido por unidad (CONVERT a DECIMAL(18,2),
       como el checkout de la Tienda) y las líneas de regalía a precio 0. La usan sp_Seller_CreateOrder y
       sp_Seller_CotizarPedido (0332), de modo que lo cotizado es lo que se registra.

   Qué NO se unificó: el checkout de la Tienda conserva su propia implementación dentro del procedimiento
   (bloquea inventario y reparte regalías con combos en el mismo paso); no se modificó para no arriesgar la
   compra en la Tienda. La paridad se comprobó con un ensayo en LocalDB (mismos carritos por ambos caminos).

   Aditiva: solo funciones nuevas, sin tablas ni datos. Rollback en 0331_promociones_calculo_compartido.rollback.md. */
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL THROW 59310,N'Falta el ledger 0001.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0331_promociones_calculo_compartido' AND Status=N'Applied')
    THROW 59311,N'0331 ya figura aplicada.',1;
IF OBJECT_ID(N'dbo.Promociones',N'U') IS NULL OR OBJECT_ID(N'dbo.PromocionAplicaciones',N'U') IS NULL
   OR COL_LENGTH(N'dbo.PedidoDetalle',N'EsRegalo') IS NULL
    THROW 59312,N'Faltan las promociones (0005/0012) o PedidoDetalle.EsRegalo.',1;

BEGIN TRANSACTION;
GO

CREATE OR ALTER FUNCTION dbo.fn_Promociones_Aplicar
(
    @ClienteUsuarioId INT,
    @ItemsJson NVARCHAR(MAX)
)
RETURNS TABLE
AS
RETURN
WITH Cart AS
(
    SELECT source.ProductoId, SUM(CONVERT(BIGINT, source.Cantidad)) AS Cantidad
    FROM OPENJSON(@ItemsJson)
         WITH (ProductoId INT '$.productoId', Cantidad INT '$.cantidad') source
    WHERE source.ProductoId > 0 AND ISNULL(source.Cantidad, 0) > 0
    GROUP BY source.ProductoId
),
Segmento AS
(
    SELECT ISNULL((SELECT TOP (1) ISNULL(NULLIF(userAccount.SegmentoCliente, N''), N'Minorista')
                   FROM dbo.Usuarios userAccount
                   WHERE userAccount.UsuarioId = @ClienteUsuarioId), N'Minorista') AS Nombre
),
Ranked AS
(
    SELECT promotion.PromocionId,
           promotion.Nombre,
           promotion.ProductoId,
           promotion.Tipo,
           promotion.PorcentajeDescuento,
           promotion.ProductoRegaloId,
           promotion.CantidadRegalo,
           promotion.CantidadMinima,
           promotion.Prioridad,
           item.Cantidad AS CantidadComprada,
           ROW_NUMBER() OVER
           (
               PARTITION BY promotion.ProductoId
               ORDER BY promotion.Prioridad DESC, promotion.PromocionId
           ) AS Position
    FROM Cart item
    CROSS JOIN Segmento segment
    INNER JOIN dbo.Promociones promotion ON promotion.ProductoId = item.ProductoId
    WHERE item.Cantidad >= promotion.CantidadMinima
      AND promotion.Estado = N'Activa'
      AND CONVERT(DATE, SYSDATETIME()) BETWEEN promotion.FechaInicio AND promotion.FechaFin
      AND promotion.SegmentoCliente IN (N'Todos', segment.Nombre)
),
Applications AS
(
    SELECT candidate.PromocionId,
           candidate.Nombre AS PromocionNombre,
           candidate.ProductoId,
           IIF(candidate.Tipo = N'DescuentoPorcentual', N'Descuento', N'Regalia') AS TipoBeneficio,
           CASE WHEN candidate.Tipo = N'DescuentoPorcentual'
                THEN CONVERT(DECIMAL(18,2), ROUND(candidate.CantidadComprada * product.Precio * candidate.PorcentajeDescuento / 100.0, 2))
           END AS MontoDescontado,
           CASE WHEN candidate.Tipo = N'RegaliaPorVolumen'
                THEN CONVERT(INT, (candidate.CantidadComprada / candidate.CantidadMinima) * candidate.CantidadRegalo)
           END AS UnidadesRegalo,
           candidate.ProductoRegaloId,
           candidate.Prioridad
    FROM Ranked candidate
    INNER JOIN dbo.Productos product ON product.ProductoId = candidate.ProductoId AND product.Activo = 1
    LEFT JOIN dbo.Productos gift ON gift.ProductoId = candidate.ProductoRegaloId AND gift.Activo = 1
    WHERE candidate.Position = 1
      AND ((candidate.Tipo = N'DescuentoPorcentual' AND candidate.PorcentajeDescuento > 0)
        OR (candidate.Tipo = N'RegaliaPorVolumen' AND gift.ProductoId IS NOT NULL AND candidate.CantidadRegalo > 0))
),
Prior AS
(
    SELECT application.PromocionId,
           application.ProductoId,
           application.UnidadesRegalo,
           locked.Stock
             - ISNULL((SELECT cartLine.Cantidad FROM Cart cartLine WHERE cartLine.ProductoId = application.ProductoRegaloId), 0)
             - ISNULL
               (
                   SUM(CONVERT(BIGINT, application.UnidadesRegalo)) OVER
                   (
                       PARTITION BY application.ProductoRegaloId
                       ORDER BY application.Prioridad DESC, application.PromocionId, application.ProductoId
                       ROWS BETWEEN UNBOUNDED PRECEDING AND 1 PRECEDING
                   ), 0
               ) AS Disponible
    FROM Applications application
    INNER JOIN dbo.Productos locked ON locked.ProductoId = application.ProductoRegaloId
    WHERE application.TipoBeneficio = N'Regalia'
),
Allocation AS
(
    SELECT prior.PromocionId,
           prior.ProductoId,
           CASE WHEN prior.Disponible <= 0 THEN 0
                WHEN prior.UnidadesRegalo <= prior.Disponible THEN prior.UnidadesRegalo
                ELSE CONVERT(INT, prior.Disponible) END AS Permitidas
    FROM Prior prior
)
SELECT application.PromocionId,
       application.PromocionNombre,
       application.ProductoId,
       application.TipoBeneficio,
       application.MontoDescontado,
       CASE WHEN application.TipoBeneficio = N'Regalia' THEN allocation.Permitidas END AS UnidadesRegalo,
       application.ProductoRegaloId,
       application.Prioridad
FROM Applications application
LEFT JOIN Allocation allocation
    ON allocation.PromocionId = application.PromocionId
   AND allocation.ProductoId = application.ProductoId
WHERE application.TipoBeneficio = N'Descuento'
   OR ISNULL(allocation.Permitidas, 0) > 0;
GO

CREATE OR ALTER FUNCTION dbo.fn_Venta_Lineas
(
    @ClienteUsuarioId INT,
    @ItemsJson NVARCHAR(MAX)
)
RETURNS TABLE
AS
RETURN
WITH Cart AS
(
    SELECT source.ProductoId, SUM(source.Cantidad) AS Cantidad
    FROM OPENJSON(@ItemsJson)
         WITH (ProductoId INT '$.productoId', Cantidad INT '$.cantidad') source
    WHERE source.ProductoId > 0 AND ISNULL(source.Cantidad, 0) > 0
    GROUP BY source.ProductoId
),
Aplicadas AS
(
    SELECT * FROM dbo.fn_Promociones_Aplicar(@ClienteUsuarioId, @ItemsJson)
),
Descuentos AS
(
    SELECT discount.ProductoId,
           SUM(discount.MontoDescontado) AS Monto,
           MIN(discount.PromocionId) AS PromocionId,
           MIN(discount.PromocionNombre) AS PromocionNombre
    FROM Aplicadas discount
    WHERE discount.TipoBeneficio = N'Descuento'
    GROUP BY discount.ProductoId
)
SELECT 1 AS Orden,
       product.ProductoId,
       product.Nombre AS ProductoNombre,
       cart.Cantidad,
       product.Precio AS PrecioLista,
       CONVERT(DECIMAL(18,2), ISNULL(discount.Monto, 0)) AS MontoDescuento,
       CONVERT(DECIMAL(18,2),
               CASE WHEN discount.Monto IS NOT NULL AND cart.Cantidad > 0
                    THEN (product.Precio * cart.Cantidad - discount.Monto) / cart.Cantidad
                    ELSE product.Precio END) AS PrecioUnitario,
       CONVERT(BIT, 0) AS EsRegalo,
       discount.PromocionId,
       discount.PromocionNombre
FROM Cart cart
INNER JOIN dbo.Productos product ON product.ProductoId = cart.ProductoId AND product.Activo = 1
LEFT JOIN Descuentos discount ON discount.ProductoId = cart.ProductoId
UNION ALL
SELECT 2,
       gift.ProductoId,
       gift.Nombre,
       SUM(application.UnidadesRegalo),
       gift.Precio,
       CONVERT(DECIMAL(18,2), 0),
       CONVERT(DECIMAL(18,2), 0),
       CONVERT(BIT, 1),
       MIN(application.PromocionId),
       MIN(application.PromocionNombre)
FROM Aplicadas application
INNER JOIN dbo.Productos gift ON gift.ProductoId = application.ProductoRegaloId
WHERE application.TipoBeneficio = N'Regalia'
GROUP BY gift.ProductoId, gift.Nombre, gift.Precio;
GO

IF XACT_STATE()<>1 THROW 59313,N'La transacción 0331 no está disponible.',1;
DECLARE @MigrationSha256 NVARCHAR(128)=N'$(MigrationSha256)';
IF LEN(@MigrationSha256)<>64 OR @MigrationSha256 LIKE N'%[^0-9A-Fa-f]%' THROW 59314,N'SHA-256 inválido para 0331.',1;
INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes)
VALUES(N'0331_promociones_calculo_compartido',N'0331_promociones_calculo_compartido.sql',UPPER(@MigrationSha256),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),
       N'Pruebas post instalación obs. 3: funciones dbo.fn_Promociones_Aplicar y dbo.fn_Venta_Lineas (regla de promociones de la Tienda aplicada a un carrito de productos) para Venta móvil y su cotización. Aditiva.');
COMMIT TRANSACTION;
GO
