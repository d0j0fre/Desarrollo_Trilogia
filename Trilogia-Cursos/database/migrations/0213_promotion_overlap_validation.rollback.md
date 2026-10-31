# Rollback de 0213 — validación de promociones traslapadas (BUG-17-01)

## Alcance del cambio

Redefine `dbo.sp_Promociones_Create` y `dbo.sp_Promociones_Update` con
`CREATE OR ALTER` (conserva `object_id` y permisos). No toca tablas ni filas.

## Procedimiento de reversión

Volver a ejecutar las definiciones anteriores de esos dos procedimientos
(bloques `4.1` y `4.2` de `database_Esteban/cu171_174_promociones.sql`,
idénticos a los de la base antes de 0213) y marcar el ledger:

```sql
UPDATE dbo.SchemaMigrationHistory
SET Status = N'RolledBack',
    Notes = CONCAT(Notes, N' | Revertida el ', CONVERT(NVARCHAR(30), SYSUTCDATETIME(), 126), N' UTC.')
WHERE MigrationId = N'0213_promotion_overlap_validation';
```

Si ya se aplicó 0214 (que vuelve a definir estos procedimientos), revertirla
primero. Tras revertir, la aplicación vuelve a permitir promociones
traslapadas; el controlador sigue funcionando (el mensaje específico de
traslape simplemente deja de aparecer).

## Restauración de BACPAC

No procede.
