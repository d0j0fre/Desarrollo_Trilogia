# Rollback de 0252

## Qué introduce

- Reemplaza, en nueve columnas de texto de negocio (`Productos.Nombre`,
  `Productos.Descripcion`, `MovimientosInventario.ProductoNombre`,
  `MovimientosInventario.Motivo`, `FacturaDetalle.ProductoNombre`,
  `Usuarios.NombreCompleto`, `Usuarios.Direccion`, `Pedidos.DireccionEntrega`,
  `Pedidos.Observaciones`), los pares de doble codificación UTF-8 («Ã±» → «ñ»,
  «Ã©» → «é», «Â¿» → «¿»…) por la letra correcta.
- No toca montos, estados, fechas ni otras columnas. El número de reemplazos
  queda en `Notes` del ledger.

## Revertir

No hay vuelta atrás automática: el texto dañado no tiene valor de negocio y la
migración no guarda el valor anterior. Si hiciera falta el estado previo, se
obtiene del BACKUP/BACPAC tomado antes de aplicarla (paso 4 del flujo del
README). Para permitir reaplicarla, borrar la fila del ledger:

```sql
DELETE FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0252_reparar_texto_mal_codificado';
```
