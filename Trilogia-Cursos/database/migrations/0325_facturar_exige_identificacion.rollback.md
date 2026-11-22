# Rollback de 0325

## Qué introduce

- Vuelve a crear tres procedimientos con la misma definición vigente y una sola validación nueva: facturar un
  pedido sin `Pedidos.IdentificacionCliente` se rechaza con «Falta la identificación del cliente para facturar.»
  - `dbo.sp_Admin_GenerateInvoiceFromOrder` (definición de 0012): error 50906.
  - `dbo.sp_Seller_CreateOrder` (definición de 0255): error 50718; el pedido completo se revierte.
  - `dbo.sp_Manager_ApproveOrder` (definición de `cu098_retencion_autorizacion_pedidos.sql`): error 50737.
- No cambia tablas ni datos. Las facturas existentes no se tocan.

## Revertir

Ejecutar de nuevo los `CREATE OR ALTER PROCEDURE` de esos tres procedimientos desde los archivos de origen
(0012, 0255 y cu098) y quitar el registro del ledger:

```sql
DELETE FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0325_facturar_exige_identificacion';
```

Si se revierte la base, el sitio y la API siguen funcionando: solo dejan de mostrar el mensaje propio.
