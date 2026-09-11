# DistribuidoraJJ / Licorera La Bodega

Sistema de distribución y licorera. Proyecto universitario, Universidad Fidélitas.

- `Trilogia-Cursos/Proyecto_Final/` — aplicación web ASP.NET Core MVC (.NET 9)
- `Trilogia-Cursos/Proyecto_FinalAPI/` — API REST de autenticación y catálogo (.NET 9)
- `Trilogia-Cursos/database/` — esquema, procedimientos, migraciones y datos demo

---

## Arrancar el proyecto

La base de datos vive en Azure SQL y es compartida por todo el equipo. **La conexión no
usa contraseña**: cada persona se autentica con su propia cuenta institucional de
Microsoft. Por eso no hay ningún secreto en este repositorio y no hay que pedirle
credenciales a nadie.

### Requisitos

- [.NET SDK 9](https://dotnet.microsoft.com/download) o superior
- [Azure CLI](https://aka.ms/installazurecliwindows)
- Visual Studio 2022 / VS Code / Rider — opcional, `dotnet` solo basta
- Cuenta de la universidad con acceso al grupo de recursos del proyecto

### 1. Iniciar sesión en Azure

Una sola vez por computadora:

```powershell
az login
```

Se abre el navegador. Entrá con tu cuenta institucional. Eso es lo que le da permiso a la
aplicación para conectarse a la base.

> Si tu cuenta es invitada (no es `@ufide.ac.cr`), agregá el tenant:
> `az login --tenant ufidelitas.ac.cr`

### 2. Registrar tu dirección de internet

Azure SQL rechaza conexiones desde direcciones que no estén en su lista. Cada persona
registra la suya desde el portal, sin depender de nadie:

**portal.azure.com** › buscar **SQL servers** › **sql-trilogia-free-cr01** ›
menú izquierdo **Security** › **Networking** › pestaña **Public access** ›
botón **+ Add your client IPv4 address** › **Save**

El botón detecta tu dirección solo. Poné un nombre reconocible a la regla
(`dev-tunombre-20260911`) antes de guardar.

Hay que repetirlo cuando tu proveedor de internet te cambie la dirección — pasa cada
cierto tiempo y es normal.

### 3. Levantar los dos proyectos

Ambos tienen que correr a la vez: el sitio web hace el login contra la API.

```powershell
dotnet run --project Trilogia-Cursos/Proyecto_FinalAPI
```

```powershell
dotnet run --project Trilogia-Cursos/Proyecto_Final
```

En Visual Studio: clic derecho en la solución › *Configure Startup Projects* ›
*Multiple startup projects* › ambos en **Start**.

| Servicio | URL |
|---|---|
| Sitio web | https://localhost:7013 |
| API | https://localhost:57540 |
| Estado de cada uno | `/health` |

### 4. Entrar

Las credenciales de prueba **no se guardan en este repositorio**. Pedilas al equipo por
un canal privado.

---

## Cómo está configurada la conexión

La cadena vive en `appsettings.json` de cada proyecto y se ve así:

```
Server=tcp:sql-trilogia-free-cr01.database.windows.net,1433;
Initial Catalog=DistribuidoraJJ_DB;
Authentication=Active Directory Default;
Encrypt=True;
```

`Active Directory Default` hace que la aplicación busque tu sesión de Azure en este orden:
variables de entorno, identidad administrada, Visual Studio, VS Code, Azure CLI. Con haber
corrido `az login` alcanza.

**No contiene contraseña, por eso puede estar en un repositorio público.**

### Trabajar contra otra base de datos

No edites `appsettings.json`. Sobreescribí la cadena en *user-secrets*, que se carga
después y nunca llega a git:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<tu cadena>" --project Trilogia-Cursos/Proyecto_Final
```

Para volver a la configuración compartida, borrá el secreto:

```powershell
dotnet user-secrets remove "ConnectionStrings:DefaultConnection" --project Trilogia-Cursos/Proyecto_Final
```

> `appsettings.Development.json` **no define** `ConnectionStrings` a propósito: se carga
> después de `appsettings.json` y una cadena vacía ahí anularía la buena.

---

## Si algo falla

**`Cannot open server ... Client with IP address ... is not allowed`**
Tu dirección no está registrada, o cambió. Volvé al paso 2.

**`Can not connect to the database in its current state`**
La base estaba dormida y está despertando. Toma cerca de un minuto; reintentá. Solo es
grave si persiste más de cinco minutos.

**`Login failed for user '<token-identified principal>'`**
Tu cuenta no tiene usuario en la base. Pedile a un administrador del equipo que te lo
cree; es una sola instrucción.

**`Failed to authenticate ... DefaultAzureCredential`**
No hay sesión de Azure en esta computadora. Corré `az login`.

**La primera consulta del día tarda.**
Es normal: la base es *serverless* y se duerme sola tras una hora sin uso.

---

## Reglas del repositorio

1. Nunca subir contraseñas, cadenas con credenciales ni archivos de secretos.
2. Los cambios de SQL van en un script nuevo numerado bajo `Trilogia-Cursos/database/`.
3. Trabajar en una rama, no directo sobre `main`.

Ver `Trilogia-Cursos/CLAUDE.md` para las convenciones completas del proyecto y
`Trilogia-Cursos/database/migrations/README.md` para el proceso de migraciones.
