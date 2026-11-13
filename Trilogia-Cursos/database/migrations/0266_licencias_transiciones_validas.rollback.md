# Reversión de 0266

Restaurar la definición anterior de `dbo.sp_Admin_UpdateEmployeeLeaveRequestStatus` (sin validación de transiciones):

```sql
CREATE OR ALTER PROCEDURE dbo.sp_Admin_UpdateEmployeeLeaveRequestStatus
    @SolicitudId INT, @Estado NVARCHAR(30), @RespuestaAdmin NVARCHAR(500) = NULL,
    @UsuarioRespuestaId INT = NULL, @UsuarioRespuestaNombre NVARCHAR(150) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM dbo.EmpleadoSolicitudesTiempoLibre WHERE SolicitudId = @SolicitudId)
        THROW 51006, 'No se encontró la solicitud indicada.', 1;
    UPDATE dbo.EmpleadoSolicitudesTiempoLibre
    SET Estado = @Estado, RespuestaAdmin = @RespuestaAdmin, UsuarioRespuestaId = @UsuarioRespuestaId,
        UsuarioRespuestaNombre = @UsuarioRespuestaNombre, FechaRespuesta = SYSDATETIME()
    WHERE SolicitudId = @SolicitudId;
END;
```

No cambia tablas ni datos. Marcar la fila `0266_licencias_transiciones_validas` como `RolledBack` en `dbo.SchemaMigrationHistory`.
