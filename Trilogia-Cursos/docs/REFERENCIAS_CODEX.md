# Referencias actuales para Codex

Este archivo es el indice principal de documentacion vigente del proyecto.

Antes de iniciar un bloque nuevo, leer primero este archivo y luego abrir unicamente los documentos especificos que apliquen al bloque.

## Estado actual importante

- La Fase 0 quedo cerrada con commits locales.
- Los `appsettings` fueron limpiados y usan placeholders.
- No se debe volver a poner secretos ni conexiones locales personales en Git.
- `cu101_sp_auth_validate_user_password.sql` fue ejecutado manualmente en SSMS.
- `cu102_sp_seller_get_my_orders.sql` fue ejecutado manualmente en SSMS.
- Azure SQL DEV quedo creado e importado desde el BACPAC final `DistribuidoraJJ_DB_Azure_Final_20260702.bacpac`.
- MVC/API local fueron probados contra Azure SQL DEV mediante variable de entorno.
- Swagger/OpenAPI quedo habilitado en `Proyecto_FinalAPI` para el Bloque 8.
- `Proyecto_FinalAPI` tiene endpoints de diagnostico `GET /` y `GET /health`.
- El proyecto todavia usa contrasenas en texto plano; la migracion a hash queda para una fase futura.
- La publicacion de API/MVC en Azure App Service todavia esta pendiente.

## Documentos vigentes

### API

- `docs/api-endpoints.md`: endpoints actuales del API.
- `docs/api-pruebas-manuales.md`: checklist de pruebas manuales del API.
- `docs/api-auth-futura.md`: analisis para autenticacion futura con JWT u otro mecanismo.

### Base de datos y SQL

- `docs/azure-sql-dev-companeros.md`: guia para que los companeros usen Azure SQL DEV compartido sin tocar appsettings ni subir secretos.
- `docs/inventario-sql-directo.md`: inventario de SQL directo en C# y ruta de migracion gradual a procedimientos almacenados.
- `docs/resumen-mejoras-seguridad.md`: resumen de mejoras de seguridad, SQL, permisos, reportes y arquitectura.

### Cliente, pedidos y facturacion

- `docs/portal-cliente-pedidos.md`: reglas del portal cliente, historial, cancelacion y comprobantes.
- `docs/resumen-final-proyecto.md`: resumen general del estado funcional del proyecto.

### QA y pruebas

- `docs/qa-final.md`: checklist general de pruebas.
- `docs/seguridad-sprint3-refuerzo.md`: refuerzos de seguridad posteriores al Sprint 3.
- `docs/acceso-red-local.md`: guia para probar desde celular u otra computadora en red local.

## Regla para ahorrar tokens

No leer todos los documentos de `docs` por defecto.

Para cada bloque:

1. Leer este indice.
2. Leer solo los documentos relacionados con el bloque.
3. No usar prompts antiguos como fuente de verdad.
4. No asumir que documentacion historica esta actualizada si contradice el codigo o scripts actuales.

## Referencias recomendadas por tipo de bloque

### Si el bloque es API

Leer:

- `docs/api-endpoints.md`
- `docs/api-pruebas-manuales.md`
- `docs/api-auth-futura.md`

### Si el bloque es base de datos, SQL o refactor SOLID

Leer:

- `docs/inventario-sql-directo.md`
- `docs/resumen-mejoras-seguridad.md`
- scripts en `database/`

### Si el bloque es portal cliente, pedidos o facturacion

Leer:

- `docs/portal-cliente-pedidos.md`
- `docs/resumen-final-proyecto.md`
- `docs/qa-final.md`

### Si el bloque es pruebas finales

Leer:

- `docs/qa-final.md`
- `docs/api-pruebas-manuales.md`
- `docs/acceso-red-local.md`

### Si el bloque es Azure

Leer:

- Este indice.
- `docs/azure-sql-dev-companeros.md`
- `docs/resumen-final-proyecto.md`
- `docs/api-endpoints.md`
- revisar `appsettings` y `Program.cs` directamente.
