# AGENTS.md — Desarrollo_Trilogia / Trilogia-Cursos

## Identidad y fuente de verdad

Aplicación universitaria .NET 9 MVC + API + SQL Server para DistribuidoraJJ / Licorera La Bodega.

- MVC: `Proyecto_Final`
- API: `Proyecto_FinalAPI`
- Solución: `Proyecto_Final.slnx`
- SQL: `database/`, `database/migrations/` y archivo histórico `database_Esteban/`
- Documentación: `docs/`

La fuente de verdad es el último `origin/main`. Todo cambio se hace en una rama `codex/*` o de funcionalidad, mediante PR y con CI verde. No usar ramas personales antiguas como base.

## Reglas no negociables

1. Nunca versionar credenciales, cadenas de conexión funcionales, contraseñas SMTP, tokens, perfiles de publicación ni datos personales.
2. Los `appsettings*.json` compartidos son plantillas sanitizadas. La configuración real llega por variables de entorno, User Secrets o App Service Configuration.
3. No agregar `bin/`, `obj/`, `.vs/`, ZIP, BACPAC, backups, `.env`, `App_Data` ni archivos cargados por usuarios.
4. Inspeccionar el código vigente antes de editar y preservar cambios ajenos.
5. Toda operación basada en sesión debe comprobar rol/permiso, pertenencia del recurso y antiforgery cuando modifica estado.
6. No devolver `ex.Message` al navegador. Registrar el detalle con `ILogger` y mostrar un mensaje genérico.
7. No confiar en IDs, rutas físicas, roles o precios enviados por el cliente.
8. Mantener commits lógicos. No hacer merge directo ni automático a `main`.
9. No reescribir historial compartido ni hacer force-push sin coordinación expresa.

## SQL y migraciones

- Toda evolución nueva va en `database/migrations/` y sigue el orden documentado en su `README.md`.
- No ejecutar baselines, `Fase*.sql`, `00_todo_en_uno.sql`, `DistribuidoraJJ_DB.sql` ni `database_Esteban/` sobre una base compartida.
- No incluir `USE`, secretos, nombres de servidor ni datos demo en migraciones.
- Preferir cambios aditivos, idempotencia, `XACT_ABORT`, transacciones y rollback documentado.
- Una migración aplicada no se modifica: se crea la siguiente.
- Validar sintaxis recursivamente; la validación no ejecuta SQL ni sustituye una prueba en base desechable.

## Convenciones vigentes

- Tabla de permisos: `PerfilPermisos`.
- Asignación: `UsuarioAsignacionId`, `UsuarioAsignacionNombre`.
- Pedidos offline CU-072: `PedidoOfflineGuid`, canal `Venta móvil offline`.
- El asistente se describe como: **Asistente conversacional basado en reglas e interpretación de intenciones**.
- El chat usa servicios especializados; no agregar nueva lógica de chat a `AdminDbService`.
- Evidencias de entrega se almacenan fuera de `wwwroot` mediante `IEvidenceStorageService`.

## Validación mínima antes de publicar

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/security/Test-RepositorySecrets.ps1
dotnet restore Proyecto_Final.slnx
dotnet build Proyecto_Final.slnx --configuration Release --no-restore
dotnet test Proyecto_Final.slnx --configuration Release --no-build
dotnet run --project tools/SqlSyntaxValidator/SqlSyntaxValidator.csproj -- database database_Esteban
git diff --check
```

Además, revisar archivos nuevos/rastreados y documentar cualquier prueba de entorno que no haya podido ejecutarse.

## Documentación inicial

Leer primero `docs/REFERENCIAS_CODEX.md`; después, solo los documentos relacionados con el trabajo. La documentación histórica de Azure acredita ejecuciones anteriores, no valida automáticamente el código actual.
