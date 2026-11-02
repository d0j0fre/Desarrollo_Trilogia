# Rollback de 0257

## Qué introduce

- Reemplaza `dbo.sp_Promociones_List` (solo lectura): una promoción `Activa`
  cuya `FechaFin` es anterior a hoy se devuelve y se filtra como `Vencida`.
- No cambia datos: `Promociones.Estado` queda como está y el carrito sigue
  decidiendo por fecha (`sp_Promociones_Vigentes`).

## Revertir

Reaplicar la definición de `sp_Promociones_List` de
`0214_promotion_unit_limit.sql` (`CREATE OR ALTER`) y borrar la fila del ledger:

```sql
DELETE FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0257_promociones_vencidas_en_lista';
```

La lista vuelve a mostrar como «Activa» las promociones vencidas y el filtro
«Vencida» vuelve a quedar vacío.
