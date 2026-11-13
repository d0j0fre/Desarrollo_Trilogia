# Manual de usuario — Distribuidora JJ / Licorera La Bodega

Base del manual de usuario (curso Implantación). Un solo Word, dividido en una parte general y una parte por puesto, con índice interactivo.

| Archivo | Qué es |
|---|---|
| `mapa_puestos_modulos.md` | Qué ve cada perfil (web y app), qué módulo es de qué parte, reparto en 3 grupos y hallazgos. **Empiece aquí.** |
| `GUIA_ESTILO.md` | Cómo escribir un capítulo, con el ejemplo completo. |
| `HERRAMIENTAS.md` | Entorno, capturas anotadas (web y móvil), generación del Word. |
| `mapa.json` / `manual.json` | Datos del reparto y de la portada. |
| `capitulos/` | Texto del manual (Markdown). La Parte I y los anexos están completos. |
| `capturas/` | Especificaciones (`specs/`) e imágenes anotadas. |
| `salida/` | Word y PDF generados (no se guardan en git: se regeneran con `npm install` y `node herramientas/generar_word.js`; agregar `--puesto <id>` para el manual de un puesto). La versión entregada está en OneDrive, `Implantacion/Manual de Usuario/`. |

Ciclo de trabajo:

```powershell
node herramientas/capturar.js capturas/specs/<parte>.json
node herramientas/generar_word.js
.\herramientas\actualizar_word.ps1
```
