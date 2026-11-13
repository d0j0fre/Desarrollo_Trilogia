SET NOCOUNT ON;

/* Verificacion de 0288. Deja la base como estaba: la prueba corre en una transaccion que se revierte. */
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0288_documentos_alertas_flota' AND Status = N'Applied')
    THROW 56930, N'0288 no figura aplicada en el ledger.', 1;
IF COL_LENGTH(N'dbo.VehiculoDocumentos', N'DadoDeBaja') IS NULL
   OR OBJECT_ID(N'dbo.sp_VehiculoDocumento_List', N'P') IS NULL OR OBJECT_ID(N'dbo.sp_VehiculoDocumento_Update', N'P') IS NULL
   OR OBJECT_ID(N'dbo.sp_VehiculoDocumento_Baja', N'P') IS NULL OR OBJECT_ID(N'dbo.sp_VehiculoDocumento_GetById', N'P') IS NULL
    THROW 56931, N'Faltan la columna DadoDeBaja o los procedimientos de la 0288.', 1;

BEGIN TRANSACTION;
DECLARE @V INT, @Viejo INT, @Nuevo INT, @Hoy DATE = CAST(SYSDATETIME() AS DATE);
INSERT dbo.Vehiculos (Placa, Descripcion, Capacidad, Activo, KilometrajeActual) VALUES (N'VERIFY-0288', N'Verificacion 0288', 10, 1, 9800);
SET @V = SCOPE_IDENTITY();
DECLARE @D TABLE (Id INT);
DECLARE @A TABLE (Categoria NVARCHAR(40), Placa NVARCHAR(40), Detalle NVARCHAR(400), Fecha DATE, Dias INT, Severidad NVARCHAR(20));

-- G2-17: el seguro vencido se reemplaza por el renovado y solo este ultimo cuenta en las alertas.
INSERT @D EXEC dbo.sp_VehiculoDocumento_Create @VehiculoId = @V, @Tipo = N'Seguro', @FechaVencimiento = '2020-01-31';
SET @Viejo = (SELECT TOP (1) Id FROM @D);
INSERT @A EXEC dbo.sp_Flota_Alertas @DiasAviso = 15;
IF NOT EXISTS (SELECT 1 FROM @A WHERE Placa = N'VERIFY-0288' AND Categoria = N'Documento' AND Severidad = N'Vencido')
BEGIN ROLLBACK TRANSACTION; THROW 56932, N'El seguro vencido no genero alerta.', 1; END;
DELETE @D;
INSERT @D EXEC dbo.sp_VehiculoDocumento_Create @VehiculoId = @V, @Tipo = N'Seguro', @FechaVencimiento = '2999-12-31';
SET @Nuevo = (SELECT TOP (1) Id FROM @D);
DELETE @A;
INSERT @A EXEC dbo.sp_Flota_Alertas @DiasAviso = 15;
IF EXISTS (SELECT 1 FROM @A WHERE Placa = N'VERIFY-0288' AND Categoria = N'Documento')
BEGIN ROLLBACK TRANSACTION; THROW 56933, N'El documento viejo sigue en las alertas despues de renovarlo.', 1; END;

-- La lista marca cual es el vigente.
DECLARE @L TABLE (DocumentoId INT, VehiculoId INT, Placa NVARCHAR(40), Tipo NVARCHAR(80), Vence DATE, Obs NVARCHAR(600), Registro DATETIME2, EsVigente BIT, Dias INT);
INSERT @L EXEC dbo.sp_VehiculoDocumento_List @VehiculoId = @V;
IF (SELECT COUNT(*) FROM @L) <> 2 OR (SELECT EsVigente FROM @L WHERE DocumentoId = @Nuevo) <> 1 OR (SELECT EsVigente FROM @L WHERE DocumentoId = @Viejo) <> 0
BEGIN ROLLBACK TRANSACTION; THROW 56934, N'La lista no marca bien el documento vigente.', 1; END;

-- Se corrige una fecha mal digitada y se da de baja un documento.
EXEC dbo.sp_VehiculoDocumento_Update @DocumentoId = @Nuevo, @Tipo = N'Seguro', @FechaVencimiento = '2000-01-01', @Observaciones = N'Corregido';
DELETE @A;
INSERT @A EXEC dbo.sp_Flota_Alertas @DiasAviso = 15;
IF NOT EXISTS (SELECT 1 FROM @A WHERE Placa = N'VERIFY-0288' AND Categoria = N'Documento' AND Fecha = '2020-01-31')
BEGIN ROLLBACK TRANSACTION; THROW 56935, N'La edicion del vigente no cambio cual alerta se muestra.', 1; END;
EXEC dbo.sp_VehiculoDocumento_Baja @DocumentoId = @Nuevo;
EXEC dbo.sp_VehiculoDocumento_Baja @DocumentoId = @Viejo;
DELETE @A;
INSERT @A EXEC dbo.sp_Flota_Alertas @DiasAviso = 15;
IF EXISTS (SELECT 1 FROM @A WHERE Placa = N'VERIFY-0288' AND Categoria = N'Documento')
BEGIN ROLLBACK TRANSACTION; THROW 56936, N'Un documento dado de baja sigue en las alertas.', 1; END;
BEGIN TRY
    EXEC dbo.sp_VehiculoDocumento_Baja @DocumentoId = @Viejo;
    THROW 56937, N'Se dio de baja dos veces el mismo documento.', 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() = 56937 BEGIN IF XACT_STATE() <> 0 ROLLBACK TRANSACTION; THROW; END;
    IF ERROR_NUMBER() <> 53432 BEGIN IF XACT_STATE() <> 0 ROLLBACK TRANSACTION; THROW; END;
END CATCH;

-- G2-20: ordenes en proceso atrasadas, correctiva en proceso, correctiva pendiente y kilometraje proximo.
INSERT dbo.OrdenesMantenimiento (VehiculoId, Tipo, Descripcion, Estado, FechaProgramada) VALUES (@V, N'Preventivo', N'VERIF preventivo atrasado', N'EnProceso', DATEADD(DAY, -5, @Hoy));
INSERT dbo.OrdenesMantenimiento (VehiculoId, Tipo, Descripcion, Estado) VALUES (@V, N'Correctivo', N'VERIF correctivo en proceso', N'EnProceso');
INSERT dbo.OrdenesMantenimiento (VehiculoId, Tipo, Descripcion, Estado) VALUES (@V, N'Correctivo', N'VERIF correctivo pendiente', N'Programada');
INSERT dbo.OrdenesMantenimiento (VehiculoId, Tipo, Descripcion, Estado, KilometrajeProximo) VALUES (@V, N'Preventivo', N'VERIF km cercano', N'Programada', 10000);
INSERT dbo.OrdenesMantenimiento (VehiculoId, Tipo, Descripcion, Estado, KilometrajeProximo) VALUES (@V, N'Preventivo', N'VERIF km lejano', N'Programada', 20000);
DELETE @A;
INSERT @A EXEC dbo.sp_Flota_Alertas @DiasAviso = 15, @KmAviso = 500;
IF NOT EXISTS (SELECT 1 FROM @A WHERE Placa = N'VERIFY-0288' AND Detalle LIKE N'%VERIF preventivo atrasado%' AND Severidad = N'Vencido')
   OR NOT EXISTS (SELECT 1 FROM @A WHERE Placa = N'VERIFY-0288' AND Detalle LIKE N'%VERIF correctivo en proceso%' AND Severidad = N'EnCurso')
   OR NOT EXISTS (SELECT 1 FROM @A WHERE Placa = N'VERIFY-0288' AND Detalle LIKE N'%VERIF correctivo pendiente%' AND Severidad = N'Pendiente')
   OR NOT EXISTS (SELECT 1 FROM @A WHERE Placa = N'VERIFY-0288' AND Detalle LIKE N'%VERIF km cercano%' AND Severidad = N'PorVencer')
BEGIN ROLLBACK TRANSACTION; THROW 56938, N'Faltan alertas de mantenimiento en proceso, correctivo o kilometraje proximo.', 1; END;
IF EXISTS (SELECT 1 FROM @A WHERE Detalle LIKE N'%VERIF km lejano%')
BEGIN ROLLBACK TRANSACTION; THROW 56939, N'Se aviso un kilometraje todavia lejano.', 1; END;

ROLLBACK TRANSACTION;
PRINT N'Verificación 0288 completada.';
