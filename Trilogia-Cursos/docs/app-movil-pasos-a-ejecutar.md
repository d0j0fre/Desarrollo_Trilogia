# Aplicación móvil — Pasos a ejecutar

> ## Estado al 16 de septiembre de 2026
>
> **Los pasos 1 al 5 ya están ejecutados.** Quedan pendientes el 6, 7, 8 y 9,
> que son los que requieren decisiones o acciones de una persona.
>
> | # | Paso | Estado |
> |---|---|---|
> | 1 | Clave de firma de los tokens | ✅ Hecho (local y en Azure) |
> | 2 | Respaldo de la base | ✅ Verificado (PITR, 7 días) |
> | 3 | Migraciones 0024, 0025 y 0026 | ✅ Aplicadas en Azure DEV |
> | 4 | Verificación | ✅ En verde |
> | 5 | Configurar el servidor en Azure | ✅ Recreado y desplegado |
> | 6 | Keystore de firma del APK | ⬜ **Te toca a vos** |
> | 7 | Compilar el APK definitivo | ⬜ Depende del paso 6 |
> | 8 | Publicar la versión | ⬜ Depende del paso 7 |
> | 9 | Prueba con un chofer real | ⬜ **Te toca a vos** |
>
> ### Direcciones reales del sistema
>
> | Servicio | Dirección |
> |---|---|
> | API | `https://api-trilogia-free-cr01.azurewebsites.net` |
> | Sitio web | `https://web-trilogia-free-cr01.azurewebsites.net` |
> | Página de la app | `https://web-trilogia-free-cr01.azurewebsites.net/MobileApp` |
>
> Las direcciones que aparecían antes en la documentación
> (`api-trilogia-free-cr01`, `web-trilogia-free-cr01`) pertenecen a
> una suscripción de Azure que quedó **deshabilitada**: sus recursos existen
> pero están congelados en solo lectura y no se pueden volver a encender.
>
> El resto de este documento conserva el detalle completo de cada paso, porque
> sigue siendo la referencia para repetirlos en otro ambiente o para entender
> qué se hizo.


Todo lo que una persona tiene que hacer a mano para poner la aplicación móvil
en funcionamiento. El código ya está listo; esto es lo que **no** puede hacer
el código por sí solo.

Está en orden. Cada bloque dice quién lo hace, cuánto toma y cómo saber si
salió bien.

> **Nada de esto se ejecuta sin leer antes el paso completo.** Los bloques 3
> y 4 tocan la base de datos compartida y siguen el proceso de
> `database/migrations/README.md`: script revisado en PR, ejecutor designado,
> respaldo previo y registro en el ledger.

---

## Resumen de un vistazo

| # | Qué | Quién | Dónde | Reversible |
|---|---|---|---|---|
| 1 | Generar y guardar la clave de firma JWT | Cualquier desarrollador | Local + Azure | Sí |
| 2 | Respaldo de la base antes de migrar | Ejecutor designado | Portal de Azure | — |
| 3 | Aplicar migraciones 0024, 0025 y 0026 | Ejecutor designado | Azure SQL | Sí, con rollback documentado |
| 4 | Verificar las migraciones | Ejecutor designado | Azure SQL | — |
| 5 | Configurar la API en Azure | Quien administre Azure | App Service | Sí |
| 6 | Crear el keystore de firma del APK | Una sola persona, una sola vez | Local | **No** — respaldarlo |
| 7 | Compilar el APK | Cualquier desarrollador | Local o CI | Sí |
| 8 | Publicar la versión en el sistema | Administrador | Sistema web | Sí |
| 9 | Prueba en un teléfono real | Un chofer | Teléfono | — |

---

## Paso 1 — Clave de firma de los tokens

**Por qué:** la API firma los tokens de la aplicación con una clave secreta.
Sin ella, fuera de desarrollo la API **no arranca**, a propósito: una clave por
defecto en producción es peor que no tener autenticación, porque parece segura.

### 1.1 Generar la clave

En PowerShell:

```powershell
[Convert]::ToBase64String((1..48 | ForEach-Object { Get-Random -Maximum 256 }))
```

Guardá el resultado en un gestor de contraseñas. **No lo pegues en un chat, ni
en un correo, ni en un archivo del repositorio.**

### 1.2 Configurarla en tu máquina

Cada persona que corra la API localmente ejecuta esto una vez:

```powershell
dotnet user-secrets set "Jwt:SigningKey" "<la clave generada>" --project Trilogia-Cursos/Proyecto_FinalAPI
```

> Si no la configurás, la API igual arranca en desarrollo: genera una clave
> temporal para ese proceso y lo avisa en el log. Los tokens dejan de servir al
> reiniciar, que en una máquina de desarrollo está bien.

### 1.3 Configurarla en Azure

Portal de Azure → **App Services** → `api-trilogia-free-cr01` →
**Configuración** → **Variables de aplicación** → **+ Agregar**:

| Nombre | Valor |
|---|---|
| `Jwt__SigningKey` | la clave generada |

Son **dos guiones bajos**, no dos puntos. Guardá y esperá el reinicio.

**Cómo saber si salió bien:** entrá a
`https://api-trilogia-free-cr01.azurewebsites.net/health`
y verificá que responda `"status": "OK"`. Si la API no levanta, la clave falta
o tiene menos de 32 caracteres.

---

## Paso 2 — Respaldo antes de migrar

**Obligatorio.** Lo exige `database/migrations/README.md` y no se salta.

Portal de Azure → **SQL databases** → `DistribuidoraJJ_DB` → **Exportar**.
Guardá el BACPAC con fecha en el nombre.

Anotá en el PR: quién exportó, a qué hora y dónde quedó el archivo.

---

## Paso 3 — Migraciones de base de datos

### 3.0 Antes de empezar: resolver la numeración

Existen **dos archivos `0023_*`** en el repositorio
(`0023_supervisor_rrhh_permisos.sql` y `0023_toggle_product_status_returns_state.sql`),
y el commit `b8c575c` dice que 0023 aún no se aplicó en Azure DEV.

Consultá el ledger, que es la única fuente de verdad, antes de tocar nada:

```sql
SELECT MigrationId, FileName, Status, AppliedAtUtc
FROM dbo.SchemaMigrationHistory
ORDER BY AppliedAtUtc DESC;
```

Si 0023 (cualquiera de los dos) no figura como `Applied`, decidí con el equipo
si se aplica antes. **Las migraciones móviles no dependen de 0023**, así que se
pueden aplicar igual, pero la decisión debe quedar escrita en el PR.

### 3.1 Calcular el SHA-256 de cada script

Los scripts exigen su propia huella y no se ejecutan sin ella.

```powershell
Get-FileHash Trilogia-Cursos/database/migrations/0024_mobile_auth_jwt.sql -Algorithm SHA256 | Select-Object Hash
Get-FileHash Trilogia-Cursos/database/migrations/0025_mobile_driver_surface.sql -Algorithm SHA256 | Select-Object Hash
Get-FileHash Trilogia-Cursos/database/migrations/0026_mobile_app_distribution.sql -Algorithm SHA256 | Select-Object Hash
```

Anotá los tres valores. Van en el PR como evidencia.

### 3.2 Ejecutar, en este orden exacto

Cada script valida que el anterior esté aplicado y falla si no. Ejecutalos en
**SSMS** o **Azure Data Studio**, con la base `DistribuidoraJJ_DB` seleccionada
y el **modo SQLCMD activado** (en SSMS: menú *Consulta* → *Modo SQLCMD*).

El modo SQLCMD es obligatorio: los scripts usan la variable `$(MigrationSha256)`.

```
1º  database/migrations/0024_mobile_auth_jwt.sql
2º  database/migrations/0025_mobile_driver_surface.sql
3º  database/migrations/0026_mobile_app_distribution.sql
```

Antes de cada uno, definí su huella:

```sql
:setvar MigrationSha256 "AQUI_EL_SHA256_DEL_ARCHIVO"
```

**Qué hace cada uno:**

| Script | Qué agrega | Qué NO toca |
|---|---|---|
| **0024** | Tabla de tokens de refresco y 7 procedimientos de autenticación | El login actual, ni el del sistema web |
| **0025** | Permisos `MOVIL_ACCESO` y `FLOTA_KILOMETRAJE_PROPIO`, kilometraje con alcance de chofer, columna `SyncGuid` | `sp_Kilometraje_Abrir` ni `sp_Kilometraje_Cerrar` del módulo web |
| **0026** | Catálogo de versiones del APK y permiso `MOVIL_APP_PUBLICAR` | Nada existente |

Los tres son **aditivos**: crean objetos nuevos. Ninguno modifica ni borra
tablas, columnas o procedimientos que ya estaban.

### 3.3 Si algo falla a mitad

Los scripts usan `XACT_ABORT` y transacción: un error revierte todo el script
automáticamente. Revisá el mensaje, corregí la causa y volvé a ejecutar el
script completo. **No ejecutes partes sueltas.**

Cada migración tiene su archivo `.rollback.md` al lado con el procedimiento
exacto para deshacerla.

---

## Paso 4 — Verificar las migraciones

Ejecutá los tres scripts de verificación. Si alguno lanza un error, la
migración correspondiente quedó incompleta.

```
database/migrations/0024_mobile_auth_jwt.verify.sql
database/migrations/0025_mobile_driver_surface.verify.sql
database/migrations/0026_mobile_app_distribution.verify.sql
```

No necesitan modo SQLCMD.

### 4.1 Confirmar que los permisos llegaron a los perfiles correctos

```sql
SELECT p.Nombre AS Perfil, pe.Codigo AS Permiso
FROM dbo.PerfilPermisos pp
INNER JOIN dbo.Perfiles p ON p.PerfilId = pp.PerfilId
INNER JOIN dbo.Permisos pe ON pe.PermisoId = pp.PermisoId
WHERE pe.Codigo IN (N'MOVIL_ACCESO', N'FLOTA_KILOMETRAJE_PROPIO', N'MOVIL_APP_PUBLICAR')
ORDER BY pe.Codigo, p.Nombre;
```

**Lo que deberías ver:**

- `MOVIL_ACCESO` → Administrador, Gerente, Chofer, Bodeguero, Bodega, Vendedor, Empleado
- `FLOTA_KILOMETRAJE_PROPIO` → Administrador, Chofer
- `MOVIL_APP_PUBLICAR` → Administrador

Si falta un perfil porque en tu base se llama distinto, asignalo desde el
módulo de **Permisos** del sistema web. No hace falta tocar SQL.

### 4.2 Confirmar que el login del sistema web sigue intacto

Entrá al sistema web con un usuario cualquiera. **Si el login funciona igual
que antes, las migraciones no rompieron nada.** Este es el chequeo más
importante de todo el paso 4.

---

## Paso 5 — Configurar la API en Azure

Portal de Azure → **App Services** → `api-trilogia-free-cr01` →
**Configuración** → **Variables de aplicación**:

| Nombre | Valor | Nota |
|---|---|---|
| `Jwt__SigningKey` | la clave del paso 1 | Ya configurada |
| `Jwt__Issuer` | `https://api-trilogia-free-cr01.azurewebsites.net/` | Quién emite los tokens |
| `Jwt__Audience` | `distribuidorajj.movil` | Para quién son |

Desplegá la API con el código nuevo. Verificá:

```
https://<api>/api/mobile/v1/app/version
```

Debe responder un JSON con `latestBuild: 0` — todavía no hay versión publicada,
y eso es lo correcto en este momento.

---

## Paso 6 — Keystore de firma del APK

**Una sola persona, una sola vez, y el archivo se respalda.**

**Por qué importa:** el keystore es la identidad de la aplicación. Si se
pierde, hay que publicarla con otro identificador y **todos los choferes
tendrían que desinstalar y reinstalar**. Más adelante, Google Play exige el
mismo keystore de por vida.

```powershell
keytool -genkey -v -keystore release.jks -keyalg RSA -keysize 2048 -validity 10000 -alias labodega
```

Te va a pedir dos contraseñas y algunos datos de la organización.

**Qué hacer con el archivo:**

1. Guardalo **fuera del repositorio** (`C:\claves\release.jks`, por ejemplo).
2. Respaldalo en un lugar más: una unidad externa o el gestor de contraseñas
   del equipo. GitHub solo no alcanza.
3. Guardá las dos contraseñas y el alias junto al respaldo.

Para compilar desde tu máquina, creá `Trilogia-Cursos/Proyecto_Movil/android/key.properties`
(ya está en `.gitignore`, no se sube):

```properties
storeFile=C:/claves/release.jks
storePassword=<la contraseña del almacén>
keyAlias=labodega
keyPassword=<la contraseña de la clave>
```

### 6.1 Si además querés compilar desde GitHub

Convertí el keystore a texto y cargalo como secreto:

```powershell
[Convert]::ToBase64String([IO.File]::ReadAllBytes("C:\claves\release.jks")) | Set-Clipboard
```

GitHub → repositorio → **Settings** → **Secrets and variables** → **Actions**:

| Tipo | Nombre | Valor |
|---|---|---|
| Secret | `ANDROID_KEYSTORE_BASE64` | lo que quedó en el portapapeles |
| Secret | `ANDROID_KEYSTORE_PASSWORD` | contraseña del almacén |
| Secret | `ANDROID_KEY_ALIAS` | `labodega` |
| Secret | `ANDROID_KEY_PASSWORD` | contraseña de la clave |
| Variable | `API_BASE_URL` | la dirección de la API |

---

## Paso 7 — Compilar el APK

```powershell
cd Trilogia-Cursos/Proyecto_Movil
flutter pub get
flutter build apk --release --split-per-abi --dart-define=API_BASE_URL=https://api-trilogia-free-cr01.azurewebsites.net/
```

Los archivos quedan en `build/app/outputs/flutter-apk/`. Usá el que termina en
`-arm64-v8a-release.apk`: es el que sirve para prácticamente cualquier teléfono
Android actual y pesa ~20 MB en vez de 50.

### 7.1 Calcular su huella

```powershell
Get-FileHash build/app/outputs/flutter-apk/app-arm64-v8a-release.apk -Algorithm SHA256 | Select-Object Hash
```

Anotala: se publica junto al enlace de descarga para que quien instala pueda
verificar que el archivo es el que el equipo publicó.

### 7.2 Subirlo a algún lado

El APK **no va en el repositorio**. Subilo a una de estas dos opciones:

- **GitHub Releases** (recomendado): crear un *release* en el repositorio y
  adjuntar el archivo. Copiá la dirección de descarga directa.
- **Azure Blob Storage**: un contenedor de solo lectura. Copiá la dirección.

En ambos casos la dirección tiene que empezar con `https://`. El sistema lo
valida y rechaza cualquier otra cosa.

---

## Paso 8 — Publicar la versión en el sistema

Entrá al sistema web como administrador → menú **Aplicación móvil**
(`/MobileApp`) → formulario **Publicar una versión nueva**:

| Campo | Qué poner | Ejemplo |
|---|---|---|
| Versión | Lo que ve la gente | `1.0.0` |
| Compilación | Número que **siempre sube** | `1` |
| Mínima soportada | Desde qué compilación se puede usar | `1` |
| Dirección de descarga | La del paso 7.2 | `https://...` |
| SHA-256 | La huella del paso 7.1 | 64 caracteres |
| Tamaño en bytes | Opcional, se muestra en la página | `20971520` |
| Notas | Qué trae esta versión | `Primera versión` |

**La regla de la compilación:** cada versión nueva debe tener un número mayor
que la anterior. El sistema lo rechaza si no. Esa regla es lo que permite
retirar del campo una versión con un error grave, y más adelante es requisito
de Google Play.

Al guardar, aparece el código QR. Ya se puede instalar.

---

## Paso 9 — Prueba en un teléfono real

**Esto no lo puede hacer nadie desde una computadora.** Hasta que un chofer no
complete una ruta real, la aplicación no está probada.

### 9.1 Instalar

1. Desde el teléfono, entrá al sistema web y abrí **Aplicación móvil**.
   O escaneá el QR desde la pantalla de un compañero.
2. Descargá el archivo.
3. Android va a advertir sobre "orígenes desconocidos". Es normal: la
   aplicación no está en Google Play porque es de uso interno. Tocá
   **Configuración** y habilitá la instalación desde el navegador.
4. Instalá y abrí.

### 9.2 Recorrido de prueba

Marcá cada punto:

- [ ] Entra con el correo y contraseña del sistema web.
- [ ] La pantalla de inicio muestra las tarjetas de su perfil.
- [ ] **Mis rutas** lista las rutas asignadas.
- [ ] Al abrir una ruta, las paradas aparecen en orden.
- [ ] **Navegar** abre Waze o Google Maps en la dirección correcta.
- [ ] **Llamar** abre el marcador con el número del cliente.
- [ ] Marcar una entrega como entregada pide confirmación.
- [ ] En el sistema web, esa entrega aparece como entregada.
- [ ] **Kilometraje** deja abrir jornada con el vehículo de la ruta.
- [ ] **Poner el teléfono en modo avión.** Marcar dos entregas más.
- [ ] Aparece una franja arriba: "Sin conexión · 2 acciones pendientes".
- [ ] Quitar el modo avión. La franja desaparece sola.
- [ ] En el sistema web, las dos entregas llegaron **una sola vez**.
- [ ] Cerrar jornada con el kilometraje final.

El punto del modo avión es el más importante de toda la lista: es la razón de
ser de la aplicación.

---

## Mantenimiento

### Cuando publiques una versión nueva

Repetí los pasos 7 y 8. **Subí siempre el número de compilación.**

Si la versión anterior tiene un error grave, poné la **mínima soportada** igual
a la compilación nueva: eso obliga a todos a actualizar antes de poder seguir.

### Si a alguien se le pierde el teléfono

```sql
EXEC dbo.sp_Auth_RevocarTokensUsuario
     @UsuarioId = <id del usuario>,
     @Motivo = N'Teléfono extraviado';
```

Su sesión móvil muere de inmediato. Puede volver a entrar desde otro aparato
con su misma contraseña.

### Si alguien deja la empresa

Desactivar el usuario en el sistema web ya basta: la aplicación deja de
renovar su sesión en la siguiente media hora. Para cortarlo de inmediato,
corré también la revocación de arriba.

### Limpieza de tokens vencidos

Cada tanto, para que la tabla no crezca sin límite:

```sql
EXEC dbo.sp_Auth_PurgarTokensRefresco @DiasRetencion = 30;
```

No es urgente: son unos pocos bytes por sesión.

### Revisar quién usa la aplicación

Todo lo que entra desde el teléfono queda en la auditoría marcado con
`[Móvil]` al inicio de la descripción, para poder distinguirlo de lo que se
hizo desde una computadora.

```sql
SELECT TOP (100) FechaHora, UsuarioNombre, Accion, Modulo, Descripcion
FROM dbo.HistorialAuditoria
WHERE Descripcion LIKE N'[[]Móvil]%'
ORDER BY FechaHora DESC;
```

---

## Qué NO hay que hacer

1. **No subir el APK al repositorio.** Un archivo de 20 MB por versión degrada
   el repositorio de forma permanente.
2. **No subir el keystore ni sus contraseñas** a ningún lado versionado.
3. **No poner la clave JWT** en `appsettings.json` ni en ningún archivo del
   repositorio. Va en User Secrets y en la configuración de Azure.
4. **No bajar el número de compilación** entre versiones.
5. **No ejecutar una migración dos veces.** Los scripts lo detectan y fallan,
   pero el ledger es la fuente de verdad: consultalo antes.
6. **No modificar una migración ya aplicada.** Se crea la siguiente.
7. **No distribuir un APK firmado con la clave de depuración.** Si compilaste
   sin `key.properties`, el archivo funciona pero no es el oficial y no se
   puede actualizar sobre él.
