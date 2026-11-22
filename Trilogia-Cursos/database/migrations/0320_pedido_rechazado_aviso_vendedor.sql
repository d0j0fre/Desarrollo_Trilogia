SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

/* POST-qa1-01 · CU-073-02 y CU-074-03 (pruebas post instalacion en Azure):
   cuando el Gerente rechaza un pedido retenido, el vendedor no recibe ningun
   aviso y tampoco ve el motivo. La pantalla del Gerente promete «el vendedor
   vera el motivo en su panel», pero:

   1. dbo.sp_Seller_GetMyOrders (cu102) devuelve el motivo siempre vacio (el
      script se escribio cuando Pedidos.MotivoRechazo aun no existia). Mis
      pedidos y el detalle nunca mostraban el motivo.
   2. No existia ningun aviso para el vendedor.

   Esta migracion:
   a. Vuelve a crear sp_Seller_GetMyOrders devolviendo Pedidos.MotivoRechazo
      (mismas columnas y mismo orden: el codigo no cambia).
   b. TR_Pedidos_NotificarRechazoVendedor (AFTER UPDATE en dbo.Pedidos): cuando
      un pedido pasa a Rechazado y tiene vendedor, deja un aviso en
      dbo.NotificacionesUsuario (campana y pagina Notificaciones del sitio) con
      el numero de pedido y el motivo. Va en un disparador para cubrir todos los
      caminos que rechazan (sitio web, app y lo que se agregue despues) sin
      modificar el procedimiento de rechazo.

   Aditiva, sin cambios de tablas ni de datos. Rollback en
   0320_pedido_rechazado_aviso_vendedor.rollback.md. */
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL THROW 56320,N'Falta el ledger 0001.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0320_pedido_rechazado_aviso_vendedor' AND Status=N'Applied')
    THROW 56321,N'0320 ya figura aplicada.',1;
IF OBJECT_ID(N'dbo.NotificacionesUsuario',N'U') IS NULL THROW 56322,N'Falta dbo.NotificacionesUsuario (migracion 0221).',1;
IF COL_LENGTH(N'dbo.Pedidos',N'MotivoRechazo') IS NULL OR COL_LENGTH(N'dbo.Pedidos',N'VendedorUsuarioId') IS NULL
    THROW 56323,N'Faltan Pedidos.MotivoRechazo o Pedidos.VendedorUsuarioId (cu098).',1;

BEGIN TRANSACTION;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Seller_GetMyOrders
    @VendedorUsuarioId INT,
    @Top INT = 20
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Limite INT = CASE
        WHEN @Top IS NULL OR @Top <= 0 THEN 20
        ELSE @Top
    END;

    SELECT TOP (@Limite)
        p.PedidoId,
        ISNULL(u.NombreCompleto, N'') AS Cliente,
        p.FechaPedido,
        ISNULL(p.Estado, N'') AS Estado,
        p.Total,
        CAST(ISNULL(p.MotivoRechazo, N'') AS NVARCHAR(500)) AS MotivoRechazo,
        ISNULL(f.NumeroFactura, N'') AS NumeroFactura,
        p.FechaActualizacion
    FROM dbo.Pedidos AS p
    INNER JOIN dbo.Usuarios AS u
        ON u.UsuarioId = p.UsuarioId
    OUTER APPLY (
        SELECT TOP (1)
            fac.NumeroFactura
        FROM dbo.Facturas AS fac
        WHERE fac.PedidoId = p.PedidoId
        ORDER BY fac.FacturaId DESC
    ) AS f
    WHERE p.VendedorUsuarioId = @VendedorUsuarioId
    ORDER BY p.FechaPedido DESC;
END;
GO

CREATE OR ALTER TRIGGER dbo.TR_Pedidos_NotificarRechazoVendedor
ON dbo.Pedidos
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT UPDATE(Estado) RETURN;

    INSERT dbo.NotificacionesUsuario (UsuarioId, Tipo, Titulo, Mensaje, EntidadTipo, EntidadId)
    SELECT i.VendedorUsuarioId,
           N'PedidoRechazado',
           N'Pedido rechazado',
           LEFT(CONCAT(N'El Gerente rechazó el pedido #', i.PedidoId,
                       N' de ', ISNULL(u.NombreCompleto, N'su cliente'),
                       N'. Motivo: ', ISNULL(NULLIF(LTRIM(RTRIM(i.MotivoRechazo)), N''), N'sin motivo registrado'), N'.'), 400),
           N'Pedido',
           i.PedidoId
    FROM inserted i
    INNER JOIN deleted d ON d.PedidoId = i.PedidoId
    LEFT JOIN dbo.Usuarios u ON u.UsuarioId = i.UsuarioId
    WHERE i.Estado = N'Rechazado'
      AND ISNULL(d.Estado, N'') <> N'Rechazado'
      AND i.VendedorUsuarioId IS NOT NULL;
END;
GO

IF XACT_STATE()<>1 THROW 56324,N'La transacción 0320 no está disponible.',1;
DECLARE @MigrationSha256 NVARCHAR(128)=N'$(MigrationSha256)';
IF LEN(@MigrationSha256)<>64 OR @MigrationSha256 LIKE N'%[^0-9A-Fa-f]%' THROW 56325,N'SHA-256 inválido para 0320.',1;
INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes)
VALUES(N'0320_pedido_rechazado_aviso_vendedor',N'0320_pedido_rechazado_aviso_vendedor.sql',UPPER(@MigrationSha256),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),
       N'POST-qa1-01: sp_Seller_GetMyOrders devuelve el motivo de rechazo y TR_Pedidos_NotificarRechazoVendedor avisa al vendedor. Aditiva.');
COMMIT TRANSACTION;
GO
