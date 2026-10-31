SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

/* BUG-16-04 · CU-164-02: Rentabilidad de equipos no aplica la regla de 30
   días ni genera una alerta de baja rentabilidad. Antes se resaltaba todo
   comodato sin compras desde la asignación, aun con 0 días prestado.

   Regla (una sola vez, en dbo.fn_Comodato_Rentabilidad):
     Compras válidas  = pedidos del cliente del comodato (ClienteUsuarioId)
                        desde la fecha de asignación con Estado Aprobado,
                        EnProceso, EnRuta o Entregado.
     Referencia       = fecha de la última compra válida; si no hay ninguna,
                        la fecha de asignación.
     DiasSinCompra    = DATEDIFF(DAY, Referencia, hoy).
     SugerirRetiro    = 1 cuando DiasSinCompra >= @DiasUmbral (30).
   Un comodato con menos de 30 días desde la asignación nunca se marca.

   1. dbo.fn_Comodato_Rentabilidad(@DiasUmbral): la regla.
   2. sp_Comodato_Rentabilidad: lee la función y agrega DiasSinCompra y
      SugerirRetiro AL FINAL (posiciones 12 y 13); las alertas salen primero.
   3. sp_Comodato_GenerarAlertasRentabilidad: deja en la bandeja de avisos
      (dbo.NotificacionesUsuario, 0221) un aviso "ComodatoBajaRentabilidad"
      a cada usuario activo cuyo perfil tiene COMODATOS_CONSULTAR (los que
      consultan el reporte: Administrador, Gerente y Auditor cuando tenga el
      permiso). Un aviso por comodato y por usuario en cada período sin
      compras: si el cliente vuelve a comprar y luego deja de hacerlo otros
      30 días, se genera uno nuevo. Devuelve los comodatos con aviso nuevo
      para que la aplicación los registre en la bitácora de auditoría.

   'EnRuta' se agrega a las compras válidas porque 0220 lo usa como estado
   del pedido mientras va en camino.

   Aditiva. Rollback en 0242_comodato_regla_30_dias.rollback.md. */
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL THROW 56420,N'Falta el ledger 0001.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0242_comodato_regla_30_dias' AND Status=N'Applied')
    THROW 56421,N'0242 ya figura aplicada.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0241_comodato_rentabilidad_ubicacion' AND Status=N'Applied')
    THROW 56422,N'Falta 0241.',1;
IF OBJECT_ID(N'dbo.NotificacionesUsuario',N'U') IS NULL
    THROW 56423,N'Falta dbo.NotificacionesUsuario (0221).',1;

BEGIN TRANSACTION;
GO

CREATE OR ALTER FUNCTION dbo.fn_Comodato_Rentabilidad (@DiasUmbral INT)
RETURNS TABLE
AS
RETURN
    SELECT
        c.ComodatoId, a.CodigoActivo, a.Nombre AS ActivoNombre, a.Tipo AS ActivoTipo,
        c.ClienteNombre, c.ClienteUsuarioId, c.FechaAsignacion,
        DATEDIFF(DAY, c.FechaAsignacion, CAST(SYSDATETIME() AS DATE)) AS DiasEnComodato,
        ISNULL(compras.NumPedidos, 0)   AS NumPedidos,
        ISNULL(compras.TotalComprado, 0) AS TotalComprado,
        compras.UltimaCompra,
        ISNULL(c.Ubicacion, N'') AS Ubicacion,
        ref.DiasSinCompra,
        CAST(CASE WHEN ref.DiasSinCompra >= @DiasUmbral THEN 1 ELSE 0 END AS BIT) AS SugerirRetiro,
        ref.FechaReferencia
    FROM dbo.Comodatos c
    INNER JOIN dbo.Activos a ON a.ActivoId = c.ActivoId
    OUTER APPLY (
        SELECT COUNT(*) AS NumPedidos, SUM(p.Total) AS TotalComprado, MAX(p.FechaPedido) AS UltimaCompra
        FROM dbo.Pedidos p
        WHERE c.ClienteUsuarioId IS NOT NULL
          AND p.UsuarioId = c.ClienteUsuarioId
          AND p.Estado IN (N'Entregado', N'Aprobado', N'EnProceso', N'EnRuta')
          AND p.FechaPedido >= c.FechaAsignacion
    ) compras
    CROSS APPLY (
        SELECT CAST(ISNULL(compras.UltimaCompra, c.FechaAsignacion) AS DATE) AS FechaReferencia,
               DATEDIFF(DAY, CAST(ISNULL(compras.UltimaCompra, c.FechaAsignacion) AS DATE), CAST(SYSDATETIME() AS DATE)) AS DiasSinCompra
    ) ref
    WHERE c.Estado = N'Activo';
GO

CREATE OR ALTER PROCEDURE dbo.sp_Comodato_Rentabilidad
AS
BEGIN
    SET NOCOUNT ON;
    SELECT ComodatoId, CodigoActivo, ActivoNombre, ActivoTipo,
           ClienteNombre, ClienteUsuarioId, FechaAsignacion, DiasEnComodato,
           NumPedidos, TotalComprado, UltimaCompra, Ubicacion,
           DiasSinCompra, SugerirRetiro
    FROM dbo.fn_Comodato_Rentabilidad(30)
    ORDER BY SugerirRetiro DESC, TotalComprado ASC;  -- alertas y menos rentables primero
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Comodato_GenerarAlertasRentabilidad
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Nuevas TABLE (ComodatoId INT NOT NULL);  -- una fila por aviso (varios usuarios por comodato)

    INSERT dbo.NotificacionesUsuario (UsuarioId, Tipo, Titulo, Mensaje, EntidadTipo, EntidadId)
    OUTPUT inserted.EntidadId INTO @Nuevas (ComodatoId)
    SELECT DISTINCT u.UsuarioId,
           N'ComodatoBajaRentabilidad',
           N'Baja rentabilidad: sugerir retiro de equipo',
           LEFT(CONCAT(N'El cliente ', r.ClienteNombre, N' no compra hace ', r.DiasSinCompra,
                       N' días y tiene en comodato el equipo ', r.CodigoActivo, N' (', r.ActivoNombre, N')',
                       CASE WHEN r.Ubicacion <> N'' THEN CONCAT(N' en ', r.Ubicacion) ELSE N'' END,
                       N'. Se sugiere retirar el equipo. Ver Comodatos > Rentabilidad.'), 400),
           N'Comodato',
           r.ComodatoId
    FROM dbo.fn_Comodato_Rentabilidad(30) r
    CROSS JOIN dbo.Usuarios u
    WHERE r.SugerirRetiro = 1
      AND ISNULL(u.Activo, 1) = 1
      AND EXISTS (SELECT 1
                  FROM dbo.PerfilPermisos pp
                  INNER JOIN dbo.Permisos pe ON pe.PermisoId = pp.PermisoId
                  WHERE pp.PerfilId = u.PerfilId AND pe.Codigo = N'COMODATOS_CONSULTAR' AND pe.Activo = 1)
      AND NOT EXISTS (SELECT 1
                      FROM dbo.NotificacionesUsuario n
                      WHERE n.UsuarioId = u.UsuarioId
                        AND n.Tipo = N'ComodatoBajaRentabilidad'
                        AND n.EntidadTipo = N'Comodato'
                        AND n.EntidadId = r.ComodatoId
                        AND n.FechaCreacion >= DATEADD(DAY, 30, CAST(r.FechaReferencia AS DATETIME2(0))));

    SELECT r.ComodatoId, r.CodigoActivo, r.ClienteNombre, r.DiasSinCompra
    FROM dbo.fn_Comodato_Rentabilidad(30) r
    WHERE r.ComodatoId IN (SELECT ComodatoId FROM @Nuevas);
END;
GO

IF XACT_STATE()<>1 THROW 56424,N'La transacción 0242 no está disponible.',1;
DECLARE @MigrationSha256 NVARCHAR(128)=N'$(MigrationSha256)';
IF LEN(@MigrationSha256)<>64 OR @MigrationSha256 LIKE N'%[^0-9A-Fa-f]%' THROW 56425,N'SHA-256 inválido para 0242.',1;
INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes)
VALUES(N'0242_comodato_regla_30_dias',N'0242_comodato_regla_30_dias.sql',UPPER(@MigrationSha256),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),
       N'BUG-16-04: regla de 30 días sin compras (fn_Comodato_Rentabilidad) y avisos de baja rentabilidad.');
COMMIT TRANSACTION;
GO
