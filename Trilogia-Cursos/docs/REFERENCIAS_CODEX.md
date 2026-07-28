# Referencias actuales para Codex

Este es el índice operativo vigente. El código y el último `origin/main` prevalecen sobre notas históricas.

## Estado al 23 de julio de 2026

- Rama de integración: `codex/p0-saneamiento-integracion-total-20260722`, basada en el último `origin/main`.
- Los cuatro `appsettings` compartidos están sanitizados; la configuración funcional no vive en Git.
- La credencial SMTP detectada debe revocarse o rotarse fuera del repositorio. La limpieza del historial requiere coordinación y no se ejecuta automáticamente.
- CI separa secretos, SQL, build y tests; `final-gate` consolida el resultado.
- Las migraciones incrementales vigentes son 0001–0011. En Azure DEV se verificaron 0007–0011 como aplicadas; esto no demuestra que 0002–0006 se hayan ejecutado ni que el historial completo sea consecutivo.
- Sprint 4 de Danny está implementado y sus objetos/permisos 0007–0011 fueron verificados en Azure. Falta login con una contraseña de prueba autorizada y QA funcional autenticado.
- El diagnóstico de autenticación está en `docs/diagnostico-login-azure-sprint4-20260724.md`: cuenta y procedimiento son correctos; la credencial presentada no coincide con el valor directo almacenado y no se modificaron contraseñas.
- Chat, evidencia privada, checkout/promociones y garantías requieren aplicar migraciones y efectuar QA de entorno antes de declararlos operativos.
- La protección de `main` está activa y documentada en `docs/configuracion-proteccion-main.md`.
- Las validaciones Azure documentadas anteriormente son evidencia histórica y no validan esta rama.

## Corrección PR #115 — 27 de julio de 2026

- La ruta de integración sigue siendo `codex/integracion-sprint4-danny-david-final`
  en Draft. Las correcciones de combos, correo y hash se documentan en
  `docs/sprint4-integracion-danny-david.md`.
- 0012 no está aplicada en Azure DEV. Su ejecución exige BACPAC, ejecutor único,
  `scripts/database/Invoke-Migration0012.ps1` y el verify de solo lectura.
- Las métricas SQL no son intercambiables: el alcance focalizado `database/` y
  el alcance CI recursivo `database` + `database_Esteban` se reportan por separado.
- `database/00_todo_en_uno.sql` no tiene diferencia con la base de PR #115 y
  nunca se ejecutó durante la integración.
- El ejecutor de 0012 distingue `Entra` (`sqlcmd -G`) de `Windows`
  (`sqlcmd -E`), transmite `MigrationSha256=<SHA-256>` como una sola variable y
  no acepta contraseñas. Su prueba sin conexión está en
  `scripts/database/Test-InvokeMigration0012.ps1`.
- La evidencia LocalDB de 0012 incluye la consulta directa de detalle público
  de un combo antes/después de inactivar, reactivar y dejar el componente sin
  stock. Azure DEV sigue sin modificaciones; BACPAC y QA autenticado pendientes.

## Índice por tema

- Seguridad y configuración: `docs/credenciales-configuracion-segura.md`, `SECURITY.md`, `docs/incidente-seguridad-credencial-smtp-20260722.md`.
- CI y rama principal: `.github/workflows/ci-security-build.yml`, `docs/configuracion-proteccion-main.md`.
- SQL: `database/migrations/README.md`, `docs/inventario-sql-y-migraciones.md`.
- Trazabilidad funcional: `docs/matriz-historias-estado.md`, `docs/resumen-final-proyecto.md`.
- QA: `docs/qa-final.md`, `docs/api-pruebas-manuales.md`.
- Sprint 4 Danny: `docs/sprint4-danny-cu201-cu202-cu221-cu222-cu223.md`.
- Cliente/pedidos: `docs/portal-cliente-pedidos.md`.
- API: `docs/api-endpoints.md`, `docs/api-auth-futura.md`.
- Azure histórico: `docs/azure-despliegue-final-qa.md` y guías relacionadas.

## Regla de lectura

1. Leer este índice.
2. Inspeccionar los archivos actuales del módulo.
3. Leer solo la documentación relacionada.
4. No tratar prompts, ramas personales ni QA histórico como fuente de verdad.
