# Matriz de integración — `restructuracion-2.0` → `main`

Fuentes: rama `origin/restructuracion-2.0` (commit `9aa7045`, 29/10/2026) e informe del autor [`fuentes/INFORME-STAGE2-INTEGRACION.pdf`](fuentes/INFORME-STAGE2-INTEGRACION.pdf) (texto en `fuentes/INFORME-STAGE2-INTEGRACION.txt`; la rama trae también `docs/entrega-stage2/`).

Decisiones posibles: **Adoptar** · **Adoptar con ajustes** · **Mantener `main`** · **Posponer** (no entra en esta entrega) · **Descartar**.
Estado: `Propuesta` hasta que el equipo la apruebe.

## 1. Tamaño real de la reestructuración

| Dato | Valor |
|---|---|
| Base común con `main` | `6e36ae1` (12/08/2026) |
| Commits solo en la rama / solo en `main` | 64 / 2 (los 2 de `main` son la app móvil + API JWT) |
| Diferencia contra `main` | **719 archivos, +39 958 / −16 318 líneas** |
| Por área | Vistas 168 · BD 123 · Servicios MVC 86 · Tests 82 · Docs 51 · wwwroot 48 · API 39 · Controladores 36 · Modelos 33 · `shared/` 13 · `tools/ChatV2Broker` 2 |
| Archivos tocados por ambos lados | **141** (incluye `Program.cs` MVC y API, `AuthController.cs` de la API, `AdminDbService.cs`, `_Layout.cshtml`) |
| Pruebas declaradas por el autor | 1 262 .NET + 133 JS en verde, build Release sin advertencias. Son pruebas unitarias/contrato. **No equivalen a los 502 casos QA**, que nunca se corrieron sobre esta versión |
| Estado declarado por el autor | "Integración parcial", no certificada. Hay funciones que son solo *candidatos* probados en copias aisladas |
| App móvil | La rama no tiene `Proyecto_Movil` (no la toca), pero sí cambia `AuthController`, `Program.cs` y `appsettings` de la API que usa la app |

No es solo un cambio visual. Cambia login y sesiones, el modelo de pedidos y cotizaciones, las notificaciones por correo, los documentos, el chat (Chat V2 con un proceso *broker* aparte) y agrega 32 migraciones.

## 2. Riesgos críticos detectados

| # | Riesgo | Impacto | Mitigación |
|---|---|---|---|
| R1 | **Colisión de migraciones 0023–0028**: en `main` son la app móvil/JWT; en la rama son seguridad Stage 1 (ownership, revocación, hash-only, capacidades). Mismo número, contenido distinto. Además la rama ocupa 0029–0061 | Romper el ledger `SchemaMigrationHistory` de Azure o aplicar scripts en orden incorrecto | Renumerar **todas** las migraciones de la rama en un bloque nuevo (`0400+`) durante la integración, cotejando el hash, los objetos y las dependencias. Las correcciones de la Etapa 1 usan `0200–0319` (ver plan) |
| R2 | **API compartida con la app móvil**: la rama reescribe `AuthController`/`Program.cs` (login original 8 h, `OriginalLoginIssuer`) y `main` tiene la API JWT de la app | La app móvil deja de autenticar | Integrar la API conservando los endpoints JWT de `main`. Verificar con la app real (login por rol) antes de mergear |
| R3 | **Chat V2 requiere un proceso broker separado** (`tools/ChatV2Broker`, named pipes) con una pausa de ~46 s sin causa demostrada; el chat departamental y de clientes (D/C) es solo candidato | No desplegable con certeza en Azure ni en la presentación | **Posponer Chat V2.** Se mantiene el chat de `main` y se corrigen ahí los BUG-23-xx (lote de Gerald) |
| R4 | **Nuevo modelo de estados de pedido** (Liberado / Facturado / Entregado, aprobación > ₡2 000 000, cotizaciones del servidor, recibos UUID) | Toca los mismos flujos que los lotes E3/E4 (estados desde la app, liquidaciones) y la app móvil | E3/E4 se corrigen en `main`; el modelo nuevo se decide en la integración (D2) |
| R5 | **Notificaciones nuevas por correo** (factura, entrega, despacho de ruta, contacto) con evidencia local; boletas y alertas documentales solo candidatas | Se superpone con E5 (correos) | Adoptar los 4 canales con evidencia en lugar de reescribirlos en E5. Boletas: corregir en `main` (BUG-11-05) |
| R6 | Cuatro vistas de Flota/Vehículos marcadas por el autor como "modificaciones previas protegidas" de procedencia dudosa | Pisar trabajo de otro compañero | Mantener `main` y revisar con el autor |
| R7 | Tamaño: 141 archivos en conflicto hoy, más los que toquen las correcciones | Integración larga y costosa en uso de IA | Integrar por capas (ver §4) y que cada PR de corrección deje su sección "Re-aplicar en el nuevo diseño" |

## 3. Matriz por grupo (propuesta)

| Grupo | Contenido en la rama | Evidencia del autor | Propuesta | Condición |
|---|---|---|---|---|
| G1 Sistema visual | Tokens/CSS `stage2`, `_WorkspaceLayout`, `_StorefrontLayout`, Field, componentes, dashboards, superficies públicas y de error | Pruebas de presentación | **Adoptar con ajustes** | Conservar campos, mensajes y botones de las correcciones; responsive y accesibilidad por rol |
| G2 Seguridad Stage 1 | Ownership, revocación de sesiones, hash-only, capacidades efectivas, separación lectura/mutación | Tests + PR #127 | **Adoptar con ajustes** | Renumerar migraciones (R1). Puede cerrar BUG-04-03, 11-06, 09-02, 16-07 y 22-02 |
| G3 Login original 8 h y login transaccional | `OriginalLoginService`, migraciones 0034/0036, `SessionValidationMiddleware` | Evidencia local | **Adoptar con ajustes** | No romper la API JWT de la app (R2) |
| G4 Contratos de pedidos, facturación y stock | Procedimientos gerenciales/factura que faltaban, `CommercialOrderService`, recibos, checkout con precio del servidor | Evidencia local SQL/HTTP | **Adoptar con ajustes** (decisión **D2**) | El autor reporta que **faltaban SPs** de pedidos y factura: probable causa de BUG-09-03/09-05. Confirmar en el triaje |
| G5 Reclamos y garantías | SPs y resultsets corregidos, recibos UUID | Pruebas QA locales | **Adoptar** | Probable solución de BUG-14-03 |
| G6 Notificaciones por correo | Factura, entrega, despacho de ruta y contacto (0057–0059) | Envío real controlado | **Adoptar con ajustes** | SMTP por configuración/secretos; puede cerrar BUG-03-01, 08-01 y 10-08 (parcial) |
| G7 Boletas y alertas documentales | Contrato nuevo **no activado** | Solo copia aislada | **Posponer** | BUG-11-05 se corrige en `main` |
| G8 Documentos y delegación | Ownership, delegación, versiones (0040, 0055, 0056, 0061) | Evidencia local | **Adoptar con ajustes** | Ningún bug del QA lo exige; entra en la capa 4 si hay tiempo |
| G9 Chat V2 personal + broker | Hub, broker, archivo legacy | Evidencia local de P | **Posponer** (R3) | — |
| G10 Chat D/C | Candidato | Solo copia aislada | **Posponer** | — |
| G11 Relaciones comerciales / asignaciones | `CommercialAssignments`, 0047 | Datos de entorno | **Posponer** | Requiere datos reales de asignación |
| G12 Presupuestos legacy | Archivo de cuentas legacy (0046) | Ruta documentada | **Adoptar con ajustes** | Revisar si corrige BUG-22-01 |
| G13 Tests y scripts UI | 97 archivos de test, scripts JS, e2e | En verde en la rama | **Adoptar con ajustes** | Ajustar a los contratos finales tras integrar |
| G14 `shared/` y `tools/` | `JJ.Commerce`, `RestrictedSqlGate`, `LocalRuntimeControl` | — | Según G3, G4 y G9 | Solo lo que necesiten los grupos adoptados |
| G15 Exclusivo de `main` | App móvil, API JWT, migraciones 0023–0028, cuatro vistas de flota | QA ejecutado | **Mantener `main`** | No sobrescribir |
| G16 Material privado | Respaldos, datos QA, claves, logs, bin/obj | — | **Descartar** del repo | — |

## 4. Orden de integración (Etapa 3 del plan)

1. **Capa 0, migraciones:** renumerar las de la rama, armar el orden único y probarlo en LocalDB desde cero y sobre una copia de Azure.
2. **Capa 1, G1 visual + G13 tests:** sin lógica nueva.
3. **Capa 2, G2 + G3 seguridad/login**, protegiendo la API JWT de la app.
4. **Capa 3, G4 + G5 + G6 + G12 flujos comerciales y correo**, módulo por módulo con sus casos QA.
5. **Capa 4, G8 y lo que quede** si hay tiempo. G7, G9, G10 y G11 quedan fuera de esta entrega.

## 5. Decisiones que el equipo debe tomar

| ID | Decisión | Recomendación |
|---|---|---|
| D1 | ¿Chat V2 entra en esta entrega? | **No.** Se corrige el chat de `main` |
| D2 | ¿Se adopta el modelo comercial nuevo (estados Liberado/Facturado/Entregado, aprobación por monto, cotización del servidor)? | **Se decide en la Etapa 3, según el tiempo.** E3 y E4 se corrigen normalmente en `main` (la app móvil depende de ellos); si se adopta el modelo nuevo, se adapta al integrar |
| D3 | Boletas y alertas documentales | Corregir en `main` y posponer los candidatos |
| D4 | Numeración de migraciones | Correcciones en `0200–0319`; la rama se renumera a `0400+` al integrar |
| D5 | Bugs que la rama ya resuelve | Cada sesión revisa la rama antes de corregir; si está resuelto, adapta esa solución a `main` |

## 6. Cuándo se integra

Primero se corrigen todos los bugs en `main` y se re-prueba (Etapas 1 y 2): esa es la entrega asegurada. Luego, según el tiempo que quede antes de la presentación:

| Escenario | Qué se integra |
|---|---|
| Poco tiempo | Solo la capa 1 visual (G1), que es la de menor riesgo y la que más se nota |
| Tiempo medio | Capas 1 y 2 (visual + seguridad/login, protegiendo la API JWT de la app) |
| Tiempo suficiente | Capas 1–3, módulo por módulo con sus casos QA |

Siempre quedan fuera de esta entrega G7, G9, G10 y G11 (boletas y alertas nuevas, Chat V2, Chat D/C, asignaciones comerciales).
