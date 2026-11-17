SET NOCOUNT ON;

/* Verificacion de 0306. Deja la base como estaba: la prueba corre en una transaccion que se revierte. */
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0306_evidencia_archivos_en_base' AND Status = N'Applied')
    THROW 57110, N'0306 no figura aplicada en el ledger.', 1;
IF OBJECT_ID(N'dbo.EvidenciaArchivos', N'U') IS NULL THROW 57111, N'Falta dbo.EvidenciaArchivos.', 1;
IF OBJECT_ID(N'dbo.sp_EvidenciaArchivo_Preparar', N'P') IS NULL
   OR OBJECT_ID(N'dbo.sp_EvidenciaArchivo_Confirmar', N'P') IS NULL
   OR OBJECT_ID(N'dbo.sp_EvidenciaArchivo_Descartar', N'P') IS NULL
   OR OBJECT_ID(N'dbo.sp_EvidenciaArchivo_Obtener', N'P') IS NULL
    THROW 57112, N'Faltan procedimientos sp_EvidenciaArchivo_*.', 1;

BEGIN TRANSACTION;

DECLARE @Png VARBINARY(MAX) = 0x89504E470D0A1A0A0000000D49484452;
DECLARE @Leidos TABLE (ContentType NVARCHAR(30), Contenido VARBINARY(MAX));

-- Preparada: todavia no se puede leer.
EXEC dbo.sp_EvidenciaArchivo_Preparar N'00000000000000000000000000000306.png', N'00000000000000000000000000000306.stage', N'image/png', @Png;
INSERT @Leidos EXEC dbo.sp_EvidenciaArchivo_Obtener N'00000000000000000000000000000306.png';
IF EXISTS (SELECT 1 FROM @Leidos) THROW 57113, N'Una imagen sin confirmar no debe poder leerse.', 1;

-- Confirmada: se lee igual a como se guardo.
EXEC dbo.sp_EvidenciaArchivo_Confirmar N'00000000000000000000000000000306.stage', N'00000000000000000000000000000306.png';
INSERT @Leidos EXEC dbo.sp_EvidenciaArchivo_Obtener N'00000000000000000000000000000306.png';
IF NOT EXISTS (SELECT 1 FROM @Leidos WHERE ContentType = N'image/png' AND Contenido = @Png)
    THROW 57114, N'La imagen confirmada no se lee igual.', 1;

-- Descartar no borra una confirmada.
EXEC dbo.sp_EvidenciaArchivo_Descartar N'00000000000000000000000000000306.stage';
IF NOT EXISTS (SELECT 1 FROM dbo.EvidenciaArchivos WHERE StorageKey = N'00000000000000000000000000000306.png')
    THROW 57115, N'Descartar no debe borrar una imagen confirmada.', 1;

-- Descartar si borra una preparada.
EXEC dbo.sp_EvidenciaArchivo_Preparar N'10000000000000000000000000000306.png', N'10000000000000000000000000000306.stage', N'image/png', @Png;
EXEC dbo.sp_EvidenciaArchivo_Descartar N'10000000000000000000000000000306.stage';
IF EXISTS (SELECT 1 FROM dbo.EvidenciaArchivos WHERE StorageKey = N'10000000000000000000000000000306.png')
    THROW 57116, N'Descartar debe borrar la imagen preparada.', 1;

-- Confirmar algo que no existe falla.
BEGIN TRY
    EXEC dbo.sp_EvidenciaArchivo_Confirmar N'20000000000000000000000000000306.stage', N'20000000000000000000000000000306.png';
    THROW 57117, N'Confirmar una imagen inexistente debio fallar.', 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() <> 57105 THROW;
END CATCH;

ROLLBACK TRANSACTION;
PRINT N'0306 verificada.';
