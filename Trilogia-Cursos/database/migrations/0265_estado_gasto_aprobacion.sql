SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL OR OBJECT_ID(N'dbo.vw_OperatingExpenseImpact',N'V') IS NULL THROW 56660,N'Faltan dependencias de gastos.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0265_estado_gasto_aprobacion' AND Status=N'Applied') THROW 56661,N'La migración ya fue aplicada.',1;
BEGIN TRANSACTION;
IF EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.GastosOperativos') AND name=N'CK_GastosOperativos_Estado') ALTER TABLE dbo.GastosOperativos DROP CONSTRAINT CK_GastosOperativos_Estado;
ALTER TABLE dbo.GastosOperativos ADD CONSTRAINT CK_GastosOperativos_Estado CHECK(Estado IS NULL OR Estado IN(N'Registrado',N'Requiere Aprobación',N'Aprobado',N'Rechazado',N'Pagado',N'Anulado'));
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
OUTER APPLY(SELECT SUM(CASE WHEN x.Estado IN(N'Aprobado',N'Pagado') THEN x.Total ELSE 0 END) GastoReal,SUM(CASE WHEN x.Estado IN(N'Registrado',N'Requiere Aprobación') THEN x.Total ELSE 0 END) GastoComprometido FROM dbo.GastosOperativos x WHERE YEAR(x.FechaGasto)=YEAR(g.FechaGasto) AND MONTH(x.FechaGasto)=MONTH(g.FechaGasto) AND x.DepartamentoId=g.DepartamentoId AND x.CategoriaId=g.CategoriaId)r;
GO
CREATE OR ALTER PROCEDURE dbo.sp_OperatingExpense_List @Busqueda NVARCHAR(180)=NULL,@Desde DATE=NULL,@Hasta DATE=NULL,@DepartamentoId INT=NULL,@CategoriaId INT=NULL,@Estado NVARCHAR(30)=NULL,@Pagina INT=1,@TamanoPagina INT=20 AS
BEGIN
 SET NOCOUNT ON; SET @Pagina=CASE WHEN @Pagina<1 THEN 1 ELSE @Pagina END;SET @TamanoPagina=CASE WHEN @TamanoPagina<1 THEN 20 WHEN @TamanoPagina>100 THEN 100 ELSE @TamanoPagina END;
 ;WITH q AS(SELECT *,COUNT(*)OVER()TotalResultados FROM dbo.vw_OperatingExpenseImpact WHERE(@Busqueda IS NULL OR NumeroDocumento LIKE N'%'+@Busqueda+N'%' OR Proveedor LIKE N'%'+@Busqueda+N'%' OR Descripcion LIKE N'%'+@Busqueda+N'%')AND(@Desde IS NULL OR FechaGasto>=@Desde)AND(@Hasta IS NULL OR FechaGasto<=@Hasta)AND(@DepartamentoId IS NULL OR DepartamentoId=@DepartamentoId)AND(@CategoriaId IS NULL OR CategoriaId=@CategoriaId)AND(@Estado IS NULL OR Estado=@Estado))
 SELECT * FROM q ORDER BY FechaGasto DESC,GastoId DESC OFFSET(@Pagina-1)*@TamanoPagina ROWS FETCH NEXT @TamanoPagina ROWS ONLY;
 DECLARE @presupuesto DECIMAL(18,2)=(SELECT ISNULL(SUM(pd.MontoAsignado),0)FROM dbo.PresupuestosAnuales p JOIN dbo.PresupuestoDetalles pd ON pd.PresupuestoId=p.PresupuestoId WHERE p.Estado=N'Aprobado' AND p.Activo=1 AND(@DepartamentoId IS NULL OR p.DepartamentoId=@DepartamentoId)AND(@CategoriaId IS NULL OR pd.CategoriaId=@CategoriaId)AND(@Desde IS NULL OR DATEFROMPARTS(p.Anio,pd.Mes,1)>=DATEFROMPARTS(YEAR(@Desde),MONTH(@Desde),1))AND(@Hasta IS NULL OR DATEFROMPARTS(p.Anio,pd.Mes,1)<=EOMONTH(@Hasta)));
 DECLARE @real DECIMAL(18,2)=(SELECT ISNULL(SUM(Total),0)FROM dbo.GastosOperativos WHERE Estado IN(N'Aprobado',N'Pagado')AND(@Desde IS NULL OR FechaGasto>=@Desde)AND(@Hasta IS NULL OR FechaGasto<=@Hasta)AND(@DepartamentoId IS NULL OR DepartamentoId=@DepartamentoId)AND(@CategoriaId IS NULL OR CategoriaId=@CategoriaId));
 SELECT ISNULL(SUM(CASE WHEN Estado IN(N'Registrado',N'Requiere Aprobación') THEN Total ELSE 0 END),0),ISNULL(SUM(CASE WHEN Estado=N'Aprobado' THEN Total ELSE 0 END),0),ISNULL(SUM(CASE WHEN Estado=N'Pagado' THEN Total ELSE 0 END),0),ISNULL(SUM(CASE WHEN Estado IN(N'Registrado',N'Requiere Aprobación') THEN Total ELSE 0 END),0),@presupuesto-@real FROM dbo.GastosOperativos WHERE(@Desde IS NULL OR FechaGasto>=@Desde)AND(@Hasta IS NULL OR FechaGasto<=@Hasta)AND(@DepartamentoId IS NULL OR DepartamentoId=@DepartamentoId)AND(@CategoriaId IS NULL OR CategoriaId=@CategoriaId);
END;
GO
CREATE OR ALTER PROCEDURE dbo.sp_OperatingExpense_GetForEdit @GastoId INT AS
BEGIN SET NOCOUNT ON;SELECT GastoId,FechaGasto,DepartamentoId,CategoriaId,Proveedor,NumeroDocumento,TipoDocumento,Descripcion,Subtotal,Impuesto,MetodoPago,TokenOperacion FROM dbo.GastosOperativos WHERE GastoId=@GastoId AND Estado IN(N'Registrado',N'Requiere Aprobación');END;
GO
CREATE OR ALTER PROCEDURE dbo.sp_OperatingExpense_Create @GastoId INT=NULL,@FechaGasto DATE,@FechaNegocio DATE,@DepartamentoId INT,@CategoriaId INT,@Proveedor NVARCHAR(180)=NULL,@NumeroDocumento NVARCHAR(80),@TipoDocumento NVARCHAR(40),@Descripcion NVARCHAR(500),@Subtotal DECIMAL(18,2),@Impuesto DECIMAL(18,2),@MetodoPago NVARCHAR(40),@TokenOperacion UNIQUEIDENTIFIER,@ComprobanteNombreOriginal NVARCHAR(255)=NULL,@ComprobanteStorageKey NVARCHAR(80)=NULL,@ComprobanteMimeType NVARCHAR(100)=NULL,@ComprobanteExtension NVARCHAR(10)=NULL,@ComprobanteTamanoBytes BIGINT=NULL,@ComprobanteHashSha256 CHAR(64)=NULL,@UsuarioId INT,@UsuarioNombre NVARCHAR(150) AS
BEGIN
 SET NOCOUNT ON;SET XACT_ABORT ON;SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
 IF @FechaGasto>@FechaNegocio OR @FechaGasto<DATEADD(YEAR,-2,@FechaNegocio) THROW 54300,N'Fecha de gasto inválida.',1;
 IF @Subtotal<=0 OR @Impuesto<0 THROW 54301,N'Montos de gasto inválidos.',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.DepartamentosOperativos WHERE DepartamentoId=@DepartamentoId AND Activo=1) OR NOT EXISTS(SELECT 1 FROM dbo.CategoriasGasto WHERE CategoriaId=@CategoriaId AND Activo=1) THROW 54312,N'Departamento o categoría inactivo.',1;
 BEGIN TRANSACTION;DECLARE @id INT;
 SELECT @id=GastoId FROM dbo.GastosOperativos WITH(UPDLOCK,HOLDLOCK) WHERE TokenOperacion=@TokenOperacion;
 IF @id IS NOT NULL BEGIN COMMIT;SELECT GastoId,CAST(1 AS BIT)Duplicado,NivelConsumo,PorcentajeEjecucion FROM dbo.vw_OperatingExpenseImpact WHERE GastoId=@id;RETURN;END;
 IF EXISTS(SELECT 1 FROM dbo.GastosOperativos WITH(UPDLOCK,HOLDLOCK) WHERE DepartamentoId=@DepartamentoId AND NumeroDocumento=LTRIM(RTRIM(@NumeroDocumento)) AND ISNULL(Proveedor,N'')=ISNULL(NULLIF(LTRIM(RTRIM(@Proveedor)),N''),N'') AND Estado<>N'Anulado') THROW 54313,N'Existe un gasto activo con el mismo proveedor y número de documento.',1;
 INSERT dbo.GastosOperativos(FechaGasto,DepartamentoId,CategoriaId,Proveedor,NumeroDocumento,TipoDocumento,Descripcion,Subtotal,Impuesto,Total,MetodoPago,Moneda,Estado,TokenOperacion,ComprobanteNombreOriginal,ComprobanteStorageKey,ComprobanteMimeType,ComprobanteExtension,ComprobanteTamanoBytes,ComprobanteHashSha256,ComprobanteStorageStatus,CreadoPorUsuarioId,CreadoPorNombre,ActualizadoPorUsuarioId,ActualizadoPorNombre,FechaCreacionUtc,FechaActualizacionUtc)
 VALUES(@FechaGasto,@DepartamentoId,@CategoriaId,NULLIF(LTRIM(RTRIM(@Proveedor)),N''),LTRIM(RTRIM(@NumeroDocumento)),@TipoDocumento,LTRIM(RTRIM(@Descripcion)),@Subtotal,@Impuesto,ROUND(@Subtotal+@Impuesto,2),@MetodoPago,'CRC',N'Registrado',@TokenOperacion,@ComprobanteNombreOriginal,@ComprobanteStorageKey,@ComprobanteMimeType,@ComprobanteExtension,@ComprobanteTamanoBytes,@ComprobanteHashSha256,CASE WHEN @ComprobanteStorageKey IS NULL THEN NULL ELSE N'Pending' END,@UsuarioId,@UsuarioNombre,@UsuarioId,@UsuarioNombre,SYSUTCDATETIME(),SYSUTCDATETIME());SET @id=CONVERT(INT,SCOPE_IDENTITY());
 UPDATE dbo.GastosOperativos SET Estado=N'Requiere Aprobación' WHERE GastoId=@id AND EXISTS(SELECT 1 FROM dbo.vw_OperatingExpenseImpact WHERE GastoId=@id AND GastoReal+GastoComprometido>MontoPresupuesto);
 INSERT dbo.GastoOperativoAuditoria(GastoId,Accion,UsuarioId,UsuarioNombre,Detalle)VALUES(@id,N'Registrar',@UsuarioId,@UsuarioNombre,N'Gasto creado con token idempotente.');COMMIT;
 SELECT GastoId,CAST(0 AS BIT)Duplicado,NivelConsumo,PorcentajeEjecucion FROM dbo.vw_OperatingExpenseImpact WHERE GastoId=@id;
END;
GO
CREATE OR ALTER PROCEDURE dbo.sp_OperatingExpense_Update @GastoId INT,@FechaGasto DATE,@FechaNegocio DATE,@DepartamentoId INT,@CategoriaId INT,@Proveedor NVARCHAR(180)=NULL,@NumeroDocumento NVARCHAR(80),@TipoDocumento NVARCHAR(40),@Descripcion NVARCHAR(500),@Subtotal DECIMAL(18,2),@Impuesto DECIMAL(18,2),@MetodoPago NVARCHAR(40),@TokenOperacion UNIQUEIDENTIFIER,@ComprobanteNombreOriginal NVARCHAR(255)=NULL,@ComprobanteStorageKey NVARCHAR(80)=NULL,@ComprobanteMimeType NVARCHAR(100)=NULL,@ComprobanteExtension NVARCHAR(10)=NULL,@ComprobanteTamanoBytes BIGINT=NULL,@ComprobanteHashSha256 CHAR(64)=NULL,@UsuarioId INT,@UsuarioNombre NVARCHAR(150) AS
BEGIN
 SET NOCOUNT ON;SET XACT_ABORT ON;BEGIN TRANSACTION;IF @FechaGasto>@FechaNegocio OR @FechaGasto<DATEADD(YEAR,-2,@FechaNegocio) THROW 54300,N'Fecha de gasto inválida.',1;IF @Subtotal<=0 OR @Impuesto<0 THROW 54301,N'Montos de gasto inválidos.',1;IF NOT EXISTS(SELECT 1 FROM dbo.DepartamentosOperativos WHERE DepartamentoId=@DepartamentoId AND Activo=1) OR NOT EXISTS(SELECT 1 FROM dbo.CategoriasGasto WHERE CategoriaId=@CategoriaId AND Activo=1) THROW 54312,N'Departamento o categoría inactivo.',1;UPDATE dbo.GastosOperativos SET FechaGasto=@FechaGasto,DepartamentoId=@DepartamentoId,CategoriaId=@CategoriaId,Proveedor=NULLIF(LTRIM(RTRIM(@Proveedor)),N''),NumeroDocumento=LTRIM(RTRIM(@NumeroDocumento)),TipoDocumento=@TipoDocumento,Descripcion=LTRIM(RTRIM(@Descripcion)),Subtotal=@Subtotal,Impuesto=@Impuesto,Total=ROUND(@Subtotal+@Impuesto,2),MetodoPago=@MetodoPago,ComprobanteNombreOriginal=COALESCE(@ComprobanteNombreOriginal,ComprobanteNombreOriginal),ComprobanteStorageKey=COALESCE(@ComprobanteStorageKey,ComprobanteStorageKey),ComprobanteMimeType=COALESCE(@ComprobanteMimeType,ComprobanteMimeType),ComprobanteExtension=COALESCE(@ComprobanteExtension,ComprobanteExtension),ComprobanteTamanoBytes=COALESCE(@ComprobanteTamanoBytes,ComprobanteTamanoBytes),ComprobanteHashSha256=COALESCE(@ComprobanteHashSha256,ComprobanteHashSha256),ComprobanteStorageStatus=CASE WHEN @ComprobanteStorageKey IS NULL THEN ComprobanteStorageStatus ELSE N'Pending' END,ActualizadoPorUsuarioId=@UsuarioId,ActualizadoPorNombre=@UsuarioNombre,FechaActualizacionUtc=SYSUTCDATETIME() WHERE GastoId=@GastoId AND Estado IN(N'Registrado',N'Requiere Aprobación');
 IF @@ROWCOUNT<>1 THROW 54302,N'Sólo se puede editar un gasto registrado.',1; UPDATE dbo.GastosOperativos SET Estado=CASE WHEN EXISTS(SELECT 1 FROM dbo.vw_OperatingExpenseImpact WHERE GastoId=@GastoId AND GastoReal+GastoComprometido>MontoPresupuesto) THEN N'Requiere Aprobación' ELSE N'Registrado' END WHERE GastoId=@GastoId;INSERT dbo.GastoOperativoAuditoria(GastoId,Accion,UsuarioId,UsuarioNombre,Detalle)VALUES(@GastoId,N'Editar',@UsuarioId,@UsuarioNombre,N'Datos del gasto actualizados.'); COMMIT TRANSACTION;
END;
GO
CREATE OR ALTER PROCEDURE dbo.sp_OperatingExpense_Transition @GastoId INT,@Accion NVARCHAR(20),@Motivo NVARCHAR(500)=NULL,@AutorizarExceso BIT=0,@UsuarioId INT,@UsuarioNombre NVARCHAR(150) AS
BEGIN
 SET NOCOUNT ON;SET XACT_ABORT ON;BEGIN TRANSACTION;DECLARE @estado NVARCHAR(20),@creador INT,@fecha DATE,@dep INT,@cat INT,@total DECIMAL(18,2),@nuevo NVARCHAR(20);SELECT @estado=Estado,@creador=CreadoPorUsuarioId,@fecha=FechaGasto,@dep=DepartamentoId,@cat=CategoriaId,@total=Total FROM dbo.GastosOperativos WITH(UPDLOCK,HOLDLOCK) WHERE GastoId=@GastoId;
 IF @estado IS NULL THROW 54304,N'El gasto no existe.',1;
 IF @Accion=N'Aprobar' BEGIN IF @estado NOT IN(N'Registrado',N'Requiere Aprobación') THROW 54305,N'Transición inválida.',1;IF @creador=@UsuarioId THROW 54306,N'El registrador no puede aprobar su propio gasto.',1;DECLARE @budget DECIMAL(18,2)=(SELECT ISNULL(SUM(pd.MontoAsignado),0)FROM dbo.PresupuestosAnuales p JOIN dbo.PresupuestoDetalles pd ON pd.PresupuestoId=p.PresupuestoId WHERE p.Anio=YEAR(@fecha)AND p.DepartamentoId=@dep AND pd.CategoriaId=@cat AND pd.Mes=MONTH(@fecha)AND p.Estado=N'Aprobado'AND p.Activo=1);DECLARE @spent DECIMAL(18,2)=(SELECT ISNULL(SUM(Total),0)FROM dbo.GastosOperativos WHERE YEAR(FechaGasto)=YEAR(@fecha)AND MONTH(FechaGasto)=MONTH(@fecha)AND DepartamentoId=@dep AND CategoriaId=@cat AND Estado IN(N'Aprobado',N'Pagado'));IF(@budget=0 OR @spent+@total>@budget)AND @AutorizarExceso=0 THROW 54307,N'El gasto excede el presupuesto; se requiere autorización especial.',1;SET @nuevo=N'Aprobado';END
 ELSE IF @Accion=N'Rechazar' BEGIN IF @estado NOT IN(N'Registrado',N'Requiere Aprobación') OR NULLIF(LTRIM(RTRIM(@Motivo)),N'')IS NULL THROW 54308,N'El rechazo requiere motivo y estado registrado.',1;SET @nuevo=N'Rechazado';END
 ELSE IF @Accion=N'Pagar' BEGIN IF @estado<>N'Aprobado' THROW 54309,N'Sólo se paga un gasto aprobado.',1;SET @nuevo=N'Pagado';END
 ELSE IF @Accion=N'Anular' BEGIN IF @estado IN(N'Pagado',N'Anulado') OR NULLIF(LTRIM(RTRIM(@Motivo)),N'')IS NULL THROW 54310,N'No se puede anular o falta el motivo.',1;SET @nuevo=N'Anulado';END ELSE THROW 54311,N'Acción inválida.',1;
 UPDATE dbo.GastosOperativos SET Estado=@nuevo,MotivoCancelacion=CASE WHEN @nuevo IN(N'Rechazado',N'Anulado')THEN @Motivo ELSE MotivoCancelacion END,AnuladoPorUsuarioId=CASE WHEN @nuevo=N'Anulado'THEN @UsuarioId ELSE AnuladoPorUsuarioId END,FechaAnulacionUtc=CASE WHEN @nuevo=N'Anulado'THEN SYSUTCDATETIME() ELSE FechaAnulacionUtc END,ActualizadoPorUsuarioId=@UsuarioId,ActualizadoPorNombre=@UsuarioNombre,FechaActualizacionUtc=SYSUTCDATETIME()WHERE GastoId=@GastoId;INSERT dbo.GastoOperativoAuditoria(GastoId,Accion,UsuarioId,UsuarioNombre,Detalle)VALUES(@GastoId,@Accion,@UsuarioId,@UsuarioNombre,CONCAT(@estado,N' -> ',@nuevo,N'. ',COALESCE(@Motivo,N''),CASE WHEN @AutorizarExceso=1 THEN N' Exceso autorizado.' ELSE N'' END));COMMIT;
END;
GO
CREATE OR ALTER PROCEDURE dbo.sp_BudgetComparison_Dashboard
 @Anio INT,@DepartamentoId INT=NULL,@Mes INT=NULL,@CategoriaId INT=NULL,@EstadoGasto NVARCHAR(30)=NULL,@Pagina INT=1,@TamanoPagina INT=20,@FechaNegocio DATE
AS
BEGIN
 SET NOCOUNT ON;
 IF @Anio NOT BETWEEN 2000 AND 2100 THROW 54400,N'Año inválido.',1;
 SET @Pagina=CASE WHEN @Pagina<1 THEN 1 ELSE @Pagina END;SET @TamanoPagina=CASE WHEN @TamanoPagina<1 THEN 20 WHEN @TamanoPagina>100 THEN 100 ELSE @TamanoPagina END;
 SELECT p.DepartamentoId,pd.CategoriaId,pd.Mes,pd.MontoAsignado INTO #B
 FROM dbo.PresupuestosAnuales p INNER JOIN dbo.PresupuestoDetalles pd ON pd.PresupuestoId=p.PresupuestoId
 WHERE p.Anio=@Anio AND p.Estado=N'Aprobado' AND p.Activo=1 AND(@DepartamentoId IS NULL OR p.DepartamentoId=@DepartamentoId)AND(@Mes IS NULL OR pd.Mes=@Mes)AND(@CategoriaId IS NULL OR pd.CategoriaId=@CategoriaId);
 SELECT * INTO #E FROM dbo.vw_OperatingExpenseImpact WHERE YEAR(FechaGasto)=@Anio AND Estado<>N'Anulado' AND(@DepartamentoId IS NULL OR DepartamentoId=@DepartamentoId)AND(@Mes IS NULL OR MONTH(FechaGasto)=@Mes)AND(@CategoriaId IS NULL OR CategoriaId=@CategoriaId)AND(@EstadoGasto IS NULL OR Estado=@EstadoGasto);
 DECLARE @anual DECIMAL(18,2)=(SELECT ISNULL(SUM(pd.MontoAsignado),0)FROM dbo.PresupuestosAnuales p JOIN dbo.PresupuestoDetalles pd ON pd.PresupuestoId=p.PresupuestoId WHERE p.Anio=@Anio AND p.Estado=N'Aprobado'AND p.Activo=1);
 DECLARE @budget DECIMAL(18,2)=(SELECT ISNULL(SUM(MontoAsignado),0)FROM #B),@registered DECIMAL(18,2)=(SELECT ISNULL(SUM(CASE WHEN Estado IN(N'Registrado',N'Requiere Aprobación')THEN Total ELSE 0 END),0)FROM #E),@approved DECIMAL(18,2)=(SELECT ISNULL(SUM(CASE WHEN Estado=N'Aprobado'THEN Total ELSE 0 END),0)FROM #E),@paid DECIMAL(18,2)=(SELECT ISNULL(SUM(CASE WHEN Estado=N'Pagado'THEN Total ELSE 0 END),0)FROM #E);
 DECLARE @real DECIMAL(18,2)=@approved+@paid,@elapsed INT=CASE WHEN @Anio<YEAR(@FechaNegocio)THEN 12 WHEN @Anio>YEAR(@FechaNegocio)THEN 0 ELSE MONTH(@FechaNegocio)END;
 SELECT @anual AnnualBudget,@budget FilteredBudget,@registered RegisteredExpense,@approved ApprovedExpense,@paid PaidExpense,@registered PendingExpense,@real RealExpense,@budget-@real Available,@real-@budget Variance,
  CAST(CASE WHEN @budget=0 THEN 0 ELSE @real/@budget*100 END AS DECIMAL(9,2))ExecutionPercent,CAST(CASE WHEN @elapsed=0 THEN 0 ELSE @real/@elapsed*12 END AS DECIMAL(18,2))AnnualProjection,
  (SELECT COUNT(*)FROM #E)ExpenseCount,(SELECT COUNT(*)FROM dbo.DepartamentosOperativos d WHERE d.Activo=1 AND(@DepartamentoId IS NULL OR d.DepartamentoId=@DepartamentoId)AND NOT EXISTS(SELECT 1 FROM dbo.PresupuestosAnuales p WHERE p.Anio=@Anio AND p.DepartamentoId=d.DepartamentoId AND p.Estado=N'Aprobado'AND p.Activo=1))DepartmentsWithoutBudget,
  (SELECT COUNT(*)FROM(SELECT c.CategoriaId FROM dbo.CategoriasGasto c OUTER APPLY(SELECT SUM(MontoAsignado)Budget FROM #B WHERE CategoriaId=c.CategoriaId)b OUTER APPLY(SELECT SUM(Total)Real FROM #E WHERE CategoriaId=c.CategoriaId AND Estado IN(N'Aprobado',N'Pagado'))e WHERE c.Activo=1 AND ISNULL(e.Real,0)>ISNULL(b.Budget,0))q)OverBudgetCategories;

 SELECT d.DepartamentoId Id,d.Nombre,ISNULL(b.Budget,0)Budget,ISNULL(e.Pending,0)Pending,ISNULL(e.Real,0)Real,ISNULL(b.Budget,0)-ISNULL(e.Real,0)Available,CAST(CASE WHEN ISNULL(b.Budget,0)=0 THEN 0 ELSE ISNULL(e.Real,0)/b.Budget*100 END AS DECIMAL(9,2))ExecutionPercent
 FROM dbo.DepartamentosOperativos d OUTER APPLY(SELECT SUM(MontoAsignado)Budget FROM #B WHERE DepartamentoId=d.DepartamentoId)b OUTER APPLY(SELECT SUM(CASE WHEN Estado IN(N'Registrado',N'Requiere Aprobación')THEN Total ELSE 0 END)Pending,SUM(CASE WHEN Estado IN(N'Aprobado',N'Pagado')THEN Total ELSE 0 END)Real FROM #E WHERE DepartamentoId=d.DepartamentoId)e
 WHERE d.Activo=1 AND(@DepartamentoId IS NULL OR d.DepartamentoId=@DepartamentoId) ORDER BY d.Nombre;

 SELECT c.CategoriaId Id,c.Nombre,ISNULL(b.Budget,0)Budget,ISNULL(e.Pending,0)Pending,ISNULL(e.Real,0)Real,ISNULL(b.Budget,0)-ISNULL(e.Real,0)Available,CAST(CASE WHEN ISNULL(b.Budget,0)=0 THEN 0 ELSE ISNULL(e.Real,0)/b.Budget*100 END AS DECIMAL(9,2))ExecutionPercent
 FROM dbo.CategoriasGasto c OUTER APPLY(SELECT SUM(MontoAsignado)Budget FROM #B WHERE CategoriaId=c.CategoriaId)b OUTER APPLY(SELECT SUM(CASE WHEN Estado IN(N'Registrado',N'Requiere Aprobación')THEN Total ELSE 0 END)Pending,SUM(CASE WHEN Estado IN(N'Aprobado',N'Pagado')THEN Total ELSE 0 END)Real FROM #E WHERE CategoriaId=c.CategoriaId)e
 WHERE c.Activo=1 AND(@CategoriaId IS NULL OR c.CategoriaId=@CategoriaId) ORDER BY c.Nombre;

 ;WITH meses AS(SELECT Mes FROM(VALUES(1),(2),(3),(4),(5),(6),(7),(8),(9),(10),(11),(12))m(Mes))
 SELECT m.Mes,ISNULL(b.Budget,0)Budget,ISNULL(e.Pending,0)Pending,ISNULL(e.Real,0)Real FROM meses m
 OUTER APPLY(SELECT SUM(MontoAsignado)Budget FROM #B WHERE Mes=m.Mes)b OUTER APPLY(SELECT SUM(CASE WHEN Estado IN(N'Registrado',N'Requiere Aprobación')THEN Total ELSE 0 END)Pending,SUM(CASE WHEN Estado IN(N'Aprobado',N'Pagado')THEN Total ELSE 0 END)Real FROM #E WHERE MONTH(FechaGasto)=m.Mes)e ORDER BY m.Mes;

 ;WITH detalle AS(SELECT *,COUNT(*)OVER()TotalResultados FROM #E)
 SELECT * FROM detalle ORDER BY FechaGasto DESC,GastoId DESC OFFSET(@Pagina-1)*@TamanoPagina ROWS FETCH NEXT @TamanoPagina ROWS ONLY;
 SELECT DepartamentoId,Nombre FROM dbo.DepartamentosOperativos WHERE Activo=1 ORDER BY Nombre;
 SELECT CategoriaId,Nombre FROM dbo.CategoriasGasto WHERE Activo=1 ORDER BY Nombre;
END;
GO
DECLARE @sha NVARCHAR(128)=N'$(MigrationSha256)';
IF LEN(@sha)<>64 OR @sha LIKE N'%[^0-9A-Fa-f]%' THROW 56662,N'SHA-256 inválido.',1;
INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes) VALUES(N'0265_estado_gasto_aprobacion',N'0265_estado_gasto_aprobacion.sql',UPPER(@sha),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),N'Estado persistente para gastos excedidos y sumas de pendientes.');
COMMIT TRANSACTION;
GO
