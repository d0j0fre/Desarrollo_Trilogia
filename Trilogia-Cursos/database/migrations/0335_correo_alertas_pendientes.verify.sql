SET NOCOUNT ON;

/* Verificacion de 0335. Solo lectura. */
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0335_correo_alertas_pendientes' AND Status = N'Applied')
    THROW 58510, N'0335 no figura aplicada en el ledger.', 1;
IF OBJECT_ID(N'dbo.VehiculoDocumentoAvisos', N'U') IS NULL
    THROW 58511, N'Falta la tabla dbo.VehiculoDocumentoAvisos.', 1;
IF OBJECT_ID(N'dbo.sp_DocumentAlert_PendingNotifications', N'P') IS NULL
    THROW 58512, N'Falta dbo.sp_DocumentAlert_PendingNotifications.', 1;
IF OBJECT_ID(N'dbo.sp_FlotaDocumentos_AvisosPendientes', N'P') IS NULL
    THROW 58513, N'Falta dbo.sp_FlotaDocumentos_AvisosPendientes.', 1;
IF OBJECT_ID(N'dbo.sp_FlotaDocumentos_RegistrarAviso', N'P') IS NULL
    THROW 58514, N'Falta dbo.sp_FlotaDocumentos_RegistrarAviso.', 1;
IF CHARINDEX(N'Fallido', OBJECT_DEFINITION(OBJECT_ID(N'dbo.sp_DocumentAlert_RegisterNotification'))) = 0
    THROW 58515, N'sp_DocumentAlert_RegisterNotification no reintenta los intentos fallidos.', 1;

-- Los procedimientos de lectura se ejecutan sin error y devuelven las columnas esperadas.
DECLARE @P TABLE (AlertaId INT, DocumentoId INT, UmbralDias INT, Titulo NVARCHAR(300), FechaVencimiento DATE, Destinatario NVARCHAR(320));
INSERT @P EXEC dbo.sp_DocumentAlert_PendingNotifications;
EXEC dbo.sp_FlotaDocumentos_AvisosPendientes @Umbrales = N'30,15,7,1,0';
PRINT N'Verificacion 0335 completada.';
