-- Original operation receipts are independent of later order lifecycle states.
-- Only two internal modules change; no data rewrite, grants, price, stock, delivery or TTL changes.
SET NOCOUNT ON;SET XACT_ABORT ON;
IF SESSIONPROPERTY('QUOTED_IDENTIFIER')<>1 OR SESSIONPROPERTY('ANSI_NULLS')<>1 THROW 58930,N'SQL options unavailable.',1;
DECLARE @Hash nvarchar(128)=N'$(MigrationSha256)';
IF LEN(@Hash)<>64 OR @Hash LIKE N'%[^0-9a-fA-F]%' THROW 58930,N'Invalid migration hash.',1;
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL THROW 58930,N'Ledger unavailable.',1;
BEGIN TRY BEGIN TRANSACTION;
DECLARE @Lock int;EXEC @Lock=sys.sp_getapplock @Resource=N'Stage2:0054:install',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=5000;
IF @Lock<0 THROW 58930,N'Installation conflict.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0054_stage2_order_operation_receipts') BEGIN
 IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0054_stage2_order_operation_receipts' AND Status=N'Applied' AND FileSha256=UPPER(@Hash)) THROW 58930,N'Ledger conflict.',1;
 COMMIT;RETURN;END;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId LIKE N'0054%') THROW 58930,N'Migration identifier collision.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0051_stage2_commercial_orders' AND Status=N'Applied' AND FileSha256=N'AB00E0743617C3E28877295F4BD69C747B2A3AE3A8D5BD370EC4E062710E4A31') THROW 58930,N'Exact prerequisite unavailable.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0052_stage2_seller_history' AND Status=N'Applied' AND FileSha256=N'0B832E41114D9435975226F9DE695C532B785855F7B7C4BAE9BD92C84B1F72A6') THROW 58930,N'Exact prerequisite unavailable.',1;
IF ISNULL(HASHBYTES('SHA2_256',CONVERT(varbinary(max),OBJECT_DEFINITION(OBJECT_ID(N'CommercialOrders.Result')))),0x)<>0x928541B7D6E98974CAC721EB9C62C1ABA5F08DB36999925E3E6D6C6A40FEEB76 THROW 58930,N'Previous module differs: Result.',1;
IF ISNULL(HASHBYTES('SHA2_256',CONVERT(varbinary(max),OBJECT_DEFINITION(OBJECT_ID(N'CommercialOrders.Decide')))),0x)<>0x2C7A129B0254CF1AFCE94DBCE94ADA2DC560188CFA1B9AF5AF938577EE97999A THROW 58930,N'Previous module differs: Decide.',1;
IF (SELECT COUNT(*) FROM sys.triggers WHERE object_id IN(OBJECT_ID(N'CommercialOrders.OrdersImmutable'),OBJECT_ID(N'CommercialOrders.MilestonesImmutable'),OBJECT_ID(N'CommercialOrders.DecisionsImmutable')) AND is_disabled=0)<>3 THROW 58930,N'Immutable evidence unavailable.',1;
EXEC(N'ALTER PROCEDURE CommercialOrders.Result
 @PedidoId int,@Decision bit=0 AS BEGIN SET NOCOUNT ON;
 IF @@TRANCOUNT=0 THROW 58900,N''Commercial transaction required.'',1;
 IF @Decision IS NULL THROW 58901,N''Operation kind required.'',1;
 DECLARE @Final decimal(18,2),@State nvarchar(30),@Invoice int=0,@Number nvarchar(50)=N'''',@Choice nvarchar(20);
 SELECT @Final=q.FinalTotal FROM CommercialOrders.Orders o WITH(HOLDLOCK)
 JOIN CommerceQuotes.Quotes q WITH(HOLDLOCK) ON q.RequestId=o.RequestId WHERE o.PedidoId=@PedidoId;
 IF @Final IS NULL THROW 58902,N''Original order operation missing.'',1;
 IF @Decision=1
 BEGIN
  SELECT @Choice=Decision FROM CommercialOrders.Decisions WITH(HOLDLOCK) WHERE PedidoId=@PedidoId;
  IF @Choice IS NULL THROW 58902,N''Original decision missing.'',1;
  SET @State=CASE WHEN @Choice=N''Aprobado'' THEN N''Facturado'' ELSE N''Rechazado'' END;
 END
 ELSE SET @State=CASE WHEN @Final>2000000 THEN N''Retenido'' ELSE N''Facturado'' END;
 IF NOT EXISTS(SELECT 1 FROM CommercialOrders.Milestones WITH(HOLDLOCK) WHERE PedidoId=@PedidoId AND State=@State)
  THROW 58902,N''Committed original operation milestone missing.'',1;
 IF @State=N''Facturado''
 BEGIN
  SELECT @Invoice=f.FacturaId,@Number=f.NumeroFactura FROM dbo.Facturas f WITH(HOLDLOCK)
  JOIN dbo.Stage2OrderAmounts a WITH(HOLDLOCK) ON a.PedidoId=f.PedidoId AND a.ContractVersion=2
  WHERE f.PedidoId=@PedidoId AND f.Total=@Final AND f.Total=a.TotalFinal AND f.Subtotal=a.Neto AND f.Impuesto=a.Impuesto;
  IF @Invoice<=0 OR NULLIF(LTRIM(RTRIM(@Number)),N'''') IS NULL
   OR NOT EXISTS(SELECT 1 FROM CommercialOrders.Milestones WITH(HOLDLOCK) WHERE PedidoId=@PedidoId AND State=N''Liberado'')
   THROW 58902,N''Committed invoice evidence missing.'',1;
 END;
 IF @State=N''Rechazado'' AND EXISTS(SELECT 1 FROM dbo.Facturas WITH(HOLDLOCK) WHERE PedidoId=@PedidoId)
  THROW 58902,N''Rejected operation has conflicting invoice evidence.'',1;
 SELECT @PedidoId AS PedidoId,@State AS Estado,@Invoice AS FacturaId,@Number AS NumeroFactura;
 END;');
EXEC(N'ALTER PROCEDURE CommercialOrders.Decide
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
  EXEC CommercialOrders.Result @PedidoId,@Decision=1;COMMIT;RETURN;
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
 EXEC CommercialOrders.Result @PedidoId,@Decision=1;COMMIT;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK;THROW;END CATCH; END;');
INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes)
VALUES(N'0054_stage2_order_operation_receipts',N'0054_stage2_order_operation_receipts.sql',UPPER(@Hash),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),N'Original Consume and Decide outcomes reconstructed only from committed immutable quote/milestone/decision evidence; later lifecycle remains visible through DetailsV2 and HistoryV2. Same original-login, ownership, revision and transaction checks. No data rewrite or new grants.');
COMMIT;END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK;THROW;END CATCH;
