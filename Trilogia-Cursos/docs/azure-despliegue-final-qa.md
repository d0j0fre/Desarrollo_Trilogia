# Despliegue final Azure y QA

## 1. Objetivo del documento

Este documento resume el estado final del despliegue Azure DEV del proyecto `Trilogia-Cursos / Licorera La Bodega / DistribuidoraJJ`, los recursos creados, las variables requeridas y el checklist de QA aprobado o pendiente.

No contiene contrasenas reales, connection strings completas ni secretos.

## Actualización 11/11/2026: versión corregida publicada

Las secciones 2 a 12 describen el despliegue original (julio, grupo `rg-trilogia-cursos-dev`). El
ambiente vigente es otro y desde el 11/11/2026 tiene publicada la rama `correcciones-finales`
(91 errores del reporte QA y 76 defectos del manual).

| Recurso | Valor |
| ------- | ----- |
| Suscripción | Azure for Students (cuenta del administrador Entra del servidor SQL) |
| Resource Group | `rg-trilogia-cursos-free` |
| Azure SQL | `sql-trilogia-free-cr01` / base `DistribuidoraJJ_DB` (serverless, oferta gratuita, se pausa sola) |
| Plan | `asp-trilogia-cursos-dev`, Linux, F1, Central US, `DOTNETCORE\|9.0` |
| API | `https://api-trilogia-free-cr01.azurewebsites.net` (`/health`, `/api/productos`) |
| MVC | `https://web-trilogia-free-cr01.azurewebsites.net` |

Pasos que se siguieron (repetibles para una próxima versión):

1. **Copia de seguridad** sin costo en Azure: `sqlpackage /Action:Export` con token de `az account
   get-access-token --resource https://database.windows.net/` hacia un `.bacpac` local. La copia previa
   a esta publicación es `Azure_DistribuidoraJJ_DB_antes_correcciones_20261111.bacpac` (se guarda
   fuera del repositorio porque contiene datos de usuarios).
2. **Ensayo**: importar ese `.bacpac` en una instancia LocalDB (requiere `sp_configure 'contained
   database authentication', 1`) y correr ahí `tools/azure/aplicar_migraciones.ps1`.
3. **Migraciones en Azure**: el mismo script con `-Azure`. La base tenía hasta la 0028; se aplicaron
   65: `0023_supervisor_rrhh_permisos`, `0023_toggle_product_status_returns_state` y 0200–0313.
   La `0269` se aplica al final porque exige la `0310`. La `0310` no se puede revertir: las
   contraseñas quedan solo como hash (los 58 usuarios conservan su clave; se comprobó que ninguno
   quedó sin hash).
4. **Publicación**: `dotnet publish -c Release`, comprimir con `tar -a -c -f x.zip *` (el
   `Compress-Archive` de PowerShell 5.1 usa `\` y el servidor Linux falla con `rsync ... Invalid
   argument`) y `az webapp deploy --type zip --clean true --restart true`.
5. **Revisión rápida**: `/health` de ambos (200), `/api/productos` (200), inicio y login del MVC
   (200, con el texto corregido «Regístrese»).

Avisos de verificación que se aceptaron (la migración quedó aplicada; el aviso viene de datos que
solo existen en Azure):

- `0251`: «Hay un perfil del personal sin MOVIL_ACCESO». Son perfiles de prueba creados en Azure
  (`Auditor QA0710 Editado`, `Auditor QA 2`, `QA Rol temporal`, `QA Pagador`). No se modificaron.
- `0253`: la tabla de la verificación declara `TipoMovimiento NVARCHAR(20)` y en Azure existe el tipo
  `TransformacionEntrada` (21 caracteres). La pantalla no filtra por ese tipo; el listado funciona.

Variables agregadas el 11/11/2026: `PaySlips__PublicBaseUrl` (MVC) y `PasswordRecovery__PublicBaseUrl`
(API), ambas con la URL del MVC.

Pendiente en Azure:

- `ConfiguracionCorreo__Remitente` y `ConfiguracionCorreo__Contrasenna` no están en ninguno de los dos
  App Service: sin ellas no salen correos (recuperación de contraseña, comprobantes, boletas). Las
  debe cargar el dueño de la cuenta SMTP desde el portal.
- Probar a mano cada función contra Azure (verificación final del listado de errores).

## 2. Estado final del despliegue Azure

| Bloque | Estado |
| ------ | ------ |
| Bloque 5 - Azure SQL DEV e importacion BACPAC | Completado |
| Bloque 6 - Pruebas locales contra Azure SQL DEV | Completado |
| Bloque 7 - Guia para companeros | Completado |
| Bloque 8 - API publicada en Azure App Service | Completado |
| Bloque 9 - MVC publicado en Azure App Service | Completado |

Resultado general:

- Azure SQL DEV quedo importado y validado.
- API local y MVC local fueron probados contra Azure SQL DEV.
- API quedo publicada en Azure App Service y responde endpoints publicos.
- MVC quedo publicado en Azure App Service y consume la API publicada.
- MVC tambien usa Azure SQL DEV mediante `ConnectionStrings__DefaultConnection`.

## 3. Recursos Azure creados

| Recurso | Nombre / Valor |
| ------- | -------------- |
| Resource Group | `rg-trilogia-cursos-dev` |
| Region | `Central US` |
| Azure SQL Server | `sql-trilogia-cursos-dev-cr01` |
| Azure SQL FQDN | `sql-trilogia-cursos-dev-cr01.database.windows.net` |
| Azure SQL Database | `DistribuidoraJJ_DB_DEV` |
| Storage Account temporal | `sttrilogiadevcr01` |
| Container BACPAC | `bacpac` |
| BACPAC final usado | `DistribuidoraJJ_DB_Azure_Final_20260702.bacpac` |
| App Service Plan | `asp-trilogia-cursos-dev` |
| App Service API | `api-trilogia-cursos-dev-cr01` |
| App Service MVC | `web-trilogia-cursos-dev-cr01` |

## 4. URLs oficiales

| Servicio | URL |
| -------- | --- |
| API | `https://api-trilogia-cursos-dev-cr01-haefhbgrd8dvcvc5.centralus-01.azurewebsites.net/` |
| MVC | `https://web-trilogia-cursos-dev-cr01-bbeyeedbfjejcgau.centralus-01.azurewebsites.net/` |
| Swagger | `https://api-trilogia-cursos-dev-cr01-haefhbgrd8dvcvc5.centralus-01.azurewebsites.net/swagger` |
| Healthcheck | `https://api-trilogia-cursos-dev-cr01-haefhbgrd8dvcvc5.centralus-01.azurewebsites.net/health` |
| Productos API | `https://api-trilogia-cursos-dev-cr01-haefhbgrd8dvcvc5.centralus-01.azurewebsites.net/api/productos` |

## 5. Configuracion de Azure SQL DEV

| Elemento | Valor |
| -------- | ----- |
| Servidor | `sql-trilogia-cursos-dev-cr01.database.windows.net` |
| Base | `DistribuidoraJJ_DB_DEV` |
| Region | `Central US` |
| Plan usado | Azure SQL DEV validado desde Portal; revisar SKU exacto en Azure Portal si se requiere para entrega formal. |
| Firewall / IP | Acceso por reglas de firewall especificas. No abrir rangos amplios. |
| Acceso desde Azure | Habilitado para que API y MVC publicados conecten con la base. |

Datos validados despues de la importacion:

| Tabla / Modulo | Cantidad |
| -------------- | -------- |
| Usuarios | 27 |
| Productos | 25 |
| Pedidos | 47 |
| Facturas | 39 |

## 6. Configuracion del API App Service

| Elemento | Valor |
| -------- | ----- |
| App Service | `api-trilogia-cursos-dev-cr01` |
| Runtime | `.NET 9` |
| Sistema operativo | Windows |
| Plan | `asp-trilogia-cursos-dev` |
| SKU | Gratis F1 |
| Region | `Central US` |

Variables requeridas:

| Variable | Valor esperado | Sensible |
| -------- | -------------- | -------- |
| `ASPNETCORE_ENVIRONMENT` | `Staging` | No |
| `Swagger__Enabled` | `true` (habilitación explícita sólo para Azure DEV) | No |
| `PasswordRecovery__PublicBaseUrl` | URL HTTPS pública del MVC | No |
| `ConnectionStrings__DefaultConnection` | Connection string a Azure SQL DEV con `<sql-user>` y `<sql-password>` | Si |
| `ConfiguracionCorreo__Remitente` | Cuenta SMTP que envía la recuperación de contraseña | No |
| `ConfiguracionCorreo__Contrasenna` | Contraseña de aplicación SMTP (rotada tras el incidente del 2026-07-22) | Si |

Sin las dos variables de correo la API no puede enviar el enlace de recuperación y
ahora lo informa al usuario en lugar de confirmar la solicitud.

Endpoints validados:

- `GET /`
- `GET /health`
- `GET /health` incluye únicamente el SHA inmutable de compilación, cuando el artefacto fue construido por CI.
- `GET /swagger`
- `GET /swagger/v1/swagger.json`
- `GET /api/productos`

## 7. Configuracion del MVC App Service

| Elemento | Valor |
| -------- | ----- |
| App Service | `web-trilogia-cursos-dev-cr01` |
| Runtime | `.NET 9` |
| Sistema operativo | Windows |
| Plan | `asp-trilogia-cursos-dev` |
| SKU | Gratis F1 |
| Region | `Central US` |

Variables requeridas:

| Variable | Valor esperado | Sensible |
| -------- | -------------- | -------- |
| `ASPNETCORE_ENVIRONMENT` | `Production` | No |
| `ApiSettings__BaseUrl` | `https://api-trilogia-cursos-dev-cr01-haefhbgrd8dvcvc5.centralus-01.azurewebsites.net/` | No |
| `ConnectionStrings__DefaultConnection` | Connection string a Azure SQL DEV con `<sql-user>` y `<sql-password>` | Si |
| `ConfiguracionCorreo__Remitente` | Cuenta SMTP que envía consultas, comprobantes y boletas | No |
| `ConfiguracionCorreo__Contrasenna` | Contraseña de aplicación SMTP (rotada tras el incidente del 2026-07-22) | Si |
| `PaySlips__PublicBaseUrl` | URL HTTPS pública del MVC (por ejemplo `https://web-trilogia-cursos-dev-cr01.azurewebsites.net`); es la base del enlace al portal privado que lleva el correo de «Notificar» en Boletas de pago | No |

Sin `PaySlips__PublicBaseUrl` (o si no es HTTPS) la pantalla Personal → Boletas de pago no envía la
notificación y lo explica: «falta configurar la URL pública del sitio (PaySlips:PublicBaseUrl…)». En
local el valor sale de `appsettings.Development.json` (`https://localhost:7250`); no se guarda
ningún secreto en ese archivo. Sin las variables de correo la misma pantalla indica que el correo del
sistema no está configurado; la boleta permanece disponible en el portal en ambos casos.

Sin las variables de correo, Contacto, Facturación, Tienda y Venta móvil muestran
que no fue posible enviar el correo. Para pruebas locales sin SMTP se puede definir
`ConfiguracionCorreo__DirectorioRecogida` (MVC y API): los correos quedan como `.eml`
en esa carpeta y no se envían. No usarla en Azure.

Dependencias:

- El MVC consume el API publicado mediante `ApiSettings__BaseUrl`.
- El MVC tambien consulta directamente Azure SQL DEV mediante `ConnectionStrings__DefaultConnection`.
- MVC y API exponen `GET /health` con `status`, `service` y `build`; el valor `build` es el SHA inyectado por CI, no contiene secretos ni rutas locales.
- La referencia directa del MVC hacia `Proyecto_FinalAPI` fue removida para evitar el error `NETSDK1152` al publicar.
- Los servicios de correo necesarios para MVC quedaron locales al proyecto MVC.

## 8. Checklist QA aprobado

- [x] Azure SQL online.
- [x] SSMS conecta.
- [x] Stored Procedures principales validados.
- [x] API local contra Azure SQL.
- [x] MVC local contra Azure SQL.
- [x] API Azure `/health`.
- [x] API Azure `/swagger`.
- [x] API Azure `/api/productos`.
- [x] MVC Azure Home.
- [x] MVC Azure Login.
- [x] Login admin.
- [x] Login cliente.
- [x] Login vendedor/empleado.
- [x] Admin.
- [x] Shop.

## 9. Checklist QA pendiente o recomendado

- [ ] Checkout completo en Azure.
- [ ] Carrito completo en Azure.
- [ ] Facturacion completa en Azure.
- [ ] Inventario completo en Azure.
- [ ] Subida de imagenes en Azure.
- [ ] Recuperacion de contrasena / SMTP real.
- [ ] CORS si se detecta problema entre MVC y API.
- [ ] Pruebas con companeros desde otras IPs.

## 10. Riesgos conocidos

- El proyecto todavia usa contrasenas en texto plano.
- La contrasena SQL debe rotarse antes de compartir acceso externo si fue expuesta.
- SMTP real sigue pendiente.
- El Storage Account temporal `sttrilogiadevcr01` debe eliminarse cuando ya no se necesite.
- Uploads en `wwwroot` no son una solucion ideal a largo plazo en Azure.
- La sesion en memoria funciona para 1 instancia, pero no es ideal para escalado.
- `cu098` sigue pendiente y no debe ejecutarse sin bloque propio.

## 11. Reglas de operacion

- No modificar `appsettings` con secretos.
- No subir perfiles de publicacion.
- No subir archivos `.PublishSettings`.
- No subir archivos `.pubxml` con credenciales.
- No ejecutar scripts SQL sin aprobacion.
- No cambiar variables Azure sin documentarlo.
- No documentar contrasenas reales.
- Usar `<sql-user>` y `<sql-password>` cuando se necesite mostrar una connection string.

## 12. Proximos bloques recomendados

- Bloque 11 - QA completo en Azure.
- Bloque 12 - Seguridad pendiente.
- Bloque 14 - Baseline final de base de datos.
- Bloque 15 - Cierre final del proyecto.
