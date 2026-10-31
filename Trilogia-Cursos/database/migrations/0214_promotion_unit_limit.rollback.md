# Rollback de 0214 — límite de unidades de promociones (BUG-17-02)

## Alcance del cambio

- Agrega `dbo.Promociones.LimiteUnidades INT NULL` (con `CK_Promo_LimiteUnidades`)
  y `dbo.Promociones.UnidadesConsumidas INT NOT NULL` (con `DF_Promo_UnidadesConsumidas`).
- Redefine `sp_Promociones_Create`, `sp_Promociones_Update`,
  `sp_Promociones_GetById` y `sp_Promociones_List`.
- Crea el disparador `dbo.TR_PromocionAplicaciones_LimiteUnidades`.

No modifica `sp_Store_CreateOrderWithPromotions`.

## Procedimiento de reversión

1. Revertir primero el código de aplicación: la versión con BUG-17-02 envía
   `@LimiteUnidades`.
2. Ejecutar en una ventana controlada:

```sql
SET XACT_ABORT ON;
BEGIN TRANSACTION;

DROP TRIGGER IF EXISTS dbo.TR_PromocionAplicaciones_LimiteUnidades;

-- Volver a ejecutar aquí los bloques CREATE OR ALTER de sp_Promociones_Create y
-- sp_Promociones_Update de 0213, y los de sp_Promociones_GetById y
-- sp_Promociones_List de database_Esteban/cu171_174_promociones.sql (4.4 y 4.5).

ALTER TABLE dbo.Promociones DROP CONSTRAINT CK_Promo_LimiteUnidades;
ALTER TABLE dbo.Promociones DROP COLUMN LimiteUnidades;
ALTER TABLE dbo.Promociones DROP CONSTRAINT DF_Promo_UnidadesConsumidas;
ALTER TABLE dbo.Promociones DROP COLUMN UnidadesConsumidas;

UPDATE dbo.SchemaMigrationHistory
SET Status = N'RolledBack',
    Notes = CONCAT(Notes, N' | Revertida el ', CONVERT(NVARCHAR(30), SYSUTCDATETIME(), 126), N' UTC.')
WHERE MigrationId = N'0214_promotion_unit_limit';

COMMIT TRANSACTION;
```

Las promociones que el disparador inactivó por límite conservan su estado
`Inactiva` y su motivo; reactivarlas, si se desea, es una decisión de negocio.

## Restauración de BACPAC

No procede.
