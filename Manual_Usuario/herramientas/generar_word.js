// Genera el manual en Word (.docx) a partir de manual.json, mapa.json y los capítulos en Markdown.
//
//   node herramientas/generar_word.js            → salida/Manual_de_Usuario_Distribuidora_JJ.docx
//   node herramientas/generar_word.js --estricto → falla si hay enlaces rotos o capturas faltantes
//
// Después, herramientas/actualizar_word.ps1 abre el archivo en Word, actualiza el índice,
// el índice de figuras y los números de página, y exporta el PDF de revisión.
// Formato de los capítulos: GUIA_ESTILO.md.
const fs = require('fs');
const path = require('path');
const { marked } = require('marked');
const D = require('docx');
const {
  Document, Packer, Paragraph, TextRun, ImageRun, HeadingLevel, AlignmentType, Table, TableRow, TableCell,
  WidthType, BorderStyle, ShadingType, InternalHyperlink, ExternalHyperlink, Bookmark, SimpleField, TableOfContents,
  Header, Footer, PageNumber, NumberFormat, TabStopType, LevelFormat, PageBreak, VerticalAlign, TableLayoutType,
} = D;

const RAIZ = path.resolve(__dirname, '..');
const estricto = process.argv.includes('--estricto');
const manual = JSON.parse(fs.readFileSync(path.join(RAIZ, 'manual.json'), 'utf8'));
const mapa = JSON.parse(fs.readFileSync(path.join(RAIZ, 'mapa.json'), 'utf8'));

// ───────────────────────── Estilo ─────────────────────────
const C = { vino: '7A1424', oro: 'B08A2E', texto: '262626', gris: '595959', linea: 'D9D9D9', fondoTabla: 'F7F3EE' };
const FUENTE = 'Calibri';
const CAJAS = {
  NOTA: { titulo: 'Nota', borde: '1F6FB2', fondo: 'EAF2FB', icono: 'ℹ' },
  CONSEJO: { titulo: 'Consejo', borde: '2E7D32', fondo: 'EAF5EA', icono: '✔' },
  ADVERTENCIA: { titulo: 'Advertencia', borde: 'C62828', fondo: 'FDECEA', icono: '⚠' },
  IMPORTANTE: { titulo: 'Importante', borde: 'B08A2E', fondo: 'FBF4E2', icono: '★' },
};
const ANCHO_UTIL_CM = 21.59 - 2 * 2.2;           // carta con márgenes de 2,2 cm
const cmApx = cm => Math.round(cm / 2.54 * 96);  // la librería mide las imágenes en px a 96 ppp
const cmAtw = cm => Math.round(cm / 2.54 * 1440); // twips

// ───────────────────────── Utilidades ─────────────────────────
const avisos = [];
const avisar = m => avisos.push(m);
const romano = n => ['', 'I', 'II', 'III', 'IV', 'V', 'VI', 'VII', 'VIII', 'IX', 'X', 'XI', 'XII', 'XIII', 'XIV', 'XV', 'XVI', 'XVII', 'XVIII', 'XIX', 'XX'][n] || String(n);
function marcador(id) { // nombre de marcador válido en Word (≤ 40, letra inicial, sin guiones)
  let b = 'm_' + id.normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/[^A-Za-z0-9_]/g, '_');
  return b.length > 40 ? b.slice(0, 40) : b;
}
function slug(t) { return t.toLowerCase().normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, ''); }
function tamPng(buf) { return { w: buf.readUInt32BE(16), h: buf.readUInt32BE(20) }; }
function decodificar(s) { return s.replace(/&amp;/g, '&').replace(/&lt;/g, '<').replace(/&gt;/g, '>').replace(/&quot;/g, '"').replace(/&#39;/g, "'"); }
const anclasDefinidas = new Set();
const anclasUsadas = []; // { id, donde }

// ───────────────────────── Texto en línea ─────────────────────────
// Convierte los tokens en línea de marked en runs de docx.
function enLinea(tokens, fmt = {}, donde = '') {
  const out = [];
  for (const t of tokens || []) {
    switch (t.type) {
      case 'text':
        if (t.tokens && t.tokens.length) out.push(...enLinea(t.tokens, fmt, donde));
        else out.push(new TextRun({ text: decodificar(t.text), ...fmt }));
        break;
      case 'escape': out.push(new TextRun({ text: decodificar(t.text), ...fmt })); break;
      case 'strong': out.push(...enLinea(t.tokens, { ...fmt, bold: true }, donde)); break;
      case 'em': out.push(...enLinea(t.tokens, { ...fmt, italics: true }, donde)); break;
      case 'del': out.push(...enLinea(t.tokens, { ...fmt, strike: true }, donde)); break;
      case 'codespan':
        out.push(new TextRun({ text: decodificar(t.text), ...fmt, font: 'Consolas', size: 20, shading: { type: ShadingType.CLEAR, fill: 'EFEFEF', color: 'auto' } }));
        break;
      case 'br': out.push(new TextRun({ text: '', break: 1 })); break;
      case 'link': {
        const hijos = enLinea(t.tokens, { ...fmt, style: 'Hyperlink' }, donde);
        if (t.href.startsWith('#')) {
          const id = t.href.slice(1);
          anclasUsadas.push({ id, donde });
          out.push(new InternalHyperlink({ anchor: marcador(id), children: hijos }));
        } else out.push(new ExternalHyperlink({ link: t.href, children: hijos }));
        break;
      }
      case 'image': out.push(new TextRun({ text: `[imagen: ${t.text}]`, ...fmt })); break; // imágenes en línea: se tratan como figuras aparte
      case 'html': break; // comentarios <!-- --> se ignoran
      default: if (t.text) out.push(new TextRun({ text: decodificar(t.text), ...fmt }));
    }
  }
  return out;
}

// ───────────────────────── Figuras ─────────────────────────
let numFigura = 0;
function resolverImagen(ruta, dirCap) {
  for (const base of [dirCap, RAIZ]) {
    const p = path.resolve(base, decodeURIComponent(ruta));
    if (fs.existsSync(p)) return p;
  }
  return null;
}
function figura(img, dirCap, sangria = 0, enTabla = false) {
  numFigura++;
  const archivo = resolverImagen(img.href, dirCap);
  const opciones = Object.fromEntries((img.title || '').split(/[;,\s]+/).filter(Boolean).map(x => x.split('=')));
  const parrafos = [];
  if (!archivo) {
    avisar(`Captura faltante: ${img.href}`);
    parrafos.push(new Paragraph({
      alignment: AlignmentType.CENTER, indent: { left: sangria }, keepNext: true, spacing: { before: 120, after: 60 },
      border: { top: { style: BorderStyle.DASHED, size: 6, color: 'A0A0A0' }, bottom: { style: BorderStyle.DASHED, size: 6, color: 'A0A0A0' }, left: { style: BorderStyle.DASHED, size: 6, color: 'A0A0A0' }, right: { style: BorderStyle.DASHED, size: 6, color: 'A0A0A0' } },
      children: [new TextRun({ text: `Captura pendiente: ${path.basename(img.href)}`, color: '808080', italics: true })],
    }));
  } else {
    const buf = fs.readFileSync(archivo);
    const { w, h } = tamPng(buf);
    const vertical = h > w * 1.3;
    // Escala fija para las horizontales (55 px por cm, máximo 16 cm): así el texto de todas las capturas
    // sale de un tamaño parecido. Las verticales (teléfono) van a 6,2 cm.
    let anchoCm = opciones.ancho ? parseFloat(opciones.ancho) : (vertical ? 6.2 : Math.min(16, w / 55));
    if (enTabla) anchoCm = Math.min(anchoCm, opciones.ancho ? anchoCm : (vertical ? 6.2 : 7.8));
    anchoCm = Math.min(anchoCm, ANCHO_UTIL_CM - sangria / 567);
    let altoCm = anchoCm * h / w;
    const maxAlto = vertical ? 13.5 : 19;
    if (altoCm > maxAlto) { anchoCm = anchoCm * maxAlto / altoCm; altoCm = maxAlto; }
    parrafos.push(new Paragraph({
      alignment: AlignmentType.CENTER, indent: { left: sangria }, keepNext: true, spacing: { before: 120, after: 40 },
      children: [new ImageRun({ type: 'png', data: buf, transformation: { width: cmApx(anchoCm), height: cmApx(altoCm) },
        altText: { title: img.text, description: img.text, name: path.basename(archivo) } })],
    }));
  }
  parrafos.push(new Paragraph({
    style: 'Caption', alignment: AlignmentType.CENTER, indent: { left: sangria }, spacing: { after: 200 },
    children: [new TextRun('Figura '), new SimpleField('SEQ Figura \\* ARABIC', String(numFigura)), new TextRun(`. ${decodificar(img.text)}`)],
  }));
  return parrafos;
}
// Dos o más imágenes en el mismo párrafo: se ponen lado a lado.
function figurasLadoALado(imgs, dirCap) {
  const celdas = imgs.map(img => new TableCell({
    children: figura(img, dirCap, 0, true), verticalAlign: VerticalAlign.BOTTOM,
    borders: sinBordes(), width: { size: Math.floor(100 / imgs.length), type: WidthType.PERCENTAGE },
  }));
  return [new Table({ rows: [new TableRow({ children: celdas, cantSplit: true })], width: { size: 100, type: WidthType.PERCENTAGE }, borders: sinBordesTabla() }), new Paragraph({ spacing: { after: 60 } })];
}
function sinBordes() { const n = { style: BorderStyle.NONE, size: 0, color: 'FFFFFF' }; return { top: n, bottom: n, left: n, right: n }; }
function sinBordesTabla() { const n = { style: BorderStyle.NONE, size: 0, color: 'FFFFFF' }; return { top: n, bottom: n, left: n, right: n, insideHorizontal: n, insideVertical: n }; }

// ───────────────────────── Bloques ─────────────────────────
let instanciaLista = 0;
const inicios = new Set([1]);
// Pasos: el segundo nivel también es numérico, para coincidir con los números de las figuras.
function nivelesPasos(inicio) {
  return [
    { level: 0, format: LevelFormat.DECIMAL, text: '%1.', start: inicio, alignment: AlignmentType.LEFT, style: { run: { bold: true, color: C.vino }, paragraph: { indent: { left: 567, hanging: 397 } } } },
    { level: 1, format: LevelFormat.DECIMAL, text: '%2.', start: inicio, alignment: AlignmentType.LEFT, style: { run: { bold: true, color: C.oro }, paragraph: { indent: { left: 1134, hanging: 397 } } } },
    { level: 2, format: LevelFormat.LOWER_LETTER, text: '%3)', start: inicio, alignment: AlignmentType.LEFT, style: { paragraph: { indent: { left: 1701, hanging: 397 } } } },
  ];
}
// ctx: { dirCap, numCap, numSec, donde }
function bloques(tokens, ctx, nivelLista = 0, sangria = 0) {
  const out = [];
  for (const t of tokens) {
    switch (t.type) {
      case 'space': case 'hr': break;
      case 'html':
        if (/<!--\s*salto\s*-->/.test(t.text)) out.push(new Paragraph({ children: [new PageBreak()] }));
        break;
      case 'heading': out.push(...titulo(t, ctx)); break;
      case 'paragraph': case 'text': {
        const toks = t.tokens || [{ type: 'text', text: t.text }];
        const imgs = toks.filter(x => x.type === 'image');
        const resto = toks.filter(x => x.type !== 'image' && !(x.type === 'text' && !x.text.trim()) && x.type !== 'br');
        if (imgs.length && !resto.length) {
          if (imgs.length === 1) out.push(...figura(imgs[0], ctx.dirCap, sangria));
          else out.push(...figurasLadoALado(imgs, ctx.dirCap));
        } else {
          out.push(new Paragraph({ children: enLinea(toks.filter(x => x.type !== 'image'), {}, ctx.donde), indent: sangria ? { left: sangria } : undefined, spacing: { after: 120 } }));
          for (const img of imgs) out.push(...figura(img, ctx.dirCap, sangria));
        }
        break;
      }
      case 'list': out.push(...lista(t, ctx, nivelLista)); break;
      case 'blockquote': out.push(...cuadro(t, ctx, sangria)); break;
      case 'table': out.push(...tabla(t, ctx, sangria)); break;
      case 'code':
        out.push(new Paragraph({ shading: { type: ShadingType.CLEAR, fill: 'F2F2F2', color: 'auto' }, indent: { left: sangria },
          children: t.text.split('\n').flatMap((l, i) => [new TextRun({ text: l, font: 'Consolas', size: 19, break: i ? 1 : 0 })]) }));
        break;
      default: avisar(`Bloque no soportado (${t.type}) en ${ctx.donde}`);
    }
  }
  return out;
}

function lista(t, ctx, nivel) {
  const out = [];
  const inicio = t.ordered && t.start > 1 ? t.start : 1;
  inicios.add(inicio);
  const ref = !t.ordered ? 'vinetas' : inicio === 1 ? 'pasos' : `pasos_${inicio}`;
  const instancia = ++instanciaLista;
  const sangriaTexto = 567 * (nivel + 1) * 0.75 + 200;
  for (const it of t.items) {
    const toks = it.tokens || [];
    const [primero, ...resto] = toks;
    let runs = [];
    let siguientes = resto;
    if (primero && (primero.type === 'text' || primero.type === 'paragraph')) {
      const inl = (primero.tokens || [{ type: 'text', text: primero.text }]);
      const imgs = inl.filter(x => x.type === 'image');
      runs = enLinea(inl.filter(x => x.type !== 'image'), {}, ctx.donde);
      if (imgs.length) siguientes = [{ type: 'paragraph', tokens: imgs }, ...resto];
    } else siguientes = toks;
    out.push(new Paragraph({ children: runs, numbering: { reference: ref, level: Math.min(nivel, 2), instance: instancia }, spacing: { after: 80 } }));
    for (const s of siguientes) {
      if (s.type === 'list') out.push(...lista(s, ctx, nivel + 1));
      else out.push(...bloques([s], ctx, nivel + 1, sangriaTexto));
    }
  }
  return out;
}

function cuadro(t, ctx, sangria) {
  const crudo = t.text || '';
  const m = crudo.match(/^\s*\[!(NOTA|CONSEJO|ADVERTENCIA|IMPORTANTE)\]\s*/i);
  const tipo = m ? m[1].toUpperCase() : 'NOTA';
  const st = CAJAS[tipo];
  // Quita la marca [!TIPO] del primer párrafo.
  const toks = JSON.parse(JSON.stringify(t.tokens));
  if (m && toks[0] && toks[0].tokens) {
    const p0 = toks[0];
    let quitar = m[0].length;
    p0.tokens = p0.tokens.filter(x => {
      if (quitar <= 0) return true;
      if (x.type === 'text' || x.type === 'escape') {
        const txt = x.text.replace(/^\s*\[!\w+\]\s*/i, '');
        quitar = 0; x.text = txt; if (x.tokens) x.tokens = null; return txt.length > 0;
      }
      return true;
    });
  }
  const contenido = bloques(toks, ctx, 0, 0);
  const encabezado = new Paragraph({ spacing: { after: 60 }, keepNext: true, children: [new TextRun({ text: `${st.icono}  ${st.titulo}`, bold: true, color: st.borde })] });
  const fin = new Paragraph({ spacing: { after: 0 }, children: [] });
  const borde = { style: BorderStyle.SINGLE, size: 4, color: st.fondo };
  return [new Table({
    width: { size: 100, type: WidthType.PERCENTAGE }, indent: sangria ? { size: sangria, type: WidthType.DXA } : undefined,
    rows: [new TableRow({ cantSplit: true, children: [new TableCell({
      shading: { type: ShadingType.CLEAR, fill: st.fondo, color: 'auto' },
      margins: { top: 100, bottom: 60, left: 180, right: 180 },
      borders: { top: borde, bottom: borde, right: borde, left: { style: BorderStyle.SINGLE, size: 36, color: st.borde } },
      children: [encabezado, ...contenido, fin],
    })] })],
  }), new Paragraph({ spacing: { after: 120 }, children: [] })];
}

function tabla(t, ctx, sangria) {
  const n = t.header.length;
  const borde = { style: BorderStyle.SINGLE, size: 4, color: C.linea };
  const celda = (cell, cab, i, fila) => new TableCell({
    shading: cab ? { type: ShadingType.CLEAR, fill: C.vino, color: 'auto' } : (fila % 2 ? { type: ShadingType.CLEAR, fill: C.fondoTabla, color: 'auto' } : undefined),
    margins: { top: 60, bottom: 60, left: 100, right: 100 },
    borders: { top: borde, bottom: borde, left: borde, right: borde },
    children: [new Paragraph({ alignment: t.align[i] === 'center' ? AlignmentType.CENTER : t.align[i] === 'right' ? AlignmentType.RIGHT : AlignmentType.LEFT,
      children: enLinea(cell.tokens, cab ? { bold: true, color: 'FFFFFF' } : {}, ctx.donde) })],
  });
  const filas = [new TableRow({ tableHeader: true, cantSplit: true, children: t.header.map((c, i) => celda(c, true, i, 0)) }),
    ...t.rows.map((r, f) => new TableRow({ cantSplit: true, children: r.map((c, i) => celda(c, false, i, f)) }))];
  return [new Table({ rows: filas, width: { size: 100, type: WidthType.PERCENTAGE }, indent: sangria ? { size: sangria, type: WidthType.DXA } : undefined }),
    new Paragraph({ spacing: { after: 120 }, children: [] })];
}

// Títulos: "# " capítulo (Título 2), "## " sección (Título 3), "### " apartado (Título 4).
function titulo(t, ctx) {
  let texto = t.text;
  let id = null;
  const m = texto.match(/\s*\{#([A-Za-z0-9_-]+)\}\s*$/);
  if (m) { id = m[1]; texto = texto.slice(0, m.index); }
  texto = decodificar(texto.replace(/\*\*|__|`/g, ''));
  let nivel, etiqueta;
  if (t.depth === 1 && ctx.anexo) { ctx.numCap = String.fromCharCode(65 + estado.anexo++); ctx.numSec = 0; nivel = HeadingLevel.HEADING_2; etiqueta = `Anexo ${ctx.numCap}. ${texto}`; if (!id) id = 'anx-' + slug(texto); }
  else if (t.depth === 1) { ctx.numCap = ++estado.capitulo; ctx.numSec = 0; nivel = HeadingLevel.HEADING_2; etiqueta = `${ctx.numCap}. ${texto}`; if (!id) id = 'cap-' + slug(texto); }
  else if (t.depth === 2) { ctx.numSec++; nivel = HeadingLevel.HEADING_3; etiqueta = `${ctx.numCap}.${ctx.numSec} ${texto}`; if (!id) id = `sec-${ctx.numCap}-${slug(texto)}`; }
  else { nivel = HeadingLevel.HEADING_4; etiqueta = texto; }
  const hijos = [new TextRun(etiqueta)];
  if (id) {
    if (anclasDefinidas.has(id)) avisar(`Ancla repetida: #${id} (${ctx.donde})`);
    anclasDefinidas.add(id);
    return [new Paragraph({ heading: nivel, children: [new Bookmark({ id: marcador(id), children: hijos })] })];
  }
  return [new Paragraph({ heading: nivel, children: hijos })];
}

// ───────────────────────── Partes y mapa de puestos ─────────────────────────
const estado = { capitulo: 0, anexo: 0 };
const modulosDe = parteId => mapa.modulos.filter(m => m.dueno === parteId);
const parteDe = id => mapa.partes.find(p => p.id === id);
function usuariosDe(m) { return Object.fromEntries(m.usuarios.split(',').map(s => s.trim().split(':')).map(([p, n]) => [p.trim(), (n || 'completo').trim()])); }
const NIVEL = { completo: 'Uso completo', parcial: 'Uso parcial', consulta: 'Solo consulta' };

// ── Modo «manual por puesto»: node herramientas/generar_word.js --puesto cajero
// Incluye la parte general, la del puesto, los módulos de otras partes que el puesto usa,
// la aplicación móvil (si la usa) y los anexos.
const iPuesto = process.argv.indexOf('--puesto');
const PUESTO = iPuesto > 0 ? process.argv[iPuesto + 1] : null;
let partesSalida = mapa.partes;
let PERFIL = null;
if (PUESTO) {
  const pp = parteDe(PUESTO);
  PERFIL = pp && pp.perfil;
  if (!pp || !pp.perfil) throw new Error('Puesto desconocido: ' + PUESTO);
  const usa = m => !!usuariosDe(m)[pp.perfil];
  const ajenos = mapa.modulos.filter(m => m.dueno !== pp.id && m.dueno !== 'app' && usa(m));
  const conApp = mapa.modulos.some(m => m.dueno === 'app' && usa(m));
  manual.titulo = `Manual de usuario: ${pp.titulo}`;
  manual.salida = `salida/puestos/Manual de Usuario - ${pp.titulo}.docx`;
  partesSalida = [parteDe('general'), pp];
  if (ajenos.length) partesSalida.push({ id: 'otros', titulo: 'Otros módulos que usa este puesto', resumen: `Procedimientos de otras áreas del sistema que el puesto ${pp.titulo} también usa.`, ajenos });
  if (conApp) partesSalida.push(parteDe('app'));
  partesSalida.push(parteDe('anexos'));
}
const explicadoEn = m => {
  if (!PUESTO) return `Parte ${romano(mapa.partes.indexOf(parteDe(m.dueno)) + 1)}: ${parteDe(m.dueno).titulo}`;
  const j = partesSalida.findIndex(x => x.id === (m.dueno === 'app' ? 'app' : 'otros'));
  return j >= 0 ? `Parte ${romano(j + 1)}: ${partesSalida[j].titulo}` : parteDe(m.dueno).titulo;
};

function enlace(texto, id) { anclasUsadas.push({ id, donde: 'mapa' }); return new InternalHyperlink({ anchor: marcador(id), children: [new TextRun({ text: texto, style: 'Hyperlink' })] }); }
function tablaSimple(cabeceras, filas, anchos) {
  const borde = { style: BorderStyle.SINGLE, size: 4, color: C.linea };
  const celda = (contenido, cab, f, i) => new TableCell({
    shading: cab ? { type: ShadingType.CLEAR, fill: C.vino, color: 'auto' } : (f % 2 ? { type: ShadingType.CLEAR, fill: C.fondoTabla, color: 'auto' } : undefined),
    margins: { top: 50, bottom: 50, left: 90, right: 90 }, borders: { top: borde, bottom: borde, left: borde, right: borde },
    width: anchos ? { size: anchos[i], type: WidthType.PERCENTAGE } : undefined,
    children: [new Paragraph({ children: Array.isArray(contenido) ? contenido : [new TextRun({ text: String(contenido), bold: cab, color: cab ? 'FFFFFF' : undefined, size: cab ? 20 : 20 })] })],
  });
  return new Table({ width: { size: 100, type: WidthType.PERCENTAGE }, layout: TableLayoutType.FIXED, rows: [
    new TableRow({ tableHeader: true, children: cabeceras.map((c, i) => celda(c, true, 0, i)) }),
    ...filas.map((r, f) => new TableRow({ cantSplit: true, children: r.map((c, i) => celda(c, false, f, i)) })),
  ] });
}

// Página de presentación de una parte por puesto: qué hace, módulos propios y módulos de otras partes.
function presentacionParte(p) {
  const out = [];
  if (p.resumen) out.push(new Paragraph({ spacing: { after: 160 }, children: [new TextRun({ text: p.resumen, size: 24, color: C.gris })] }));
  if (!p.perfil && p.id !== 'app') return out;
  const propios = modulosDe(p.id).filter(m => !(PERFIL && p.id === 'app') || usuariosDe(m)[PERFIL]);
  const ajenos = p.perfil ? mapa.modulos.filter(m => m.dueno !== p.id && usuariosDe(m)[p.perfil]) : [];
  const sub = txt => new Paragraph({ style: 'SubtituloParte', children: [new TextRun(txt)] });
  if (propios.length) {
    out.push(sub(p.id === 'app' ? 'Funciones de la aplicación descritas en esta parte' : 'Módulos que se explican en esta parte'));
    out.push(tablaSimple(['Módulo', 'Dónde está', p.id === 'app' ? 'Puestos que la usan' : 'Plataforma'],
      propios.map(m => [[enlace(m.nombre, m.id)], m.acceso, p.id === 'app' ? Object.keys(usuariosDe(m)).join(', ') : (m.plataforma === 'app' ? 'App móvil' : 'Web')]), [34, 40, 26]));
    out.push(new Paragraph({ spacing: { after: 120 } }));
  }
  if (ajenos.length) {
    out.push(sub('Módulos de otras partes que también usa este puesto'));
    out.push(new Paragraph({ spacing: { after: 100 }, children: [new TextRun({ text: 'El procedimiento está escrito una sola vez, en la parte del puesto que más lo usa. Haga clic en el nombre para ir a él.', color: C.gris, size: 20 })] }));
    out.push(tablaSimple(['Módulo', 'Dónde está', 'Su acceso', 'Explicado en'],
      ajenos.map(m => [[enlace(m.nombre, m.id)], m.acceso, NIVEL[usuariosDe(m)[p.perfil]] || '', explicadoEn(m)]), [28, 34, 16, 22]));
    out.push(new Paragraph({ spacing: { after: 120 } }));
  }
  if (p.perfil) {
    out.push(sub('Funciones comunes'));
    out.push(new Paragraph({ children: [new TextRun('Inicio de sesión, contraseña, menú, perfil, notificaciones y aplicación móvil: vea la '), enlace('Parte I. Información general', 'parte-general'), new TextRun('.')] }));
  }
  return out;
}

// Capítulo provisional para un módulo que aún no tiene archivo: así los enlaces nunca quedan rotos.
function provisional(m) {
  const md = `# ${m.nombre} {#${m.id}}\n\n> [!NOTA] Capítulo en preparación. Aquí se describirá cómo usar **${m.nombre}** (${m.acceso}).\n`;
  return md;
}

function capitulosDeParte(p) {
  if (p.id === 'otros') {
    const vistos = new Set(); const caps = [];
    for (const m of p.ajenos) {
      const d = parteDe(m.dueno); const dir = path.join(RAIZ, d.carpeta);
      const f = fs.readdirSync(dir).filter(x => x.endsWith('.md') && !x.startsWith('_') && !/hallazgo/i.test(x) && !/^datos/i.test(x))
        .find(x => fs.readFileSync(path.join(dir, x), 'utf8').includes(`{#${m.id}}`));
      if (f && !vistos.has(d.carpeta + f)) { vistos.add(d.carpeta + f); caps.push({ md: fs.readFileSync(path.join(dir, f), 'utf8'), dir, donde: path.join(d.carpeta, f) }); }
    }
    return caps;
  }
  const dir = path.join(RAIZ, p.carpeta);
  // Nunca entran al Word los archivos de trabajo: los que empiezan con «_» (datos de ejemplo,
  // borradores) ni los de hallazgos, aunque alguien olvide el guion bajo.
  const deTrabajo = f => f.startsWith('_') || /hallazgo/i.test(f) || /^datos/i.test(f);
  const archivos = fs.existsSync(dir) ? fs.readdirSync(dir).filter(f => f.endsWith('.md') && !deTrabajo(f)).sort() : [];
  const leidos = archivos.map(f => ({ md: fs.readFileSync(path.join(dir, f), 'utf8'), dir, donde: path.join(p.carpeta, f) }));
  // Orden: primero los archivos que no son de un módulo (intro del puesto), luego los módulos en el
  // orden de mapa.json (con un capítulo provisional si aún no tienen archivo) y al final el resto.
  const deModulo = m => leidos.find(c => c.md.includes(`{#${m.id}}`));
  const modulos = modulosDe(p.id);
  const usados = new Set();
  const caps = leidos.filter(c => path.basename(c.donde).startsWith('00') && !modulos.some(m => c.md.includes(`{#${m.id}}`)));
  caps.forEach(c => usados.add(c));
  for (const m of modulos) {
    const c = deModulo(m);
    if (c && !usados.has(c)) { caps.push(c); usados.add(c); }
    else if (!c) caps.push({ md: provisional(m), dir, donde: `${p.carpeta} (provisional ${m.id})`, provisional: true });
  }
  caps.push(...leidos.filter(c => !usados.has(c)));
  if (PERFIL && p.id === 'app') {
    const propiosApp = modulos.filter(m => usuariosDe(m)[PERFIL]);
    return caps.filter(c => !c.provisional && (path.basename(c.donde).startsWith('00') || propiosApp.some(m => c.md.includes(`{#${m.id}}`))));
  }
  return caps;
}

// ───────────────────────── Portada y preliminares ─────────────────────────
function portada() {
  const logo = fs.readFileSync(path.join(RAIZ, 'recursos', 'logo-transparente.png'));
  const linea = new Paragraph({ border: { bottom: { style: BorderStyle.SINGLE, size: 18, color: C.oro } }, spacing: { after: 300 } });
  return [
    new Paragraph({ spacing: { before: 1200 }, alignment: AlignmentType.CENTER, children: [new ImageRun({ type: 'png', data: logo, transformation: { width: cmApx(5), height: cmApx(5 * 200 / 202) } })] }),
    new Paragraph({ spacing: { before: 600 }, alignment: AlignmentType.CENTER, children: [new TextRun({ text: manual.titulo.toUpperCase(), bold: true, size: 64, color: C.vino, characterSpacing: 40 })] }),
    new Paragraph({ alignment: AlignmentType.CENTER, spacing: { after: 120 }, children: [new TextRun({ text: manual.sistema, size: 32, color: C.texto })] }),
    linea,
    new Paragraph({ alignment: AlignmentType.CENTER, spacing: { after: 80 }, children: [new TextRun({ text: manual.empresa, bold: true, size: 30, color: C.oro })] }),
    new Paragraph({ alignment: AlignmentType.CENTER, spacing: { before: 1800 }, children: [new TextRun({ text: `${manual.estado} · Versión ${manual.version}`, size: 24, color: C.gris })] }),
    new Paragraph({ alignment: AlignmentType.CENTER, children: [new TextRun({ text: manual.fecha, size: 24, color: C.gris })] }),
    new Paragraph({ alignment: AlignmentType.CENTER, spacing: { before: 400 }, children: [new TextRun({ text: manual.curso, size: 20, color: C.gris })] }),
  ];
}
function tituloPreliminar(txt) { return new Paragraph({ style: 'TituloPreliminar', children: [new TextRun(txt)] }); }
function preliminares() {
  return [
    tituloPreliminar('Control de versiones'),
    tablaSimple(['Versión', 'Fecha', 'Descripción del cambio', 'Responsable'], manual.versiones, [11, 14, 53, 22]),
    new Paragraph({ spacing: { before: 240 }, children: [new TextRun({ text: 'Este manual describe el sistema tal como funciona en la versión vigente. Cuando el sistema cambie, se actualizan las secciones afectadas y se registra aquí la nueva versión.', color: C.gris, size: 20 })] }),
    new Paragraph({ children: [new PageBreak()] }),
    tituloPreliminar('Contenido'),
    new TableOfContents('Contenido', { hyperlink: true, headingStyleRange: '1-3' }),
    new Paragraph({ children: [new PageBreak()] }),
    tituloPreliminar('Índice de figuras'),
    new TableOfContents('Índice de figuras', { hyperlink: true, captionLabelIncludingNumbers: 'Figura' }),
  ];
}

// ───────────────────────── Encabezado y pie ─────────────────────────
function encabezado(derecha) {
  return new Header({ children: [new Paragraph({
    tabStops: [{ type: TabStopType.RIGHT, position: cmAtw(ANCHO_UTIL_CM) }],
    border: { bottom: { style: BorderStyle.SINGLE, size: 6, color: C.oro, space: 4 } },
    children: [new TextRun({ text: `${manual.titulo} · Distribuidora JJ`, size: 18, color: C.gris }), new TextRun({ text: `\t${derecha}`, size: 18, color: C.vino, bold: true })],
  })] });
}
function pie() {
  return new Footer({ children: [new Paragraph({
    tabStops: [{ type: TabStopType.RIGHT, position: cmAtw(ANCHO_UTIL_CM) }],
    border: { top: { style: BorderStyle.SINGLE, size: 4, color: C.linea, space: 4 } },
    children: [new TextRun({ text: `Versión ${manual.version} · ${manual.fecha}`, size: 18, color: C.gris }),
      new TextRun({ children: ['\tPágina ', PageNumber.CURRENT], size: 18, color: C.gris })],
  })] });
}

// ───────────────────────── Documento ─────────────────────────
const pagina = { size: { width: cmAtw(21.59), height: cmAtw(27.94) }, margin: { top: cmAtw(2.4), bottom: cmAtw(2.2), left: cmAtw(2.2), right: cmAtw(2.2), header: cmAtw(1.1), footer: cmAtw(1.0) } };
const secciones = [];
secciones.push({ properties: { page: pagina }, children: portada() });
secciones.push({ properties: { page: { ...pagina, pageNumbers: { start: 1, formatType: NumberFormat.LOWER_ROMAN } } },
  headers: { default: encabezado('Preliminares') }, footers: { default: pie() }, children: preliminares() });

let primeraParte = true;
partesSalida.forEach((p, i) => {
  const hijos = [];
  const nombreParte = p.id === 'anexos' ? 'Anexos' : `Parte ${romano(i + 1)}. ${p.titulo}`;
  anclasDefinidas.add(`parte-${p.id}`);
  hijos.push(new Paragraph({ heading: HeadingLevel.HEADING_1, children: [new Bookmark({ id: marcador(`parte-${p.id}`), children: [new TextRun(nombreParte)] })] }));
  hijos.push(...presentacionParte(p));
  for (const cap of capitulosDeParte(p)) {
    const ctx = { dirCap: cap.dir, numCap: estado.capitulo, numSec: 0, donde: cap.donde, anexo: p.id === 'anexos' };
    let md = cap.md;
    // Marcadores automáticos que un capítulo puede pedir.
    md = md.replace(/<!--\s*mapa-puestos\s*-->/g, '\n\n@@MAPA@@\n\n');
    const partes = md.split('@@MAPA@@');
    partes.forEach((trozo, k) => {
      hijos.push(...bloques(marked.lexer(trozo), ctx));
      if (k < partes.length - 1) hijos.push(...tablaMapa());
    });
  }
  secciones.push({
    properties: { page: { ...pagina, pageNumbers: primeraParte ? { start: 1, formatType: NumberFormat.DECIMAL } : undefined } },
    headers: { default: encabezado(nombreParte) }, footers: { default: pie() }, children: hijos,
  });
  primeraParte = false;
});

// Tabla del anexo: puestos × módulos (por parte dueña).
function tablaMapa() {
  const out = [];
  for (const p of mapa.partes.filter(x => x.perfil)) {
    const propios = modulosDe(p.id).filter(m => m.plataforma === 'web');
    const ajenos = mapa.modulos.filter(m => m.dueno !== p.id && usuariosDe(m)[p.perfil]);
    const web = ajenos.filter(m => m.plataforma === 'web'), app = ajenos.filter(m => m.plataforma === 'app');
    const lista = arr => arr.flatMap((m, j) => [...(j ? [new TextRun('; ')] : []), enlace(m.nombre, m.id)]);
    out.push(new Paragraph({ style: 'SubtituloParte', keepNext: true, children: [new TextRun(p.titulo)] }));
    out.push(tablaSimple(['Tipo', 'Módulos'], [
      ['Explicados en su parte', propios.length ? lista(propios) : [new TextRun({ text: '— (solo usa módulos de otras partes)', color: C.gris })]],
      ['De otras partes (web)', web.length ? lista(web) : [new TextRun('—')]],
      ['Aplicación móvil', app.length ? lista(app) : [new TextRun(p.perfil === 'Cliente' ? 'No usa la aplicación' : '—')]],
    ], [24, 76]));
    out.push(new Paragraph({ spacing: { after: 160 } }));
  }
  return out;
}

const doc = new Document({
  creator: 'Equipo del proyecto', title: `${manual.titulo} — ${manual.empresa}`, description: manual.sistema, language: 'es-CR',
  styles: {
    default: { document: { run: { font: FUENTE, size: 22, color: C.texto }, paragraph: { spacing: { after: 120, line: 276 } } } },
    paragraphStyles: [
      { id: 'Heading1', name: 'Heading 1', basedOn: 'Normal', next: 'Normal', quickFormat: true,
        run: { size: 48, bold: true, color: C.vino }, paragraph: { outlineLevel: 0, spacing: { before: 2400, after: 360 }, border: { bottom: { style: BorderStyle.SINGLE, size: 18, color: C.oro, space: 8 } } } },
      { id: 'Heading2', name: 'Heading 2', basedOn: 'Normal', next: 'Normal', quickFormat: true,
        run: { size: 36, bold: true, color: C.vino }, paragraph: { pageBreakBefore: true, keepNext: true, outlineLevel: 1, spacing: { before: 240, after: 240 } } },
      { id: 'Heading3', name: 'Heading 3', basedOn: 'Normal', next: 'Normal', quickFormat: true,
        run: { size: 28, bold: true, color: C.vino }, paragraph: { keepNext: true, outlineLevel: 2, spacing: { before: 320, after: 120 } } },
      { id: 'Heading4', name: 'Heading 4', basedOn: 'Normal', next: 'Normal', quickFormat: true,
        run: { size: 23, bold: true, color: C.oro }, paragraph: { keepNext: true, outlineLevel: 3, spacing: { before: 200, after: 80 } } },
      { id: 'Caption', name: 'caption', basedOn: 'Normal', next: 'Normal', quickFormat: true, run: { size: 19, italics: true, color: C.gris } },
      { id: 'TituloPreliminar', name: 'Título preliminar', basedOn: 'Normal', next: 'Normal', run: { size: 36, bold: true, color: C.vino }, paragraph: { spacing: { before: 240, after: 300 } } },
      { id: 'SubtituloParte', name: 'Subtítulo de parte', basedOn: 'Normal', next: 'Normal', run: { size: 24, bold: true, color: C.oro }, paragraph: { keepNext: true, spacing: { before: 240, after: 100 } } },
      { id: 'TOC1', name: 'toc 1', basedOn: 'Normal', next: 'Normal', run: { bold: true, color: C.vino }, paragraph: { spacing: { before: 160, after: 40 } } },
      { id: 'TOC2', name: 'toc 2', basedOn: 'Normal', next: 'Normal', paragraph: { indent: { left: 240 }, spacing: { after: 20 } } },
      { id: 'TOC3', name: 'toc 3', basedOn: 'Normal', next: 'Normal', run: { size: 20, color: C.gris }, paragraph: { indent: { left: 480 }, spacing: { after: 0 } } },
      { id: 'TableofFigures', name: 'table of figures', basedOn: 'Normal', next: 'Normal', run: { size: 20 }, paragraph: { spacing: { after: 20 } } },
    ],
    characterStyles: [{ id: 'Hyperlink', name: 'Hyperlink', basedOn: 'DefaultParagraphFont', run: { color: '1F5FA8', underline: { type: 'single', color: '1F5FA8' } } }],
  },
  numbering: { config: [
    // Una referencia por número inicial: una lista que sigue después de una figura puede empezar en 4.
    ...[...inicios].map(n => ({ reference: n === 1 ? 'pasos' : `pasos_${n}`, levels: nivelesPasos(n) })),
    { reference: 'vinetas', levels: [
      { level: 0, format: LevelFormat.BULLET, text: '•', alignment: AlignmentType.LEFT, style: { run: { color: C.oro }, paragraph: { indent: { left: 567, hanging: 340 } } } },
      { level: 1, format: LevelFormat.BULLET, text: '–', alignment: AlignmentType.LEFT, style: { paragraph: { indent: { left: 1134, hanging: 340 } } } },
      { level: 2, format: LevelFormat.BULLET, text: '·', alignment: AlignmentType.LEFT, style: { paragraph: { indent: { left: 1701, hanging: 340 } } } },
    ] },
  ] },
  sections: secciones,
});

// ───────────────────────── Validación y salida ─────────────────────────
for (const u of anclasUsadas) if (!anclasDefinidas.has(u.id)) avisar(`Enlace roto: #${u.id} (${u.donde})`);
const destino = path.join(RAIZ, manual.salida);
fs.mkdirSync(path.dirname(destino), { recursive: true });
Packer.toBuffer(doc).then(buf => {
  fs.writeFileSync(destino, buf);
  const unicos = [...new Set(avisos)];
  fs.writeFileSync(path.join(RAIZ, 'salida', 'validacion.txt'), unicos.join('\n') + '\n');
  console.log(`Word generado: ${path.relative(RAIZ, destino)} (${estado.capitulo} capítulos, ${numFigura} figuras)`);
  if (unicos.length) console.log(`${unicos.length} aviso(s):\n  ` + unicos.slice(0, 40).join('\n  ') + (unicos.length > 40 ? '\n  …' : ''));
  if (estricto && unicos.length) process.exit(1);
});
