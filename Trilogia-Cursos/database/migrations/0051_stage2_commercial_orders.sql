SET NOCOUNT ON;SET XACT_ABORT ON;
IF SESSIONPROPERTY('QUOTED_IDENTIFIER')<>1 OR SESSIONPROPERTY('ANSI_NULLS')<>1 THROW 58910,N'SQL options unavailable.',1;
DECLARE @Hash nvarchar(128)=N'$(MigrationSha256)';
IF LEN(@Hash)<>64 OR @Hash LIKE N'%[^0-9a-fA-F]%' THROW 58911,N'Invalid migration hash.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0051_stage2_commercial_orders') BEGIN
 IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0051_stage2_commercial_orders' AND FileSha256=UPPER(@Hash) AND Status=N'Applied') THROW 58912,N'Ledger conflict.',1;
 RETURN;END;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId LIKE N'0051%') OR SCHEMA_ID(N'CommercialOrders') IS NOT NULL THROW 58912,N'Identifier or schema collision.',1;
IF (SELECT COUNT(*) FROM dbo.SchemaMigrationHistory WHERE Status=N'Applied' AND
 ((MigrationId=N'0039_stage2_aggregate_amounts_v2' AND FileSha256=N'2CA5A1CC97629FD4AB65083D204DB47B37457894EEC74C0D5B247B1D6321CB96')
 OR (MigrationId=N'0047_stage2_commercial_relationships' AND FileSha256=N'9BC4DCB6D8F52B92AF666885F90BEF48ED07D178D15881F4C821889D9F60B50E')
 OR (MigrationId=N'0049_stage2_server_quote_authority' AND FileSha256=N'F8E08FCEAB1E8ED72DFDC30CF8F02DACEBD4303E3ECF292B72FB74F0520CEF7E')
 OR (MigrationId=N'0050_stage2_quote_empty_promotions' AND FileSha256=N'46616159907485A026785648A690727FECCF50CB0EAB17343A6D692406565BE5')))<>4 THROW 58913,N'Exact prerequisites unavailable.',1;
IF NOT EXISTS(SELECT 1 FROM sys.indexes i JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
 WHERE i.object_id=OBJECT_ID(N'dbo.Facturas') AND i.is_unique=1 AND i.is_disabled=0 AND ic.key_ordinal=1 AND c.name=N'PedidoId'
 AND (SELECT COUNT(*) FROM sys.index_columns k WHERE k.object_id=i.object_id AND k.index_id=i.index_id AND k.key_ordinal>0)=1) THROW 58914,N'Invoice uniqueness unavailable.',1;
BEGIN TRY BEGIN TRANSACTION;
DECLARE @Lock int;EXEC @Lock=sys.sp_getapplock @Resource=N'Stage2:0051:install',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=5000;
IF @Lock<0 THROW 58915,N'Installation conflict.',1;
EXEC(N'CREATE SCHEMA CommercialOrders AUTHORIZATION dbo');
CREATE TABLE CommercialOrders.Orders(RequestId uniqueidentifier NOT NULL PRIMARY KEY REFERENCES CommerceQuotes.Quotes(RequestId),PedidoId int NOT NULL UNIQUE REFERENCES dbo.Pedidos(PedidoId),CreatedUtc datetime2(7) NOT NULL);
CREATE TABLE CommercialOrders.Milestones(PedidoId int NOT NULL REFERENCES CommercialOrders.Orders(PedidoId),State nvarchar(20) NOT NULL,Actor int NOT NULL REFERENCES dbo.Usuarios(UsuarioId),OriginalLoginId uniqueidentifier NOT NULL REFERENCES dbo.Stage2OriginalLogins(LoginId),CreatedUtc datetime2(7) NOT NULL,
 CONSTRAINT PK_CommercialOrderMilestones PRIMARY KEY(PedidoId,State),CONSTRAINT CK_CommercialOrderMilestones_State CHECK(State IN(N'Retenido',N'Liberado',N'Facturado',N'Rechazado')));
CREATE TABLE CommercialOrders.Decisions(OperationId uniqueidentifier NOT NULL PRIMARY KEY,PedidoId int NOT NULL UNIQUE REFERENCES CommercialOrders.Orders(PedidoId),OriginalLoginId uniqueidentifier NOT NULL REFERENCES dbo.Stage2OriginalLogins(LoginId),Actor int NOT NULL REFERENCES dbo.Usuarios(UsuarioId),ProfileId int NOT NULL REFERENCES dbo.Perfiles(PerfilId),SecurityStamp uniqueidentifier NOT NULL,BindingHash binary(32) NOT NULL,SupervisorRevision bigint NOT NULL,Decision nvarchar(20) NOT NULL,Reason nvarchar(500) NULL,CreatedUtc datetime2(7) NOT NULL,
 CONSTRAINT CK_CommercialOrderDecision_Context CHECK(OperationId<>'00000000-0000-0000-0000-000000000000' AND SupervisorRevision>0 AND SecurityStamp<>'00000000-0000-0000-0000-000000000000'),
 CONSTRAINT CK_CommercialOrderDecision_Reason CHECK((Decision=N'Aprobado' AND Reason IS NULL) OR (Decision=N'Rechazado' AND LEN(LTRIM(RTRIM(Reason)))>0 AND Reason IS NOT NULL)));
EXEC(N'CREATE PROCEDURE CommercialOrders.Result
 @PedidoId int AS BEGIN SET NOCOUNT ON;
 IF @@TRANCOUNT=0 THROW 58900,N''Commercial transaction required.'',1;
 SELECT p.PedidoId,p.Estado,ISNULL(f.FacturaId,0) AS FacturaId,ISNULL(f.NumeroFactura,N'''') AS NumeroFactura
 FROM CommercialOrders.Orders link JOIN dbo.Pedidos p ON p.PedidoId=link.PedidoId
 LEFT JOIN dbo.Facturas f ON f.PedidoId=p.PedidoId WHERE link.PedidoId=@PedidoId;
 END;');
EXEC(N'CREATE PROCEDURE CommercialOrders.Finalize
 @PedidoId int,@Actor int,@LoginId uniqueidentifier AS BEGIN SET NOCOUNT ON;
 IF @@TRANCOUNT=0 THROW 58900,N''Commercial transaction required.'',1;
 DECLARE @State nvarchar(30),@Debited bit,@Request uniqueidentifier,@Total decimal(18,2),@Net decimal(18,2),@Tax decimal(18,2),@Customer int;
 SELECT @State=p.Estado,@Debited=p.InventarioDescontado,@Request=link.RequestId,@Customer=p.UsuarioId
 FROM CommercialOrders.Orders link WITH(UPDLOCK,HOLDLOCK) JOIN dbo.Pedidos p WITH(UPDLOCK,HOLDLOCK) ON p.PedidoId=link.PedidoId WHERE link.PedidoId=@PedidoId;
 SELECT @Total=TotalFinal,@Net=Neto,@Tax=Impuesto FROM dbo.Stage2OrderAmounts WITH(HOLDLOCK) WHERE PedidoId=@PedidoId AND ContractVersion=2;
 IF @State IS NULL OR @State<>N''Retenido'' OR @Debited<>0 OR @Total IS NULL OR EXISTS(SELECT 1 FROM dbo.Facturas WITH(UPDLOCK,HOLDLOCK) WHERE PedidoId=@PedidoId)
  THROW 58902,N''Order cannot be finalized.'',1;
 IF @Total>2000000 AND NOT EXISTS(SELECT 1 FROM CommercialOrders.Decisions WITH(HOLDLOCK) WHERE PedidoId=@PedidoId AND Decision=N''Aprobado'' AND Actor=@Actor AND OriginalLoginId=@LoginId)
  THROW 58903,N''Supervisor approval required.'',1;
 IF NOT EXISTS(SELECT 1 FROM CommerceQuotes.Quotes q JOIN dbo.Stage2OrderAmounts a ON a.PedidoId=@PedidoId WHERE q.RequestId=@Request
  AND a.Bruto=q.Gross AND a.Descuento=q.Discount AND a.Neto=q.Net AND a.Impuesto=q.Tax AND a.TotalFinal=q.FinalTotal)
  THROW 58904,N''Monetary snapshot unavailable.'',1;
 DECLARE @Name nvarchar(150),@Email nvarchar(256),@Role nvarchar(100),@CustomerName nvarchar(150),@CustomerEmail nvarchar(150);
 SELECT @Name=u.NombreCompleto,@Email=u.Correo,@Role=p.Nombre FROM dbo.Usuarios u JOIN dbo.Perfiles p ON p.PerfilId=u.PerfilId WHERE u.UsuarioId=@Actor AND u.Activo=1 AND p.Activo=1;
 SELECT @CustomerName=NombreCompleto,@CustomerEmail=Correo FROM dbo.Usuarios WITH(HOLDLOCK) WHERE UsuarioId=@Customer AND Activo=1;
 IF @Name IS NULL OR @CustomerName IS NULL THROW 58904,N''Canonical actors unavailable.'',1;
 DECLARE @Need TABLE(Product int PRIMARY KEY,Quantity int,BeforeStock int,Name nvarchar(150));
 INSERT @Need(Product,Quantity,Name) SELECT Product,Quantity,ProductName FROM CommerceQuotes.Lines WHERE RequestId=@Request;
 IF NOT EXISTS(SELECT 1 FROM @Need) OR EXISTS(SELECT 1 FROM @Need n WHERE n.Quantity<=0) THROW 58904,N''Invalid order lines.'',1;
 DECLARE @Product int,@Stock int;
 DECLARE stock_cursor CURSOR LOCAL FAST_FORWARD FOR SELECT Product FROM @Need ORDER BY Product;
 OPEN stock_cursor; FETCH NEXT FROM stock_cursor INTO @Product;
 WHILE @@FETCH_STATUS=0 BEGIN
  SET @Stock=NULL; SELECT @Stock=Stock FROM dbo.Productos WITH(UPDLOCK,HOLDLOCK) WHERE ProductoId=@Product AND Activo=1;
  IF @Stock IS NULL OR @Stock<(SELECT Quantity FROM @Need WHERE Product=@Product) THROW 58905,N''Current stock unavailable.'',1;
  UPDATE @Need SET BeforeStock=@Stock WHERE Product=@Product; FETCH NEXT FROM stock_cursor INTO @Product;
 END; CLOSE stock_cursor; DEALLOCATE stock_cursor;
 UPDATE p SET Stock=p.Stock-n.Quantity FROM dbo.Productos p JOIN @Need n ON n.Product=p.ProductoId WHERE p.Stock>=n.Quantity;
 IF @@ROWCOUNT<>(SELECT COUNT(*) FROM @Need) THROW 58905,N''Concurrent stock conflict.'',1;
 INSERT dbo.MovimientosInventario(ProductoId,ProductoNombre,TipoMovimiento,Cantidad,StockAnterior,StockNuevo,Motivo,UsuarioId,UsuarioNombre,FechaMovimiento)
 SELECT Product,Name,N''Salida'',Quantity,BeforeStock,BeforeStock-Quantity,CONCAT(N''Pedido comercial #'',@PedidoId),@Actor,@Name,SYSDATETIME() FROM @Need;
 UPDATE dbo.Pedidos SET InventarioDescontado=1,Estado=N''Liberado'',FechaActualizacion=SYSDATETIME() WHERE PedidoId=@PedidoId;
 INSERT CommercialOrders.Milestones(PedidoId,State,Actor,OriginalLoginId,CreatedUtc) VALUES(@PedidoId,N''Liberado'',@Actor,@LoginId,SYSUTCDATETIME());
 INSERT dbo.Facturas(PedidoId,NumeroFactura,UsuarioId,ClienteNombre,ClienteCorreo,Subtotal,Impuesto,Total,Estado)
 VALUES(@PedidoId,CONCAT(N''FAC-S2C-'',@PedidoId),@Customer,@CustomerName,@CustomerEmail,@Net,@Tax,@Total,N''Generada'');
 DECLARE @Invoice int=CONVERT(int,SCOPE_IDENTITY());
 INSERT dbo.FacturaDetalle(FacturaId,ProductoId,ProductoNombre,Cantidad,PrecioUnitario)
 SELECT @Invoice,Product,ProductName,Quantity,Price FROM CommerceQuotes.Lines WHERE RequestId=@Request;
 UPDATE dbo.Pedidos SET Estado=N''Facturado'',FechaActualizacion=SYSDATETIME() WHERE PedidoId=@PedidoId;
 INSERT CommercialOrders.Milestones(PedidoId,State,Actor,OriginalLoginId,CreatedUtc) VALUES(@PedidoId,N''Facturado'',@Actor,@LoginId,SYSUTCDATETIME());
 INSERT dbo.InventarioOperacionAuditoria(Modulo,EntidadId,Accion,UsuarioId,UsuarioNombre,Detalle)
 VALUES(N''Pedidos'',@PedidoId,N''Facturar'',@Actor,@Name,N''Existencias, factura e importes agregados confirmados en una sola transaccion. No acredita entrega fisica.'');
 INSERT dbo.AuditoriaSistema(UsuarioId,UsuarioNombre,UsuarioCorreo,Rol,Accion,Modulo,Descripcion)
 VALUES(@Actor,@Name,@Email,@Role,N''Liberar y facturar'',N''Pedidos'',CONCAT(N''Pedido #'',@PedidoId,N''; factura #'',@Invoice,N''; importes de cotizacion inmutables. No acredita entrega fisica.''));
 END;');
EXEC(N'CREATE PROCEDURE CommercialOrders.Consume
 @LoginId uniqueidentifier,@UsuarioId int,@PerfilId int,@SecurityStamp uniqueidentifier,
 @BindingHash binary(32),@IssuedUtc datetime2(7),@ExpiresUtc datetime2(7),@RequestId uniqueidentifier,@Fingerprint binary(32) AS BEGIN SET NOCOUNT ON;SET XACT_ABORT ON;
 IF @RequestId IS NULL OR @RequestId=''00000000-0000-0000-0000-000000000000'' OR @Fingerprint IS NULL THROW 58901,N''Quoted intent binding required.'',1;
 BEGIN TRY BEGIN TRANSACTION;
 EXEC CommerceQuotes.RequireOwner @LoginId,@UsuarioId,@PerfilId,@SecurityStamp,@BindingHash,@IssuedUtc,@ExpiresUtc,@RequestId,@Fingerprint;
 DECLARE @PedidoId int;
 SELECT @PedidoId=PedidoId FROM CommercialOrders.Orders WITH(UPDLOCK,HOLDLOCK) WHERE RequestId=@RequestId;
 IF @PedidoId IS NOT NULL BEGIN EXEC CommercialOrders.Result @PedidoId; COMMIT; RETURN; END;
 DECLARE @Intent nvarchar(max),@Customer int,@OriginalSource binary(32),@Day date,@CurrentDay date,@Source nvarchar(max),@Segment nvarchar(20),@Items nvarchar(max),@Final decimal(18,2),@Channel nvarchar(50);
 SELECT @Intent=CanonicalIntent,@Customer=Customer,@OriginalSource=SourceHash,@Day=EvaluationDate FROM CommerceQuotes.Owners WITH(UPDLOCK,HOLDLOCK) WHERE RequestId=@RequestId;
 SELECT @Final=FinalTotal FROM CommerceQuotes.Quotes WITH(UPDLOCK,HOLDLOCK) WHERE RequestId=@RequestId;
 IF @Final IS NULL THROW 58904,N''Published server quote required.'',1;
 SET @CurrentDay=CONVERT(date,SYSUTCDATETIME() AT TIME ZONE ''UTC'' AT TIME ZONE ''Central America Standard Time'');
 IF @CurrentDay<>@Day THROW 58904,N''Quote day expired.'',1;
 SET @Items=JSON_QUERY(@Intent,''$.items'');SET @Channel=JSON_VALUE(@Intent,''$.channel'');
 EXEC CommerceQuotes.CaptureSource @Customer,@Items,@Day,@Source OUTPUT,@Segment OUTPUT;
 IF @Source IS NULL OR HASHBYTES(''SHA2_256'',CONVERT(varbinary(max),@Source))<>@OriginalSource THROW 58906,N''Pricing source changed.'',1;
 IF @Channel=N''Venta móvil offline'' AND EXISTS(SELECT 1 FROM dbo.Pedidos WITH(UPDLOCK,HOLDLOCK) WHERE PedidoOfflineGuid=@RequestId)
  THROW 58907,N''Historical or foreign offline identifier.'',1;
 DECLARE @SellerName nvarchar(150)=(SELECT NombreCompleto FROM dbo.Usuarios WHERE UsuarioId=@UsuarioId);
 INSERT dbo.Pedidos(UsuarioId,Estado,TipoEntrega,DireccionEntrega,Observaciones,IdentificacionCliente,Total,VendedorUsuarioId,VendedorNombre,CanalPedido,FechaActualizacion,PedidoOfflineGuid,InventarioDescontado,MetodoPago,EstadoPago)
 VALUES(@Customer,N''Retenido'',JSON_VALUE(@Intent,''$.delivery''),JSON_VALUE(@Intent,''$.address''),JSON_VALUE(@Intent,''$.notes''),JSON_VALUE(@Intent,''$.identification''),@Final,@UsuarioId,@SellerName,@Channel,SYSDATETIME(),CASE WHEN @Channel=N''Venta móvil offline'' THEN @RequestId ELSE NULL END,0,N''Efectivo contra entrega'',N''Pendiente'');
 SET @PedidoId=CONVERT(int,SCOPE_IDENTITY());INSERT CommercialOrders.Orders(RequestId,PedidoId,CreatedUtc) VALUES(@RequestId,@PedidoId,SYSUTCDATETIME());
 INSERT dbo.Stage2OrderAmounts(PedidoId,ContractVersion,Bruto,Descuento,Neto,Impuesto,TotalFinal)
 SELECT @PedidoId,2,Gross,Discount,Net,Tax,FinalTotal FROM CommerceQuotes.Quotes WHERE RequestId=@RequestId;
 INSERT dbo.PedidoDetalle(PedidoId,ProductoId,Cantidad,PrecioUnitario,ProductoNombreSnapshot,EsRegalo)
 SELECT @PedidoId,Product,Quantity,Price,ProductName,0 FROM CommerceQuotes.Lines WHERE RequestId=@RequestId;
 INSERT dbo.Stage2OrderAmountLines(PedidoId,LineKind,LineId,ProductoId,Cantidad,PrecioBruto,Bruto,Descuento,Neto)
 SELECT @PedidoId,N''Producto'',d.PedidoDetalleId,l.Product,l.Quantity,l.Price,CONVERT(decimal(18,2),l.Quantity*l.Price),l.Discount,CONVERT(decimal(18,2),l.Quantity*l.Price)-l.Discount
 FROM CommerceQuotes.Lines l JOIN dbo.PedidoDetalle d ON d.PedidoId=@PedidoId AND d.ProductoId=l.Product WHERE l.RequestId=@RequestId;
 IF @Final>2000000 BEGIN
  INSERT CommercialOrders.Milestones(PedidoId,State,Actor,OriginalLoginId,CreatedUtc) VALUES(@PedidoId,N''Retenido'',@UsuarioId,@LoginId,SYSUTCDATETIME());
  INSERT dbo.InventarioOperacionAuditoria(Modulo,EntidadId,Accion,UsuarioId,UsuarioNombre,Detalle)
  VALUES(N''Pedidos'',@PedidoId,N''Retener'',@UsuarioId,@SellerName,N''Total final superior a 2000000; requiere supervisor asignado. Sin descuento de stock ni factura.'');
 END ELSE EXEC CommercialOrders.Finalize @PedidoId,@UsuarioId,@LoginId;
 EXEC CommerceQuotes.RequireOwner @LoginId,@UsuarioId,@PerfilId,@SecurityStamp,@BindingHash,@IssuedUtc,@ExpiresUtc,@RequestId,@Fingerprint;
 EXEC CommercialOrders.Result @PedidoId;COMMIT;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK;THROW;END CATCH; END;');
EXEC(N'CREATE PROCEDURE CommercialOrders.RequireSupervisor
 @LoginId uniqueidentifier,@UsuarioId int,@PerfilId int,@SecurityStamp uniqueidentifier,
 @BindingHash binary(32),@IssuedUtc datetime2(7),@ExpiresUtc datetime2(7),@PedidoId int,@ExpectedRevision bigint=NULL,@Revision bigint OUTPUT AS BEGIN SET NOCOUNT ON;
 IF @@TRANCOUNT=0 THROW 58900,N''Commercial transaction required.'',1;
 EXEC dbo.sp_Stage2_RequireOriginalLogin @LoginId,@UsuarioId,@PerfilId,@SecurityStamp,@BindingHash,@IssuedUtc,@ExpiresUtc,N''PEDIDOS_AUTORIZAR_RECHAZAR'';
 DECLARE @Seller int,@Customer int,@CustomerRevision bigint,@Current bigint;
 SELECT @Seller=o.Actor,@Customer=o.Customer,@CustomerRevision=o.CustomerRelationshipRevision
 FROM CommercialOrders.Orders link WITH(UPDLOCK,HOLDLOCK) JOIN CommerceQuotes.Owners o WITH(HOLDLOCK) ON o.RequestId=link.RequestId WHERE link.PedidoId=@PedidoId;
 IF @Seller IS NULL OR @Seller=@UsuarioId THROW 58908,N''Assigned supervisor required.'',1;
 EXEC CommercialSecurity.RequireAssignment ''SC'',@Seller,@Customer,@CustomerRevision,@Current OUTPUT;
 EXEC CommercialSecurity.RequireAssignment ''SS'',@UsuarioId,@Seller,@ExpectedRevision,@Revision OUTPUT;
 END;');
EXEC(N'CREATE PROCEDURE CommercialOrders.Decide
 @LoginId uniqueidentifier,@UsuarioId int,@PerfilId int,@SecurityStamp uniqueidentifier,
 @BindingHash binary(32),@IssuedUtc datetime2(7),@ExpiresUtc datetime2(7),@PedidoId int,@OperationId uniqueidentifier,@ExpectedRevision bigint,@Decision nvarchar(20),@Reason nvarchar(500)=NULL AS BEGIN SET NOCOUNT ON;SET XACT_ABORT ON;
 IF @OperationId IS NULL OR @OperationId=''00000000-0000-0000-0000-000000000000'' OR @ExpectedRevision IS NULL OR @ExpectedRevision<=0 OR @Decision IS NULL OR @Decision NOT IN(N''Aprobado'',N''Rechazado'')
  THROW 58901,N''Invalid supervisor decision.'',1;
 SET @Reason=NULLIF(LTRIM(RTRIM(@Reason)),N'''');IF @Decision=N''Aprobado'' AND @Reason IS NOT NULL THROW 58901,N''Approval reason is not part of this contract.'',1;
 IF @Decision=N''Rechazado'' AND @Reason IS NULL THROW 58901,N''Rejection reason required.'',1;
 BEGIN TRY BEGIN TRANSACTION;
 DECLARE @Revision bigint;EXEC CommercialOrders.RequireSupervisor @LoginId,@UsuarioId,@PerfilId,@SecurityStamp,@BindingHash,@IssuedUtc,@ExpiresUtc,@PedidoId,@ExpectedRevision,@Revision OUTPUT;
 IF EXISTS(SELECT 1 FROM CommercialOrders.Decisions WITH(UPDLOCK,HOLDLOCK) WHERE OperationId=@OperationId OR PedidoId=@PedidoId)
 BEGIN
  IF NOT EXISTS(SELECT 1 FROM CommercialOrders.Decisions WHERE OperationId=@OperationId AND PedidoId=@PedidoId AND OriginalLoginId=@LoginId AND Actor=@UsuarioId AND ProfileId=@PerfilId AND SecurityStamp=@SecurityStamp AND BindingHash=@BindingHash AND SupervisorRevision=@Revision AND Decision=@Decision AND ISNULL(Reason,N'''') COLLATE Latin1_General_100_BIN2=ISNULL(@Reason,N'''') COLLATE Latin1_General_100_BIN2)
   THROW 58907,N''Decision context conflict.'',1;
  EXEC CommercialOrders.Result @PedidoId;COMMIT;RETURN;
 END;
 IF NOT EXISTS(SELECT 1 FROM dbo.Pedidos WITH(UPDLOCK,HOLDLOCK) WHERE PedidoId=@PedidoId AND Estado=N''Retenido'' AND InventarioDescontado=0)
  OR EXISTS(SELECT 1 FROM dbo.Facturas WITH(UPDLOCK,HOLDLOCK) WHERE PedidoId=@PedidoId) THROW 58902,N''Order is not retained without effects.'',1;
 INSERT CommercialOrders.Decisions(OperationId,PedidoId,OriginalLoginId,Actor,ProfileId,SecurityStamp,BindingHash,SupervisorRevision,Decision,Reason,CreatedUtc)
 VALUES(@OperationId,@PedidoId,@LoginId,@UsuarioId,@PerfilId,@SecurityStamp,@BindingHash,@Revision,@Decision,@Reason,SYSUTCDATETIME());
 IF @Decision=N''Aprobado'' EXEC CommercialOrders.Finalize @PedidoId,@UsuarioId,@LoginId;
 ELSE BEGIN
  UPDATE dbo.Pedidos SET Estado=N''Rechazado'',FechaActualizacion=SYSDATETIME() WHERE PedidoId=@PedidoId;
  INSERT CommercialOrders.Milestones(PedidoId,State,Actor,OriginalLoginId,CreatedUtc) VALUES(@PedidoId,N''Rechazado'',@UsuarioId,@LoginId,SYSUTCDATETIME());
  INSERT dbo.InventarioOperacionAuditoria(Modulo,EntidadId,Accion,UsuarioId,UsuarioNombre,Detalle)
  SELECT N''Pedidos'',@PedidoId,N''Rechazar'',@UsuarioId,NombreCompleto,@Reason FROM dbo.Usuarios WHERE UsuarioId=@UsuarioId;
 END;
 EXEC CommercialOrders.RequireSupervisor @LoginId,@UsuarioId,@PerfilId,@SecurityStamp,@BindingHash,@IssuedUtc,@ExpiresUtc,@PedidoId,@ExpectedRevision,@Revision OUTPUT;
 EXEC CommercialOrders.Result @PedidoId;COMMIT;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK;THROW;END CATCH; END;');
EXEC(N'CREATE PROCEDURE CommercialOrders.Clients
 @LoginId uniqueidentifier,@UsuarioId int,@PerfilId int,@SecurityStamp uniqueidentifier,
 @BindingHash binary(32),@IssuedUtc datetime2(7),@ExpiresUtc datetime2(7),@Buscar nvarchar(150)=NULL AS BEGIN SET NOCOUNT ON;SET XACT_ABORT ON;
 BEGIN TRY BEGIN TRANSACTION;
 EXEC dbo.sp_Stage2_RequireOriginalLogin @LoginId,@UsuarioId,@PerfilId,@SecurityStamp,@BindingHash,@IssuedUtc,@ExpiresUtc,N''VENTA_MOVIL_CREAR'';
 SELECT c.UsuarioId,c.NombreCompleto,c.Correo,ISNULL(c.Telefono,N'''') AS Telefono,ISNULL(c.Direccion,N'''') AS Direccion
 FROM CommercialSecurity.Assignments a WITH(HOLDLOCK) JOIN dbo.Usuarios c WITH(HOLDLOCK) ON c.UsuarioId=a.ChildUser
 JOIN dbo.Perfiles p WITH(HOLDLOCK) ON p.PerfilId=c.PerfilId
 WHERE a.Kind=''SC'' AND a.ParentUser=@UsuarioId AND a.Enabled=1 AND (a.ExpiresUtc IS NULL OR a.ExpiresUtc>SYSUTCDATETIME())
 AND c.Activo=1 AND p.Activo=1 AND p.Nombre=N''Cliente''
 AND (@Buscar IS NULL OR c.NombreCompleto LIKE N''%''+@Buscar+N''%'' OR c.Correo LIKE N''%''+@Buscar+N''%'') ORDER BY c.NombreCompleto,c.UsuarioId;
 COMMIT;END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK;THROW;END CATCH;END;');
EXEC(N'CREATE PROCEDURE CommercialOrders.History
 @LoginId uniqueidentifier,@UsuarioId int,@PerfilId int,@SecurityStamp uniqueidentifier,
 @BindingHash binary(32),@IssuedUtc datetime2(7),@ExpiresUtc datetime2(7),@Top int=50 AS BEGIN SET NOCOUNT ON;SET XACT_ABORT ON;
 IF @Top IS NULL OR @Top NOT BETWEEN 1 AND 200 THROW 58901,N''Invalid history limit.'',1;
 BEGIN TRY BEGIN TRANSACTION;
 EXEC dbo.sp_Stage2_RequireOriginalLogin @LoginId,@UsuarioId,@PerfilId,@SecurityStamp,@BindingHash,@IssuedUtc,@ExpiresUtc,N''VENTA_MOVIL_CREAR'';
 SELECT TOP(@Top) p.PedidoId,c.NombreCompleto,p.FechaPedido,p.Estado,p.Total,ISNULL(p.TipoEntrega,N'''') AS TipoEntrega,
 ISNULL(p.CanalPedido,N'''') AS CanalPedido,ISNULL(f.NumeroFactura,N'''') AS NumeroFactura,p.PedidoOfflineGuid
 FROM dbo.Pedidos p WITH(HOLDLOCK) JOIN dbo.Usuarios c ON c.UsuarioId=p.UsuarioId LEFT JOIN dbo.Facturas f ON f.PedidoId=p.PedidoId
 WHERE p.VendedorUsuarioId=@UsuarioId ORDER BY p.FechaPedido DESC,p.PedidoId DESC;
 COMMIT;END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK;THROW;END CATCH;END;');
EXEC(N'CREATE PROCEDURE CommercialOrders.Details
 @LoginId uniqueidentifier,@UsuarioId int,@PerfilId int,@SecurityStamp uniqueidentifier,
 @BindingHash binary(32),@IssuedUtc datetime2(7),@ExpiresUtc datetime2(7),@PedidoId int,@Scope varchar(20) AS BEGIN SET NOCOUNT ON;SET XACT_ABORT ON;
 IF @Scope IS NULL OR @Scope NOT IN(''Seller'',''Supervisor'') THROW 58901,N''Invalid detail scope.'',1;
 BEGIN TRY BEGIN TRANSACTION;
 DECLARE @Revision bigint=NULL;
 IF @Scope=''Seller'' BEGIN
 EXEC dbo.sp_Stage2_RequireOriginalLogin @LoginId,@UsuarioId,@PerfilId,@SecurityStamp,@BindingHash,@IssuedUtc,@ExpiresUtc,N''VENTA_MOVIL_CREAR'';
  IF NOT EXISTS(SELECT 1 FROM dbo.Pedidos WITH(UPDLOCK,HOLDLOCK) WHERE PedidoId=@PedidoId AND VendedorUsuarioId=@UsuarioId) THROW 58908,N''Order unavailable.'',1;
 END ELSE EXEC CommercialOrders.RequireSupervisor @LoginId,@UsuarioId,@PerfilId,@SecurityStamp,@BindingHash,@IssuedUtc,@ExpiresUtc,@PedidoId,NULL,@Revision OUTPUT;
 SELECT p.PedidoId,p.UsuarioId,c.NombreCompleto,c.Correo,p.FechaPedido,p.Estado,p.TipoEntrega,p.DireccionEntrega,p.Observaciones,p.Total,
 p.MetodoPago,p.EstadoPago,p.ReferenciaPago,p.FechaPago,d.Reason AS MotivoRechazo,p.InventarioDescontado,
 f.FacturaId,ISNULL(f.NumeroFactura,N'''') AS NumeroFactura,@Revision AS SupervisorRevision
 FROM dbo.Pedidos p WITH(HOLDLOCK) JOIN dbo.Usuarios c ON c.UsuarioId=p.UsuarioId
 LEFT JOIN CommercialOrders.Decisions d ON d.PedidoId=p.PedidoId AND d.Decision=N''Rechazado'' LEFT JOIN dbo.Facturas f ON f.PedidoId=p.PedidoId WHERE p.PedidoId=@PedidoId;
 SELECT d.PedidoDetalleId,COALESCE(d.ProductoNombreSnapshot,p.Nombre) AS Producto,d.ProductoId,d.Cantidad,d.PrecioUnitario,
 COALESCE(a.Neto,CONVERT(decimal(18,2),d.Cantidad*d.PrecioUnitario)) AS Neto,p.Stock,CONVERT(bit,0) AS IsCombo
 FROM dbo.PedidoDetalle d JOIN dbo.Productos p ON p.ProductoId=d.ProductoId
 LEFT JOIN dbo.Stage2OrderAmountLines a ON a.PedidoId=d.PedidoId AND a.LineKind=N''Producto'' AND a.LineId=d.PedidoDetalleId
 WHERE d.PedidoId=@PedidoId AND d.PedidoComboId IS NULL
 UNION ALL SELECT -pc.PedidoComboId,CONCAT(N''Combo: '',pc.ComboNombreSnapshot),0,pc.Cantidad,pc.PrecioUnitario,
 COALESCE(a.Neto,CONVERT(decimal(18,2),pc.Cantidad*pc.PrecioUnitario)),0,CONVERT(bit,1)
 FROM dbo.PedidoCombos pc LEFT JOIN dbo.Stage2OrderAmountLines a ON a.PedidoId=pc.PedidoId AND a.LineKind=N''Combo'' AND a.LineId=pc.PedidoComboId WHERE pc.PedidoId=@PedidoId ORDER BY PedidoDetalleId;
 COMMIT;END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK;THROW;END CATCH;END;');
EXEC(N'CREATE PROCEDURE CommercialOrders.Retained
 @LoginId uniqueidentifier,@UsuarioId int,@PerfilId int,@SecurityStamp uniqueidentifier,
 @BindingHash binary(32),@IssuedUtc datetime2(7),@ExpiresUtc datetime2(7),@Buscar nvarchar(150)=NULL AS BEGIN SET NOCOUNT ON;SET XACT_ABORT ON;
 BEGIN TRY BEGIN TRANSACTION;
 EXEC dbo.sp_Stage2_RequireOriginalLogin @LoginId,@UsuarioId,@PerfilId,@SecurityStamp,@BindingHash,@IssuedUtc,@ExpiresUtc,N''PEDIDOS_AUTORIZAR_RECHAZAR'';
 SELECT TOP(200) p.PedidoId,c.NombreCompleto,c.Correo,p.FechaPedido,p.VendedorNombre,p.Total,ISNULL(p.TipoEntrega,N'''') AS TipoEntrega,
 ISNULL(p.CanalPedido,N'''') AS CanalPedido,(SELECT COUNT(*) FROM dbo.PedidoDetalle d WHERE d.PedidoId=p.PedidoId) AS TotalLineas
 FROM CommercialOrders.Orders link WITH(HOLDLOCK) JOIN CommerceQuotes.Owners o WITH(HOLDLOCK) ON o.RequestId=link.RequestId
 JOIN dbo.Pedidos p WITH(HOLDLOCK) ON p.PedidoId=link.PedidoId
 JOIN CommercialSecurity.Assignments ss WITH(HOLDLOCK) ON ss.Kind=''SS'' AND ss.ParentUser=@UsuarioId AND ss.ChildUser=o.Actor AND ss.Enabled=1 AND (ss.ExpiresUtc IS NULL OR ss.ExpiresUtc>SYSUTCDATETIME())
 JOIN CommercialSecurity.Assignments sc WITH(HOLDLOCK) ON sc.Kind=''SC'' AND sc.ParentUser=o.Actor AND sc.ChildUser=o.Customer AND sc.Enabled=1 AND sc.Revision=o.CustomerRelationshipRevision AND (sc.ExpiresUtc IS NULL OR sc.ExpiresUtc>SYSUTCDATETIME())
 JOIN dbo.Usuarios c WITH(HOLDLOCK) ON c.UsuarioId=o.Customer AND c.Activo=1 JOIN dbo.Perfiles cp WITH(HOLDLOCK) ON cp.PerfilId=c.PerfilId AND cp.Activo=1 AND cp.Nombre=N''Cliente''
 JOIN dbo.Usuarios seller WITH(HOLDLOCK) ON seller.UsuarioId=o.Actor AND seller.Activo=1 JOIN dbo.Perfiles sp WITH(HOLDLOCK) ON sp.PerfilId=seller.PerfilId AND sp.Activo=1 AND sp.Nombre<>N''Cliente''
 WHERE p.Estado=N''Retenido'' AND o.Actor<>@UsuarioId
 AND EXISTS(SELECT 1 FROM dbo.PerfilPermisos pp WITH(HOLDLOCK) JOIN dbo.Permisos cap WITH(HOLDLOCK) ON cap.PermisoId=pp.PermisoId AND cap.Activo=1 AND cap.Codigo=N''VENTA_MOVIL_CREAR'' WHERE pp.PerfilId=seller.PerfilId)
 AND (@Buscar IS NULL OR c.NombreCompleto LIKE N''%''+@Buscar+N''%'' OR c.Correo LIKE N''%''+@Buscar+N''%'' OR CONVERT(nvarchar(12),p.PedidoId)=@Buscar)
 ORDER BY p.FechaPedido DESC,p.PedidoId DESC;
 COMMIT;END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK;THROW;END CATCH;END;');
EXEC(N'CREATE TRIGGER CommercialOrders.OrdersImmutable ON CommercialOrders.Orders INSTEAD OF UPDATE,DELETE AS BEGIN THROW 58900,N''Immutable commercial record.'',1;END;');
EXEC(N'CREATE TRIGGER CommercialOrders.MilestonesImmutable ON CommercialOrders.Milestones INSTEAD OF UPDATE,DELETE AS BEGIN THROW 58900,N''Immutable commercial record.'',1;END;');
EXEC(N'CREATE TRIGGER CommercialOrders.DecisionsImmutable ON CommercialOrders.Decisions INSTEAD OF UPDATE,DELETE AS BEGIN THROW 58900,N''Immutable commercial record.'',1;END;');
INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes)
VALUES(N'0051_stage2_commercial_orders',N'0051_stage2_commercial_orders.sql',UPPER(@Hash),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),N'Versioned quote consumption and assigned supervisor decisions; original login ownership, atomic stock/invoice, aggregate money. No historical rewrite, grants, delivery claim or business assignments.');
COMMIT;END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK;THROW;END CATCH;
