# Herramientas del manual de usuario

Todo vive en `C:\dev\Manual_Usuario\` (fuera de git y de OneDrive). Node 24 y las dependencias ya están instaladas (`npm install` si falta `node_modules`).

```
Manual_Usuario/
├─ manual.json              Portada, versión, control de versiones, nombre del .docx
├─ mapa.json                Partes, puestos y módulos: quién es dueño de cada procedimiento (fuente única)
├─ mapa_puestos_modulos.md  Generado: reparto, qué ve cada perfil, tabla maestra y hallazgos
├─ hallazgos.md             Hallazgos escritos a mano (se agregan al mapa)
├─ GUIA_ESTILO.md           Cómo escribir un capítulo (con ejemplo completo)
├─ capitulos/<parte>/NN_tema.md   El texto del manual, en Markdown
├─ capturas/specs/<parte>.json    Especificaciones de capturas (una por parte)
├─ capturas/<parte>/*.png         Capturas anotadas que genera la herramienta
├─ recursos/                Logo de la portada
├─ herramientas/            Scripts (abajo)
└─ salida/                  Word, PDF de revisión, validación y páginas renderizadas
```

## 1. Poner en marcha el sistema para capturas

Cada sesión usa **su propia base** y **sus propios puertos**, para no pisarse los datos.

```powershell
# 1. Base propia (nombre en minúsculas, sin espacios): crea (localdb)\Trilogia_<nombre>
C:\dev\_basedatos\Nueva-BaseSesion.ps1 -Nombre manual2

# 2. Permisos de los perfiles de oficina (la base demo los trae vacíos; ver hallazgos.md n.º 1)
$pipe = ((SqlLocalDB.exe info Trilogia_manual2) | Select-String 'pipe name:').ToString().Split(' ')[-1]
& 'C:\Program Files\SqlCmd\sqlcmd.exe' -S $pipe -d DistribuidoraJJ_DB -E -I -b -i C:\dev\Manual_Usuario\herramientas\sql\fix_perfiles_oficina.sql

# 3. Datos de ejemplo de la parte general (una ruta despachada y tres mensajes de chat → avisos)
& 'C:\Program Files\SqlCmd\sqlcmd.exe' -S $pipe -d DistribuidoraJJ_DB -E -I -b -i C:\dev\Manual_Usuario\herramientas\sql\datos_demo_manual.sql
```

Compilar y arrancar (desde `Trilogia-Cursos` de su worktree; **nunca contra Azure**):

```powershell
dotnet build Proyecto_Final/Proyecto_Final.csproj -c Release -p:Deterministic=false
dotnet build Proyecto_FinalAPI/Proyecto_FinalAPI.csproj -c Release -p:Deterministic=false

$cs = 'Server=(localdb)\Trilogia_manual2;Database=DistribuidoraJJ_DB;Trusted_Connection=True;MultipleActiveResultSets=True;Encrypt=False;TrustServerCertificate=True;Connection Timeout=30;'
# API (en una terminal)
$env:ConnectionStrings__DefaultConnection=$cs; $env:ASPNETCORE_ENVIRONMENT='Development'
cd Proyecto_FinalAPI; dotnet bin\Release\net9.0\Proyecto_FinalAPI.dll --urls http://0.0.0.0:7271
# MVC (en otra terminal)
$env:ConnectionStrings__DefaultConnection=$cs; $env:ApiSettings__BaseUrl='http://localhost:7271/'; $env:ASPNETCORE_ENVIRONMENT='Development'
cd Proyecto_Final; dotnet bin\Release\net9.0\Proyecto_Final.dll --urls http://localhost:7270
```

Puertos sugeridos: base del manual 7170/7171; grupo 1 7270/7271; grupo 2 7370/7371; grupo 3 7470/7471.
Las herramientas leen estas variables (si no están, usan los valores de la base del manual):

| Variable | Para qué | Por defecto |
|---|---|---|
| `MANUAL_WEB` | Dirección del sitio | `http://localhost:7170` |
| `MANUAL_API` | Dirección de la API | `http://localhost:7171` |
| `MANUAL_INSTANCIA` | Instancia LocalDB para el campo `sql` de las capturas | `Trilogia_manual` |
| `MANUAL_CLAVE` | Clave de los usuarios demo de la base local (obligatoria para capturar) | sin valor: la herramienta se detiene |

Usuarios demo: `admin`, `gerente`, `supervisor`, `auditor`, `empleado`, `cajero`, `bodeguero`, `facturador`, `credito`, `compras`, `soporte`, `vendedor`, `chofer`, `cliente`, `mayorista` (todos `@labodega.cr`, clave de la base demo). En las especificaciones se escribe solo el alias (`"usuario": "gerente"`).

> El sitio permite **10 inicios de sesión por minuto** por IP. La herramienta reutiliza la sesión de cada usuario y, si choca con el límite, espera 30 s y reintenta.

## 2. Emulador y aplicación móvil

- Emulador: `Medium_Phone_API_36.1` (1080 × 2400). Arranque: `& "$env:LOCALAPPDATA\Android\Sdk\emulator\emulator.exe" -avd Medium_Phone_API_36.1 -no-snapshot-save`
- **Hay un solo emulador**: lo usa solo el grupo 3 (dueño de la Parte XVI). Si otro grupo necesita una captura móvil, se la pide.
- La app publicada solo acepta HTTPS. Para capturas se compila en modo *profile* apuntando a la API local y se instala:

  ```powershell
  C:\dev\Manual_Usuario\herramientas\movil\compilar_app.ps1 -Repo <worktree>\Trilogia-Cursos -Api http://10.0.2.2:7171/
  ```

  El script copia temporalmente una política de red que permite HTTP **solo** hacia `10.0.2.2` (la PC vista desde el emulador), compila, instala y **borra** la copia. No queda nada para subir a git (Flutter sí toca `android/gradle.properties` y `analysis_options.yaml`: devolverlos con `git checkout` si no se van a commitear). No use el modo debug: en el emulador se congela («isn't responding»).
- Para que la página **Aplicación móvil** del sitio muestre la descarga, publique una versión como admin (`herramientas/movil/app-manual.apk`, versión 1.2.0, compilación 5).
- La herramienta pone la barra de estado del emulador en modo demostración (batería llena, sin notificaciones, hora real) y quita el marco verde de ~4 px que agrega el emulador.

## 3. Capturas anotadas

```powershell
cd C:\dev\Manual_Usuario
node herramientas/capturar.js capturas/specs/gerente.json                     # todas las de la parte
node herramientas/capturar.js capturas/specs/gerente.json --solo ger-rutas-01,ger-rutas-02
```

Salida: `capturas/<carpeta>/<id>.png`. Se puede volver a correr cuantas veces haga falta (por ejemplo, si cambia el diseño): las imágenes se rehacen idénticas.

### Formato de la especificación

```json
{
  "carpeta": "gerente",
  "capturas": [
    {
      "id": "ger-retenidos-02",
      "usuario": "gerente",
      "url": "/ManagerOrders/Index",
      "ancho": 1280,
      "sql": "UPDATE ... (opcional: deja los datos como la captura los necesita)",
      "acciones": [ { "clic": "text=Aprobar" }, { "esperar": 500 } ],
      "recorte": { "selector": "main table", "margen": [20, 30, 20, 80] },
      "ocultar": [".chat-window"],
      "enmascarar": ["#Password"],
      "anotaciones": [
        { "tipo": "paso", "n": 1, "selector": "main table tbody tr >> nth=0", "posicion": "izquierda" },
        { "tipo": "flecha", "selector": "button:has-text('Aprobar')", "desde": "derecha", "texto": "Aprobar" }
      ]
    }
  ]
}
```

| Campo | Web / Móvil | Qué hace |
|---|---|---|
| `id` | ambos | Nombre del archivo (sin `.png`). Ver convención en GUIA_ESTILO.md. |
| `plataforma` | — | `"web"` (por defecto) o `"movil"`. |
| `usuario` | ambos | Alias del usuario demo; `null` = sin sesión. |
| `url` | web | Ruta del sitio (sin dominio). |
| `ancho`, `alto`, `escala` | web | Tamaño de la ventana (por defecto 1760 × 990, escala 1,1 → imagen de 1936 px). Use `1024` para pantallas de contenido: el texto queda más grande en la hoja. Gerente: `1920` y `escala 1` si se ve el menú completo. |
| `sql` | ambos | Texto SQL (o lista) que se ejecuta antes, en la base de `MANUAL_INSTANCIA`. Ideal para reponer el estado (avisos sin leer, borrar una jornada de prueba…). |
| `acciones` | ambos | Lo que se hace antes de fotografiar, en orden (tabla abajo). |
| `recorte` | ambos | Web: `"pagina"` (completa), `{ "selector", "indice", "todos", "margen" }` o `{ "x", "y", "ancho", "alto" }`. Sin recorte = la ventana visible. `margen` es un número o `[arriba, derecha, abajo, izquierda]` (deje lugar para los números de paso). Móvil: `{ "y", "alto" }`. |
| `ocultar` / `enmascarar` | web | Selectores que se esconden o se difuminan (datos sensibles). |
| `pausa` | ambos | Milisegundos de espera antes de la foto. |

Acciones web: `ir` (ruta), `clic` (espera la página nueva si la hay), `clicSinEsperar` (menús desplegables), `rellenar` `[selector, valor]`, `seleccionar` `[selector, valor]`, `marcar`, `hover`, `tecla` (`"Enter"`), `esperar` (ms), `esperarSelector`, `desplazar` (lleva el elemento al centro), `desplazarY` (px), `abrir` (abre un `<details>`, por ejemplo un área del menú Administración), `js` (código de página).

Acciones móviles: `tocar` (texto visible o `[x, y]`), `escribir`, `atras`, `ocultarTeclado`, `tecla` (código Android), `esperar` (ms), `esperarTexto`, `deslizar` `[x1, y1, x2, y2]`, `shell` (comando adb shell).

Selectores web: los de Playwright — CSS (`#Email`, `main form button[type=submit]`), texto (`button:has-text('Guardar')`), posición (`tr >> nth=0`).

### Anotaciones

| `tipo` | Dibuja | Opciones |
|---|---|---|
| `paso` | Recuadro + círculo con número | `n` (número), `posicion` preferida del círculo: `izquierda`, `derecha`, `arriba`, `abajo`, `arriba-derecha`, `arriba-izquierda`, `abajo-derecha`, `abajo-izquierda`, `dentro`; `recuadro: false` para solo el número. Si en esa posición taparía texto de la pantalla, otro recuadro u otro número, la herramienta prueba las demás y usa la primera libre (o la que menos tape). |
| `recuadro` | Recuadro | — |
| `flecha` | Flecha que apunta al elemento | `desde` (`izquierda`, `derecha`, `arriba`, `abajo`, `arriba-izquierda`…), `largo` (px), `texto` (etiqueta en la cola), `recuadro: true`. Si no cabe, cambia de lado sola. |
| `etiqueta` | Texto blanco sobre fondo magenta | `texto`, `posicion` |
| `foco` | Oscurece todo menos el elemento | `opacidad` (0,45), `recuadro: false` |

Cómo se ubica el elemento: **web** `selector` (+ `indice`, o `todos: true` para un recuadro que abarque todos los que coinciden) o `caja: {x,y,w,h}` en px de la página; **móvil** `elemento` (texto visible, pista del campo o descripción accesible; `indice` si hay varios) o `coordenadas: [x, y, ancho, alto]` en px del teléfono (1080 × 2400).

Para encontrar textos y coordenadas en el teléfono:

```powershell
node -e "const m=require('./herramientas/movil');console.log(m.nodos().filter(n=>n.texto||n.desc).map(n=>JSON.stringify([n.texto,n.desc,n.caja])).join('\n'))"
```

Estilo (fijo en `herramientas/anotar.js`): magenta `#E6007E` con borde blanco, trazo de 5 px, círculos de 22 px de radio; en móvil todo se agranda ×1,35 porque la imagen se inserta más angosta. Web y móvil se dibujan con el mismo código.

## 4. Generar el Word

```powershell
cd C:\dev\Manual_Usuario
node herramientas/generar_word.js                 # → salida\Manual_de_Usuario_Distribuidora_JJ.docx
.\herramientas\actualizar_word.ps1                # abre Word: actualiza índice, figuras y páginas; guarda; exporta el PDF
```

- `generar_word.js` arma portada, control de versiones, índice, índice de figuras, una sección por parte (encabezado con el nombre de la parte, pie con versión y página), la tabla automática de cada puesto (módulos propios y de otras partes, con enlaces), los capítulos y el anexo del mapa (`<!-- mapa-puestos -->`). Si un módulo de `mapa.json` todavía no tiene capítulo, inserta uno provisional «en preparación» con su ancla, para que ningún enlace quede roto.
- Avisos (enlaces rotos, capturas faltantes, anclas repetidas) en consola y en `salida\validacion.txt`. Con `--estricto` falla si hay alguno.
- `actualizar_word.ps1` usa Word instalado (COM). Hace dos pasadas para que los números de página del índice queden bien. `-SinPdf` para omitir el PDF.

Revisión rápida del resultado:

```powershell
node herramientas/pdf_a_png.js salida/Manual_de_Usuario_Distribuidora_JJ.pdf 40-49 salida/paginas 0.9   # páginas a PNG
node herramientas/hoja_contactos.js salida/paginas salida/hoja.png 5 40 49                               # una sola imagen
node herramientas/verificar_enlaces.js salida/Manual_de_Usuario_Distribuidora_JJ.pdf 3                   # enlaces del PDF
```

**Solo una sesión a la vez genera el Word final** (el gerente o quien él indique): las demás generan para revisar su parte, sin copiarlo a OneDrive.

## 5. Mapa de puestos y sondeos

```powershell
node herramientas/sondeo_perfiles.js   # entra con los 14 perfiles: menú visible y acceso real a ~65 módulos web
node herramientas/sondeo_movil.js      # funciones de la app que la API habilita a cada perfil
node herramientas/estructura.js        # regenera mapa_puestos_modulos.md (incluye hallazgos.md)
```

Si cambia el reparto, edite `mapa.json` (campo `dueno` de cada módulo) y regenere el Word: las tablas de cada parte y el anexo se actualizan solos.

## 6. Problemas conocidos

| Síntoma | Causa | Solución |
|---|---|---|
| Presupuestos/Gastos dan error 503 | Validación de decimales con cultura es-CR (hallazgos n.º 4) | Esperar el PR #131 o capturar en una máquina con Windows en inglés. |
| «Una directiva de Control de aplicaciones bloqueó este archivo» al arrancar | Smart App Control | Compilar con `-p:Deterministic=false`. |
| La app se congela («isn't responding») | Compilación debug | Usar `compilar_app.ps1` (modo profile). |
| `No se encontró «…» en la pantalla del emulador` | La pantalla no terminó de cargar o el texto es otro | Agregar `{ "esperar": 2000 }` o revisar el árbol con el comando de la sección 3. |
| Los avisos ya salen leídos | La app o una captura anterior los marcó | Campo `sql` con `UPDATE NotificacionesUsuario SET Leida=0 …`. |
| El menú del sitio se ve encimado | Muchos botones a menos de 1700 px | Usar el ancho por defecto (1760) o 1920. |
