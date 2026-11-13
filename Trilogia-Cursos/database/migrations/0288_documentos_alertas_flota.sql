SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

/* G2-17 y G2-20: documentos legales de la flota y alertas de flota (CU-154).

   G2-17: no había lista de documentos ni forma de corregir o borrar uno, y
     sp_Flota_Alertas avisaba de TODOS los documentos, así que un seguro vencido
     seguía en Alertas activas aunque ya estuviera renovado.
     - VehiculoDocumentos.DadoDeBaja / FechaBaja: la baja conserva la fila.
     - sp_VehiculoDocumento_List (por vehículo, con EsVigente), _GetById, _Update y _Baja.
     - Documento vigente = el de vencimiento más reciente de cada vehículo y tipo (y, a
       igual fecha, el último registrado), sin contar los dados de baja. «Otro» no se
       reemplaza entre sí: cada documento «Otro» es vigente mientras no se dé de baja.
     - sp_Flota_Alertas avisa de los documentos vigentes únicamente.
   G2-20 (decisión de negocio tomada: sí avisan):
     - Preventivo y correctivo Programada o EnProceso con fecha programada próxima o vencida.
     - Correctivo En proceso (vehículo en reparación): aviso «EnCurso», o «Vencido» si su
       fecha programada ya pasó; correctivo Programada sin fecha: aviso «Pendiente».
     - Preventivo por kilometraje: «Vencido» cuando el odómetro llegó al kilometraje y
       «PorVencer» desde @KmAviso km antes (500 por defecto).
   La firma de sp_Flota_Alertas solo agrega @KmAviso con valor por defecto.
   Rollback en 0288_documentos_alertas_flota.rollback.md. */
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL THROW 56880,N'Falta el ledger 0001.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0288_documentos_alertas_flota' AND Status=N'Applied')
    THROW 56881,N'0288 ya figura aplicada.',1;
IF OBJECT_ID(N'dbo.VehiculoDocumentos',N'U') IS NULL OR OBJECT_ID(N'dbo.OrdenesMantenimiento',N'U') IS NULL OR OBJECT_ID(N'dbo.Vehiculos',N'U') IS NULL
    THROW 56882,N'Faltan las tablas de documentos, mantenimiento o vehículos (cu154).',1;

BEGIN TRANSACTION;
GO

IF COL_LENGTH(N'dbo.VehiculoDocumentos', N'DadoDeBaja') IS NULL
    ALTER TABLE dbo.VehiculoDocumentos ADD
        DadoDeBaja BIT       NOT NULL CONSTRAINT DF_VehiculoDocumentos_Baja DEFAULT 0,
        FechaBaja  DATETIME2 NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.VehiculoDocumentos') AND name = N'IX_VehiculoDocumentos_Vehiculo_Tipo')
    CREATE INDEX IX_VehiculoDocumentos_Vehiculo_Tipo ON dbo.VehiculoDocumentos (VehiculoId, Tipo, FechaVencimiento) WHERE DadoDeBaja = 0;
GO

CREATE OR ALTER PROCEDURE dbo.sp_VehiculoDocumento_Create
    @VehiculoId INT,
    @Tipo NVARCHAR(40),
    @FechaVencimiento DATE,
    @Observaciones NVARCHAR(300) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM dbo.Vehiculos WHERE VehiculoId = @VehiculoId)
        THROW 53054, N'El vehículo indicado no existe.', 1;
    SET @Tipo = NULLIF(LTRIM(RTRIM(@Tipo)), N'');
    IF @Tipo IS NULL THROW 53055, N'Debe indicar el tipo de documento.', 1;
    IF @FechaVencimiento IS NULL THROW 53430, N'Debe indicar la fecha de vencimiento del documento.', 1;
    IF EXISTS (SELECT 1 FROM dbo.VehiculoDocumentos
               WHERE VehiculoId = @VehiculoId AND Tipo = @Tipo AND FechaVencimiento = @FechaVencimiento AND DadoDeBaja = 0)
        THROW 53431, N'Ese vehículo ya tiene un documento de ese tipo con esa fecha de vencimiento.', 1;
    INSERT INTO dbo.VehiculoDocumentos (VehiculoId, Tipo, FechaVencimiento, Observaciones)
    VALUES (@VehiculoId, @Tipo, @FechaVencimiento, NULLIF(LTRIM(RTRIM(@Observaciones)), N''));
    SELECT CAST(SCOPE_IDENTITY() AS INT) AS DocumentoId;
END;
GO

/* Documentos (no dados de baja) con su vigencia: el más reciente de cada vehículo y tipo es el vigente. */
CREATE OR ALTER PROCEDURE dbo.sp_VehiculoDocumento_List
    @VehiculoId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT d.DocumentoId, d.VehiculoId, v.Placa AS VehiculoPlaca, d.Tipo, d.FechaVencimiento,
           ISNULL(d.Observaciones, N'') AS Observaciones, d.FechaRegistro,
           CAST(CASE WHEN d.Tipo = N'Otro' OR d.Orden = 1 THEN 1 ELSE 0 END AS BIT) AS EsVigente,
           DATEDIFF(DAY, CAST(SYSDATETIME() AS DATE), d.FechaVencimiento) AS DiasRestantes
    FROM (
        SELECT x.*, ROW_NUMBER() OVER (PARTITION BY x.VehiculoId, x.Tipo ORDER BY x.FechaVencimiento DESC, x.DocumentoId DESC) AS Orden
        FROM dbo.VehiculoDocumentos x
        WHERE x.DadoDeBaja = 0
    ) d
    INNER JOIN dbo.Vehiculos v ON v.VehiculoId = d.VehiculoId
    WHERE @VehiculoId IS NULL OR d.VehiculoId = @VehiculoId
    ORDER BY v.Placa, d.Tipo, d.FechaVencimiento DESC, d.DocumentoId DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_VehiculoDocumento_GetById
    @DocumentoId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT d.DocumentoId, d.VehiculoId, v.Placa AS VehiculoPlaca, d.Tipo, d.FechaVencimiento, ISNULL(d.Observaciones, N'') AS Observaciones
    FROM dbo.VehiculoDocumentos d
    INNER JOIN dbo.Vehiculos v ON v.VehiculoId = d.VehiculoId
    WHERE d.DocumentoId = @DocumentoId AND d.DadoDeBaja = 0;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_VehiculoDocumento_Update
    @DocumentoId INT,
    @Tipo NVARCHAR(40),
    @FechaVencimiento DATE,
    @Observaciones NVARCHAR(300) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @VehiculoId INT;
    SELECT @VehiculoId = VehiculoId FROM dbo.VehiculoDocumentos WHERE DocumentoId = @DocumentoId AND DadoDeBaja = 0;
    IF @VehiculoId IS NULL THROW 53432, N'No se encontró el documento indicado.', 1;
    SET @Tipo = NULLIF(LTRIM(RTRIM(@Tipo)), N'');
    IF @Tipo IS NULL THROW 53055, N'Debe indicar el tipo de documento.', 1;
    IF @FechaVencimiento IS NULL THROW 53430, N'Debe indicar la fecha de vencimiento del documento.', 1;
    IF EXISTS (SELECT 1 FROM dbo.VehiculoDocumentos
               WHERE VehiculoId = @VehiculoId AND Tipo = @Tipo AND FechaVencimiento = @FechaVencimiento
                 AND DadoDeBaja = 0 AND DocumentoId <> @DocumentoId)
        THROW 53431, N'Ese vehículo ya tiene un documento de ese tipo con esa fecha de vencimiento.', 1;

    UPDATE dbo.VehiculoDocumentos
    SET Tipo = @Tipo, FechaVencimiento = @FechaVencimiento, Observaciones = NULLIF(LTRIM(RTRIM(@Observaciones)), N'')
    WHERE DocumentoId = @DocumentoId;
END;
GO

/* Baja (no borra la fila): el documento deja de contarse en la lista y en las alertas. */
CREATE OR ALTER PROCEDURE dbo.sp_VehiculoDocumento_Baja
    @DocumentoId INT
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Tipo NVARCHAR(80), @Placa NVARCHAR(40);
    SELECT @Tipo = d.Tipo, @Placa = v.Placa
    FROM dbo.VehiculoDocumentos d INNER JOIN dbo.Vehiculos v ON v.VehiculoId = d.VehiculoId
    WHERE d.DocumentoId = @DocumentoId AND d.DadoDeBaja = 0;
    IF @Tipo IS NULL THROW 53432, N'No se encontró el documento indicado.', 1;

    UPDATE dbo.VehiculoDocumentos SET DadoDeBaja = 1, FechaBaja = SYSDATETIME() WHERE DocumentoId = @DocumentoId;
    SELECT @Tipo AS Tipo, @Placa AS VehiculoPlaca;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Flota_Alertas
    @DiasAviso INT = 15,
    @KmAviso   INT = 500
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Hoy DATE = CAST(SYSDATETIME() AS DATE);

    SELECT * FROM (
        -- Documentos legales vigentes (el más reciente de cada vehículo y tipo) por vencer o vencidos
        SELECT
            N'Documento' AS Categoria,
            v.Placa AS VehiculoPlaca,
            CONCAT(d.Tipo, N' vence el ', FORMAT(d.FechaVencimiento, 'dd/MM/yyyy')) AS Detalle,
            d.FechaVencimiento AS Fecha,
            DATEDIFF(DAY, @Hoy, d.FechaVencimiento) AS DiasRestantes,
            CASE WHEN d.FechaVencimiento < @Hoy THEN N'Vencido' ELSE N'PorVencer' END AS Severidad
        FROM (
            SELECT x.*, ROW_NUMBER() OVER (PARTITION BY x.VehiculoId, x.Tipo ORDER BY x.FechaVencimiento DESC, x.DocumentoId DESC) AS Orden
            FROM dbo.VehiculoDocumentos x
            WHERE x.DadoDeBaja = 0
        ) d
        INNER JOIN dbo.Vehiculos v ON v.VehiculoId = d.VehiculoId
        WHERE (d.Tipo = N'Otro' OR d.Orden = 1)
          AND DATEDIFF(DAY, @Hoy, d.FechaVencimiento) <= @DiasAviso

        UNION ALL

        -- Mantenimientos programados o en proceso, por fecha (menos la correctiva en proceso, que va aparte)
        SELECT
            N'Mantenimiento',
            v.Placa,
            CONCAT(m.Tipo, CASE m.Estado WHEN N'EnProceso' THEN N' en proceso: ' ELSE N' programado: ' END, m.Descripcion),
            m.FechaProgramada,
            DATEDIFF(DAY, @Hoy, m.FechaProgramada),
            CASE WHEN m.FechaProgramada < @Hoy THEN N'Vencido' ELSE N'PorVencer' END
        FROM dbo.OrdenesMantenimiento m
        INNER JOIN dbo.Vehiculos v ON v.VehiculoId = m.VehiculoId
        WHERE m.Estado IN (N'Programada', N'EnProceso')
          AND NOT (m.Tipo = N'Correctivo' AND m.Estado = N'EnProceso')
          AND m.FechaProgramada IS NOT NULL
          AND DATEDIFF(DAY, @Hoy, m.FechaProgramada) <= @DiasAviso

        UNION ALL

        -- Correctivo en proceso: el vehículo está en reparación (atrasado si su fecha programada ya pasó)
        SELECT
            N'Mantenimiento',
            v.Placa,
            CONCAT(N'Vehículo en reparación (correctivo en proceso): ', m.Descripcion),
            m.FechaProgramada,
            CASE WHEN m.FechaProgramada IS NULL THEN NULL ELSE DATEDIFF(DAY, @Hoy, m.FechaProgramada) END,
            CASE WHEN m.FechaProgramada < @Hoy THEN N'Vencido' ELSE N'EnCurso' END
        FROM dbo.OrdenesMantenimiento m
        INNER JOIN dbo.Vehiculos v ON v.VehiculoId = m.VehiculoId
        WHERE m.Tipo = N'Correctivo' AND m.Estado = N'EnProceso'

        UNION ALL

        -- Correctivo programado sin fecha: pendiente de iniciar
        SELECT
            N'Mantenimiento',
            v.Placa,
            CONCAT(N'Correctivo pendiente de iniciar: ', m.Descripcion),
            NULL,
            NULL,
            N'Pendiente'
        FROM dbo.OrdenesMantenimiento m
        INNER JOIN dbo.Vehiculos v ON v.VehiculoId = m.VehiculoId
        WHERE m.Tipo = N'Correctivo' AND m.Estado = N'Programada' AND m.FechaProgramada IS NULL

        UNION ALL

        -- Preventivos por desgaste (odómetro del vehículo): vencido al llegar, por vencer a @KmAviso km
        SELECT
            N'Mantenimiento',
            v.Placa,
            CASE WHEN v.KilometrajeActual >= m.KilometrajeProximo
                 THEN CONCAT(N'Preventivo por kilometraje (', m.KilometrajeProximo, N' km, actual ', v.KilometrajeActual, N' km): ', m.Descripcion)
                 ELSE CONCAT(N'Preventivo por kilometraje (faltan ', m.KilometrajeProximo - v.KilometrajeActual, N' km; próximo a los ', m.KilometrajeProximo, N' km, actual ', v.KilometrajeActual, N' km): ', m.Descripcion)
            END,
            NULL,
            NULL,
            CASE WHEN v.KilometrajeActual >= m.KilometrajeProximo THEN N'Vencido' ELSE N'PorVencer' END
        FROM dbo.OrdenesMantenimiento m
        INNER JOIN dbo.Vehiculos v ON v.VehiculoId = m.VehiculoId
        WHERE m.Tipo = N'Preventivo' AND m.Estado IN (N'Programada', N'EnProceso')
          AND m.KilometrajeProximo IS NOT NULL
          AND v.KilometrajeActual >= m.KilometrajeProximo - @KmAviso
    ) AS Alertas
    ORDER BY CASE Severidad WHEN N'Vencido' THEN 0 WHEN N'PorVencer' THEN 1 WHEN N'EnCurso' THEN 2 ELSE 3 END, DiasRestantes;
END;
GO

IF XACT_STATE()<>1 THROW 56883,N'La transacción 0288 no está disponible.',1;
DECLARE @MigrationSha256 NVARCHAR(128)=N'$(MigrationSha256)';
IF LEN(@MigrationSha256)<>64 OR @MigrationSha256 LIKE N'%[^0-9A-Fa-f]%' THROW 56884,N'SHA-256 inválido para 0288.',1;
INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes)
VALUES(N'0288_documentos_alertas_flota',N'0288_documentos_alertas_flota.sql',UPPER(@MigrationSha256),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),
       N'G2-17, G2-20: lista, edición y baja de documentos de flota; alertas solo del documento vigente; alertas de mantenimiento en proceso, correctivos y kilometraje próximo.');
COMMIT TRANSACTION;
GO
