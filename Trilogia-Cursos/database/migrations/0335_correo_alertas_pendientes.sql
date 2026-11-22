SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

/* fin-correo (CU-202-01, CU-154-02): correos de alertas que no se enviaban nunca.

   1) Alertas documentales (CU-202). sp_DocumentAlert_Generate (0008) solo devuelve las alertas NUEVAS; si el correo
      aun no estaba configurado (o DocumentAlerts__EmailEnabled estaba apagado) cuando se creo la alerta, el correo
      no se enviaba nunca despues. Se agrega:
        - dbo.sp_DocumentAlert_PendingNotifications: alertas activas del vencimiento vigente del documento cuyo
          correo nunca se envio con exito (sin intento o con intento fallido), una por documento (la de menor umbral)
          y con vencimiento de hace 30 dias o menos, para no inundar de historia vieja al activar el correo.
        - sp_DocumentAlert_RegisterNotification pasa a actualizar un intento "Fallido" previo (antes solo insertaba
          y un fallo quedaba para siempre sin reintento).

   2) Documentos de flota (CU-154). No habia ningun correo para Marchamo, Seguro, etc.: solo la alerta en pantalla.
      Se agrega la tabla dbo.VehiculoDocumentoAvisos (un aviso por documento y umbral de dias, idempotente) y
        - dbo.sp_FlotaDocumentos_AvisosPendientes @Umbrales: primer resultado = documentos de flota por vencer
          (vigente mas reciente de cada vehiculo y tipo, igual que sp_Flota_Alertas) cuyo aviso aun no se envio;
          segundo resultado = destinatarios: usuarios activos con correo del perfil Administrador o de un perfil con
          el permiso FLOTA_MANTENIMIENTO (Administracion y Logistica).
        - dbo.sp_FlotaDocumentos_RegistrarAviso: registra el intento (Enviado/Fallido, reintenta los fallidos).

   Solo agrega objetos; no cambia datos existentes. Rollback en 0335_correo_alertas_pendientes.rollback.md. */
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL THROW 58500,N'Falta el ledger 0001.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0335_correo_alertas_pendientes' AND Status=N'Applied')
    THROW 58501,N'0335 ya figura aplicada.',1;
IF OBJECT_ID(N'dbo.AlertasDocumento',N'U') IS NULL OR OBJECT_ID(N'dbo.NotificacionesAlertaDocumento',N'U') IS NULL
    THROW 58502,N'0335 necesita las alertas documentales de 0008. Aplicar 0008 primero.',1;
IF OBJECT_ID(N'dbo.VehiculoDocumentos',N'U') IS NULL OR OBJECT_ID(N'dbo.sp_Flota_Alertas',N'P') IS NULL
    THROW 58503,N'0335 necesita los documentos de flota (0288). Aplicar 0288 primero.',1;

BEGIN TRANSACTION;
GO

IF OBJECT_ID(N'dbo.VehiculoDocumentoAvisos',N'U') IS NULL
BEGIN
    CREATE TABLE dbo.VehiculoDocumentoAvisos
    (
        AvisoId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_VehiculoDocumentoAvisos PRIMARY KEY,
        DocumentoId INT NOT NULL,
        UmbralDias INT NOT NULL,
        Canal NVARCHAR(20) NOT NULL CONSTRAINT DF_VehiculoDocumentoAvisos_Canal DEFAULT N'Email',
        EstadoEnvio NVARCHAR(20) NOT NULL,
        Destinatarios INT NOT NULL CONSTRAINT DF_VehiculoDocumentoAvisos_Dest DEFAULT 0,
        ErrorTecnicoResumido NVARCHAR(100) NULL,
        FechaIntentoUtc DATETIME2(0) NOT NULL CONSTRAINT DF_VehiculoDocumentoAvisos_Fecha DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_VehiculoDocumentoAvisos_Documento FOREIGN KEY (DocumentoId) REFERENCES dbo.VehiculoDocumentos(DocumentoId),
        CONSTRAINT UQ_VehiculoDocumentoAvisos UNIQUE (DocumentoId, UmbralDias, Canal),
        CONSTRAINT CK_VehiculoDocumentoAvisos_Estado CHECK (EstadoEnvio IN (N'Enviado', N'Fallido'))
    );
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_DocumentAlert_PendingNotifications
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Hoy DATE = CAST(SYSDATETIME() AS DATE);

    SELECT q.AlertaId, q.DocumentoId, q.UmbralDias, q.Titulo, q.FechaVencimiento, q.Destinatario
    FROM
    (
        SELECT a.AlertaId, a.DocumentoId, a.UmbralDias, d.Titulo, a.FechaVencimiento,
               ISNULL(u.Correo, N'') AS Destinatario,
               ROW_NUMBER() OVER (PARTITION BY a.DocumentoId ORDER BY a.UmbralDias ASC, a.AlertaId DESC) AS Orden
        FROM dbo.AlertasDocumento a
        INNER JOIN dbo.DocumentosDigitales d ON d.DocumentoId = a.DocumentoId
        LEFT JOIN dbo.Usuarios u ON u.UsuarioId = d.ResponsableUsuarioId
        WHERE a.Estado = N'Activa'
          AND d.Activo = 1
          AND d.NoVence = 0
          AND a.FechaVencimiento = d.FechaVencimiento
          AND a.FechaVencimiento >= DATEADD(DAY, -30, @Hoy)
          AND NOT EXISTS (SELECT 1 FROM dbo.NotificacionesAlertaDocumento n
                          WHERE n.AlertaId = a.AlertaId AND n.Canal = N'Email' AND n.EstadoEnvio = N'Enviado')
    ) q
    WHERE q.Orden = 1
    ORDER BY q.AlertaId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_DocumentAlert_RegisterNotification
    @AlertaId INT, @Canal NVARCHAR(20), @Destinatario NVARCHAR(320), @EstadoEnvio NVARCHAR(20), @ErrorTecnicoResumido NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    -- Un intento fallido previo se actualiza (reintento); uno enviado no se toca (idempotente).
    UPDATE dbo.NotificacionesAlertaDocumento
    SET Destinatario = @Destinatario, EstadoEnvio = @EstadoEnvio, ErrorTecnicoResumido = @ErrorTecnicoResumido,
        FechaIntentoUtc = SYSUTCDATETIME()
    WHERE AlertaId = @AlertaId AND Canal = @Canal AND EstadoEnvio = N'Fallido';

    IF @@ROWCOUNT = 0
       AND NOT EXISTS (SELECT 1 FROM dbo.NotificacionesAlertaDocumento WHERE AlertaId = @AlertaId AND Canal = @Canal)
        INSERT dbo.NotificacionesAlertaDocumento (AlertaId, Canal, Destinatario, EstadoEnvio, ErrorTecnicoResumido)
        VALUES (@AlertaId, @Canal, @Destinatario, @EstadoEnvio, @ErrorTecnicoResumido);
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_FlotaDocumentos_AvisosPendientes
    @Umbrales NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Hoy DATE = CAST(SYSDATETIME() AS DATE);
    DECLARE @t TABLE (Dias INT PRIMARY KEY);
    INSERT @t (Dias)
    SELECT DISTINCT TRY_CONVERT(INT, value) FROM STRING_SPLIT(@Umbrales, N',') WHERE TRY_CONVERT(INT, value) BETWEEN 0 AND 365;

    -- 1) Documentos de flota con un umbral alcanzado y sin aviso enviado (vigente mas reciente por vehiculo y tipo).
    SELECT d.DocumentoId, v.Placa AS VehiculoPlaca, d.Tipo, d.FechaVencimiento,
           DATEDIFF(DAY, @Hoy, d.FechaVencimiento) AS DiasRestantes, umbral.Dias AS UmbralDias
    FROM
    (
        SELECT x.DocumentoId, x.VehiculoId, x.Tipo, x.FechaVencimiento,
               ROW_NUMBER() OVER (PARTITION BY x.VehiculoId, x.Tipo ORDER BY x.FechaVencimiento DESC, x.DocumentoId DESC) AS Orden
        FROM dbo.VehiculoDocumentos x
        WHERE x.DadoDeBaja = 0
    ) d
    INNER JOIN dbo.Vehiculos v ON v.VehiculoId = d.VehiculoId
    CROSS APPLY (SELECT MIN(t.Dias) AS Dias FROM @t t WHERE t.Dias >= DATEDIFF(DAY, @Hoy, d.FechaVencimiento)) umbral
    WHERE (d.Tipo = N'Otro' OR d.Orden = 1)
      AND umbral.Dias IS NOT NULL
      AND DATEDIFF(DAY, @Hoy, d.FechaVencimiento) >= -30
      AND NOT EXISTS (SELECT 1 FROM dbo.VehiculoDocumentoAvisos a
                      WHERE a.DocumentoId = d.DocumentoId AND a.UmbralDias = umbral.Dias
                        AND a.Canal = N'Email' AND a.EstadoEnvio = N'Enviado')
    ORDER BY d.FechaVencimiento, d.DocumentoId;

    -- 2) Destinatarios: Administrador y perfiles que gestionan la flota (permiso FLOTA_MANTENIMIENTO).
    SELECT u.UsuarioId, u.NombreCompleto, LTRIM(RTRIM(u.Correo)) AS Correo
    FROM dbo.Usuarios u
    INNER JOIN dbo.Perfiles p ON p.PerfilId = u.PerfilId
    WHERE ISNULL(u.Activo, 1) = 1
      AND NULLIF(LTRIM(RTRIM(u.Correo)), N'') IS NOT NULL
      AND (p.Nombre = N'Administrador'
           OR EXISTS (SELECT 1
                      FROM dbo.PerfilPermisos pp
                      INNER JOIN dbo.Permisos pe ON pe.PermisoId = pp.PermisoId
                      WHERE pp.PerfilId = p.PerfilId AND pe.Codigo = N'FLOTA_MANTENIMIENTO' AND pe.Activo = 1))
    ORDER BY u.UsuarioId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_FlotaDocumentos_RegistrarAviso
    @DocumentoId INT, @UmbralDias INT, @EstadoEnvio NVARCHAR(20), @Destinatarios INT, @ErrorTecnicoResumido NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF @EstadoEnvio NOT IN (N'Enviado', N'Fallido') THROW 54131, N'El estado del aviso no es válido.', 1;

    UPDATE dbo.VehiculoDocumentoAvisos
    SET EstadoEnvio = @EstadoEnvio, Destinatarios = @Destinatarios, ErrorTecnicoResumido = @ErrorTecnicoResumido,
        FechaIntentoUtc = SYSUTCDATETIME()
    WHERE DocumentoId = @DocumentoId AND UmbralDias = @UmbralDias AND Canal = N'Email';

    IF @@ROWCOUNT = 0
        INSERT dbo.VehiculoDocumentoAvisos (DocumentoId, UmbralDias, Canal, EstadoEnvio, Destinatarios, ErrorTecnicoResumido)
        VALUES (@DocumentoId, @UmbralDias, N'Email', @EstadoEnvio, @Destinatarios, @ErrorTecnicoResumido);
END;
GO

IF XACT_STATE()<>1 THROW 58504,N'La transacción 0335 no está disponible.',1;
DECLARE @MigrationSha256 NVARCHAR(128)=N'$(MigrationSha256)';
IF LEN(@MigrationSha256)<>64 OR @MigrationSha256 LIKE N'%[^0-9A-Fa-f]%' THROW 58505,N'SHA-256 inválido para 0335.',1;
INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes)
VALUES(N'0335_correo_alertas_pendientes',N'0335_correo_alertas_pendientes.sql',UPPER(@MigrationSha256),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),
       N'fin-correo (CU-202-01, CU-154-02): sp_DocumentAlert_PendingNotifications y reintento de fallidos; tabla VehiculoDocumentoAvisos, sp_FlotaDocumentos_AvisosPendientes y sp_FlotaDocumentos_RegistrarAviso (correo de documentos de flota a Administracion y Logistica). Aditiva.');
COMMIT TRANSACTION;
GO
