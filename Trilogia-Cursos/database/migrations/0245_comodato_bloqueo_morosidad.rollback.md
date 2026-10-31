# Rollback de 0245

## Qué introduce

- `dbo.fn_Comodato_DeudaVencida(@DiasPlazo)`: deuda vencida por cliente a
  partir de `dbo.ClienteCreditoMovimientos` (abonos aplicados a los cargos más
  antiguos; vencido = más de 30 días) (BUG-16-02).
- `dbo.sp_Comodato_ClientesParaAsignar` y `dbo.sp_Comodato_EvaluarMorosidad`.
- `dbo.sp_Comodato_Asignar`: exige un cliente registrado y activo (error
  53087), toma el nombre de `dbo.Usuarios` y rechaza clientes con saldo
  vencido (error 53086). Mismos parámetros y resultado.

No cambia tablas ni procedimientos de crédito o clientes.

## Alinear con la regla de BUG-06-02

Si la sesión de crédito publica su propia regla de deuda vencida, reemplazar
solo el cuerpo de `dbo.fn_Comodato_DeudaVencida` manteniendo las columnas
`UsuarioId`, `SaldoActual`, `SaldoVencido` y `CargoVencidoMasAntiguo`.

## Revertir

Primero desplegar la MVC anterior (cliente en texto libre). Luego volver a la
definición de `database_Esteban/cu162_163_164_comodatos.sql`:

```sql
CREATE OR ALTER PROCEDURE dbo.sp_Comodato_Asignar
    @ActivoId              INT,
    @ClienteUsuarioId      INT           = NULL,
    @ClienteNombre         NVARCHAR(150),
    @ClienteIdentificacion NVARCHAR(50)  = NULL,
    @Ubicacion             NVARCHAR(200) = NULL,
    @FechaAsignacion       DATE          = NULL,
    @CondicionEntrega      NVARCHAR(300) = NULL,
    @Observaciones         NVARCHAR(300) = NULL,
    @UsuarioId             INT           = NULL,
    @Nombre                NVARCHAR(150) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET @ClienteNombre = NULLIF(LTRIM(RTRIM(@ClienteNombre)), N'');
    IF @ClienteNombre IS NULL THROW 53080, 'Debe indicar el cliente que recibe el equipo.', 1;

    DECLARE @Estado NVARCHAR(20) = (SELECT Estado FROM dbo.Activos WHERE ActivoId = @ActivoId AND Activo = 1);
    IF @Estado IS NULL           THROW 53081, 'El activo no existe o está inactivo.', 1;
    IF @Estado <> N'Disponible'  THROW 53082, 'El activo no está disponible para comodato.', 1;

    BEGIN TRANSACTION;
    BEGIN TRY
        INSERT INTO dbo.Comodatos
            (ActivoId, ClienteUsuarioId, ClienteNombre, ClienteIdentificacion, Ubicacion, FechaAsignacion,
             CondicionEntrega, Observaciones, RegistradoPorUsuarioId, RegistradoPorNombre)
        VALUES
            (@ActivoId, @ClienteUsuarioId, @ClienteNombre, NULLIF(LTRIM(RTRIM(@ClienteIdentificacion)), N''),
             NULLIF(LTRIM(RTRIM(@Ubicacion)), N''), ISNULL(@FechaAsignacion, CAST(SYSDATETIME() AS DATE)),
             NULLIF(LTRIM(RTRIM(@CondicionEntrega)), N''), NULLIF(LTRIM(RTRIM(@Observaciones)), N''), @UsuarioId, @Nombre);

        DECLARE @Id INT = SCOPE_IDENTITY();

        UPDATE dbo.Activos
        SET Estado = N'Prestado', ClientePrestamo = @ClienteNombre, FechaActualizacion = SYSDATETIME()
        WHERE ActivoId = @ActivoId;

        COMMIT;
        SELECT @Id AS ComodatoId;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK; THROW;
    END CATCH
END;
GO
DROP PROCEDURE IF EXISTS dbo.sp_Comodato_EvaluarMorosidad;
DROP PROCEDURE IF EXISTS dbo.sp_Comodato_ClientesParaAsignar;
DROP FUNCTION IF EXISTS dbo.fn_Comodato_DeudaVencida;
DELETE FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0245_comodato_bloqueo_morosidad';
```
