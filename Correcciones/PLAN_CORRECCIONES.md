# Plan de correcciones — Distribuidora JJ / Licorera La Bodega

Fuente oficial: `Entregas/G8_SC803_J_BugReport.pdf` (91 errores), con el texto completo en
[`fuentes/BugReport_G8_SC803_J.txt`](fuentes/BugReport_G8_SC803_J.txt). Estado por error en [`BACKLOG.md`](BACKLOG.md).
Reparto para el equipo (versión de lectura): `Reparto_Correcciones.xlsx`, en la carpeta Implantacion de OneDrive.

| Prioridad | Errores |
|---|---:|
| Crítica (P1) | 7 |
| Alta (P2) | 19 |
| Media (P3) | 65 |
| **Total** | **91** |

BUG-09-05 aparece dos veces en el PDF (P1 y P2); se trata como un solo error P1.

## Etapas

| Etapa | Qué | Resultado |
|---|---|---|
| **1. Correcciones** | Los 91 errores sobre `main`, en 4 paquetes (~2 semanas) | Todo corregido |
| **2. Re-prueba de `main`** | Casos QA de los errores corregidos más un recorrido de regresión por módulo | `main` queda **listo**: es la entrega asegurada |
| **3. Reestructuración (por aparte)** | Se decide después de la Etapa 2, según el tiempo que quede. Ver [`REESTRUCTURACION.md`](REESTRUCTURACION.md) y [`MATRIZ_INTEGRACION.md`](MATRIZ_INTEGRACION.md) | No forma parte de las correcciones |

Durante las correcciones **no se usa ni se consulta** la rama `restructuracion-2.0`. Se corrige únicamente lo que pide cada error, sobre `main`.

---

## 1. Entorno de trabajo (obligatorio para todos)

| Qué | Dónde |
|---|---|
| Repositorio de trabajo | `C:\dev\Desarrollo_Trilogia` (clon limpio, **fuera de OneDrive**) |
| Repositorio remoto | https://github.com/d0j0fre/Desarrollo_Trilogia — rama base `main` |
| Documentación QA y entregas | `OneDrive\...\Implantacion\Proyecto3\Desarrollo_Trilogia\Entregas` y `Trilogia-Cursos\QA-Ejecucion*` (no se mueven al repo) |
| Evidencias de cada error | `...\Trilogia-Cursos\QA-Ejecucion*\evidencias\` y JSON en `...\QA-Ejecucion*\bugs*.json` |
| Credenciales y scripts de BD | `OneDrive\...\Proyecto3\_local_setup` (fuera de git a propósito) |

> La copia antigua en OneDrive tenía el `.git` corrupto. **No usar esa carpeta para programar ni hacer commits.**

Compañeros: clonar con `git clone https://github.com/d0j0fre/Desarrollo_Trilogia C:\dev\Desarrollo_Trilogia`, nunca dentro de OneDrive.

### Base de datos

- Desarrollo y pruebas: **LocalDB** (`_local_setup/Build-LocalDb.ps1`).
- **Azure SQL compartido**: plan gratuito con ~55 h de cómputo al mes. No dejar las apps apuntando a Azure mientras se programa; solo se usa para la prueba final.
- Cambios de tablas o procedimientos: script nuevo en `Trilogia-Cursos/database/migrations/` siguiendo su `README.md` (idempotente, con `.verify.sql` y nota de rollback). **Nadie aplica scripts en Azure**; Esteban los aplica después de unir cada rama.
- Cada bloque de trabajo usa su propio rango de numeración para que los scripts no se repitan:

| Bloque | Rango | Bloque | Rango |
|---|---|---|---|
| Esteban · Pedidos y correos | 0200–0209 | Esteban · Comodatos | 0240–0249 |
| Esteban · Productos y promociones | 0210–0219 | Esteban · Asistente virtual | 0250–0259 |
| Esteban · App móvil | 0220–0229 | Danny | 0260–0279 |
| Esteban · Carrito, portal y clientes | 0230–0239 | Gerald | 0280–0289 |
| | | David | 0290–0299 |

## 2. Flujo en GitHub

1. Una rama por persona (o por bloque, en el caso de Esteban) desde `main` actualizado: `correcciones-<nombre>` o `correcciones-esteban-<tema>`.
2. Commits pequeños, uno por error o por grupo de errores con la misma causa: `fix(BUG-05-01): guardar producto responde 405`.
3. Antes de pedir revisión: `dotnet build Trilogia-Cursos/Proyecto_Final.slnx` sin errores y `dotnet test` en verde. Móvil: `flutter analyze` sin errores.
4. Pull request hacia `main` con la lista de errores corregidos, cómo se verificó y los scripts SQL nuevos. Actualizar en el mismo PR las filas propias de `BACKLOG.md`.
5. Esteban revisa y une los PR de uno en uno. Si `main` avanzó, la rama se actualiza antes de unirla.
6. Se corrige solo lo que el error pide. No se rediseñan pantallas ni se reformatean archivos.

Archivos que todos pueden tocar (cambiar solo lo necesario y avisar): `Services/AdminDbService.cs`, `Program.cs`, `Views/Shared/_Layout*.cshtml`, menús por rol y `database/00_todo_en_uno.sql`.

## 3. Cuándo un error está terminado

- Reproducido antes del cambio, o con la causa explicada si no se reproduce en LocalDB.
- Cumple el **resultado esperado** del reporte de errores.
- Build y tests en verde; prueba nueva cuando la lógica lo permite.
- Fila del error en `BACKLOG.md` con estado `Corregido`, rama/PR y una nota corta (y en la hoja de Excel de cada uno).

## 4. Reparto

Cada paquete agrupa áreas que dependen entre sí, para que dos personas no trabajen sobre lo mismo. Esfuerzo = cada error difícil vale 3 puntos, uno medio 2 y uno sencillo 1.

| Paquete | Responsable | Áreas | Errores | Esfuerzo |
|---|---|---|---:|---:|
| 1 | **Esteban** (con sesiones de IA) | Productos, promociones y combos · carrito, portal del cliente y devoluciones · clientes y crédito · comodatos · pedidos y facturación · asistente virtual · recuperación de contraseña y correos · app móvil y notificaciones | 44 | 96 |
| 2 | Danny | Roles y permisos · empleados y planilla · presupuestos y gastos | 16 | 36 |
| 3 | Gerald | Rutas, entregas y liquidaciones · orden de visitas · flota · Mi perfil | 14 | 25 |
| 4 | David | Reportes y tablero gerencial · metas de vendedores · sugerencias de compra y tendencias · chat interno | 17 | 26 |

### Puntos donde se tocan las áreas

| Quiénes | Tema | Qué acordar |
|---|---|---|
| Esteban y Gerald | Estados de los pedidos | La app móvil cambia el pedido a En ruta / Entregado / No entregado (Esteban) y la liquidación de rutas usa esos estados (Gerald). Acordar los nombres antes de empezar |
| Esteban y Gerald | Entregas pendientes | El resumen del Asistente (Esteban) cuenta las entregas pendientes de las rutas; Gerald es dueño de las rutas y Esteban solo lee |
| Esteban y Danny | Ingresos y Egresos | Danny da el permiso al Gerente (BUG-09-02); Esteban arregla la pantalla que se cae (BUG-09-04) |
| Esteban y Danny | Correos | Esteban arregla el envío de correos (BUG-01-01, BUG-03-01); la boleta de pago en PDF de Danny (BUG-11-05) usa ese mismo envío |
| Danny y todos | Roles y permisos | Los cambios de permisos afectan los menús de todos (por ejemplo, el acceso del Auditor al reporte de equipos prestados, BUG-16-07). Danny avisa antes de subirlos |

## 5. Detalle por paquete

Para cada error, leer su bloque en `fuentes/BugReport_G8_SC803_J.txt` (pasos, resultado actual y esperado).

### Paquete 1 — Esteban (sesiones de IA, máximo 3 en paralelo)

| Bloque | Errores | Fase |
|---|---|:---:|
| Pedidos y facturación que se caen, recuperación de contraseña y correos | BUG-09-01, BUG-09-03, BUG-09-04, BUG-09-05, BUG-14-03, BUG-01-01, BUG-01-02, BUG-01-03, BUG-03-01 | 1 |
| Productos, promociones y combos | BUG-05-01, BUG-05-02, BUG-05-03, BUG-05-04, BUG-20-01, BUG-17-01, BUG-17-02, BUG-18-01 | 1 |
| App móvil y notificaciones | BUG-07-01, BUG-07-02, BUG-08-01, BUG-10-06, BUG-10-08, BUG-23-05, BUG-25-04 | 1 |
| Carrito, portal del cliente, devoluciones, clientes y crédito | BUG-12-01, BUG-12-02, BUG-12-03, BUG-14-01, BUG-14-02, BUG-06-01, BUG-06-02, BUG-06-03 | 2 |
| Comodatos (equipos prestados a clientes) | BUG-16-01, BUG-16-02, BUG-16-03, BUG-16-04, BUG-16-05, BUG-16-06 | 2 |
| Asistente virtual y sugerencias en Venta móvil | BUG-26-01, BUG-26-02, BUG-26-03, BUG-26-04, BUG-26-05, BUG-26-06 | 3 |

Fases (días desde que arranca la Etapa 1): **1** = días 1–5 · **2** = días 5–9 · **3** = días 9–12 · días 12–14: cierre y prueba en Azure. El bloque de carrito y clientes va en la Fase 2 porque depende de que productos y pedidos ya funcionen.

Notas:
- Las pantallas de pedidos que se caen (BUG-09-03, BUG-09-04, BUG-09-05 y BUG-14-03) probablemente comparten causa (procedimientos o contratos distintos entre el código y la base). Investigarlas juntas.
- BUG-01-02: la HU pide indicar que el correo no está registrado. Se implementa como lo pide la HU.

### Paquete 2 — Danny
- Roles y permisos: BUG-04-01, BUG-04-02, BUG-04-03, BUG-09-02, BUG-16-07.
- Empleados y planilla: BUG-11-01, BUG-11-02, BUG-11-03, BUG-11-04, BUG-11-05, BUG-11-06, BUG-11-07.
- Presupuestos y gastos: BUG-22-01, BUG-22-02, BUG-22-03, BUG-22-04.

Orden sugerido: BUG-22-01 y BUG-11-02 (pantallas que se caen) → BUG-04-03 y BUG-11-06 (acceso que no se corta) → el resto.

### Paquete 3 — Gerald
- Rutas, entregas y liquidaciones: BUG-10-01, BUG-10-02, BUG-10-03, BUG-10-04, BUG-10-05, BUG-10-07, BUG-06-04.
- Orden de visitas de las rutas: BUG-25-01, BUG-25-02, BUG-25-03.
- Flota y mantenimiento: BUG-15-01, BUG-15-02.
- Mi perfil: BUG-02-01, BUG-02-02.

Orden sugerido: BUG-10-04 y BUG-10-05 → BUG-10-07 → el resto.

### Paquete 4 — David
- Reportes y tablero gerencial: BUG-13-01, BUG-13-02, BUG-13-03.
- Metas y desempeño de vendedores: BUG-21-01, BUG-21-02, BUG-21-03.
- Sugerencias de compra y tendencias: BUG-24-01, BUG-24-02, BUG-24-03, BUG-24-04, BUG-24-05, BUG-24-06, BUG-24-07.
- Chat interno: BUG-23-01, BUG-23-02, BUG-23-03, BUG-23-04.

Orden sugerido: BUG-24-02 → validaciones y mensajes → exportaciones a Excel (usar una sola forma de exportar en todo el paquete).

## 6. Organización de archivos (al cierre, sesión "Documentacion")

Estructura objetivo en OneDrive (solo documentos; el código vive en `C:\dev`):

```
Implantacion/
├── 01_Plantillas/
├── 02_Entregas_Oficiales/        ← PlanQA, CasosQA, BugReport, InformeQA (QA03) + versiones ENTREGABLE1
├── 03_Ejecucion_QA/              ← QA-Ejecucion, -03-13, -14-18, -Companeros (bitácoras, JSON, evidencias)
├── 04_Pruebas_de_compañeros/     ← DeCompañeros + Entregas/Pruebas compañeros
├── 05_Pruebas_a_Grupo1/          ← Pruebas-Grupo1
├── 06_Correcciones/              ← re-pruebas y evidencias de cada error corregido
└── _local_setup/                 ← credenciales (nunca a git)
```

`cuentas_prueba.md` y cualquier archivo con contraseñas no se suben a GitHub.
