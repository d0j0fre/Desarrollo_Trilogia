# Rollback de 0282

## Qué introduce

- Reemplaza `dbo.sp_Rutas_Secuenciar` (BUG-25-01): si alguna parada pendiente
  no tiene latitud y longitud, no modifica nada y lanza el error 54012
  («N de M punto(s) de la ruta no tienen coordenada…»). Con todas las
  coordenadas se comporta igual que en la 0281 (prioridad y luego cercanía).
- No cambia tablas ni datos.

## Revertir

Reaplicar la definición de `sp_Rutas_Secuenciar` de
`0281_rutas_prioridad_secuenciacion.sql` (`CREATE OR ALTER`) y borrar la fila del ledger:

```sql
DELETE FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0282_rutas_secuenciar_exige_ubicacion';
```

Secuenciar vuelve a procesar la ruta con paradas sin coordenada (las deja al
final y avisa después).
