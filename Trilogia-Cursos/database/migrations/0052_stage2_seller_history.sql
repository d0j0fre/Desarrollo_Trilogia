SET NOCOUNT ON;SET XACT_ABORT ON;
IF SESSIONPROPERTY('QUOTED_IDENTIFIER')<>1 OR SESSIONPROPERTY('ANSI_NULLS')<>1 THROW 59010,N'SQL options unavailable.',1;
DECLARE @Hash nvarchar(128)=N'$(MigrationSha256)';
IF LEN(@Hash)<>64 OR @Hash LIKE N'%[^0-9a-fA-F]%' THROW 59011,N'Invalid migration hash.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0052_stage2_seller_history') BEGIN
 IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0052_stage2_seller_history' AND Status=N'Applied' AND FileSha256=UPPER(@Hash)) THROW 59012,N'Ledger conflict.',1;
 RETURN;END;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId LIKE N'0052%') OR OBJECT_ID(N'CommercialOrders.HistoryV2') IS NOT NULL OR OBJECT_ID(N'CommercialOrders.DetailsV2') IS NOT NULL THROW 59012,N'Identifier collision.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0051_stage2_commercial_orders' AND Status=N'Applied' AND FileSha256=N'AB00E0743617C3E28877295F4BD69C747B2A3AE3A8D5BD370EC4E062710E4A31') THROW 59013,N'Exact commercial order prerequisite unavailable.',1;
BEGIN TRY BEGIN TRANSACTION;
DECLARE @Lock int;EXEC @Lock=sys.sp_getapplock @Resource=N'Stage2:0052:install',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=5000;
IF @Lock<0 THROW 59014,N'Installation conflict.',1;
EXEC(N'CREATE PROCEDURE CommercialOrders.HistoryV2
 @LoginId uniqueidentifier,@UsuarioId int,@PerfilId int,@SecurityStamp uniqueidentifier,@BindingHash binary(32),@IssuedUtc datetime2(7),@ExpiresUtc datetime2(7),@Top int=20
 AS BEGIN SET NOCOUNT ON;SET XACT_ABORT ON;
 IF @Top IS NULL OR @Top NOT BETWEEN 1 AND 200 THROW 58901,N''Invalid history limit.'',1;
 BEGIN TRY BEGIN TRANSACTION;
 EXEC dbo.sp_Stage2_RequireOriginalLogin @LoginId,@UsuarioId,@PerfilId,@SecurityStamp,@BindingHash,@IssuedUtc,@ExpiresUtc,N''VENTA_MOVIL_CREAR'';
 SELECT TOP(@Top) p.PedidoId,c.NombreCompleto,p.FechaPedido,p.Estado,p.Total,
 ISNULL(d.Reason,N'''') AS MotivoRechazo,ISNULL(f.NumeroFactura,N'''') AS NumeroFactura,p.FechaActualizacion
 FROM dbo.Pedidos p WITH(HOLDLOCK) JOIN dbo.Usuarios c ON c.UsuarioId=p.UsuarioId
 LEFT JOIN CommercialOrders.Decisions d ON d.PedidoId=p.PedidoId AND d.Decision=N''Rechazado''
 LEFT JOIN dbo.Facturas f ON f.PedidoId=p.PedidoId
 WHERE p.VendedorUsuarioId=@UsuarioId ORDER BY p.FechaPedido DESC,p.PedidoId DESC;
 COMMIT;END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK;THROW;END CATCH;END;');
EXEC(N'CREATE PROCEDURE CommercialOrders.DetailsV2
 @LoginId uniqueidentifier,@UsuarioId int,@PerfilId int,@SecurityStamp uniqueidentifier,@BindingHash binary(32),@IssuedUtc datetime2(7),@ExpiresUtc datetime2(7),@PedidoId int,@Scope varchar(20)
 AS BEGIN SET NOCOUNT ON;SET XACT_ABORT ON;
 BEGIN TRY BEGIN TRANSACTION;
 EXEC CommercialOrders.Details @LoginId,@UsuarioId,@PerfilId,@SecurityStamp,@BindingHash,@IssuedUtc,@ExpiresUtc,@PedidoId,@Scope;
 SELECT a.Bruto,a.Descuento,a.Neto,a.Impuesto,a.TotalFinal
 FROM dbo.Stage2OrderAmounts a JOIN CommercialOrders.Orders link ON link.PedidoId=a.PedidoId WHERE a.PedidoId=@PedidoId;
 COMMIT;END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK;THROW;END CATCH;END;');
INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes)
VALUES(N'0052_stage2_seller_history',N'0052_stage2_seller_history.sql',UPPER(@Hash),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),N'Additive authorized readers: rejection reason/update time and immutable aggregate amount breakdown. Preserves 0051 and historical records; no grants or data mutations.');
COMMIT;END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK;THROW;END CATCH;
