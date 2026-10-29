SET NOCOUNT ON;
DECLARE @Hash nvarchar(128)=N'$(MigrationSha256)';
IF LEN(@Hash)<>64 OR @Hash LIKE N'%[^0-9a-fA-F]%' THROW 58710,N'Invalid migration hash.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0048_stage2_order_approval_capability' AND Status=N'Applied' AND FileSha256=UPPER(@Hash)) THROW 58710,N'Approval-capability ledger/hash missing.',1;
IF (SELECT COUNT(*) FROM dbo.Permisos WHERE Codigo=N'PEDIDOS_AUTORIZAR_RECHAZAR' AND Modulo=N'Pedidos' AND Activo=1)<>1 THROW 58710,N'Approval capability not accredited.',1;
SELECT N'PASS' AS ApprovalCapabilityVerification;
