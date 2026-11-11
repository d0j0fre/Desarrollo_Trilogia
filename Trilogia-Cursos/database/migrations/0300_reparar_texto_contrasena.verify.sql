SET NOCOUNT ON;
IF OBJECT_ID(N'dbo.sp_Auth_UpdatePassword',N'P') IS NULL THROW 58006,N'Falta sp_Auth_UpdatePassword.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0300_reparar_texto_contrasena' AND Status=N'Applied') THROW 58007,N'La migracion 0300 no esta registrada.',1;
DECLARE @d NVARCHAR(MAX)=OBJECT_DEFINITION(OBJECT_ID(N'dbo.sp_Auth_UpdatePassword'));
IF CHARINDEX(NCHAR(195) COLLATE Latin1_General_BIN2, @d COLLATE Latin1_General_BIN2)>0 OR CHARINDEX(NCHAR(194) COLLATE Latin1_General_BIN2, @d COLLATE Latin1_General_BIN2)>0 THROW 58008,N'Sigue habiendo texto danado en sp_Auth_UpdatePassword.',1;
IF CHARINDEX((N'Credencial inv' + NCHAR(225) + N'lida.') COLLATE Latin1_General_BIN2, @d COLLATE Latin1_General_BIN2)=0 THROW 58009,N'Falta el texto correcto de credencial invalida.',1;
-- Ningun procedimiento ni disparador conserva pares de doble codificacion.
IF EXISTS(SELECT 1 FROM sys.sql_modules m JOIN sys.objects o ON o.object_id=m.object_id WHERE o.is_ms_shipped=0 AND (m.definition COLLATE Latin1_General_BIN2 LIKE N'%' + NCHAR(195) + NCHAR(161) + N'%' OR m.definition COLLATE Latin1_General_BIN2 LIKE N'%' + NCHAR(195) + NCHAR(179) + N'%' OR m.definition COLLATE Latin1_General_BIN2 LIKE N'%' + NCHAR(195) + NCHAR(177) + N'%'))
    THROW 58008,N'Quedan objetos con texto de doble codificacion.',1;
PRINT N'Verificacion 0300 completada.';
