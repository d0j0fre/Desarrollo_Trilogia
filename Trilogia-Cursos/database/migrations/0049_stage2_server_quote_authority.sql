-- Durable seller quotes. Installs no identities, grants, products or assignments.
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF SESSIONPROPERTY('QUOTED_IDENTIFIER')<>1 OR SESSIONPROPERTY('ANSI_NULLS')<>1 THROW 58800,N'SQL options not accredited.',1;
DECLARE @Hash nvarchar(128)=N'$(MigrationSha256)';
IF LEN(@Hash)<>64 OR @Hash LIKE N'%[^0-9a-fA-F]%' THROW 58800,N'Invalid migration hash.',1;
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL THROW 58800,N'Migration ledger missing.',1;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @Lock int;
 EXEC @Lock=sys.sp_getapplock @Resource=N'Stage2:0049:install',@LockMode=N'Exclusive',@LockOwner=N'Transaction',@LockTimeout=10000;
 IF @Lock<0 THROW 58800,N'Concurrent installation.',1;
 IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0049_stage2_server_quote_authority')
 BEGIN
  IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0049_stage2_server_quote_authority' AND FileSha256=UPPER(@Hash) AND Status=N'Applied') THROW 58800,N'Ledger conflict.',1;
  COMMIT; RETURN;
 END;
 IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId LIKE N'0049%') THROW 58800,N'Migration identifier collision.',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0047_stage2_commercial_relationships' AND FileSha256=N'9BC4DCB6D8F52B92AF666885F90BEF48ED07D178D15881F4C821889D9F60B50E' AND Status=N'Applied') THROW 58800,N'Relationship prerequisite not accredited.',1;
 IF SCHEMA_ID(N'CommerceQuotes') IS NOT NULL THROW 58800,N'Existing quote schema requires reconciliation.',1;
 IF EXISTS(SELECT 1 FROM (VALUES(N'Productos',N'Precio'),(N'Productos',N'Activo'),(N'Promociones',N'Prioridad'),(N'Usuarios',N'SegmentoCliente')) x(t,c) WHERE COL_LENGTH(N'dbo.'+x.t,x.c) IS NULL) THROW 58800,N'Canonical pricing source missing.',1;
 EXEC(N'CREATE SCHEMA CommerceQuotes AUTHORIZATION dbo');
 EXEC(N'CREATE TABLE CommerceQuotes.SourceRevision(Singleton tinyint NOT NULL PRIMARY KEY,Revision bigint NOT NULL,
 CONSTRAINT CK_CommerceSourceRevision CHECK(Singleton=1 AND Revision>0)); INSERT CommerceQuotes.SourceRevision VALUES(1,1);');
 EXEC(N'CREATE TRIGGER dbo.tr_CommerceQuote_ProductRevision ON dbo.Productos AFTER INSERT,UPDATE,DELETE AS BEGIN SET NOCOUNT ON;
 IF NOT EXISTS(SELECT 1 FROM inserted) AND NOT EXISTS(SELECT 1 FROM deleted) RETURN;
 IF EXISTS(SELECT 1 FROM inserted) AND EXISTS(SELECT 1 FROM deleted) AND NOT(UPDATE(Precio) OR UPDATE(Activo) OR UPDATE(Nombre)) RETURN;
 UPDATE CommerceQuotes.SourceRevision SET Revision=Revision+1 WHERE Singleton=1; END;');
 EXEC(N'CREATE TRIGGER dbo.tr_CommerceQuote_PromotionRevision ON dbo.Promociones AFTER INSERT,UPDATE,DELETE AS BEGIN SET NOCOUNT ON;
 IF NOT EXISTS(SELECT 1 FROM inserted) AND NOT EXISTS(SELECT 1 FROM deleted) RETURN;
 UPDATE CommerceQuotes.SourceRevision SET Revision=Revision+1 WHERE Singleton=1; END;');
 EXEC(N'CREATE TRIGGER dbo.tr_CommerceQuote_SegmentRevision ON dbo.Usuarios AFTER UPDATE AS BEGIN SET NOCOUNT ON;
 IF UPDATE(SegmentoCliente) AND EXISTS(SELECT 1 FROM inserted) UPDATE CommerceQuotes.SourceRevision SET Revision=Revision+1 WHERE Singleton=1; END;');
 EXEC(N'CREATE TABLE CommerceQuotes.Owners(
  RequestId uniqueidentifier NOT NULL CONSTRAINT PK_CommerceQuoteOwner PRIMARY KEY,
  OriginalLoginId uniqueidentifier NOT NULL REFERENCES dbo.Stage2OriginalLogins(LoginId),
  Actor int NOT NULL REFERENCES dbo.Usuarios(UsuarioId),ProfileId int NOT NULL REFERENCES dbo.Perfiles(PerfilId),
  SecurityStamp uniqueidentifier NOT NULL,BindingHash binary(32) NOT NULL,
  Customer int NOT NULL REFERENCES dbo.Usuarios(UsuarioId),CustomerRelationshipRevision bigint NOT NULL,
  Fingerprint binary(32) NOT NULL,CanonicalIntent nvarchar(max) NOT NULL,
  SourceHash binary(32) NOT NULL,SourceJson nvarchar(max) NOT NULL,Segment nvarchar(20) NOT NULL,
  EvaluationDate date NOT NULL,CreatedUtc datetime2(7) NOT NULL,ExpiresUtc datetime2(7) NOT NULL,
  CONSTRAINT CK_CommerceQuoteOwnerLifetime CHECK(ExpiresUtc>CreatedUtc),
  CONSTRAINT CK_CommerceQuoteOwnerRelation CHECK(CustomerRelationshipRevision>0 AND Actor<>Customer),
  CONSTRAINT CK_CommerceQuoteOwnerJson CHECK(ISJSON(CanonicalIntent)=1 AND ISJSON(SourceJson)=1),
  CONSTRAINT CK_CommerceQuoteOwnerSegment CHECK(Segment IN(N''Minorista'',N''Mayorista''))
 ); CREATE INDEX IX_CommerceQuoteOwnerLogin ON CommerceQuotes.Owners(OriginalLoginId,RequestId);
 CREATE TABLE CommerceQuotes.Quotes(
  RequestId uniqueidentifier NOT NULL CONSTRAINT PK_CommerceQuote PRIMARY KEY REFERENCES CommerceQuotes.Owners(RequestId),
  Contract varchar(80) NOT NULL,CreatedUtc datetime2(7) NOT NULL,
  Gross decimal(18,2) NOT NULL,Discount decimal(18,2) NOT NULL,Net decimal(18,2) NOT NULL,
  Tax decimal(18,2) NOT NULL,FinalTotal decimal(18,2) NOT NULL,LinesHash binary(32) NOT NULL,
  CONSTRAINT CK_CommerceQuoteContract CHECK(Contract=''PromotionEngine+0012-aggregate-tax-v1''),
  CONSTRAINT CK_CommerceQuoteMoney CHECK(Gross>=0 AND Discount>=0 AND Discount<=Gross AND Net=Gross-Discount AND Tax=ROUND(Net*CONVERT(decimal(3,2),0.13),2) AND FinalTotal=Net+Tax)
 );
 CREATE TABLE CommerceQuotes.Lines(
  RequestId uniqueidentifier NOT NULL REFERENCES CommerceQuotes.Quotes(RequestId),
  Product int NOT NULL REFERENCES dbo.Productos(ProductoId),Quantity int NOT NULL,ProductName nvarchar(150) NOT NULL,
  Price decimal(18,2) NOT NULL,Discount decimal(18,2) NOT NULL,
  CONSTRAINT PK_CommerceQuoteLine PRIMARY KEY(RequestId,Product),
  CONSTRAINT CK_CommerceQuoteLine CHECK(Quantity BETWEEN 1 AND 9999 AND Price>=0 AND Discount>=0 AND Discount<=CONVERT(decimal(10,0),Quantity)*Price)
 );');
 EXEC(N'CREATE TRIGGER CommerceQuotes.OwnerImmutable ON CommerceQuotes.Owners AFTER UPDATE,DELETE AS BEGIN SET NOCOUNT ON; THROW 58800,N''Immutable quote owner.'',1; END;');
 EXEC(N'CREATE TRIGGER CommerceQuotes.QuoteImmutable ON CommerceQuotes.Quotes AFTER UPDATE,DELETE AS BEGIN SET NOCOUNT ON; THROW 58800,N''Immutable quote.'',1; END;');
 EXEC(N'CREATE TRIGGER CommerceQuotes.LinesImmutable ON CommerceQuotes.Lines AFTER UPDATE,DELETE AS BEGIN SET NOCOUNT ON; THROW 58800,N''Immutable quote lines.'',1; END;');
 EXEC(N'CREATE PROCEDURE CommerceQuotes.CaptureSource
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
 JSON_QUERY((SELECT pr.PromocionId,pr.Nombre,pr.Tipo,pr.ProductoId,pr.CantidadMinima,pr.PorcentajeDescuento,pr.ProductoRegaloId,g.Nombre AS ProductoRegaloNombre,
 ISNULL(g.Precio,0) AS ProductoRegaloPrecio,CASE WHEN g.Activo=1 THEN ISNULL(g.Stock,0) ELSE 0 END AS ProductoRegaloStock,
 pr.CantidadRegalo,pr.Prioridad,pr.SegmentoCliente,pr.FechaInicio,pr.FechaFin
 FROM dbo.Promociones pr WITH(UPDLOCK,HOLDLOCK) LEFT JOIN dbo.Productos g WITH(UPDLOCK,HOLDLOCK) ON g.ProductoId=pr.ProductoRegaloId
 WHERE pr.Estado=N''Activa'' AND pr.ProductoId IN(SELECT Product FROM @Items) AND pr.SegmentoCliente IN(N''Todos'',@Segment)
 ORDER BY pr.Prioridad DESC,pr.PromocionId FOR JSON PATH)) AS promotions FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
 DECLARE @Revision bigint=(SELECT Revision FROM CommerceQuotes.SourceRevision WITH(UPDLOCK,HOLDLOCK) WHERE Singleton=1);
 IF @Revision IS NULL THROW 58800,N''Pricing revision missing.'',1;
 SET @Source=JSON_MODIFY(@Source,''$.revision'',@Revision);
 END;');
 EXEC(N'CREATE PROCEDURE CommerceQuotes.RequireOwner
 @LoginId uniqueidentifier,@UsuarioId int,@PerfilId int,@SecurityStamp uniqueidentifier,@BindingHash binary(32),@IssuedUtc datetime2(7),@ExpiresUtc datetime2(7),
 @RequestId uniqueidentifier,@Fingerprint binary(32)=NULL
 AS BEGIN SET NOCOUNT ON;
 IF @@TRANCOUNT=0 THROW 58800,N''Quote transaction required.'',1;
 EXEC dbo.sp_Stage2_RequireOriginalLogin @LoginId,@UsuarioId,@PerfilId,@SecurityStamp,@BindingHash,@IssuedUtc,@ExpiresUtc,N''VENTA_MOVIL_CREAR'';
 DECLARE @Customer int,@Revision bigint;
 SELECT @Customer=Customer,@Revision=CustomerRelationshipRevision FROM CommerceQuotes.Owners WITH(UPDLOCK,HOLDLOCK)
 WHERE RequestId=@RequestId AND OriginalLoginId=@LoginId AND Actor=@UsuarioId AND ProfileId=@PerfilId AND SecurityStamp=@SecurityStamp AND BindingHash=@BindingHash
 AND (@Fingerprint IS NULL OR Fingerprint=@Fingerprint) AND ExpiresUtc>SYSUTCDATETIME();
 IF @Customer IS NULL THROW 58803,N''Quote context unavailable.'',1;
 DECLARE @Actual bigint; EXEC CommercialSecurity.RequireAssignment ''SC'',@UsuarioId,@Customer,@Revision,@Actual OUTPUT;
 END;');
 EXEC(N'CREATE PROCEDURE CommerceQuotes.Capture
 @LoginId uniqueidentifier,@UsuarioId int,@PerfilId int,@SecurityStamp uniqueidentifier,@BindingHash binary(32),@IssuedUtc datetime2(7),@ExpiresUtc datetime2(7),
 @RequestId uniqueidentifier,@Customer int,@Delivery nvarchar(50),@Address nvarchar(255),@Notes nvarchar(255)=NULL,@Identification nvarchar(100)=NULL,@Channel nvarchar(50),@ItemsJson nvarchar(max)
 AS BEGIN SET NOCOUNT ON; SET XACT_ABORT ON;
 IF @RequestId IS NULL OR @RequestId=''00000000-0000-0000-0000-000000000000'' OR @Customer IS NULL OR @Customer<=0
 OR NULLIF(LTRIM(RTRIM(@Delivery)),N'''') IS NULL OR NULLIF(LTRIM(RTRIM(@Address)),N'''') IS NULL OR @Channel IS NULL OR @Channel NOT IN(N''Venta móvil'',N''Venta móvil offline'')
 OR @ItemsJson IS NULL OR ISJSON(@ItemsJson)<>1 OR LEFT(LTRIM(@ItemsJson),1)<>N''['' THROW 58801,N''Invalid quote intent.'',1;
 BEGIN TRY BEGIN TRANSACTION;
 EXEC dbo.sp_Stage2_RequireOriginalLogin @LoginId,@UsuarioId,@PerfilId,@SecurityStamp,@BindingHash,@IssuedUtc,@ExpiresUtc,N''VENTA_MOVIL_CREAR'';
 DECLARE @Resource nvarchar(255)=CONCAT(N''CommerceQuote:'',@RequestId),@Lock int;
 EXEC @Lock=sys.sp_getapplock @Resource=@Resource,@LockMode=''Exclusive'',@LockOwner=''Transaction'',@LockTimeout=5000;
 IF @Lock<0 THROW 58800,N''Quote lock unavailable.'',1;
 DECLARE @Items nvarchar(max)=(SELECT Product AS product,Quantity AS quantity FROM OPENJSON(@ItemsJson) WITH(Product int ''$.product'',Quantity int ''$.quantity'') ORDER BY Product FOR JSON PATH);
 DECLARE @Intent nvarchar(max)=(SELECT @UsuarioId AS actor,@Customer AS customer,LTRIM(RTRIM(@Delivery)) AS delivery,LTRIM(RTRIM(@Address)) AS address,
 NULLIF(LTRIM(RTRIM(@Notes)),N'''') AS notes,NULLIF(LTRIM(RTRIM(@Identification)),N'''') AS identification,@Channel AS channel,JSON_QUERY(@Items) AS items FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER);
 DECLARE @Fingerprint binary(32)=HASHBYTES(''SHA2_256'',CONVERT(varbinary(max),@Intent));
 IF EXISTS(SELECT 1 FROM CommerceQuotes.Owners WITH(UPDLOCK,HOLDLOCK) WHERE RequestId=@RequestId)
  EXEC CommerceQuotes.RequireOwner @LoginId,@UsuarioId,@PerfilId,@SecurityStamp,@BindingHash,@IssuedUtc,@ExpiresUtc,@RequestId,@Fingerprint;
 ELSE BEGIN
  DECLARE @Revision bigint; EXEC CommercialSecurity.RequireAssignment ''SC'',@UsuarioId,@Customer,NULL,@Revision OUTPUT;
  DECLARE @Now datetime2(7)=SYSUTCDATETIME(),@Day date=CONVERT(date,SYSUTCDATETIME() AT TIME ZONE ''UTC'' AT TIME ZONE ''Central America Standard Time''),@Source nvarchar(max),@Segment nvarchar(20),@Until datetime2(7);
  SET @Until=CONVERT(datetime2(7),(CONVERT(datetime2(7),DATEADD(day,1,@Day)) AT TIME ZONE ''Central America Standard Time'') AT TIME ZONE ''UTC'');
  IF @ExpiresUtc<@Until SET @Until=@ExpiresUtc;
  EXEC CommerceQuotes.CaptureSource @Customer,@ItemsJson,@Day,@Source OUTPUT,@Segment OUTPUT;
  INSERT CommerceQuotes.Owners(RequestId,OriginalLoginId,Actor,ProfileId,SecurityStamp,BindingHash,Customer,CustomerRelationshipRevision,Fingerprint,CanonicalIntent,SourceHash,SourceJson,Segment,EvaluationDate,CreatedUtc,ExpiresUtc)
  VALUES(@RequestId,@LoginId,@UsuarioId,@PerfilId,@SecurityStamp,@BindingHash,@Customer,@Revision,@Fingerprint,@Intent,HASHBYTES(''SHA2_256'',CONVERT(varbinary(max),@Source)),@Source,@Segment,@Day,@Now,@Until);
 END;
 EXEC CommerceQuotes.RequireOwner @LoginId,@UsuarioId,@PerfilId,@SecurityStamp,@BindingHash,@IssuedUtc,@ExpiresUtc,@RequestId,@Fingerprint;
 COMMIT;
 SELECT o.Fingerprint,o.SourceHash,o.SourceJson,o.ExpiresUtc,o.CustomerRelationshipRevision,CONVERT(bit,CASE WHEN q.RequestId IS NULL THEN 0 ELSE 1 END) AS Published
 FROM CommerceQuotes.Owners o LEFT JOIN CommerceQuotes.Quotes q ON q.RequestId=o.RequestId WHERE o.RequestId=@RequestId;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH; END;');
 EXEC(N'CREATE PROCEDURE CommerceQuotes.Publish
 @LoginId uniqueidentifier,@UsuarioId int,@PerfilId int,@SecurityStamp uniqueidentifier,@BindingHash binary(32),@IssuedUtc datetime2(7),@ExpiresUtc datetime2(7),
 @RequestId uniqueidentifier,@Fingerprint binary(32),@SourceHash binary(32),@Lines nvarchar(max)
 AS BEGIN SET NOCOUNT ON; SET XACT_ABORT ON;
 IF @Fingerprint IS NULL OR @SourceHash IS NULL OR @Lines IS NULL OR ISJSON(@Lines)<>1 OR LEFT(LTRIM(@Lines),1)<>N''['' THROW 58801,N''Invalid quote lines.'',1;
 BEGIN TRY BEGIN TRANSACTION;
 EXEC CommerceQuotes.RequireOwner @LoginId,@UsuarioId,@PerfilId,@SecurityStamp,@BindingHash,@IssuedUtc,@ExpiresUtc,@RequestId,@Fingerprint;
 DECLARE @OwnerHash binary(32),@Source nvarchar(max),@Intent nvarchar(max),@Customer int,@Day date,@Current nvarchar(max),@Segment nvarchar(20),@Items nvarchar(max);
 SELECT @OwnerHash=SourceHash,@Source=SourceJson,@Intent=CanonicalIntent,@Customer=Customer,@Day=EvaluationDate FROM CommerceQuotes.Owners WITH(UPDLOCK,HOLDLOCK) WHERE RequestId=@RequestId;
 IF @SourceHash IS NULL OR @OwnerHash<>@SourceHash THROW 58803,N''Quote source conflict.'',1;
 DECLARE @Rows TABLE(Product int NOT NULL PRIMARY KEY,Quantity int NOT NULL,Price decimal(18,2) NOT NULL,Discount decimal(18,2) NOT NULL);
 IF (SELECT COUNT(*) FROM OPENJSON(@Lines)) NOT BETWEEN 1 AND 100 OR EXISTS(SELECT 1 FROM OPENJSON(@Lines) j WHERE
  TRY_CONVERT(int,JSON_VALUE(j.value,''$.product'')) IS NULL OR TRY_CONVERT(int,JSON_VALUE(j.value,''$.quantity'')) IS NULL
  OR TRY_CONVERT(decimal(18,2),JSON_VALUE(j.value,''$.price'')) IS NULL OR TRY_CONVERT(decimal(18,2),JSON_VALUE(j.value,''$.discount'')) IS NULL
  OR CONVERT(varchar(40),TRY_CONVERT(decimal(18,2),JSON_VALUE(j.value,''$.price''))) COLLATE Latin1_General_100_BIN2<>JSON_VALUE(j.value,''$.price'') COLLATE Latin1_General_100_BIN2
  OR CONVERT(varchar(40),TRY_CONVERT(decimal(18,2),JSON_VALUE(j.value,''$.discount''))) COLLATE Latin1_General_100_BIN2<>JSON_VALUE(j.value,''$.discount'') COLLATE Latin1_General_100_BIN2)
  THROW 58801,N''Noncanonical monetary lines.'',1;
 INSERT @Rows SELECT Product,Quantity,Price,Discount FROM OPENJSON(@Lines) WITH(Product int ''$.product'',Quantity int ''$.quantity'',Price decimal(18,2) ''$.price'',Discount decimal(18,2) ''$.discount'');
 IF EXISTS(SELECT 1 FROM @Rows WHERE Product<=0 OR Quantity NOT BETWEEN 1 AND 9999 OR Price<0 OR Discount<0 OR Discount>CONVERT(decimal(10,0),Quantity)*Price) THROW 58801,N''Invalid monetary lines.'',1;
 DECLARE @Canonical nvarchar(max)=(SELECT Product AS product,Quantity AS quantity,CONVERT(varchar(40),Price) AS price,CONVERT(varchar(40),Discount) AS discount FROM @Rows ORDER BY Product FOR JSON PATH),@LinesHash binary(32);
 SET @LinesHash=HASHBYTES(''SHA2_256'',CONVERT(varbinary(max),@Canonical));
 IF EXISTS(SELECT 1 FROM CommerceQuotes.Quotes WITH(UPDLOCK,HOLDLOCK) WHERE RequestId=@RequestId)
 BEGIN
  IF NOT EXISTS(SELECT 1 FROM CommerceQuotes.Quotes WHERE RequestId=@RequestId AND LinesHash=@LinesHash) THROW 58803,N''Immutable quote conflict.'',1;
  EXEC CommerceQuotes.RequireOwner @LoginId,@UsuarioId,@PerfilId,@SecurityStamp,@BindingHash,@IssuedUtc,@ExpiresUtc,@RequestId,@Fingerprint;
  COMMIT; RETURN;
 END;
 SET @Items=JSON_QUERY(@Intent,''$.items'');
 EXEC CommerceQuotes.CaptureSource @Customer,@Items,@Day,@Current OUTPUT,@Segment OUTPUT;
 IF HASHBYTES(''SHA2_256'',CONVERT(varbinary(max),@Current))<>@OwnerHash THROW 58805,N''Pricing source changed; new intent required.'',1;
 DECLARE @Expected TABLE(Product int PRIMARY KEY,Quantity int,Price decimal(18,2),Discount decimal(18,2),Benefit nvarchar(25));
 INSERT @Expected
 SELECT p.Product,p.Quantity,p.Price,CASE WHEN benefit.Tipo=N''DescuentoPorcentual'' THEN ROUND(CONVERT(decimal(10,0),p.Quantity)*p.Price*benefit.Rate/100,2) ELSE 0 END,benefit.Tipo
 FROM OPENJSON(@Source,''$.products'') WITH(Product int ''$.product'',Quantity int ''$.quantity'',Price decimal(18,2) ''$.price'') p
 OUTER APPLY(SELECT TOP(1) promo.Tipo,promo.Rate FROM OPENJSON(@Source,''$.promotions'') WITH(
  Id int ''$.PromocionId'',Tipo nvarchar(25) ''$.Tipo'',Product int ''$.ProductoId'',MinQuantity int ''$.CantidadMinima'',Rate decimal(5,2) ''$.PorcentajeDescuento'',
  Gift int ''$.ProductoRegaloId'',GiftQuantity int ''$.CantidadRegalo'',GiftStock int ''$.ProductoRegaloStock'',Priority int ''$.Prioridad'',StartDate date ''$.FechaInicio'',EndDate date ''$.FechaFin'') promo
  WHERE promo.Product=p.Product AND p.Quantity>=promo.MinQuantity AND @Day BETWEEN promo.StartDate AND promo.EndDate AND
   ((promo.Tipo=N''DescuentoPorcentual'' AND promo.Rate>0 AND ROUND(CONVERT(decimal(10,0),p.Quantity)*p.Price*promo.Rate/100,2)>0)
    OR (promo.Tipo=N''RegaliaPorVolumen'' AND promo.Gift IS NOT NULL AND promo.GiftQuantity>0 AND promo.GiftStock>0))
  ORDER BY promo.Priority DESC,promo.Id) benefit;
 IF EXISTS(SELECT 1 FROM @Expected WHERE Benefit=N''RegaliaPorVolumen'') THROW 58805,N''Gift validation contract required.'',1;
 IF EXISTS(SELECT Product,Quantity,Price,Discount FROM @Rows EXCEPT SELECT Product,Quantity,Price,Discount FROM @Expected)
  OR EXISTS(SELECT Product,Quantity,Price,Discount FROM @Expected EXCEPT SELECT Product,Quantity,Price,Discount FROM @Rows) THROW 58805,N''PromotionEngine monetary contract differs.'',1;
 DECLARE @Gross decimal(18,2),@Discount decimal(18,2),@Net decimal(18,2),@Tax decimal(18,2),@Final decimal(18,2);
 SELECT @Gross=TRY_CONVERT(decimal(18,2),SUM(CONVERT(decimal(10,0),Quantity)*Price)),@Discount=TRY_CONVERT(decimal(18,2),SUM(Discount)) FROM @Rows;
 IF @Gross IS NULL OR @Discount IS NULL THROW 58801,N''Money range exceeded.'',1;
 SET @Net=@Gross-@Discount; SET @Tax=ROUND(@Net*CONVERT(decimal(3,2),0.13),2); SET @Final=TRY_CONVERT(decimal(18,2),CONVERT(decimal(19,2),@Net)+@Tax);
 IF @Final IS NULL THROW 58801,N''Money range exceeded.'',1;
 INSERT CommerceQuotes.Quotes VALUES(@RequestId,''PromotionEngine+0012-aggregate-tax-v1'',SYSUTCDATETIME(),@Gross,@Discount,@Net,@Tax,@Final,@LinesHash);
 INSERT CommerceQuotes.Lines SELECT @RequestId,r.Product,r.Quantity,p.ProductName,r.Price,r.Discount FROM @Rows r JOIN OPENJSON(@Source,''$.products'') WITH(Product int ''$.product'',ProductName nvarchar(150) ''$.name'') p ON p.Product=r.Product;
 EXEC CommerceQuotes.RequireOwner @LoginId,@UsuarioId,@PerfilId,@SecurityStamp,@BindingHash,@IssuedUtc,@ExpiresUtc,@RequestId,@Fingerprint;
 COMMIT;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH; END;');
 EXEC(N'CREATE PROCEDURE CommerceQuotes.ReadOwn
 @LoginId uniqueidentifier,@UsuarioId int,@PerfilId int,@SecurityStamp uniqueidentifier,@BindingHash binary(32),@IssuedUtc datetime2(7),@ExpiresUtc datetime2(7),
 @RequestId uniqueidentifier,@Fingerprint binary(32)
 AS BEGIN SET NOCOUNT ON; SET XACT_ABORT ON;
 IF @Fingerprint IS NULL THROW 58801,N''Intent fingerprint required.'',1;
 BEGIN TRY BEGIN TRANSACTION;
 EXEC CommerceQuotes.RequireOwner @LoginId,@UsuarioId,@PerfilId,@SecurityStamp,@BindingHash,@IssuedUtc,@ExpiresUtc,@RequestId,@Fingerprint;
 SELECT o.RequestId,o.ExpiresUtc,o.CustomerRelationshipRevision,o.SourceHash,o.Fingerprint,q.Gross,q.Discount,q.Net,q.Tax,q.FinalTotal
 FROM CommerceQuotes.Owners o JOIN CommerceQuotes.Quotes q ON q.RequestId=o.RequestId WHERE o.RequestId=@RequestId;
 SELECT Product,Quantity,Price,Discount,ProductName FROM CommerceQuotes.Lines WHERE RequestId=@RequestId ORDER BY Product;
 EXEC CommerceQuotes.RequireOwner @LoginId,@UsuarioId,@PerfilId,@SecurityStamp,@BindingHash,@IssuedUtc,@ExpiresUtc,@RequestId,@Fingerprint;
 COMMIT; END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH; END;');
 INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes)
 VALUES(N'0049_stage2_server_quote_authority',N'0049_stage2_server_quote_authority.sql',UPPER(@Hash),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),N'Immutable original-session quote ownership and coherent canonical prices/promotions. Exact aggregate-tax contract; no assignments, identities, grants, stock, invoices or orders. Separate authority required.');
 COMMIT;
END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
