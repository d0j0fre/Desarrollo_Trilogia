# Credenciales y configuración segura

## Política

Contraseñas, connection strings, tokens, claves API, contraseñas SMTP, cookies, perfiles de publicación, BACPAC y capturas con valores sensibles nunca se versionan ni se copian a issues, PR, logs o documentación.

Los archivos compartidos `Proyecto_Final/appsettings*.json` y `Proyecto_FinalAPI/appsettings*.json` son plantillas sanitizadas. Valores vacíos son intencionales.

## Fuentes admitidas

- Desarrollo: .NET User Secrets o variables de entorno.
- Azure: App Service Configuration; Key Vault puede incorporarse posteriormente.
- Evidencias: `EvidenceStorage__RootPath`, siempre fuera de `wwwroot`. **Debe configurarse con la MISMA carpeta en el sitio web y en la API móvil**: la app del chofer sube fotos y firmas a la API (`POST api/mobile/v1/driver/deliveries/{id}/evidence`) y el sitio las lee de esa carpeta. Sin el valor, la API rechaza la subida (503) y la app conserva el archivo en el teléfono, en vez de guardarlo donde la web no lo vería. Si la web y la API se despliegan como App Services distintos, la carpeta debe ser un almacenamiento compartido (ver `docs/plan-app-movil-flutter.md` §9.1).
- Documentos y comprobantes: `PrivateStorage__RootPath`, persistente y siempre fuera de `wwwroot`.
- Correo de alertas: `DocumentAlerts__EmailEnabled=false` hasta completar SMTP; los umbrales no son secretos.

Ejemplo local sin valores reales:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<connection-string>" --project Proyecto_Final
dotnet user-secrets set "ConfiguracionCorreo:Remitente" "<smtp-user>" --project Proyecto_Final
dotnet user-secrets set "ConfiguracionCorreo:Contrasenna" "<smtp-app-password>" --project Proyecto_Final
```

No pegar el resultado de `dotnet user-secrets list` en terminales compartidas, documentos ni comentarios.

## Fallo seguro

Una configuración obligatoria ausente debe producir un error claro sobre la clave faltante, sin revelar valores, rutas internas o credenciales. Ningún secreto se escribe con `Console.WriteLine` o logging estructurado.

## Incidente SMTP 2026-07-22

El árbol actual fue sanitizado y el escáner reforzado. Esto no invalida una credencial ya expuesta. El propietario debe:

1. Revocar o rotar la credencial SMTP fuera de GitHub.
2. Actualizar User Secrets/App Service Configuration por canal seguro.
3. Revisar actividad relevante.
4. Coordinar una posible limpieza del historial siguiendo `docs/incidente-seguridad-credencial-smtp-20260722.md`.

No se hará force-push ni reescritura de historial sin coordinación con todos los colaboradores.

## Cuentas de QA

- Crear cuentas temporales por entorno y compartirlas solo por canal privado.
- No reutilizar semillas públicas ni credenciales locales en Azure.
- Rotar o desactivar las cuentas al finalizar.
- No almacenar contraseñas de prueba en el repositorio.

## Control antes de un PR

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/security/Test-RepositorySecrets.ps1
git diff --check
git status --short
```

El escáner revisa archivos rastreados y omite placeholders inequívocamente no funcionales. La revisión debe incluir JSON, YAML, Markdown, SQL, JavaScript y perfiles de publicación.
