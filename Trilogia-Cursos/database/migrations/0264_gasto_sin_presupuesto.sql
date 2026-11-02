SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL OR OBJECT_ID(N'dbo.vw_OperatingExpenseImpact',N'V') IS NULL THROW 56650,N'Faltan dependencias de gastos.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0264_gasto_sin_presupuesto' AND Status=N'Applied') THROW 56651,N'La migración ya fue aplicada.',1;
BEGIN TRANSACTION;
GO
CREATE OR ALTER VIEW dbo.vw_OperatingExpenseImpact AS
SELECT g.GastoId,g.FechaGasto,g.DepartamentoId,d.Nombre Departamento,g.CategoriaId,c.Nombre Categoria,g.Proveedor,g.NumeroDocumento,g.TipoDocumento,g.Descripcion,g.Subtotal,g.Impuesto,g.Total,g.MetodoPago,g.Moneda,g.Estado,g.CreadoPorUsuarioId,g.CreadoPorNombre,g.FechaCreacionUtc,g.MotivoCancelacion,g.ComprobanteNombreOriginal,
 CAST(CASE WHEN g.ComprobanteStorageStatus=N'Ready' THEN 1 ELSE 0 END AS BIT) TieneComprobante,ISNULL(b.MontoPresupuesto,0) MontoPresupuesto,ISNULL(r.GastoReal,0) GastoReal,ISNULL(r.GastoComprometido,0) GastoComprometido,
 CAST(CASE WHEN ISNULL(b.MontoPresupuesto,0)=0 THEN 0 ELSE (ISNULL(r.GastoReal,0)+ISNULL(r.GastoComprometido,0))/b.MontoPresupuesto*100 END AS DECIMAL(9,2)) PorcentajeEjecucion,
 CASE WHEN ISNULL(r.GastoReal,0)+ISNULL(r.GastoComprometido,0)>ISNULL(b.MontoPresupuesto,0) THEN N'Excedido'
 WHEN ISNULL(b.MontoPresupuesto,0)>0 AND (ISNULL(r.GastoReal,0)+ISNULL(r.GastoComprometido,0))/b.MontoPresupuesto*100>=100 THEN N'Excedido'
 WHEN ISNULL(b.MontoPresupuesto,0)>0 AND (ISNULL(r.GastoReal,0)+ISNULL(r.GastoComprometido,0))/b.MontoPresupuesto*100>=90 THEN N'Crítico'
 WHEN ISNULL(b.MontoPresupuesto,0)>0 AND (ISNULL(r.GastoReal,0)+ISNULL(r.GastoComprometido,0))/b.MontoPresupuesto*100>=80 THEN N'Advertencia' ELSE N'Normal' END NivelConsumo
FROM dbo.GastosOperativos g INNER JOIN dbo.DepartamentosOperativos d ON d.DepartamentoId=g.DepartamentoId INNER JOIN dbo.CategoriasGasto c ON c.CategoriaId=g.CategoriaId
OUTER APPLY(SELECT SUM(pd.MontoAsignado) MontoPresupuesto FROM dbo.PresupuestosAnuales p INNER JOIN dbo.PresupuestoDetalles pd ON pd.PresupuestoId=p.PresupuestoId WHERE p.Anio=YEAR(g.FechaGasto) AND p.DepartamentoId=g.DepartamentoId AND pd.CategoriaId=g.CategoriaId AND pd.Mes=MONTH(g.FechaGasto) AND p.Estado=N'Aprobado' AND p.Activo=1)b
OUTER APPLY(SELECT SUM(CASE WHEN x.Estado IN(N'Aprobado',N'Pagado') THEN x.Total ELSE 0 END) GastoReal,SUM(CASE WHEN x.Estado=N'Registrado' THEN x.Total ELSE 0 END) GastoComprometido FROM dbo.GastosOperativos x WHERE YEAR(x.FechaGasto)=YEAR(g.FechaGasto) AND MONTH(x.FechaGasto)=MONTH(g.FechaGasto) AND x.DepartamentoId=g.DepartamentoId AND x.CategoriaId=g.CategoriaId)r;
GO
DECLARE @sha NVARCHAR(128)=N'$(MigrationSha256)';
IF LEN(@sha)<>64 OR @sha LIKE N'%[^0-9A-Fa-f]%' THROW 56652,N'SHA-256 inválido.',1;
INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes)
VALUES(N'0264_gasto_sin_presupuesto',N'0264_gasto_sin_presupuesto.sql',UPPER(@sha),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),N'Clasifica como excedido el gasto comprometido sin presupuesto.');
COMMIT TRANSACTION;
GO
