SET NOCOUNT ON;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0301_trato_de_usted_procedimientos' AND Status=N'Applied') THROW 58016,N'La migracion 0301 no esta registrada.',1;
DECLARE @t TABLE(Objeto SYSNAME, Malo NVARCHAR(100), Bueno NVARCHAR(100));
INSERT @t VALUES
 (N'sp_Movil_Inventario_RegistrarMovimiento', N'Indic' + NCHAR(225) + N' el motivo', N'Indique el motivo de la salida'),
 (N'sp_Movil_Producto_CambiarEstado',         N'Indic' + NCHAR(225) + N' si el producto', N'Indique si el producto'),
 (N'sp_Movil_Rutas_Reasignar',                N'Indic' + NCHAR(225) + N' el motivo', N'Indique el motivo de la reasignaci'),
 (N'TR_Rutas_NotificarChofer',                N'Te reasignaron', N'Le reasignaron una ruta'),
 (N'TR_Rutas_NotificarChofer',                N'a tu cargo', N'a su cargo');
IF EXISTS(SELECT 1 FROM @t t WHERE OBJECT_ID(N'dbo.' + t.Objeto) IS NULL) THROW 58017,N'Falta un objeto corregido por 0301.',1;
IF EXISTS(SELECT 1 FROM @t t WHERE CHARINDEX(t.Malo COLLATE Latin1_General_BIN2, OBJECT_DEFINITION(OBJECT_ID(N'dbo.' + t.Objeto)) COLLATE Latin1_General_BIN2)>0) THROW 58018,N'Sigue habiendo texto en voseo o tuteo.',1;
IF EXISTS(SELECT 1 FROM @t t WHERE CHARINDEX(t.Bueno COLLATE Latin1_General_BIN2, OBJECT_DEFINITION(OBJECT_ID(N'dbo.' + t.Objeto)) COLLATE Latin1_General_BIN2)=0) THROW 58019,N'Falta el texto en trato de usted.',1;
PRINT N'Verificacion 0301 completada.';
