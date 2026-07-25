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
