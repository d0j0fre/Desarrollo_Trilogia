# Rollback de 0243

## Qué introduce

- `dbo.sp_Comodato_Devolver`: cuando el destino es `Mantenimiento` (equipo
  con avería) deja un aviso `ComodatoAveria` en `dbo.NotificacionesUsuario`
  para taller (perfiles con `FLOTA_MANTENIMIENTO` o `ACTIVOS_GESTIONAR`) y
  cobros (perfil `Crédito y Cobro` o con `CREDITOS_MOVIMIENTOS`) (BUG-16-03).
  Parámetros, validaciones y resultado sin cambio.

No cambia tablas.

## Revertir

La aplicación funciona con cualquiera de las dos versiones. Para volver a la
definición de `database_Esteban/cu162_163_164_comodatos.sql`:

```sql
CREATE OR ALTER PROCEDURE dbo.sp_Comodato_Devolver
    @ComodatoId          INT,
    @Destino             NVARCHAR(20),
    @CondicionDevolucion NVARCHAR(300) = NULL,
    @FechaDevolucion     DATE          = NULL,
    @UsuarioId           INT           = NULL,
    @Nombre              NVARCHAR(150) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF @Destino NOT IN (N'Inventario', N'Mantenimiento') THROW 53083, 'Destino inválido (Inventario o Mantenimiento).', 1;

    DECLARE @ActivoId INT, @EstadoCom NVARCHAR(20);
    SELECT @ActivoId = ActivoId, @EstadoCom = Estado FROM dbo.Comodatos WHERE ComodatoId = @ComodatoId;
    IF @ActivoId IS NULL          THROW 53084, 'No se encontró el comodato.', 1;
    IF @EstadoCom <> N'Activo'     THROW 53085, 'El comodato ya fue cerrado.', 1;

    BEGIN TRANSACTION;
    BEGIN TRY
        UPDATE dbo.Comodatos
        SET Estado = CASE WHEN @Destino = N'Mantenimiento' THEN N'Retirado' ELSE N'Devuelto' END,
            DestinoDevolucion = @Destino,
            CondicionDevolucion = NULLIF(LTRIM(RTRIM(@CondicionDevolucion)), N''),
            FechaDevolucion = ISNULL(@FechaDevolucion, CAST(SYSDATETIME() AS DATE)),
            FechaActualizacion = SYSDATETIME()
        WHERE ComodatoId = @ComodatoId;

        UPDATE dbo.Activos
        SET Estado = CASE WHEN @Destino = N'Mantenimiento' THEN N'Mantenimiento' ELSE N'Disponible' END,
            ClientePrestamo = NULL, FechaActualizacion = SYSDATETIME()
        WHERE ActivoId = @ActivoId;

        COMMIT;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK; THROW;
    END CATCH
END;
GO
DELETE FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0243_comodato_alerta_averia';
```

Los avisos ya generados quedan en la bandeja; para retirarlos:
`DELETE FROM dbo.NotificacionesUsuario WHERE Tipo = N'ComodatoAveria';`
