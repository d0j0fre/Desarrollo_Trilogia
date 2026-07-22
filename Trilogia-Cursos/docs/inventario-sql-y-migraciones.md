# Inventario SQL y ruta de evolución

## Fuentes activas

- `database/migrations/`: única ruta permitida para evoluciones nuevas y aditivas.
- `database/`: bootstrap histórico y parches anteriores; requiere revisión caso por caso.
- `database_Esteban/`: material histórico y de referencia; no debe ejecutarse en DEV compartido.
- `tools/SqlSyntaxValidator/`: validación recursiva UTF-8 y de sintaxis T-SQL con ScriptDom.

Al 22 de julio de 2026 se validan 70 archivos SQL y 821 lotes. Todos los `cu*.sql` de `database_Esteban/` quedaron sin un `USE` fijo; esto no cambia su condición de archivos históricos no autorizados para despliegue.

## Baselines y scripts destructivos

`database/00_todo_en_uno.sql`, `database/DistribuidoraJJ_DB.sql` y los archivos `Fase*.sql` son baselines. Pueden crear bases, eliminar o reconstruir objetos y cargar datos demostrativos. No son migraciones y nunca deben ejecutarse sobre DEV compartido o producción.

## Evolución vigente

Las migraciones 0001–0006 cubren ledger, chat seguro, departamentos de chat, evidencia privada, checkout/promociones atómicos y garantías. Deben aplicarse en orden, con BACPAC verificado, un único ejecutor, ventana controlada y evidencia posterior.

## Reglas de autoría

- No introducir `USE`, nombres de servidor, credenciales, seeds ni datos personales.
- Usar `SET XACT_ABORT ON`, cambios aditivos, nombres de constraints e idempotencia.
- No modificar una migración ya aplicada; crear la siguiente versión.
- Registrar SHA-256 real, ejecutor, ambiente, resultado y verificaciones en `SchemaMigrationHistory`.
- Validar antes del PR con:

```powershell
dotnet run --project tools/SqlSyntaxValidator/SqlSyntaxValidator.csproj -- database database_Esteban
```
