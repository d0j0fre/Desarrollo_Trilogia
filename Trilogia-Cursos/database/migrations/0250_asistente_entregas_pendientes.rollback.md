# Rollback de 0250

## Qué introduce

- Reemplaza `dbo.sp_Asistente_Metricas` (procedimiento de solo lectura del
  asistente, cu310). Solo cambia el cálculo de `EntregasPendientes`
  (BUG-26-04): ahora son las paradas `Pendiente` o `EnRuta` de las rutas en
  estado `Despachada`, igual que la columna "Pendientes" del listado de rutas.
- No cambia tablas, datos ni procedimientos de rutas.

## Revertir

Volver a la definición de cu310 (cuenta solo las paradas `Pendiente` de
cualquier ruta) y borrar la fila del ledger. cu310 solo contiene ese
procedimiento de lectura (`CREATE OR ALTER`), así que reaplicarlo no toca
nada más:

```powershell
sqlcmd -S <instancia> -d <base> -E -I -b -i Trilogia-Cursos/database_Esteban/cu310_asistente_metricas.sql
```

```sql
DELETE FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0250_asistente_entregas_pendientes';
```

El código de la aplicación no depende de la versión: lee las mismas columnas.
