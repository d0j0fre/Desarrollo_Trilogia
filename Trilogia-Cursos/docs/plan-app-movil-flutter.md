# Plan de implementación — Aplicación móvil Flutter

Ruta de trabajo para llevar las funciones operativas de **DistribuidoraJJ / Licorera
La Bodega** a una aplicación móvil nativa, sin duplicar el sistema web y sin degradar
la seguridad vigente.

Documento de diseño. No autoriza por sí mismo ninguna ejecución en Azure ni ningún
cambio en `appsettings*.json`; cada fase indica qué aprobación necesita.

---

## 1. Resumen de decisiones

| Decisión | Elección | Razón corta |
|---|---|---|
| Ubicación del código | `Trilogia-Cursos/Proyecto_Movil/` (mismo repositorio) | El contrato de API y su cliente cambian juntos; un solo PR, una sola revisión |
| Backend que consume la app | `Proyecto_FinalAPI`, **nunca** `Proyecto_Final` (MVC) | El MVC es sesión + antiforgery; un cliente nativo no encaja en ese modelo |
| Autenticación móvil | JWT Bearer, **aditivo** sobre el login actual | `docs/api-auth-futura.md` ya lo dejó diseñado; romper el contrato rompe el login MVC |
| Autorización | Rol + códigos de permiso revalidados **en cada request** | El cliente jamás es fuente de verdad del rol |
| Estado / DI en Flutter | Riverpod + capas feature-first | Testeable sin widgets, sin singletons globales |
| Persistencia local | SQLite (`drift`) con patrón *outbox* | El chofer trabaja sin señal; el backend ya es idempotente por GUID |
| Lógica de negocio | Se queda en los *stored procedures* | Es el patrón real del proyecto: el C# solo mapea |
| Distribución | APK firmado fuera de git + página con QR en el MVC | `.gitignore` prohíbe binarios; el APK se versiona como *release*, no como archivo |

**El bloqueante real no es Flutter: es que la API no tiene autorización.** Hoy
`Proyecto_FinalAPI` no registra `AddAuthentication`, `AddAuthorization` ni
`UseAuthentication`, y `POST /api/auth/login` devuelve los datos del usuario **sin
emitir token**. Mientras eso no exista, cualquier endpoint móvil sería un endpoint
público que confía en un `UsuarioId` enviado por el cliente. La Fase 1 es
obligatoria y bloquea todo lo demás.

---

## 2. Diagnóstico: qué ya existe y sirve

Buena parte del trabajo pesado ya está hecho en el backend. Reutilizarlo es la
diferencia entre un mes y un semestre.

### 2.1 Lo que se reutiliza tal cual

- **Rutas y entregas del chofer.** `sp_Chofer_GetMyRoutes`,
  `sp_Chofer_GetRouteDeliveries` y `sp_Chofer_UpdateDeliveryStatus` ya reciben
  `@ChoferUsuarioId` y validan pertenencia en SQL (rechazan con error ≥ 50000 si la
  ruta no es del chofer). Ver `Services/LogisticsDbService.cs:498-605`.
- **Idempotencia de sincronización.** `sp_Chofer_UpdateDeliveryStatus` recibe
  `@SyncGuid` y devuelve un campo `Duplicado`. Esto es exactamente lo que necesita
  una cola offline: el móvil genera el GUID, reintenta sin miedo, y el servidor
  decide. No hay que inventar nada.
- **Deep link a Waze.** Ya resuelto en
  `Views/DriverDeliveries/Route.cshtml:11` con `https://waze.com/ul?ll=...&navigate=yes`.
  Las entregas ya traen `Latitud` / `Longitud`.
- **Coordenadas y secuenciación.** `sp_Rutas_Secuenciar` y `sp_Rutas_GuardarSecuencia`
  ya ordenan la ruta; el móvil solo la consume.
- **Evidencias.** `sp_Entrega_RegisterEvidence`, `sp_Entrega_MarkEvidenceReady`,
  `sp_Entrega_DeletePendingEvidence` con compensación transaccional en
  `DriverDeliveriesController.RegisterEvidence`.
- **Pedido offline del vendedor (CU-072).** `PedidoOfflineGuid` y canal
  `"Venta móvil offline"` ya existen en `SellerOrdersController.SyncOffline`.
- **Hash de contraseñas.** Migración `0022_password_hash_transition.sql`: PBKDF2 con
  actualización gradual. El login móvil pasa por el mismo
  `sp_Auth_GetLoginCredential`, así que hereda la transición sin trabajo extra.
- **Auditoría.** `AdminDbService.CreateAuditLogAsync` con IP y user-agent.

### 2.2 Lo que falta y hay que construir

| Falta | Impacto | Fase |
|---|---|---|
| JWT en la API | Bloquea todo | 1 |
| Refresh token revocable | El chofer no puede reloguearse cada 30 min en la calle | 1 |
| Endpoints móviles (`/api/mobile/v1/...`) | No existe ninguno | 2 |
| Kilometraje con alcance de chofer | Hoy es `[AdminAuthorize("Flota","FLOTA_KILOMETRAJE")]`: un chofer sin ese permiso no puede abrir jornada | 2 |
| Almacenamiento de evidencias compartido | MVC y API son App Services distintos: archivos en disco local no se ven entre sí | 5 |
| ~~Módulo de alistamiento / picking~~ | Descartado por decisión de negocio (§16) | — |
| Página de descarga con QR | No existe | 7 |
| Verificación de versión mínima | Sin tienda de apps, no hay actualización forzada | 7 |

### 2.3 Roles vigentes

Los roles que el sistema reconoce hoy: `Administrador`, `Gerente`, `Vendedor`,
`Empleado`, `Chofer`, `Bodeguero`, `Bodega`, `Cliente`.

> **Resuelto el 2026-09-16.** El código nombra `Bodeguero` y `Bodega` como si
> fueran dos perfiles (`AssistantController.cs:11`), pero al consultar la base
> solo existe **`Bodeguero`**. La ambigüedad estaba únicamente en las listas de
> roles escritas a mano en el código, no en los datos, así que no hay usuarios
> que consolidar. Conviene limpiar esas listas, pero no bloquea la Fase 6.

---

## 3. Dónde vive el código

```
Desarrollo_Trilogia/
└── Trilogia-Cursos/
    ├── Proyecto_Final/            # MVC (sin cambios salvo la página de descarga)
    ├── Proyecto_FinalAPI/         # API — aquí crece toda la superficie móvil
    ├── Proyecto_Final.Tests/      # se amplía con tests de JWT y de contrato
    ├── Proyecto_Movil/            # ← NUEVO: aplicación Flutter
    ├── database/migrations/       # nuevas migraciones numeradas
    └── docs/
```

**Por qué el mismo repositorio y no uno aparte:**

1. El contrato de la API y su único consumidor cambian en el mismo *commit*. En
   repositorios separados, un cambio de DTO se descubre en producción.
2. El CI (`.github/workflows/ci-security-build.yml`) ya corre escaneo de secretos,
   validación de SQL, build y tests. Agregar un *job* de Flutter cuesta 20 líneas.
3. `Proyecto_Final.slnx` no toca Flutter — no hay riesgo de romper el build .NET.

**Por qué no un proyecto móvil en .NET (MAUI):** el equipo pidió Flutter, y MAUI
además arrastraría el riesgo de que alguien "reutilice" los servicios del MVC en el
cliente, que es justo lo que no se debe hacer.

### 3.1 Advertencia práctica sobre OneDrive

El repositorio está bajo `OneDrive\Desktop\...`. Flutter genera miles de archivos en
`build/`, `.dart_tool/` y `android/.gradle/`. OneDrive los sincroniza, bloquea
archivos en uso y provoca fallos de compilación intermitentes y rutas largas
(> 260 caracteres) en Windows.

**Acción recomendada antes de empezar:** mover el repositorio a una ruta corta fuera
de OneDrive (`C:\src\Desarrollo_Trilogia`) o, como mínimo, excluir de la
sincronización las carpetas de build. Esto no es opcional; es la causa número uno de
"a mí no me compila".

---

## 4. Alcance por rol

El principio: **la app no es el sistema web en pantalla pequeña.** Es el subconjunto
de acciones que se ejecutan de pie, con una mano y posiblemente sin señal.

### 4.1 Chofer — MVP (Fase 3)

| Función | Origen backend | Estado |
|---|---|---|
| Ver mis rutas asignadas | `sp_Chofer_GetMyRoutes` | Existe |
| Detalle de ruta con paradas ordenadas | `sp_Chofer_GetRouteDeliveries` | Existe |
| Abrir navegación en Waze / Google Maps | deep link con lat/lng | Existe |
| Llamar al cliente | `tel:` nativo | Trivial |
| Marcar Entregado / Fallido + motivo | `sp_Chofer_UpdateDeliveryStatus` | Existe |
| Registrar kilometraje inicial y final de jornada | `sp_Kilometraje_Abrir` / `_Cerrar` | **Requiere variante con alcance de chofer** |
| Evidencia: foto y firma | `sp_Entrega_RegisterEvidence` | Existe, pero requiere Fase 5 |
| Resumen del día (entregados / pendientes / fallidos) | derivado | Trivial |

### 4.2 Bodeguero — Fase 6

| Función | Estado |
|---|---|
| Consultar stock escaneando código de barras | Requiere columna de código de barras y endpoint |
| Registrar devolución / enviar a cuarentena | Existe (`sp_Devoluciones_*`, `CUARENTENA_GESTIONAR`) |
| Recibir orden de compra (parcial o total) | Existe (`COMPRAS_ORDENES_RECIBIR`) |
| Alistamiento / picking de pedidos | **Descartado** (decisión 3 de §16) |

> *Alistamiento* o *picking* es el paso de bodega en que alguien recorre las
> estanterías con la lista de un pedido, toma físicamente cada producto,
> verifica cantidades y lo deja armado para que el chofer lo cargue. Hoy el
> sistema no registra ese paso: un pedido pagado pasa directo a una ruta, sin
> dejar constancia de quién lo armó ni de si faltó algo.
>
> Construirlo significaría un estado nuevo en el ciclo del pedido, y ese ciclo
> toca inventario, facturación y rutas a la vez — los tres módulos más
> delicados. Queda fuera. El bodeguero igual gana valor real en el móvil sin
> él.

### 4.3 Vendedor y empleado — Fase 6

- Pedido offline (CU-072 ya resuelto en backend).
- Mi meta del mes (`0021_seller_goal_progress.sql`).
- Marcar asistencia (`MyAttendanceController` / `AttendanceDbService`).

### 4.4 Fuera de alcance, explícitamente

Administración, facturación, planilla, boletas de pago, presupuestos, gastos, roles
y permisos, auditoría, reportes gerenciales, portal de cliente. Todo eso se hace
sentado, con teclado, y no se gana nada exponiendo su superficie de ataque en un APK
que se instala fuera de una tienda.

Las **boletas de pago** merecen mención aparte: son archivos privados servidos por
`IPrivateFileStorageService` con control de pertenencia. Llevarlas al móvil implica
resolver descarga autenticada de binarios privados. No entra en este plan.

---

## 5. Fase 1 — Autenticación JWT en la API *(bloqueante)*

Implementa los bloques JWT-1 a JWT-5 ya definidos en `docs/api-auth-futura.md`. Ese
documento es la especificación; esto es la ejecución.

### 5.1 Regla de oro: aditivo, nunca sustitutivo

`AccountController.Login` del MVC depende de la forma exacta de la respuesta actual.
`AuthenticationContractTests` fija el contrato de `sp_Auth_GetLoginCredential`.

La respuesta de `POST /api/auth/login` **conserva** `success`, `message`, `userId`,
`fullName`, `email`, `role` y **agrega**:

```jsonc
{
  "success": true,
  "message": "Inicio de sesión correcto.",
  "userId": 42,
  "fullName": "...",
  "email": "...",
  "role": "Chofer",
  // nuevos, aditivos (los dos tokens se omiten aquí a propósito:
  //  el escáner de secretos del repositorio no distingue un ejemplo
  //  de una credencial real, y es correcto que no lo intente)
  "accessToken": "<JWT firmado, vida de 30 minutos>",
  "refreshToken": "<cadena opaca de 32 bytes>",
  "expiresAt": "2026-09-15T18:30:00Z",
  "permissions": ["FLOTA_KILOMETRAJE_PROPIO", "ENTREGAS_ACTUALIZAR_PROPIA"]
}
```

El MVC ignora los campos nuevos y sigue creando su sesión local. Cero cambios en
`AccountApiService.cs` en esta fase.

### 5.2 Diseño del token

| Aspecto | Valor | Razón |
|---|---|---|
| Vida del *access token* | 30 minutos | Limita la ventana si se roba el teléfono |
| Vida del *refresh token* | 14 días, **rotativo** | El chofer no puede reloguearse en la calle |
| Revocación | Tabla `dbo.RefreshTokens` con `RevocadoUtc` | Un despido o un robo debe cortar el acceso |
| Claims | `sub` (UsuarioId), `email`, `role`, `perm` (arreglo), `jti`, `device` | El permiso viaja, pero **no se confía**: se revalida |
| Algoritmo | HS256 con clave ≥ 256 bits | Suficiente para emisor y validador únicos |
| Almacén de la clave | User Secrets en local, App Service Configuration en Azure | Reglas 1 y 2 de `AGENTS.md` |

**Rotación de refresh token:** cada uso emite uno nuevo e invalida el anterior. Si
llega un refresh token ya usado, se revoca **toda la cadena** de ese dispositivo: es
la señal clásica de token robado.

### 5.3 Los permisos viajan pero no mandan

Incluir los códigos de permiso en el token evita una consulta por request, pero
**cada endpoint vuelve a validar contra `PerfilPermisos`** mediante el equivalente de
`IRolePermissionService`. El claim sirve para que la app decida qué botones dibujar;
la autorización real ocurre en el servidor. Si se revoca un permiso, el efecto es
inmediato aunque el token siga vivo.

Esto también evita el error más común de las apps con JWT: ocultar el botón y dejar
el endpoint abierto.

### 5.4 Archivos que toca

```
Proyecto_FinalAPI/
├── Program.cs                          # AddAuthentication/AddAuthorization/UseAuthentication
├── Controllers/AuthController.cs       # + refresh, + logout (revocación)
├── Models/AuthModels.cs                # campos aditivos
├── Options/JwtOptions.cs               # NUEVO — opciones validadas al arrancar
├── Services/JwtTokenService.cs         # NUEVO — emisión
├── Services/RefreshTokenService.cs     # NUEVO — rotación y revocación
└── Authorization/
    ├── PermissionRequirement.cs        # NUEVO
    └── PermissionHandler.cs            # NUEVO — revalida contra PerfilPermisos
```

Migración nueva: tabla `dbo.RefreshTokens` y `sp_RefreshTokens_Emitir` /
`_Validar` / `_Revocar` / `_RevocarCadena`.

### 5.5 Configuración sin secretos

`appsettings.json` **no se modifica sin aprobación explícita** (regla 1 de
`CLAUDE.md`). Cuando se apruebe, lleva solo la plantilla sanitizada:

```json
"Jwt": { "Issuer": "", "Audience": "", "AccessTokenMinutes": 30, "RefreshTokenDays": 14 }
```

La clave (`Jwt:SigningKey`) **jamás** aparece en un archivo versionado. En local va a
User Secrets; en Azure, a App Service Configuration. `Test-RepositorySecrets.ps1`
escanea todo archivo rastreado y debe seguir pasando en verde.

**Arranque en falso deliberado:** si falta `Jwt:SigningKey`, la API debe fallar al
arrancar con un mensaje claro, no arrancar con una clave por defecto. Una clave por
defecto en producción es peor que no tener autenticación, porque parece segura.

### 5.6 Criterios de aceptación

- [ ] Login MVC funciona igual que antes (regresión cero).
- [ ] Endpoint protegido sin token → `401`.
- [ ] Token inválido, expirado o con firma ajena → `401`.
- [ ] Rol incorrecto → `403`.
- [ ] Refresh token reutilizado → toda la cadena revocada.
- [ ] Un chofer no puede leer la ruta de otro chofer (el SP ya lo rechaza; se
      verifica que la API no lo enmascare como `500`).
- [ ] `dotnet build` 0 errores, 0 advertencias. `dotnet test` verde.
- [ ] `Test-RepositorySecrets.ps1` verde.

---

## 6. Fase 2 — Superficie API móvil `v1`

### 6.1 Convenciones

- Prefijo versionado: `/api/mobile/v1/...`. La versión está en la URL porque un APK
  viejo instalado en un teléfono no se puede forzar a actualizar de inmediato.
- Todo responde JSON. Nada de `TempData`, redirecciones ni vistas.
- **Nunca** `ex.Message` al cliente (regla 6 de `AGENTS.md`). Se registra con
  `ILogger` y se devuelve `{ "error": "codigo_estable", "message": "texto genérico" }`.
- Códigos: `401` sin token, `403` sin permiso, `404` no existe o no es suyo
  (**no se distingue**: distinguirlos filtra información), `409` conflicto de
  concurrencia, `422` regla de negocio, `503` base dormida o caída.

### 6.2 Endpoints del MVP

```
POST   /api/auth/login                       (existente, ampliado)
POST   /api/auth/refresh                     (nuevo)
POST   /api/auth/logout                      (nuevo, revoca)

GET    /api/mobile/v1/me                     perfil + permisos efectivos
GET    /api/mobile/v1/me/capabilities        qué módulos dibuja la app
GET    /api/mobile/v1/app/version            versión mínima soportada

GET    /api/mobile/v1/driver/routes          mis rutas
GET    /api/mobile/v1/driver/routes/{id}     detalle con paradas ordenadas
POST   /api/mobile/v1/driver/deliveries/{rutaPedidoId}/status
                                             body: { estado, syncGuid, motivoFallo }
POST   /api/mobile/v1/driver/mileage/open    body: { vehiculoId, kmInicial, syncGuid }
POST   /api/mobile/v1/driver/mileage/close   body: { kilometrajeId, kmFinal, syncGuid }
GET    /api/mobile/v1/driver/summary         resumen del día
```

**`/me/capabilities` es la clave del diseño.** La app no lleva cableado "si el rol es
Chofer, muestro estas cinco tarjetas". Pide al servidor qué puede hacer y dibuja eso.
Cuando mañana un chofer también reciba un permiso de bodega, la app lo refleja sin
publicar un APK nuevo.

```jsonc
{
  "userId": 42, "fullName": "...", "role": "Chofer",
  "modules": [
    { "key": "driver.routes",   "title": "Mis rutas",   "icon": "route",       "enabled": true },
    { "key": "driver.mileage",  "title": "Kilometraje", "icon": "speedometer", "enabled": true },
    { "key": "driver.evidence", "title": "Evidencias",  "icon": "camera",      "enabled": false }
  ]
}
```

### 6.3 Idempotencia: el contrato con el modo offline

Todo `POST` que cambie estado recibe un `syncGuid` generado por el cliente.

- `sp_Chofer_UpdateDeliveryStatus` ya lo soporta y devuelve `Duplicado`.
- Los endpoints de kilometraje necesitan lo mismo: **migración nueva** con
  `sp_Kilometraje_Abrir_Movil` / `_Cerrar_Movil` que reciban `@SyncGuid` y
  `@ChoferUsuarioId`, y validen que el vehículo corresponde a una ruta activa de ese
  chofer.

Sin esto, un reintento tras un timeout crea una jornada duplicada y la app queda
peor que el papel.

### 6.4 Kilometraje: el problema de permisos

Hoy `FleetController.OpenMileage` exige `[AdminAuthorize("Flota","FLOTA_KILOMETRAJE")]`
— un permiso de administración de flota. Un chofer típico no lo tiene, y dárselo le
abriría el módulo completo de flota en la web.

**Solución:** permiso nuevo `FLOTA_KILOMETRAJE_PROPIO`, asignado al perfil `Chofer`,
que solo habilita abrir y cerrar jornada **del vehículo asignado a una ruta activa
propia**. La validación de pertenencia vive en el *stored procedure*, no en C#,
igual que `sp_Chofer_GetRouteDeliveries`.

### 6.5 Reutilización de la lógica de datos

Los servicios del MVC (`LogisticsDbService`, `FleetDbService`) son mapeadores
delgados sobre *stored procedures*. **La lógica de negocio real vive en SQL.**

Por eso, la API no debe referenciar el proyecto MVC ni arrastrar sus ViewModels
(están acoplados a las vistas Razor). Se crean servicios propios en
`Proyecto_FinalAPI/Services/Mobile/` que invocan **los mismos SP**.

Para que no deriven, se agrega un test de paridad en `Proyecto_Final.Tests` que
verifica que el servicio móvil y el del MVC invocan el mismo nombre de procedimiento.
Barato, y atrapa la mayor parte de la deriva.

> Lo que **sí** se comparte es la lógica C# real: validación y *staging* de
> evidencias (`IEvidenceStorageService`) y el hash de contraseñas. Eso se extrae a
> una biblioteca común en la Fase 5, no antes.

### 6.6 Rate limiting: corregir la partición

El limitador actual particiona por `RemoteIpAddress`. Toda una flota tras el NAT de
un mismo operador móvil comparte IP y se bloquea entre sí.

Con JWT disponible, los endpoints móviles particionan por `sub` (el usuario del
token) y caen a IP solo cuando no hay token. El MVC ya hace exactamente esto en
`GetUserPartition`; se replica en la API.

---

## 7. Fase 3 — La aplicación Flutter

### 7.1 Arquitectura

Tres capas, organización *feature-first*. Cada funcionalidad es una carpeta
autocontenida; lo transversal vive en `core/`.

```
presentation  →  application  →  domain  ←  data
   (widgets)     (controllers)  (entidades,   (API, SQLite,
                                 interfaces)   implementaciones)
```

La regla que sostiene todo: **`domain` no importa nada de Flutter ni de Dio.** Si un
día cambia el cliente HTTP o se agrega una versión de escritorio, el dominio no se
entera. Y se prueba sin levantar un widget.

### 7.2 Estructura de archivos

```
Proyecto_Movil/
├── README.md
├── pubspec.yaml
├── analysis_options.yaml              # lints estrictos + reglas propias
├── .gitignore
│
├── lib/
│   ├── main.dart                      # solo bootstrap
│   ├── app.dart                       # MaterialApp + tema + router
│   │
│   ├── core/
│   │   ├── config/
│   │   │   ├── app_config.dart        # baseUrl vía --dart-define, NUNCA hardcode
│   │   │   └── environment.dart       # dev | qa | prod
│   │   ├── network/
│   │   │   ├── api_client.dart        # Dio configurado
│   │   │   ├── auth_interceptor.dart  # Bearer + refresh transparente
│   │   │   ├── retry_interceptor.dart # backoff — clave por la base serverless
│   │   │   └── api_exception.dart     # mapea 401/403/404/422/503 a tipos
│   │   ├── storage/
│   │   │   ├── secure_store.dart      # flutter_secure_storage (Keystore)
│   │   │   └── app_database.dart      # drift: caché + outbox
│   │   ├── sync/
│   │   │   ├── outbox_entry.dart
│   │   │   ├── outbox_repository.dart
│   │   │   └── sync_service.dart      # cola, reintentos, conflictos
│   │   ├── theme/
│   │   │   ├── brand_colors.dart      # espejo de brand-system.css
│   │   │   ├── ds_buttons.dart        # espejo de design-system-buttons.css
│   │   │   └── app_theme.dart
│   │   ├── routing/
│   │   │   ├── app_router.dart        # go_router
│   │   │   └── route_guards.dart      # redirección por sesión y capacidades
│   │   └── widgets/                   # AppScaffold, EmptyState, ErrorView,
│   │                                  # OfflineBanner, ConfirmSheet
│   │
│   ├── features/
│   │   ├── auth/
│   │   │   ├── data/        auth_api.dart · auth_repository_impl.dart
│   │   │   ├── domain/      session.dart · auth_repository.dart
│   │   │   ├── application/ auth_controller.dart · session_provider.dart
│   │   │   └── presentation/ login_page.dart · splash_page.dart
│   │   │
│   │   ├── home/
│   │   │   └── presentation/ home_page.dart   # se dibuja desde /me/capabilities
│   │   │
│   │   ├── driver_routes/
│   │   │   ├── data/        routes_api.dart · routes_local_dao.dart
│   │   │   │                routes_repository_impl.dart
│   │   │   ├── domain/      route.dart · delivery.dart · delivery_status.dart
│   │   │   │                routes_repository.dart
│   │   │   ├── application/ routes_controller.dart · delivery_controller.dart
│   │   │   └── presentation/ routes_list_page.dart · route_detail_page.dart
│   │   │                     delivery_card.dart · status_change_sheet.dart
│   │   │
│   │   ├── mileage/
│   │   │   ├── data/ · domain/ · application/
│   │   │   └── presentation/ mileage_page.dart · open_shift_form.dart
│   │   │
│   │   ├── evidence/                  # Fase 5
│   │   │   └── presentation/ camera_page.dart · signature_pad.dart
│   │   │
│   │   └── settings/
│   │       └── presentation/ settings_page.dart · about_page.dart
│   │
│   └── shared/
│       ├── extensions/                # formato de fecha, moneda CRC
│       └── utils/
│           ├── navigation_launcher.dart  # Waze → Google Maps → coordenadas
│           └── connectivity.dart
│
├── test/                              # unitarios: dominio + controllers
├── integration_test/                  # flujo completo del chofer
│
├── android/
│   ├── app/build.gradle.kts           # firma release desde variables de entorno
│   └── app/src/main/
│       ├── AndroidManifest.xml
│       └── res/xml/network_security_config.xml   # sin tráfico en claro
└── ios/                               # solo si se aprueba iOS
```

### 7.3 Dependencias base

```yaml
dependencies:
  flutter_riverpod        # estado + inyección
  go_router               # navegación declarativa con guardas
  dio                     # HTTP con interceptores
  drift + sqlite3_flutter_libs   # SQLite tipado (caché + outbox)
  flutter_secure_storage  # tokens en Keystore / Keychain
  connectivity_plus       # estado de red
  url_launcher            # Waze, teléfono
  image_picker            # foto de evidencia (Fase 5)
  signature               # firma del cliente (Fase 5)
  package_info_plus       # versión para el chequeo de actualización
  freezed + json_serializable   # modelos inmutables y parsing generado

dev_dependencies:
  build_runner · mocktail · flutter_lints · very_good_analysis
```

Cada dependencia entra con justificación. En un APK que se instala fuera de una
tienda, cada paquete es superficie de ataque heredada.

### 7.4 Identidad visual

El sistema web ya tiene tokens de diseño definidos. La app los refleja, no los
reinventa:

| Token web | Valor | Uso móvil |
|---|---|---|
| `--brand-red-primary` | `#8B0E16` | Barra superior, identidad |
| `--brand-gold-primary` | `#D4AF37` | Acento, estados destacados |
| `--brand-black` | `#1F1F1F` | Texto principal |
| `--ds-action` | `#1F6F4A` | Confirmar, entregar |
| `--ds-view` | `#6E1622` | Consultar, ver detalle |
| `--ds-edit` | `#C9A227` | Editar (texto oscuro encima) |
| `--ds-danger` | `#8B0E16` | Cancelar, marcar fallido |

`brand_colors.dart` los declara como constantes con un comentario que apunta al
archivo CSS de origen, para que una futura actualización de marca se propague.

**Adaptación, no copia.** El web usa densidad de escritorio; el móvil necesita:
objetivos táctiles de **48 dp mínimo** (un chofer con guantes, en movimiento),
contraste AA verificado — los colores `--ds-*` ya traen su ratio documentado —,
tipografía de 16 sp para lectura al sol, y acciones destructivas siempre tras una
hoja de confirmación, nunca a un toque.

### 7.5 Manejo de sesión

1. `SecureStore` guarda *access* y *refresh token* en Keystore (Android) /
   Keychain (iOS). **Nunca** en `SharedPreferences`.
2. `AuthInterceptor` añade `Authorization: Bearer`. Ante un `401`, pausa la cola,
   renueva con el refresh token una sola vez, y reintenta. Si la renovación falla,
   limpia la sesión y navega al login.
3. Cerrar sesión llama a `/api/auth/logout` para revocar del lado servidor. Un token
   que solo se borra del teléfono sigue siendo válido: eso no es cerrar sesión.
4. Al arrancar, `/me/capabilities` refresca los permisos. Si cambiaron, la pantalla
   principal cambia.

---

## 8. Fase 4 — Offline-first

El chofer pierde señal. Si la app depende de la red para marcar una entrega, el
chofer vuelve al papel y el proyecto fracasa.

### 8.1 Patrón outbox

**Lectura:** cada respuesta de la API se guarda en SQLite. La UI lee siempre de
SQLite. La red actualiza SQLite. Nunca hay una pantalla en blanco esperando red.

**Escritura:**

1. La acción se escribe en la tabla `outbox` con un `syncGuid` (UUID v4), la carga
   útil y estado `pendiente`.
2. El estado local se actualiza **de inmediato** — el chofer ve "Entregado".
3. `SyncService` procesa la cola cuando hay red: FIFO, reintentos con *backoff*
   exponencial (1 s, 2 s, 4 s… tope 5 min), máximo 10 intentos.
4. Respuesta `2xx` o `Duplicado: true` → `confirmado`. `422`/`409` → `conflicto`,
   visible en la UI para que el chofer decida. `5xx` o red caída → reintento.
5. Una insignia en la barra muestra cuántas acciones faltan sincronizar. La
   transparencia evita el "yo sí lo marqué".

```dart
// core/sync/outbox_entry.dart
class OutboxEntry {
  final String syncGuid;      // idéntico al @SyncGuid del SP
  final String endpoint;
  final String payloadJson;
  final int attempts;
  final DateTime? nextAttemptAt;
  final OutboxStatus status;  // pendiente | enviando | confirmado | conflicto
  final String? lastError;
}
```

El `syncGuid` es el mismo identificador de punta a punta: cliente → API → *stored
procedure*. Por eso los reintentos son seguros y la duplicación es imposible por
construcción, no por suerte.

### 8.2 Qué funciona sin red y qué no

| Funciona offline | Requiere red |
|---|---|
| Ver rutas y paradas descargadas | Descargar una ruta nueva |
| Marcar entregado / fallido | Login inicial |
| Registrar kilometraje | Renovar sesión expirada |
| Abrir Waze (si Waze tiene el mapa) | Subir evidencia (se encola) |
| Capturar foto y firma (se encolan) | |

La app **declara** su estado con una franja superior persistente: "Sin conexión ·
3 acciones pendientes". Sin adivinanzas.

### 8.3 Límite honesto: los conflictos existen

Si el chofer marca una entrega como entregada sin señal y, mientras tanto, un
administrador cancela ese pedido desde la web, la sincronización fallará con `422`.
La app **no debe** decidir sola: muestra el conflicto, explica qué pasó y ofrece
descartar o contactar a operaciones. Resolver conflictos automáticamente en logística
es cómo se pierde inventario.

---

## 9. Fase 5 — Evidencias y almacenamiento compartido

### 9.1 El problema que hay que resolver antes de escribir código

`FileEvidenceStorageService` escribe en disco local, fuera de `wwwroot`. Si el MVC y
la API se despliegan como **App Services distintos**, la evidencia que suba el móvil
a la API **no existirá** para el endpoint de descarga del MVC. La foto se pierde en
silencio, que es la peor forma de perderla.

Hay tres salidas:

1. **Azure Blob Storage detrás de `IEvidenceStorageService`** — correcta. Ya es una
   interfaz, así que el cambio está contenido. Ambos servicios apuntan al mismo
   contenedor privado; la descarga se sirve con SAS de vida corta.
2. **Azure File Share montado en ambos App Services** — funciona, pero acopla el
   despliegue y complica el desarrollo local.
3. **Desplegar MVC y API en el mismo App Service** — resuelve el síntoma, ignora la
   causa, y se rompe la primera vez que se escale uno de los dos.

**Recomendación: opción 1.** Y hasta que exista, la Fase 3 se entrega **sin subida
de evidencias**: el chofer marca estados y kilometraje, que ya es la mayor parte del
valor. Entregar la mitad que funciona vence a entregar el todo que pierde fotos.

### 9.2 Cuando se implemente

- Comprimir en el cliente antes de subir: máx. 1600 px de lado mayor, JPEG calidad
  80. Una foto de 12 MP por entrega, por 40 entregas, en datos móviles, no es viable.
- Validar tipo y tamaño **en el servidor** (el límite de 6 MB de `RequestSizeLimit`
  ya existe en el MVC; replicarlo).
- La firma viaja como PNG en base64 — ya soportado por `StageSignatureAsync`.
- Subida en segundo plano con la misma cola outbox; la foto se guarda en el
  almacenamiento privado de la app hasta confirmarse.
- Mantener la compensación transaccional que ya hace `RegisterEvidence`: si el
  *commit* del archivo falla, se borra la fila pendiente.

---

## 10. Fase 6 — Bodeguero, vendedor y empleado

Se abre solo cuando el ciclo del chofer esté en producción y estable **dos semanas**.
Cada rol repite el mismo patrón: endpoints `v1` → feature Flutter → outbox → QA.

Antes de tocar el bodeguero queda una limpieza menor, ya sin decisión de por
medio:

1. ~~Consolidar los perfiles `Bodeguero` y `Bodega`.~~ En la base solo existe
   `Bodeguero`; basta con quitar `Bodega` de las listas de roles escritas a mano
   en el código.
2. ~~Definir si el alistamiento/picking se construye.~~ Descartado (§16).

---

## 11. Fase 7 — Distribución: QR y descarga desde el sistema web

### 11.1 Dónde se aloja el APK

**No en el repositorio.** El `.gitignore` ya prohíbe ZIP y binarios, y
`Test-RepositorySecrets.ps1` escanea archivos rastreados. Un APK de 30 MB por versión
degrada el repositorio permanentemente.

Opciones, en orden de preferencia:

1. **GitHub Releases** — versionado, checksum automático, gratis, URL estable.
2. **Azure Blob Storage** (contenedor de solo lectura o SAS larga) — si se prefiere
   no depender de GitHub.
3. **Google Play (canal cerrado)** — lo correcto a largo plazo: actualizaciones
   automáticas y sin advertencias de "orígenes desconocidos". Cuesta USD 25 una vez y
   días de revisión. Vale la pena si el sistema sale del ámbito universitario.

### 11.2 Sección nueva en el MVC

```
Proyecto_Final/
├── Controllers/MobileAppController.cs
├── Views/MobileApp/Index.cshtml
└── Models/Admin/MobileAppDownloadViewModel.cs
```

```csharp
[SessionAuthorize("Administrador", "Gerente", "Chofer", "Bodeguero", "Bodega", "Vendedor", "Empleado")]
public sealed class MobileAppController : Controller
{
    [HttpGet] public async Task<IActionResult> Index();       // QR + versión + instrucciones
    [HttpGet] public async Task<IActionResult> Qr();          // PNG del QR, generado en servidor
    [HttpGet] public async Task<IActionResult> Download();    // 302 al release vigente + auditoría
}
```

La página muestra: el QR, el número de versión y su fecha, el SHA-256 del APK,
instrucciones de instalación en tres pasos con capturas, y un enlace para reportar
problemas.

### 11.3 Decisiones de seguridad de esta página

- **Requiere sesión.** Un teléfono nuevo escanea el QR desde la pantalla de un
  compañero ya autenticado; el flujo sigue siendo de dos segundos. Un APK de
  distribución interna colgado en una URL pública es un regalo para quien quiera
  estudiar los endpoints.
- **`Download` no expone la URL de almacenamiento**: redirige. Así se puede cambiar
  el alojamiento, auditar cada descarga (`CreateAuditLogAsync`) y revocar sin
  redesplegar.
- **Publicar el SHA-256** junto al enlace. Es la única forma de que quien instala
  verifique que el archivo es el que el equipo publicó.
- **El QR se genera en el servidor** (paquete `QRCoder`) y no con una librería de
  CDN: la CSP de `SecurityHeadersMiddleware` restringe orígenes, y un QR generado por
  un tercero significa enviarle la URL de distribución.
- Los metadatos de versión viven en una tabla (`dbo.AppMovilVersiones`), no en
  `appsettings.json`. Publicar una versión no debe requerir un despliegue.

### 11.4 Compuerta de versión mínima

Sin tienda de aplicaciones no hay actualización automática. La app consulta
`GET /api/mobile/v1/app/version` al arrancar:

```jsonc
{ "latestBuild": 42, "latestVersion": "1.3.0", "minSupportedBuild": 38,
  "downloadUrl": "https://.../MobileApp/Download", "mandatoryMessage": "..." }
```

- `build < minSupportedBuild` → pantalla bloqueante con el enlace de descarga.
- `build < latestBuild` → aviso descartable.

Esto **hay que construirlo en la primera versión**, no después. Si la 1.0 sale sin
compuerta, nunca se podrá retirar del campo.

---

## 12. Fase 8 — CI/CD

Se agrega `.github/workflows/ci-mobile.yml`, separado del pipeline .NET para que un
fallo de Flutter no bloquee el build del backend:

```yaml
jobs:
  analyze:   # flutter analyze --fatal-infos
  test:      # flutter test --coverage
  build-apk: # solo en tags v*; sube el APK como artefacto del release
```

La firma del APK usa un *keystore* que **nunca** entra al repositorio: se guarda como
*GitHub Secret* en base64 y se materializa en el runner. Perder ese keystore obliga a
publicar la app con otra identidad y a que todos reinstalen; hay que respaldarlo
fuera de GitHub también.

`Test-RepositorySecrets.ps1` seguirá escaneando `Proyecto_Movil/`. Por eso la URL
base va por `--dart-define` y no escrita en el código.

---

## 13. Seguridad — lista de control

### Transporte y cliente
- [ ] HTTPS obligatorio. `android:usesCleartextTraffic="false"` y
      `network_security_config.xml` sin excepciones en release.
- [ ] Tokens solo en `flutter_secure_storage`. Nunca en `SharedPreferences` ni logs.
- [ ] Sin secretos en el APK: un APK se descompila en minutos.
- [ ] Logs de red deshabilitados en release (`kReleaseMode`).
- [ ] `FLAG_SECURE` en pantallas con datos de clientes (evita capturas y miniaturas
      en el conmutador de apps).

### Servidor
- [ ] Cada endpoint revalida rol y permiso; el claim solo pinta la interfaz.
- [ ] Pertenencia validada en el *stored procedure*, no en C#.
- [ ] `404` indistinguible entre "no existe" y "no es suyo".
- [ ] Nunca `ex.Message` al cliente.
- [ ] Rate limiting particionado por usuario del token.
- [ ] Toda escritura desde móvil se audita con canal `"Móvil"` para distinguirla en
      el módulo de auditoría.
- [ ] Refresh tokens revocables y con rotación.

### Datos y privacidad
- [ ] **Ubicación solo durante una ruta activa**, con explicación previa al usuario y
      opción de negarla sin perder el resto de la app. El rastreo continuo de
      empleados tiene implicaciones legales y laborales en Costa Rica; si el negocio
      lo quiere, se documenta, se informa por escrito al personal y se decide fuera
      de este plan.
- [ ] Sin datos personales de clientes en logs ni en telemetría.
- [ ] Caché local cifrada o purgada al cerrar sesión: un teléfono se pierde.
- [ ] Sin analítica de terceros en la primera versión.

---

## 14. Trampas concretas de este repositorio

Cosas que van a romper si nadie las ve venir.

1. **`ControllerSurfaceAuditTests.AuditScope_ContainsAll58MvcAndApiControllers`**
   afirma exactamente 56 controladores MVC y 2 de API
   (`Proyecto_Final.Tests/ControllerSurfaceAuditTests.cs:19-21`). Cada controlador
   nuevo rompe este test. Actualizar los números **en el mismo PR**, y verificar que
   el nuevo controlador aparezca en el resto de auditorías de seguridad.

2. **`EveryFormPostRequiresAntiforgeryExceptTheJsonOfflineSyncEndpoint`** exige
   `[ValidateAntiForgeryToken]` en todo `POST` del MVC con una única excepción. Es
   otra razón para que los endpoints móviles vivan en la API y no en el MVC.

3. **Numeración de migraciones inconsistente.** Existen dos archivos `0023_*`
   (`0023_supervisor_rrhh_permisos.sql` y
   `0023_toggle_product_status_returns_state.sql`), y el commit `b8c575c` aclara que
   0023 aún no se aplica en Azure DEV. Antes de crear la 0024, **consultar
   `dbo.SchemaMigrationHistory`**, que es la única fuente de verdad, y resolver la
   colisión.

4. **Migraciones con proceso formal.** Cada script nuevo requiere `.verify.sql`,
   `.rollback.md`, idempotencia, `XACT_ABORT`, sin `USE`, un ejecutor designado en el
   PR, BACPAC previo y registro en el ledger. Ver `database/migrations/README.md`.
   Presupuestar ese tiempo: no es "correr un script".

5. **Azure SQL en plan gratuito se duerme tras una hora.** La primera petición del
   día puede tardar cerca de un minuto. El cliente HTTP necesita timeout generoso
   (60 s) en el arranque, reintento con *backoff*, y un mensaje honesto —
   "Despertando el servidor, un momento" — en vez de un *spinner* infinito o un
   "error de conexión" falso.

6. **Las IP de desarrollo se registran a mano en el firewall de Azure SQL.** Un
   compañero nuevo en el proyecto móvil necesita `az login` y su regla de firewall
   antes de que nada funcione. Está documentado en el README raíz.

7. **`localhost` no existe para un teléfono.** En desarrollo, la API debe escuchar en
   la IP de la LAN y el certificado de desarrollo no será válido para el dispositivo.
   Ver `docs/acceso-red-local.md`. Nunca desactivar la validación de certificados en
   código que pueda llegar a release — si se hace para desarrollo, que sea tras
   `kDebugMode` y revisado en el PR.

8. **No tocar `appsettings.json` ni `appsettings.Development.json`** sin petición
   explícita (regla 1 de `CLAUDE.md`). La configuración de JWT requiere aprobación
   previa y llega por User Secrets y App Service Configuration.

9. **Commits en español con *conventional commits*** y trabajo en rama, nunca sobre
   `main`. Ejemplo: `feat(movil): agregar cola offline de entregas`.

10. **OneDrive y Flutter no se llevan.** Ver §3.1.

---

## 15. Secuencia y criterios de salida

| Fase | Entregable | No se avanza hasta que… |
|---|---|---|
| 0 | Decisiones aprobadas (ver §16) | ✅ Cerrada el 2026-09-15 |
| 1 | JWT + refresh revocable en la API | Login MVC intacto y los 8 criterios de §5.6 en verde |
| 2 | Endpoints `/api/mobile/v1` del chofer + migración de kilometraje propio | Probados contra Azure DEV, incluyendo intentos cruzados entre choferes |
| 3 | App Flutter: login, rutas, detalle, Waze, cambio de estado, kilometraje | Un chofer real completa una ruta real en su teléfono |
| 4 | Outbox y modo offline | Ruta completa en modo avión, sincronizada al recuperar señal, sin duplicados |
| 5 | Evidencias con almacenamiento compartido | Una foto subida desde el móvil se descarga desde la web |
| 6 | Bodeguero, vendedor, empleado | Fase 5 estable dos semanas en producción |
| 7 | Página con QR, descarga auditada y compuerta de versión | Un teléfono sin la app la instala escaneando, en menos de dos minutos |
| 8 | CI de Flutter y firma de release | Un tag produce un APK firmado y verificable |

**La Fase 3 ya es útil por sí sola.** Un chofer que ve su ruta, navega con Waze y
marca entregas desde el teléfono ya no necesita computadora, que es exactamente lo
que pidió el negocio. Todo lo demás se construye encima de algo que ya está en la
calle funcionando.

---

## 16. Decisiones tomadas

Resueltas el 2026-09-15. Cierran la Fase 0.

| # | Decisión | Resuelto | Consecuencia |
|---|---|---|---|
| 1 | Plataforma | **Solo Android** | No se crea la carpeta `ios/`. El código se mantiene libre de APIs exclusivas de Android para no cerrar la puerta |
| 2 | Distribución | **APK directo**, Play más adelante | La Fase 7 se construye con QR y descarga propia. La compuerta de versión mínima pasa a ser obligatoria, no opcional |
| 3 | Alistamiento / picking | **No se construye** | El bodeguero entra en la Fase 6 con stock, devoluciones, cuarentena y recepción de compras. Se elimina el mayor riesgo de alcance |
| 4 | Ubicación del chofer | **No se rastrea** | La app no pide permiso de ubicación en segundo plano. Las coordenadas que usa son las del cliente, que ya están en la base |
| 5 | Despliegue | **Separados** (ya lo están) | La Fase 5 necesita almacenamiento compartido: Azure Blob Storage detrás de `IEvidenceStorageService` |

### Sobre la decisión 2: qué cambia cuando se mueva a Google Play

Nada del código de la aplicación. Play reemplaza la Fase 7 completa —QR, descarga
auditada y compuerta de versión— por su propio canal de distribución y
actualización. Lo que sí hay que dejar listo desde ahora para no rehacerlo:

- **Versionado semántico con `versionCode` creciente** desde la 1.0. Play rechaza
  una subida cuyo `versionCode` no supere al anterior, y un APK directo mal
  numerado deja la secuencia rota.
- **Firma con un keystore propio y respaldado.** Es el mismo keystore que Play
  pedirá; perderlo obliga a publicar la app como si fuera otra.
- **`applicationId` definitivo desde el primer APK** (por ejemplo
  `cr.distribuidorajj.movil`). Cambiarlo después es una aplicación distinta para
  el teléfono: los usuarios tendrían dos instaladas.

### Sobre la decisión 5: el estado real del despliegue

> **Corregido el 2026-09-16.** Este apartado afirmaba que el despliegue
> documentado en `azure-despliegue-final-qa.md` seguía en pie. No era así: esos
> recursos viven en una suscripción de Azure que quedó **deshabilitada** y que
> Azure marcó de solo lectura. Sus App Services están en `AdminDisabled` y no
> se pueden reactivar mientras la suscripción siga así.

El ambiente vigente se recreó en la suscripción activa:

| Recurso | Nombre | SKU |
|---|---|---|
| App Service Plan | `asp-trilogia-cursos-dev` | Gratis F1, Linux, Central US |
| App Service API | `api-trilogia-free-cr01` | — |
| App Service MVC | `web-trilogia-free-cr01` | — |
| Servidor SQL | `sql-trilogia-free-cr01` | Serverless GP_S_Gen5_1 |
| Base de datos | `DistribuidoraJJ_DB` | — |

Ambas aplicaciones se conectan **sin contraseña**, con identidad administrada:
cada App Service tiene su propia identidad de Azure y un usuario en la base que
solo puede leer, escribir y ejecutar procedimientos. No son `db_owner` — una
aplicación web no necesita alterar el esquema, y si la comprometen, el daño
queda acotado.

Comparten plan pero son aplicaciones distintas, y **cada App Service tiene su
propio `/home`**. De ahí la consecuencia de la Fase 5: una foto subida al API no
existe para el MVC.

> **Regla de firewall a tener presente.** Para que los App Services alcancen la
> base hay una regla `AllowAllWindowsAzureIps`. Es la forma que define Azure
> para "permitir servicios de Azure": no abre la base a internet, pero sí
> permite que cualquier recurso de Azure intente conectarse — el acceso real lo
> sigue frenando la identidad. La alternativa cerrada, un *private endpoint*,
> no existe en el plan gratuito.

**Se mantienen separados.** Consolidarlos resolvería el síntoma del
almacenamiento pero introduciría problemas peores: un despliegue del MVC
reiniciaría la API que usan los choferes en la calle, y los límites de CPU del
plan F1 se compartirían entre el tráfico web y el móvil sin poder distinguirlos.

> **Límite del plan F1 a vigilar.** Sesenta minutos de CPU por día para todo el
> plan, sin *Always On*: las aplicaciones se duermen tras unos minutos sin
> tráfico. Con unos pocos choferes es suficiente; si la flota crece o las
> pantallas consultan seguido, el primer síntoma serán respuestas lentas o
> un `503` esporádico, y la solución es subir el plan, no optimizar la app.
