# Rollback de 0240

## Qué introduce

- `dbo.sp_Comodato_HistorialPorActivo` devuelve además `CondicionEntrega` y
  `CondicionDevolucion` al final del resultado (BUG-16-05). Las columnas
  anteriores no cambian de posición. No toca tablas ni datos.

## Revertir

La aplicación anterior funciona con la versión nueva (lee solo las primeras
siete columnas). Para volver a la definición de
`database_Esteban/cu162_163_164_comodatos.sql`, primero desplegar la MVC sin la
columna de condición en el detalle del activo y luego:

```sql
CREATE OR ALTER PROCEDURE dbo.sp_Comodato_HistorialPorActivo
    @ActivoId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT ComodatoId, ClienteNombre, FechaAsignacion, FechaDevolucion, Estado,
           ISNULL(DestinoDevolucion, N'') AS DestinoDevolucion, ISNULL(Observaciones, N'') AS Observaciones
    FROM dbo.Comodatos WHERE ActivoId = @ActivoId ORDER BY FechaAsignacion DESC;
END;
GO
DELETE FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0240_comodato_historial_condicion';
```
