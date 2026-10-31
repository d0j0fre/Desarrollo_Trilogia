# Rollback de 0244

## Qué introduce

- `dbo.sp_Comodato_GetContrato`: datos de un comodato para emitir su contrato
  en PDF (BUG-16-01). Solo lectura. Ningún procedimiento existente cambia.

## Revertir

Primero desplegar la MVC sin la descarga del contrato (si no, el botón
"Contrato" respondería 503). Luego:

```sql
DROP PROCEDURE IF EXISTS dbo.sp_Comodato_GetContrato;
DELETE FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0244_comodato_contrato';
```
