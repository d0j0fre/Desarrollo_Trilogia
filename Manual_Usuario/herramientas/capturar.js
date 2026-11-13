// Genera las capturas anotadas del manual a partir de archivos de especificación.
//
//   node herramientas/capturar.js capturas/specs/general.json            (todas)
//   node herramientas/capturar.js capturas/specs/general.json --solo gen-login-01,gen-login-02
//   node herramientas/capturar.js capturas/specs/*.json                  (varias)
//
// Formato de la especificación: ver HERRAMIENTAS.md.
const { chromium } = require('playwright-core');
const fs = require('fs');
const path = require('path');
const cfg = require('./config');
const { anotar } = require('./anotar');

const RAIZ = path.resolve(__dirname, '..');
const ESCALA_WEB = 1.1;      // deviceScaleFactor: vista de 1760 px (el menú cabe sin encimarse) → imagen de 1936 px
const ESCALA_MOVIL = 1.35;   // la imagen móvil se inserta más angosta: trazos un poco más gruesos

// CSS que se inyecta antes de fotografiar: quita animaciones, cursor y elementos flotantes.
const CSS_CAPTURA = `
  *, *::before, *::after { transition: none !important; animation: none !important; caret-color: transparent !important; }
  .back-to-top { display: none !important; }
`;

const args = process.argv.slice(2);
const solo = (() => { const i = args.indexOf('--solo'); return i >= 0 ? args[i + 1].split(',') : null; })();
const archivos = args.filter((a, i) => !a.startsWith('--') && args[i - 1] !== '--solo');
if (!archivos.length) { console.error('Uso: node herramientas/capturar.js <spec.json> [--solo id1,id2]'); process.exit(2); }

let fallas = 0;

(async () => {
  const browser = await chromium.launch({ channel: 'chrome', headless: true });
  for (const archivo of archivos) {
    const spec = JSON.parse(fs.readFileSync(archivo, 'utf8'));
    const carpeta = path.join(RAIZ, 'capturas', spec.carpeta || path.basename(archivo, '.json'));
    fs.mkdirSync(carpeta, { recursive: true });
    for (const c of spec.capturas) {
      if (solo && !solo.includes(c.id)) continue;
      const destino = path.join(carpeta, `${c.id}.png`);
      try {
        if (c.sql) cfg.ejecutarSql(Array.isArray(c.sql) ? c.sql.join('\n') : c.sql);
        const png = (c.plataforma || 'web') === 'movil' ? await capturaMovil(browser, c) : await capturaWeb(browser, c);
        fs.writeFileSync(destino, png);
        console.log('ok  ', path.relative(RAIZ, destino));
      } catch (e) {
        fallas++;
        console.error('FALLA', c.id, '-', e.message.split('\n')[0]);
      }
    }
  }
  await browser.close();
  process.exit(fallas ? 1 : 0);
})();

// ───────────────────────────── Web ─────────────────────────────
const sesionesWeb = new Map(); // usuario → storageState, para no iniciar sesión en cada captura

async function contextoWeb(browser, c) {
  const vp = { width: c.ancho || 1760, height: c.alto || 990 };
  const base = { viewport: vp, deviceScaleFactor: c.escala || ESCALA_WEB, locale: 'es-CR' };
  if (!c.usuario) return browser.newContext(base);
  if (!sesionesWeb.has(c.usuario)) {
    const ctx = await browser.newContext(base);
    const p = await ctx.newPage();
    await cfg.login(p, c.usuario);
    sesionesWeb.set(c.usuario, await ctx.storageState());
    await ctx.close();
  }
  return browser.newContext({ ...base, storageState: sesionesWeb.get(c.usuario) });
}

async function accionWeb(page, a) {
  const [tipo, v] = Object.entries(a)[0];
  const loc = s => page.locator(s).first();
  switch (tipo) {
    case 'ir': return page.goto(cfg.web + v, { waitUntil: 'networkidle' });
    // Si el clic abre otra página, espera a que cargue; si no, sigue tras unos segundos.
    case 'clic': await Promise.all([page.waitForNavigation({ waitUntil: 'networkidle', timeout: 8000 }).catch(() => {}), loc(v).click()]); return;
    case 'clicSinEsperar': return loc(v).click();
    case 'rellenar': return loc(v[0]).fill(v[1]);
    case 'seleccionar': return loc(v[0]).selectOption(v[1]);
    case 'marcar': return loc(v).check();
    case 'hover': return loc(v).hover();
    case 'tecla': return page.keyboard.press(v);
    case 'esperar': return page.waitForTimeout(v);
    case 'esperarSelector': return page.waitForSelector(v, { state: 'visible' });
    case 'desplazar': return loc(v).evaluate(e => e.scrollIntoView({ block: 'center' }));
    case 'desplazarY': return page.evaluate(y => window.scrollTo(0, y), v);
    case 'abrir': return loc(v).evaluate(e => { e.open = true; }); // <details> del menú
    case 'js': return page.evaluate(v);
    default: throw new Error(`Acción web desconocida: ${tipo}`);
  }
}

// Caja de un elemento en coordenadas del documento.
// Con todos=true devuelve la caja que abarca todos los elementos visibles que coinciden.
async function cajaDoc(page, selector, indice = 0, todos = false) {
  if (todos) {
    const cajas = await page.locator(selector).evaluateAll(es => es.map(e => e.getBoundingClientRect()).filter(r => r.width && r.height)
      .map(r => ({ x: r.left + window.scrollX, y: r.top + window.scrollY, w: r.width, h: r.height })));
    if (!cajas.length) throw new Error(`No se encontró el selector «${selector}»`);
    const x = Math.min(...cajas.map(c => c.x)), y = Math.min(...cajas.map(c => c.y));
    return { x, y, w: Math.max(...cajas.map(c => c.x + c.w)) - x, h: Math.max(...cajas.map(c => c.y + c.h)) - y };
  }
  const loc = page.locator(selector).nth(indice);
  if (!(await loc.count())) throw new Error(`No se encontró el selector «${selector}»`);
  return loc.evaluate(e => {
    const r = e.getBoundingClientRect();
    return { x: r.left + window.scrollX, y: r.top + window.scrollY, w: r.width, h: r.height };
  });
}

async function capturaWeb(browser, c) {
  const ctx = await contextoWeb(browser, c);
  const page = await ctx.newPage();
  try {
    if (c.url) await page.goto(cfg.web + c.url, { waitUntil: 'networkidle' });
    await page.addStyleTag({ content: CSS_CAPTURA });
    for (const a of c.acciones || []) await accionWeb(page, a);
    await page.addStyleTag({ content: CSS_CAPTURA });
    for (const s of c.ocultar || []) await page.locator(s).evaluateAll(es => es.forEach(e => { e.style.visibility = 'hidden'; }));
    for (const s of c.enmascarar || []) await page.locator(s).evaluateAll(es => es.forEach(e => { e.style.filter = 'blur(6px)'; }));
    await page.waitForTimeout(c.pausa ?? 300);

    // Zona de la captura en coordenadas del documento.
    const vista = await page.evaluate(() => ({ x: window.scrollX, y: window.scrollY, w: window.innerWidth, h: window.innerHeight,
      W: document.documentElement.scrollWidth, H: document.documentElement.scrollHeight }));
    let zona;
    if (c.recorte === 'pagina') zona = { x: 0, y: 0, w: vista.W, h: vista.H };
    else if (c.recorte && c.recorte.selector) {
      const b = await cajaDoc(page, c.recorte.selector, c.recorte.indice || 0, c.recorte.todos);
      // margen: un número, o [arriba, derecha, abajo, izquierda] como en CSS.
      const mg = c.recorte.margen ?? 16;
      const [mt, mr, mb, ml] = Array.isArray(mg) ? mg : [mg, mg, mg, mg];
      zona = { x: Math.max(0, b.x - ml), y: Math.max(0, b.y - mt), w: b.w + ml + mr, h: b.h + mt + mb };
      zona.w = Math.min(zona.w, vista.W - zona.x);
    } else if (c.recorte && c.recorte.ancho) zona = { x: c.recorte.x || 0, y: c.recorte.y || 0, w: c.recorte.ancho, h: c.recorte.alto };
    else zona = { x: vista.x, y: vista.y, w: vista.w, h: vista.h };

    const k = c.escala || ESCALA_WEB;
    const anots = [];
    for (const a of c.anotaciones || []) {
      const b = a.caja ? a.caja : await cajaDoc(page, a.selector, a.indice || 0, a.todos);
      anots.push({ ...a, caja: { x: (b.x - zona.x) * k, y: (b.y - zona.y) * k, w: b.w * k, h: b.h * k } });
    }
    // Dónde hay texto en la zona: los números de paso se ubican donde no lo tapen.
    const textos = await page.evaluate(z => {
      const out = []; const w = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT);
      while (w.nextNode()) {
        const n = w.currentNode; if (!n.textContent.trim()) continue;
        const el = n.parentElement; if (!el || getComputedStyle(el).visibility === 'hidden') continue;
        const rg = document.createRange(); rg.selectNodeContents(n);
        for (const r of rg.getClientRects()) {
          const x = r.left + window.scrollX, y = r.top + window.scrollY;
          if (r.width && r.height && x < z.x + z.w && x + r.width > z.x && y < z.y + z.h && y + r.height > z.y) out.push({ x, y, w: r.width, h: r.height });
        }
      }
      return out;
    }, zona);
    const obstaculos = textos.map(t => ({ x: (t.x - zona.x) * k, y: (t.y - zona.y) * k, w: t.w * k, h: t.h * k }));
    const png = await page.screenshot({ type: 'png', fullPage: true, clip: { x: zona.x, y: zona.y, width: zona.w, height: zona.h } });
    return anotar(browser, png, Math.round(zona.w * k), Math.round(zona.h * k), anots, 1, obstaculos);
  } finally {
    await ctx.close();
  }
}

// ───────────────────────────── Móvil ─────────────────────────────
async function capturaMovil(browser, c) {
  const m = require('./movil');
  m.dispositivo();
  m.barraDemo();
  await m.sesion(c.usuario ? cfg.correoDe(c.usuario) : null, cfg.CLAVE_DEMO);
  for (const a of c.acciones || []) {
    const [tipo, v] = Object.entries(a)[0];
    if (tipo === 'tocar') {
      if (Array.isArray(v)) m.tocarEn(v[0], v[1]);
      else { const n = await m.esperarTexto(v); m.tocarEn(...m.centro(n.caja)); }
      await m.dormir(c.pausaAccion ?? 1500);
    } else if (tipo === 'escribir') { m.escribir(v); await m.dormir(400); }
    else if (tipo === 'atras') { m.sh('input keyevent 4'); await m.dormir(1200); }
    else if (tipo === 'ocultarTeclado') { m.ocultarTeclado(); await m.dormir(600); }
    else if (tipo === 'tecla') { m.sh(`input keyevent ${v}`); await m.dormir(600); }
    else if (tipo === 'esperar') await m.dormir(v);
    else if (tipo === 'esperarTexto') await m.esperarTexto(v);
    else if (tipo === 'deslizar') { m.sh(`input swipe ${v.join(' ')} 400`); await m.dormir(1000); }
    else if (tipo === 'shell') m.sh(v);
    else throw new Error(`Acción móvil desconocida: ${tipo}`);
  }
  await m.dormir(c.pausa ?? 800);
  const anots = [];
  for (const a of c.anotaciones || []) {
    let caja;
    if (a.coordenadas) { const [x, y, w, h] = a.coordenadas; caja = { x, y, w, h }; }
    else {
      let n = m.buscar(a.elemento, { indice: a.indice || 0 });
      if (!n) { await m.esperarTexto(a.elemento, 25000).catch(() => {}); n = m.buscar(a.elemento, { indice: a.indice || 0 }); }
      if (!n) throw new Error(`No se encontró «${a.elemento}» en la pantalla del emulador`);
      caja = n.caja;
    }
    anots.push({ ...a, caja });
  }
  // Textos visibles (renglones cortos del árbol de accesibilidad): los números de paso no deben taparlos.
  const obst = m.nodos().filter(n => (n.texto || n.desc) && n.caja.h <= 160 && n.caja.w < 1000).map(n => ({ ...n.caja }));
  const png = m.captura();
  const { w, h } = m.tamano();
  // El emulador dibuja un marco verde de unos 4 px en la captura: siempre se quita.
  // Recorte vertical opcional (por ejemplo, sin la barra de navegación del sistema).
  const B = 6;
  const y0 = Math.max(B, c.recorte?.y || 0);
  const alto = Math.min(h - B, c.recorte?.alto ? y0 + c.recorte.alto : h - B) - y0;
  anots.forEach(a => { a.caja.x -= B; a.caja.y -= y0; });
  obst.forEach(o => { o.x -= B; o.y -= y0; });
  const img = await recortarPng(browser, png, w, h, B, y0, w - 2 * B, alto);
  return anotar(browser, img, w - 2 * B, alto, anots, ESCALA_MOVIL, obst);
}

async function recortarPng(browser, png, w, h, x, y, cw, ch) {
  const page = await browser.newPage({ viewport: { width: cw, height: ch } });
  await page.setContent(`<body style="margin:0;overflow:hidden"><img src="data:image/png;base64,${png.toString('base64')}" style="position:absolute;left:${-x}px;top:${-y}px;width:${w}px;height:${h}px"></body>`);
  await page.waitForFunction(() => document.querySelector('img').complete);
  const out = await page.screenshot({ type: 'png' });
  await page.close();
  return out;
}
