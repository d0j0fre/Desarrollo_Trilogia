SET NOCOUNT ON;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0053_stage2_client_billing_summary' AND FileSha256=N'$(MigrationSha256)' AND Status=N'Applied') THROW 59110,N'Ledger unavailable.',1;
IF NOT EXISTS(SELECT 1 FROM sys.sql_modules WHERE object_id=OBJECT_ID(N'dbo.sp_Stage2_ClientBillingSummary') AND uses_ansi_nulls=1 AND uses_quoted_identifier=1 AND CONVERT(varchar(64),HASHBYTES('SHA2_256',CONVERT(varbinary(max),definition)),2)='F2B965FF754AB271D5ABCCEA7A19F35D3E198BC88AEFB66CC3C846687461F22D') THROW 59111,N'Billing summary module differs.',1;
IF (SELECT COUNT(*) FROM sys.parameters WHERE object_id=OBJECT_ID(N'dbo.sp_Stage2_ClientBillingSummary'))<>7 THROW 59112,N'Billing summary context differs.',1;
SELECT N'PASS' AS Result;
