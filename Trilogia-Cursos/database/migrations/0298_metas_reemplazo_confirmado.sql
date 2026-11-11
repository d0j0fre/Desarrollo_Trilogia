SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL OR OBJECT_ID(N'dbo.MetasVendedor',N'U') IS NULL OR OBJECT_ID(N'dbo.sp_Metas_Upsert',N'P') IS NULL THROW 57980,N'Faltan dependencias de metas.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0298_metas_reemplazo_confirmado' AND Status=N'Applied') THROW 57981,N'La migracion ya fue aplicada.',1;
BEGIN TRANSACTION;
GO
-- BUG-21-01: una meta nueva del mismo vendedor y mes ya no sobrescribe a la anterior en silencio.
-- Si existe una meta y @Reemplazar = 0 se lanza el error 53134 (con el monto actual) y no se cambia nada;
-- la pantalla pide confirmacion y vuelve a llamar con @Reemplazar = 1.
CREATE OR ALTER PROCEDURE dbo.sp_Metas_Upsert
    @VendedorUsuarioId INT,
    @Anio              INT,
    @Mes               INT,
    @MontoMeta         DECIMAL(18,2),
    @Observaciones     NVARCHAR(300) = NULL,
    @UsuarioId         INT           = NULL,
    @Nombre            NVARCHAR(150) = NULL,
    @Reemplazar        BIT           = 0
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @MsgVendedor NVARCHAR(200) = N'El usuario seleccionado no es un vendedor v' + NCHAR(225) + N'lido.';
    DECLARE @MsgAnio NVARCHAR(200) = N'El a' + NCHAR(241) + N'o indicado no es v' + NCHAR(225) + N'lido.';
    IF NOT EXISTS (
        SELECT 1 FROM dbo.Usuarios u
        INNER JOIN dbo.Perfiles pf ON pf.PerfilId = u.PerfilId
        WHERE u.UsuarioId = @VendedorUsuarioId AND pf.Nombre = N'Vendedor')
        THROW 53130, @MsgVendedor, 1;
    IF @Mes NOT BETWEEN 1 AND 12 THROW 53131, N'El mes debe estar entre 1 y 12.', 1;
    IF @Anio NOT BETWEEN 2000 AND 2100 THROW 53132, @MsgAnio, 1;
    IF @MontoMeta IS NULL OR @MontoMeta <= 0 THROW 53133, N'El monto de la meta debe ser mayor que cero.', 1;

    SET @Observaciones = NULLIF(LTRIM(RTRIM(@Observaciones)), N'');

    DECLARE @Actual DECIMAL(18,2);
    SELECT @Actual = MontoMeta
    FROM dbo.MetasVendedor
    WHERE VendedorUsuarioId = @VendedorUsuarioId AND Anio = @Anio AND Mes = @Mes;

    IF @Actual IS NOT NULL AND ISNULL(@Reemplazar, 0) = 0
    BEGIN
        DECLARE @Mensaje NVARCHAR(300) = CONCAT(N'Ya existe una meta de ', FORMAT(@Actual, N'N2', N'es-CR'),
            N' para ese vendedor en ', RIGHT(CONCAT(N'0', @Mes), 2), N'/', @Anio, N'. Confirme que desea reemplazarla.');
        THROW 53134, @Mensaje, 1;
    END;

    IF @Actual IS NOT NULL
        UPDATE dbo.MetasVendedor
        SET MontoMeta = @MontoMeta, Observaciones = @Observaciones, FechaActualizacion = SYSDATETIME()
        WHERE VendedorUsuarioId = @VendedorUsuarioId AND Anio = @Anio AND Mes = @Mes;
    ELSE
        INSERT INTO dbo.MetasVendedor
            (VendedorUsuarioId, Anio, Mes, MontoMeta, Observaciones, RegistradoPorUsuarioId, RegistradoPorNombre)
        VALUES
            (@VendedorUsuarioId, @Anio, @Mes, @MontoMeta, @Observaciones, @UsuarioId, @Nombre);
END;
GO
DECLARE @sha NVARCHAR(128)=N'$(MigrationSha256)';
IF LEN(@sha)<>64 OR @sha LIKE N'%[^0-9A-Fa-f]%' THROW 57982,N'SHA-256 invalido.',1;
INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes) VALUES(N'0298_metas_reemplazo_confirmado',N'0298_metas_reemplazo_confirmado.sql',UPPER(@sha),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),N'BUG-21-01: reemplazar una meta existente exige confirmacion (@Reemplazar).');
COMMIT TRANSACTION;
GO
