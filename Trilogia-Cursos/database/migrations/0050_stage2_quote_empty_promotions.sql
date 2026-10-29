-- Correct empty-array serialization observed on the accredited SQL copy.
SET NOCOUNT ON;
SET XACT_ABORT ON;
DECLARE @Hash nvarchar(128)=N'$(MigrationSha256)';
IF LEN(@Hash)<>64 OR @Hash LIKE N'%[^0-9a-fA-F]%' THROW 58810,N'Invalid migration hash.',1;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @Lock int;
 EXEC @Lock=sys.sp_getapplock @Resource=N'Stage2:0050:install',@LockMode=N'Exclusive',@LockOwner=N'Transaction',@LockTimeout=10000;
 IF @Lock<0 THROW 58810,N'Concurrent installation.',1;
 IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0050_stage2_quote_empty_promotions')
 BEGIN
  IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0050_stage2_quote_empty_promotions' AND FileSha256=UPPER(@Hash) AND Status=N'Applied') THROW 58810,N'Ledger conflict.',1;
  COMMIT; RETURN;
 END;
 IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId LIKE N'0050%') THROW 58810,N'Migration identifier collision.',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0049_stage2_server_quote_authority' AND FileSha256=N'F8E08FCEAB1E8ED72DFDC30CF8F02DACEBD4303E3ECF292B72FB74F0520CEF7E' AND Status=N'Applied') THROW 58810,N'Quote prerequisite missing.',1;
 IF ISNULL(HASHBYTES('SHA2_256',CONVERT(varbinary(max),OBJECT_DEFINITION(OBJECT_ID(N'CommerceQuotes.CaptureSource')))),0x)<>0xB4944FB741360A8C53083DF1EF2E5D55969CE1269B194C025B6693999576A9AD THROW 58810,N'Original source module differs.',1;
 EXEC(N'CREATE OR ALTER PROCEDURE CommerceQuotes.CaptureSource
 @Customer int,@ItemsJson nvarchar(max),@EvaluationDate date,@Source nvarchar(max) OUTPUT,@Segment nvarchar(20) OUTPUT
 AS BEGIN
 SET NOCOUNT ON;
 IF @@TRANCOUNT=0 THROW 58800,N''Pricing transaction required.'',1;
 IF @EvaluationDate IS NULL OR @EvaluationDate<>CONVERT(date,SYSUTCDATETIME() AT TIME ZONE ''UTC'' AT TIME ZONE ''Central America Standard Time'') THROW 58806,N''Pricing day expired.'',1;
 IF @ItemsJson IS NULL OR ISJSON(@ItemsJson)<>1 OR LEFT(LTRIM(@ItemsJson),1)<>N''['' THROW 58801,N''Invalid quantities.'',1;
 DECLARE @Items TABLE(Product int NOT NULL PRIMARY KEY,Quantity int NOT NULL);
 IF (SELECT COUNT(*) FROM OPENJSON(@ItemsJson)) NOT BETWEEN 1 AND 100 OR EXISTS(
 SELECT 1 FROM OPENJSON(@ItemsJson) j WHERE TRY_CONVERT(int,JSON_VALUE(j.value,''$.product'')) IS NULL OR TRY_CONVERT(int,JSON_VALUE(j.value,''$.quantity'')) IS NULL
 OR (SELECT COUNT(*) FROM OPENJSON(j.value))<>2 OR EXISTS(SELECT 1 FROM OPENJSON(j.value) p WHERE p.[key] NOT IN(N''product'',N''quantity'') OR p.type<>2)) THROW 58801,N''Invalid quantities.'',1;
 INSERT @Items SELECT Product,Quantity FROM OPENJSON(@ItemsJson) WITH(Product int ''$.product'',Quantity int ''$.quantity'');
 IF EXISTS(SELECT 1 FROM @Items WHERE Product<=0 OR Quantity NOT BETWEEN 1 AND 9999) THROW 58801,N''Invalid quantities.'',1;
 DECLARE @CustomerStamp uniqueidentifier,@CustomerProfile int;
 SELECT @Segment=u.SegmentoCliente,@CustomerStamp=u.SecurityStamp,@CustomerProfile=u.PerfilId FROM dbo.Usuarios u WITH(UPDLOCK,HOLDLOCK) JOIN dbo.Perfiles p WITH(UPDLOCK,HOLDLOCK) ON p.PerfilId=u.PerfilId
 WHERE u.UsuarioId=@Customer AND u.Activo=1 AND p.Activo=1 AND p.Nombre=N''Cliente'';
 IF @Segment IS NULL OR @Segment NOT IN(N''Minorista'',N''Mayorista'') THROW 58805,N''Customer pricing source unavailable.'',1;
 IF EXISTS(SELECT 1 FROM @Items i LEFT JOIN dbo.Productos p WITH(UPDLOCK,HOLDLOCK) ON p.ProductoId=i.Product WHERE p.ProductoId IS NULL OR p.Activo<>1 OR p.Precio<0) THROW 58805,N''Product pricing source unavailable.'',1;
 SELECT @Source=(SELECT @EvaluationDate AS evaluationDate,@Segment AS segment,@Customer AS customer,@CustomerProfile AS customerProfile,@CustomerStamp AS customerStamp,
 JSON_QUERY((SELECT p.ProductoId AS product,i.Quantity AS quantity,p.Nombre AS name,p.Precio AS price FROM @Items i JOIN dbo.Productos p WITH(UPDLOCK,HOLDLOCK) ON p.ProductoId=i.Product ORDER BY p.ProductoId FOR JSON PATH)) AS products,
 JSON_QUERY(COALESCE(NULLIF((SELECT pr.PromocionId,pr.Nombre,pr.Tipo,pr.ProductoId,pr.CantidadMinima,pr.PorcentajeDescuento,pr.ProductoRegaloId,g.Nombre AS ProductoRegaloNombre,
 ISNULL(g.Precio,0) AS ProductoRegaloPrecio,CASE WHEN g.Activo=1 THEN ISNULL(g.Stock,0) ELSE 0 END AS ProductoRegaloStock,
 pr.CantidadRegalo,pr.Prioridad,pr.SegmentoCliente,pr.FechaInicio,pr.FechaFin
 FROM dbo.Promociones pr WITH(UPDLOCK,HOLDLOCK) LEFT JOIN dbo.Productos g WITH(UPDLOCK,HOLDLOCK) ON g.ProductoId=pr.ProductoRegaloId
 WHERE pr.Estado=N''Activa'' AND pr.ProductoId IN(SELECT Product FROM @Items) AND pr.SegmentoCliente IN(N''Todos'',@Segment)
 ORDER BY pr.Prioridad DESC,pr.PromocionId FOR JSON PATH),N''''),N''[]'')) AS promotions FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
 DECLARE @Revision bigint=(SELECT Revision FROM CommerceQuotes.SourceRevision WITH(UPDLOCK,HOLDLOCK) WHERE Singleton=1);
 IF @Revision IS NULL THROW 58800,N''Pricing revision missing.'',1;
 SET @Source=JSON_MODIFY(@Source,''$.revision'',@Revision);
 SET @Source=JSON_MODIFY(@Source,''$.sourceVersion'',2);
 END;');
 INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes)
 VALUES(N'0050_stage2_quote_empty_promotions',N'0050_stage2_quote_empty_promotions.sql',UPPER(@Hash),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),N'Explicit empty promotions array and source contract version2. Prior incomplete immutable captures remain untouched and must not be adopted. No changes to pricing, stock, identity, assignments or grants.');
 COMMIT;
END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
