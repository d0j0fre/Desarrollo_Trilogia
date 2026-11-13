SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

/* G2-04: una promocion activa cuya fecha de fin ya paso deja de aplicarse en el
   carrito (sp_Promociones_Vigentes filtra por fecha), pero la lista la seguia
   mostrando como Activa, con botones para editar e inactivar, y el filtro
   «Vencida» nunca mostraba nada: ningun proceso asigna ese estado.

   Se reemplaza dbo.sp_Promociones_List (definicion de la 0214) para que calcule
   el estado que se muestra: «Vencida» si la promocion esta Activa y su FechaFin
   es anterior a hoy. El filtro por estado usa ese mismo valor. No cambia datos:
   Promociones.Estado sigue como esta (el CHECK ya admite Vencida) y el carrito
   sigue decidiendo por fecha como antes.

   Solo reemplaza ese procedimiento de lectura. Rollback en
   0257_promociones_vencidas_en_lista.rollback.md. */
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL THROW 56570,N'Falta el ledger 0001.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0257_promociones_vencidas_en_lista' AND Status=N'Applied')
    THROW 56571,N'0257 ya figura aplicada.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0214_promotion_unit_limit' AND Status=N'Applied')
    THROW 56572,N'0257 parte de la versión de sp_Promociones_List de la 0214. Aplicar 0214 primero.',1;

BEGIN TRANSACTION;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Promociones_List
    @Estado NVARCHAR(20)  = NULL,
    @Buscar NVARCHAR(150) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET @Estado = NULLIF(LTRIM(RTRIM(@Estado)), N'');
    SET @Buscar = NULLIF(LTRIM(RTRIM(@Buscar)), N'');
    DECLARE @Hoy DATE = CAST(SYSDATETIME() AS DATE);

    SELECT
        pr.PromocionId, pr.Nombre, pr.Tipo, pr.SegmentoCliente, e.Estado, pr.FechaInicio, pr.FechaFin,
        pr.CantidadMinima, pr.PorcentajeDescuento, pr.CantidadRegalo, pr.Prioridad,
        p.Nombre AS ProductoNombre, pg.Nombre AS ProductoRegaloNombre,
        CASE WHEN pr.Estado = N'Activa' AND @Hoy BETWEEN pr.FechaInicio AND pr.FechaFin THEN 1 ELSE 0 END AS Vigente,
        pr.LimiteUnidades, pr.UnidadesConsumidas
    FROM dbo.Promociones pr
    INNER JOIN dbo.Productos p ON p.ProductoId = pr.ProductoId
    LEFT JOIN dbo.Productos pg ON pg.ProductoId = pr.ProductoRegaloId
    -- 0257 (G2-04): una promoción activa con la fecha de fin vencida se muestra y se filtra como Vencida.
    CROSS APPLY (SELECT CASE WHEN pr.Estado = N'Activa' AND pr.FechaFin < @Hoy THEN N'Vencida' ELSE pr.Estado END AS Estado) e
    WHERE (@Estado IS NULL OR e.Estado = @Estado)
      AND (@Buscar IS NULL OR pr.Nombre LIKE N'%' + @Buscar + N'%' OR p.Nombre LIKE N'%' + @Buscar + N'%')
    ORDER BY e.Estado, pr.Prioridad DESC, pr.FechaFin;
END;
GO

IF XACT_STATE()<>1 THROW 56573,N'La transacción 0257 no está disponible.',1;
DECLARE @MigrationSha256 NVARCHAR(128)=N'$(MigrationSha256)';
IF LEN(@MigrationSha256)<>64 OR @MigrationSha256 LIKE N'%[^0-9A-Fa-f]%' THROW 56574,N'SHA-256 inválido para 0257.',1;
INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes)
VALUES(N'0257_promociones_vencidas_en_lista',N'0257_promociones_vencidas_en_lista.sql',UPPER(@MigrationSha256),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),
       N'G2-04: sp_Promociones_List muestra y filtra como Vencida la promoción activa con fecha de fin pasada. Solo lectura; no cambia datos.');
COMMIT TRANSACTION;
GO
