SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

/* BUG-10-07 / BUG-10-04: un solo camino para cerrar y liquidar una ruta.

   El botón «Liquidar ruta» del detalle de una ruta Despachada (sp_Rutas_Liquidar)
   devolvía al stock la mercadería no entregada SIN contarla y marcaba la ruta como
   liquidada: saltaba la liquidación de mercadería (0286) y liquidaba algo que no
   había terminado.

   sp_Rutas_Finalizar reemplaza ese botón: una ruta Despachada pasa a Completada;
   las paradas que no se entregaron (Pendiente o EnRuta) quedan Fallido con su
   motivo y su fecha, y los pedidos quedan Fallido («No entregado») con
   sp_Pedidos_SincronizarEstadoEntrega (0220), en la misma transacción. NO toca el
   inventario y NO marca la ruta como liquidada. Después se liquida por las dos
   pantallas de Finanzas: cobros (0285) y mercadería con conteo (0286).

   sp_Rutas_Liquidar se deja como está, sin uso desde la aplicación: no se borra. Si
   alguien lo ejecutara a mano, no puede devolver mercadería dos veces: marca
   Rutas.Liquidada = 1, que sp_Rutas_Liquidar y sp_RutaMercaderia_Registrar rechazan.

   Aditiva. Rollback en 0289_rutas_finalizar.rollback.md. */
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL THROW 56940,N'Falta el ledger 0001.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0289_rutas_finalizar' AND Status=N'Applied')
    THROW 56941,N'0289 ya figura aplicada.',1;
IF OBJECT_ID(N'dbo.Rutas',N'U') IS NULL OR OBJECT_ID(N'dbo.RutaPedidos',N'U') IS NULL OR OBJECT_ID(N'dbo.sp_Pedidos_SincronizarEstadoEntrega',N'P') IS NULL
    THROW 56942,N'Faltan las tablas de rutas o la 0220 (sp_Pedidos_SincronizarEstadoEntrega).',1;

BEGIN TRANSACTION;
GO

/* Resultados: 1) el de sp_Pedidos_SincronizarEstadoEntrega (pedidos actualizados), 2) el resumen. */
CREATE OR ALTER PROCEDURE dbo.sp_Rutas_Finalizar
    @RutaId    INT,
    @UsuarioId INT           = NULL,
    @Nombre    NVARCHAR(150) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Estado NVARCHAR(40), @Codigo NVARCHAR(60), @Msg NVARCHAR(400);
    SELECT @Estado = Estado, @Codigo = Codigo FROM dbo.Rutas WHERE RutaId = @RutaId;

    IF @Estado IS NULL THROW 53440, N'No se encontró la ruta indicada.', 1;
    IF @Estado <> N'Despachada'
    BEGIN
        SET @Msg = CONCAT(N'Solo se puede finalizar una ruta despachada. La ruta ', @Codigo, N' está ', LOWER(@Estado), N'.');
        THROW 53441, @Msg, 1;
    END;

    DECLARE @NoEntregados INT, @Entregados INT;

    BEGIN TRANSACTION;
    BEGIN TRY
        UPDATE dbo.RutaPedidos
        SET EstadoEntrega = N'Fallido',
            MotivoFallo = ISNULL(MotivoFallo, N'No entregado: la ruta se finalizó sin completar esta entrega'),
            FechaEntrega = SYSDATETIME(),
            FechaActualizacion = SYSDATETIME()
        WHERE RutaId = @RutaId AND EstadoEntrega IN (N'Pendiente', N'EnRuta');
        SET @NoEntregados = @@ROWCOUNT;

        UPDATE dbo.Rutas
        SET Estado = N'Completada', FechaCierre = SYSDATETIME(), FechaActualizacion = SYSDATETIME()
        WHERE RutaId = @RutaId;

        EXEC dbo.sp_Pedidos_SincronizarEstadoEntrega @RutaId = @RutaId;

        SELECT @Entregados = COUNT(*) FROM dbo.RutaPedidos WHERE RutaId = @RutaId AND EstadoEntrega = N'Entregado';
        COMMIT;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK;
        THROW;
    END CATCH;

    SELECT @Codigo AS Codigo, @Entregados AS Entregados, @NoEntregados AS NoEntregadosAhora,
           (SELECT COUNT(*) FROM dbo.RutaPedidos WHERE RutaId = @RutaId AND EstadoEntrega <> N'Entregado') AS TotalNoEntregados;
END;
GO

IF XACT_STATE()<>1 THROW 56943,N'La transacción 0289 no está disponible.',1;
DECLARE @MigrationSha256 NVARCHAR(128)=N'$(MigrationSha256)';
IF LEN(@MigrationSha256)<>64 OR @MigrationSha256 LIKE N'%[^0-9A-Fa-f]%' THROW 56944,N'SHA-256 inválido para 0289.',1;
INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes)
VALUES(N'0289_rutas_finalizar',N'0289_rutas_finalizar.sql',UPPER(@MigrationSha256),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),
       N'BUG-10-07: sp_Rutas_Finalizar cierra una ruta despachada como Completada con las paradas no entregadas en Fallido, sin tocar inventario ni liquidar.');
COMMIT TRANSACTION;
GO
