# Proyecto_Movil — Aplicación de operación

Aplicación Android para choferes de **Distribuidora JJ**.
Consume `Proyecto_FinalAPI`, nunca el sitio web.

Plan completo: [`docs/plan-app-movil-flutter.md`](../docs/plan-app-movil-flutter.md).

---

## Qué hace

Un chofer entra con el mismo correo y contraseña del sistema web, y desde el
teléfono puede:

- Ver sus rutas asignadas y el detalle de cada parada, en orden.
- Abrir la navegación en Waze o Google Maps.
- Llamar al cliente.
- Marcar una entrega como entregada o no entregada, con motivo.
- Abrir y cerrar la jornada de kilometraje del vehículo de su ruta.
- Ver qué acciones suyas todavía no salieron del teléfono.

**Todo lo anterior funciona sin señal**, salvo descargar rutas nuevas y el
inicio de sesión. Lo que se marca sin conexión sale solo al recuperarla.

---

## Arranque rápido

```bash
flutter pub get
flutter run
```

Sin `--dart-define`, la aplicación apunta a la API publicada en Azure
(`AppConfig.apiBaseUrl`). Para probar contra una API local, indicala:

```bash
flutter run --dart-define=API_BASE_URL=https://<ip-de-tu-maquina>:57540/
```

`localhost` no existe para un teléfono: hay que apuntar a la IP de la máquina
en la red local, o usar `10.0.2.2` desde el emulador de Android.

> Antes el valor por defecto era `10.0.2.2`. Un `flutter run` en un teléfono
> real sin el `--dart-define` quedaba apuntando a una dirección inalcanzable y
> el login respondía "Sin conexión" aunque hubiera señal.
Ver [`docs/acceso-red-local.md`](../docs/acceso-red-local.md).

### Requisitos

- Flutter 3.38 o superior (probado en 3.38.1).
- JDK 17 o superior.
- `Proyecto_FinalAPI` corriendo con las migraciones 0024, 0025 y 0026 aplicadas.

---

## Arquitectura

Tres capas, organización *feature-first*:

```
presentation  →  application  →  domain  ←  data
   (widgets)     (controllers)  (entidades,   (API, SQLite,
                                 interfaces)   implementaciones)
```

La regla que sostiene todo: **`domain` no importa nada de Flutter ni de Dio.**
Si mañana cambia el cliente HTTP, el dominio no se entera, y se prueba sin
levantar un widget.

```
lib/
├── core/
│   ├── config/       app_config.dart — la URL llega por --dart-define
│   ├── network/      api_client.dart · api_exception.dart
│   ├── storage/      secure_store.dart (Keystore) · app_database.dart (SQLite)
│   ├── sync/         outbox_entry · outbox_repository · sync_service
│   ├── theme/        brand_colors.dart — espejo de brand-system.css
│   ├── routing/      app_router.dart — go_router con guardas
│   ├── widgets/      SyncBanner · EmptyState · ErrorView · confirmSheet
│   └── providers.dart — todo el cableado de dependencias
│
├── features/
│   ├── auth/            login, sesión, permisos
│   ├── home/            inicio, resumen del día, pendientes, compuerta de versión
│   ├── driver_routes/   rutas, detalle, marcado de entregas
│   ├── mileage/         apertura y cierre de jornada
│   └── settings/        perfil y cierre de sesión
│
└── shared/utils/    navigation_launcher.dart — Waze → Google Maps → navegador
```

### Cómo funciona sin señal

**Lectura:** cada respuesta de la API se guarda en SQLite. La interfaz lee
siempre de SQLite; la red actualiza SQLite. Nunca hay una pantalla en blanco
esperando red.

**Escritura:** la acción entra a la tabla `outbox` con un `syncGuid` generado
en el teléfono, el estado local cambia de inmediato, y `SyncService` la envía
cuando haya señal, con reintentos de 1 s, 2 s, 4 s… hasta un tope de 5 minutos.

Ese `syncGuid` es el mismo identificador de punta a punta: teléfono → API →
procedimiento almacenado. Por eso los reintentos son seguros y la duplicación
es imposible por construcción, no por suerte.

**Los conflictos no se resuelven solos.** Si la oficina canceló un pedido
mientras el chofer estaba sin señal, la acción queda marcada como conflicto y
la decide una persona. Resolver conflictos automáticamente en logística es cómo
se pierde inventario.

---

## Seguridad

- **Tokens en el Keystore de Android** (`flutter_secure_storage`), nunca en
  preferencias planas.
- **Sin tráfico en claro.** `usesCleartextTraffic="false"` y
  `network_security_config.xml` sin excepciones, ni siquiera para desarrollo.
- **Sin secretos en el APK.** La URL del servidor llega por `--dart-define`.
  Un APK se descompila en minutos.
- **Sin registro de red en release.** Un log de peticiones deja direcciones de
  clientes al alcance de cualquiera con el teléfono en la mano.
- **Sin respaldo en la nube** de la sesión ni de la base local: restaurarlo en
  otro teléfono revive la sesión sin autenticarse.
- **Sin permiso de ubicación.** Por decisión de negocio no se rastrea al
  chofer; las coordenadas para navegar son las del cliente y vienen del
  servidor.
- **La aplicación no decide accesos.** Los permisos que trae el token sirven
  para saber qué dibujar; cada endpoint los revalida contra la base.

---

## Compilar para distribuir

```bash
flutter build apk --release --split-per-abi \
  --dart-define=API_BASE_URL=https://<api-de-produccion>/
```

`--split-per-abi` baja el APK de ~50 MB a ~20 MB por arquitectura. Con
distribución por QR, son 30 MB menos de datos móviles por instalación.

### Firma

El keystore **no se versiona**. Se configura de dos maneras:

1. Local: `android/key.properties` (ya está en `.gitignore`)

   ```properties
   storeFile=../../claves/release.jks
   storePassword=...
   keyAlias=labodega
   keyPassword=...
   ```

2. Servidor de compilación: variables `ANDROID_KEYSTORE_PATH`,
   `ANDROID_KEYSTORE_PASSWORD`, `ANDROID_KEY_ALIAS`, `ANDROID_KEY_PASSWORD`.

> **El keystore es la identidad de la aplicación.** Perderlo obliga a publicar
> con otro `applicationId`, y todos los choferes tendrían que desinstalar y
> reinstalar. Respaldalo fuera de este repositorio y fuera de GitHub.

### Publicar una versión

1. Subir el APK a GitHub Releases o Azure Blob Storage.
2. Calcular su SHA-256.
3. Registrarla en el sistema web: **Aplicación móvil → Publicar versión nueva**.

El `versionCode` debe ser siempre mayor que el anterior. Es lo que sostiene la
compuerta de versión mínima, y más adelante es requisito de Google Play.

---

## Verificación antes de publicar

```bash
flutter analyze --fatal-infos
flutter test
```

Y en un teléfono real, el recorrido completo:

1. Entrar con un usuario chofer.
2. Abrir una ruta y navegar a una parada.
3. **Poner el teléfono en modo avión.** Marcar dos entregas y abrir jornada.
4. Confirmar que la franja superior dice cuántas acciones están pendientes.
5. Quitar el modo avión y confirmar que la cola se vacía sin duplicar nada.
6. Verificar en el sistema web que los estados llegaron una sola vez.
