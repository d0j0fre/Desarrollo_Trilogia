# Rollback de 0281

## Qué introduce

- Columna `dbo.RutaPedidos.Prioridad` (`TINYINT NOT NULL`, valor por omisión 3,
  `CHECK` de 1 a 3): 1 = Urgente, 2 = Alta, 3 = Normal. Las paradas existentes
  quedan en 3.
- Reemplaza `sp_Rutas_Create` (acepta `prioridad` por pedido), `sp_Rutas_GetOrders`
  (devuelve `Prioridad` como última columna), `sp_Rutas_GuardarSecuencia` (acepta
  y valida `prioridad`, error 54002) y `sp_Rutas_Secuenciar` / `sp_Rutas_Recalcular`
  (prioridad primero y, dentro de cada nivel, vecino más cercano).

## Revertir

1. Desplegar primero el sitio web anterior (el actual manda `prioridad` en el
   JSON y lee la columna `Prioridad`; un procedimiento viejo ignora el JSON extra,
   pero la pantalla mostraría la prioridad sin efecto).
2. Reaplicar las definiciones anteriores (`CREATE OR ALTER`) de
   `sp_Rutas_Create` (la de `0280_rutas_cliente_inactivo.sql`), de
   `sp_Rutas_GetOrders`, `sp_Rutas_GuardarSecuencia`, `sp_Rutas_Secuenciar` y
   `sp_Rutas_Recalcular` (las de `database_Esteban/cu302_rutas_coordenadas_cliente.sql`
   y `cu251_252_253_rutas_inteligentes.sql`).
3. Quitar la columna y la fila del ledger:

```sql
ALTER TABLE dbo.RutaPedidos DROP CONSTRAINT CK_RutaPedidos_Prioridad;
ALTER TABLE dbo.RutaPedidos DROP CONSTRAINT DF_RutaPedidos_Prioridad;
ALTER TABLE dbo.RutaPedidos DROP COLUMN Prioridad;
DELETE FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0281_rutas_prioridad_secuenciacion';
```

Se pierde la prioridad que se haya elegido en las rutas ya creadas; el orden de
las paradas (`Secuencia`) no cambia.
