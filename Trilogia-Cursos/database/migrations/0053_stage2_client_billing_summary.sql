SET NOCOUNT ON;
SET XACT_ABORT ON;
IF SESSIONPROPERTY('QUOTED_IDENTIFIER')<>1 OR SESSIONPROPERTY('ANSI_NULLS')<>1
 THROW 59100,N'Required SQL options unavailable.',1;
DECLARE @Hash nvarchar(128)=N'$(MigrationSha256)';
IF LEN(@Hash)<>64 OR @Hash LIKE N'%[^0-9a-fA-F]%' THROW 59101,N'Invalid migration hash.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0053_stage2_client_billing_summary')
BEGIN
 IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0053_stage2_client_billing_summary' AND Status=N'Applied' AND FileSha256=UPPER(@Hash))
  THROW 59102,N'Ledger conflict.',1;
 RETURN;
END;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId LIKE N'0053%')
 OR OBJECT_ID(N'dbo.sp_Stage2_ClientBillingSummary') IS NOT NULL THROW 59102,N'Identifier collision.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0052_stage2_seller_history'
 AND Status=N'Applied' AND FileSha256=N'0B832E41114D9435975226F9DE695C532B785855F7B7C4BAE9BD92C84B1F72A6')
 THROW 59103,N'Exact commercial prerequisite unavailable.',1;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @Lock int;
 EXEC @Lock=sys.sp_getapplock @Resource=N'Stage2:0053:install',@LockMode=N'Exclusive',@LockOwner=N'Transaction',@LockTimeout=5000;
 IF @Lock<0 THROW 59104,N'Installation conflict.',1;
 EXEC(N'CREATE PROCEDURE dbo.sp_Stage2_ClientBillingSummary
 @LoginId uniqueidentifier,@UsuarioId int,@PerfilId int,@SecurityStamp uniqueidentifier,
 @BindingHash binary(32),@IssuedUtc datetime2(7),@ExpiresUtc datetime2(7)
 AS
 BEGIN
  SET NOCOUNT ON;SET XACT_ABORT ON;
  BEGIN TRY
   BEGIN TRANSACTION;
   EXEC dbo.sp_Stage2_RequireOriginalLogin @LoginId,@UsuarioId,@PerfilId,@SecurityStamp,@BindingHash,@IssuedUtc,@ExpiresUtc,NULL;
   IF NOT EXISTS(SELECT 1 FROM dbo.Perfiles WITH(HOLDLOCK) WHERE PerfilId=@PerfilId AND Nombre=N''Cliente'' AND Activo=1)
    THROW 59105,N''Customer context unavailable.'',1;
   DECLARE @Count int,@Total decimal(18,2);
   SELECT @Count=COUNT(*),@Total=COALESCE(SUM(f.Total),CONVERT(decimal(18,2),0))
   FROM dbo.Facturas f WITH(HOLDLOCK)
   INNER JOIN dbo.Pedidos p WITH(HOLDLOCK) ON p.PedidoId=f.PedidoId AND p.UsuarioId=f.UsuarioId
   WHERE f.UsuarioId=@UsuarioId AND f.Estado=N''Generada'';
   EXEC dbo.sp_Stage2_RequireOriginalLogin @LoginId,@UsuarioId,@PerfilId,@SecurityStamp,@BindingHash,@IssuedUtc,@ExpiresUtc,NULL;
   COMMIT;
   SELECT @Count AS InvoiceCount,@Total AS TotalBilled;
  END TRY
  BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK;THROW;END CATCH;
 END;');
 INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes)
 VALUES(N'0053_stage2_client_billing_summary',N'0053_stage2_client_billing_summary.sql',UPPER(@Hash),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),
 N'Additive own-customer invoice summary, original-login validation, current generated invoices only. No historical rewrites, purchase-state inference or grants.');
 COMMIT;
END TRY
BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK;THROW;END CATCH;
