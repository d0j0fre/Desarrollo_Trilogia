// Dibuja anotaciones (recuadros, números de paso, flechas, etiquetas y foco) sobre
// una captura PNG. Lo usan la captura web y la móvil, así el estilo es idéntico.
//
// Cada anotación llega con su caja ya resuelta en píxeles de la imagen:
//   { tipo: 'paso'|'recuadro'|'flecha'|'etiqueta'|'foco', caja: {x,y,w,h}, ... }
// Ver HERRAMIENTAS.md para la lista de opciones de cada tipo.
const ESTILO = {
  color: '#E6007E',      // magenta: contrasta con el dorado, el negro y el blanco del sistema
  contorno: '#FFFFFF',   // borde blanco alrededor de números y flechas, para fondos oscuros
  grosor: 5,             // trazo de recuadros y flechas (px de imagen, escala 1)
  radioNumero: 22,       // radio del círculo de paso
  margen: 6,             // separación entre el elemento y su recuadro
  fuente: 'Segoe UI, Arial, sans-serif',
};

function esc(s) {
  return String(s).replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));
}

// Ancho aproximado de un texto (sin medirlo en el navegador): suficiente para el fondo de la etiqueta.
function anchoTexto(t, tam) { return Math.ceil(String(t).length * tam * 0.56); }

const DIRS = {
  izquierda: [-1, 0], derecha: [1, 0], arriba: [0, -1], abajo: [0, 1],
  'arriba-izquierda': [-0.7, -0.7], 'arriba-derecha': [0.7, -0.7],
  'abajo-izquierda': [-0.7, 0.7], 'abajo-derecha': [0.7, 0.7],
};

// Punto del borde de la caja en la dirección dada (desde el centro).
function bordeEn(c, d) {
  const cx = c.x + c.w / 2, cy = c.y + c.h / 2;
  const tx = d[0] === 0 ? Infinity : (c.w / 2) / Math.abs(d[0]);
  const ty = d[1] === 0 ? Infinity : (c.h / 2) / Math.abs(d[1]);
  const t = Math.min(tx, ty);
  return [cx + d[0] * t, cy + d[1] * t];
}

function dentro(x, y, W, H, r) { return x - r >= 0 && y - r >= 0 && x + r <= W && y + r <= H; }

// obstaculos: cajas de texto de la pantalla (px de imagen). Los números de paso se ubican donde no tapen
// texto, otros recuadros ni otros números; si no hay lugar libre, donde tapen menos.
function svgAnotaciones(anots, W, H, escala, obstaculos = []) {
  const k = escala || 1;
  const g = ESTILO.grosor * k, r = ESTILO.radioNumero * k, m = ESTILO.margen * k;
  const C = ESTILO.color, B = ESTILO.contorno;
  const capas = { foco: [], cajas: [], flechas: [], numeros: [], etiquetas: [] };
  const amp = c => ({ x: c.x - m, y: c.y - m, w: c.w + 2 * m, h: c.h + 2 * m });

  // Foco: oscurece todo menos las cajas indicadas.
  const focos = anots.filter(a => a.tipo === 'foco');
  if (focos.length) {
    const huecos = focos.map(a => { const c = amp(a.caja); return `<rect x="${c.x}" y="${c.y}" width="${c.w}" height="${c.h}" rx="${8 * k}" fill="black"/>`; }).join('');
    capas.foco.push(`<mask id="foco"><rect width="${W}" height="${H}" fill="white"/>${huecos}</mask>`,
      `<rect width="${W}" height="${H}" fill="rgba(0,0,0,${focos[0].opacidad ?? 0.45})" mask="url(#foco)"/>`);
  }

  const choca = (x, y, rr, cajas) => cajas.reduce((n, o) => n + (x + rr > o.x && x - rr < o.x + o.w && y + rr > o.y && y - rr < o.y + o.h ? 1 : 0), 0);
  const ocupados = []; // números ya ubicados
  const marcos = anots.filter(a => a.tipo === 'recuadro' || a.tipo === 'paso').map(a => ({ a, b: amp(a.caja) }));
  for (const a of anots) {
    const c = a.caja;
    if (a.tipo === 'foco') {
      if (a.recuadro !== false) { const b = amp(c); capas.cajas.push(`<rect x="${b.x}" y="${b.y}" width="${b.w}" height="${b.h}" rx="${8 * k}" fill="none" stroke="${C}" stroke-width="${g}"/>`); }
      continue;
    }
    if (a.tipo === 'recuadro' || (a.tipo === 'paso' && a.recuadro !== false)) {
      const b = amp(c);
      capas.cajas.push(`<rect x="${b.x}" y="${b.y}" width="${b.w}" height="${b.h}" rx="${8 * k}" fill="none" stroke="${B}" stroke-width="${g + 3 * k}" opacity="0.9"/>`,
        `<rect x="${b.x}" y="${b.y}" width="${b.w}" height="${b.h}" rx="${8 * k}" fill="none" stroke="${C}" stroke-width="${g}"/>`);
    }
    if (a.tipo === 'paso') {
      const b = amp(c);
      const pref = a.posicion ? [a.posicion] : [];
      const orden = [...pref, 'izquierda', 'arriba', 'derecha', 'abajo', 'arriba-derecha', 'abajo-derecha', 'abajo-izquierda', 'arriba-izquierda'];
      let px, py, mejor = Infinity;
      const rr = r + 3 * k;
      const otros = marcos.filter(o => o.a !== a).map(o => o.b);
      for (const p of orden) {
        const pos = {
          izquierda: [b.x - r - 4 * k, b.y + Math.min(b.h / 2, r + 2 * k)],
          derecha: [b.x + b.w + r + 4 * k, b.y + Math.min(b.h / 2, r + 2 * k)],
          arriba: [b.x + b.w / 2, b.y - r - 4 * k],
          'arriba-derecha': [b.x + b.w - r - 4 * k, b.y - r - 4 * k],
          abajo: [b.x + b.w / 2, b.y + b.h + r + 4 * k],
          'abajo-derecha': [b.x + b.w - r - 4 * k, b.y + b.h + r + 4 * k],
          'abajo-izquierda': [b.x + r + 4 * k, b.y + b.h + r + 4 * k],
          'arriba-izquierda': [b.x + r + 4 * k, b.y - r - 4 * k],
          dentro: [b.x + r + 4 * k, b.y + b.h / 2],
        }[p];
        if (!pos || !dentro(pos[0], pos[1], W, H, r + 2)) continue;
        // Texto tapado cuenta doble que un recuadro tapado.
        const costo = 2 * choca(pos[0], pos[1], rr, obstaculos) + choca(pos[0], pos[1], rr, otros) + 3 * choca(pos[0], pos[1], rr, ocupados);
        if (costo < mejor) { mejor = costo; [px, py] = pos; }
        if (costo === 0) break;
      }
      if (px !== undefined) ocupados.push({ x: px - rr, y: py - rr, w: 2 * rr, h: 2 * rr });
      if (px === undefined) [px, py] = [Math.max(r + 2, b.x), Math.max(r + 2, b.y)];
      capas.numeros.push(`<circle cx="${px}" cy="${py}" r="${r + 3 * k}" fill="${B}"/>`,
        `<circle cx="${px}" cy="${py}" r="${r}" fill="${C}"/>`,
        `<text x="${px}" y="${py}" fill="#fff" font-family="${ESTILO.fuente}" font-weight="700" font-size="${r * 1.25}" text-anchor="middle" dominant-baseline="central">${esc(a.n)}</text>`);
    }
    if (a.tipo === 'flecha') {
      // Si la flecha no cabe en la dirección pedida, prueba las demás.
      const largo = (a.largo || 110) * k;
      const pedidas = [a.desde || 'izquierda', 'izquierda', 'derecha', 'abajo', 'arriba'];
      let u, punta, cola;
      for (const p of pedidas) {
        const d = DIRS[p] || DIRS.izquierda, n = Math.hypot(d[0], d[1]);
        u = [d[0] / n, d[1] / n];
        const [ex, ey] = bordeEn(amp(c), u);
        punta = [ex + u[0] * 4 * k, ey + u[1] * 4 * k];
        cola = [punta[0] + u[0] * largo, punta[1] + u[1] * largo];
        const holgura = a.texto ? anchoTexto(a.texto, 22 * k) * Math.abs(u[0]) + 40 * k * Math.abs(u[1]) : 0;
        if (dentro(cola[0] + u[0] * holgura, cola[1] + u[1] * holgura, W, H, 6)) break;
      }
      cola = [Math.min(Math.max(cola[0], 4), W - 4), Math.min(Math.max(cola[1], 4), H - 4)];
      const ang = Math.atan2(punta[1] - cola[1], punta[0] - cola[0]);
      const t = 16 * k; // tamaño de la punta
      const base = [punta[0] - Math.cos(ang) * t * 1.3, punta[1] - Math.sin(ang) * t * 1.3];
      const p1 = [base[0] + Math.cos(ang + Math.PI / 2) * t * 0.75, base[1] + Math.sin(ang + Math.PI / 2) * t * 0.75];
      const p2 = [base[0] + Math.cos(ang - Math.PI / 2) * t * 0.75, base[1] + Math.sin(ang - Math.PI / 2) * t * 0.75];
      const tri = `${punta[0]},${punta[1]} ${p1[0]},${p1[1]} ${p2[0]},${p2[1]}`;
      capas.flechas.push(
        `<line x1="${cola[0]}" y1="${cola[1]}" x2="${base[0]}" y2="${base[1]}" stroke="${B}" stroke-width="${g + 4 * k}" stroke-linecap="round"/>`,
        `<polygon points="${tri}" fill="${B}" stroke="${B}" stroke-width="${4 * k}" stroke-linejoin="round"/>`,
        `<line x1="${cola[0]}" y1="${cola[1]}" x2="${base[0]}" y2="${base[1]}" stroke="${C}" stroke-width="${g}" stroke-linecap="round"/>`,
        `<polygon points="${tri}" fill="${C}"/>`);
      if (a.texto) capas.etiquetas.push(etiqueta(a.texto, cola[0], cola[1], u, W, H, k));
      if (a.recuadro) { const b = amp(c); capas.cajas.push(`<rect x="${b.x}" y="${b.y}" width="${b.w}" height="${b.h}" rx="${8 * k}" fill="none" stroke="${C}" stroke-width="${g}"/>`); }
    }
    if (a.tipo === 'etiqueta') {
      const d = DIRS[a.posicion || 'derecha'] || DIRS.derecha;
      const [ex, ey] = bordeEn(amp(c), d);
      capas.etiquetas.push(etiqueta(a.texto, ex + d[0] * 10 * k, ey + d[1] * 10 * k, d, W, H, k));
    }
  }
  return `<svg xmlns="http://www.w3.org/2000/svg" width="${W}" height="${H}" viewBox="0 0 ${W} ${H}" style="position:absolute;left:0;top:0">
    ${capas.foco.join('')}${capas.cajas.join('')}${capas.flechas.join('')}${capas.numeros.join('')}${capas.etiquetas.join('')}</svg>`;
}

// Etiqueta de texto anclada en (x,y), extendiéndose en la dirección u.
function etiqueta(texto, x, y, u, W, H, k) {
  const tam = 22 * k, pad = 10 * k;
  const w = anchoTexto(texto, tam) + 2 * pad, h = tam + 2 * pad;
  let lx = u[0] < -0.3 ? x - w : u[0] > 0.3 ? x : x - w / 2;
  let ly = u[1] < -0.3 ? y - h : u[1] > 0.3 ? y : y - h / 2;
  lx = Math.min(Math.max(lx, 4), W - w - 4); ly = Math.min(Math.max(ly, 4), H - h - 4);
  return `<rect x="${lx}" y="${ly}" width="${w}" height="${h}" rx="${8 * k}" fill="${ESTILO.color}" stroke="${ESTILO.contorno}" stroke-width="${3 * k}"/>` +
    `<text x="${lx + w / 2}" y="${ly + h / 2}" fill="#fff" font-family="${ESTILO.fuente}" font-weight="700" font-size="${tam}" text-anchor="middle" dominant-baseline="central">${esc(texto)}</text>`;
}

// Compone la imagen anotada con Chrome (Playwright) y devuelve el PNG final.
async function anotar(browser, png, ancho, alto, anots, escala, obstaculos) {
  const page = await browser.newPage({ viewport: { width: ancho, height: alto }, deviceScaleFactor: 1 });
  const b64 = png.toString('base64');
  await page.setContent(`<html><body style="margin:0;background:#fff">
    <div id="lienzo" style="position:relative;width:${ancho}px;height:${alto}px">
      <img src="data:image/png;base64,${b64}" style="position:absolute;left:0;top:0;width:${ancho}px;height:${alto}px">
      ${svgAnotaciones(anots || [], ancho, alto, escala, obstaculos)}
    </div></body></html>`);
  await page.waitForFunction(() => document.querySelector('img').complete);
  const out = await page.locator('#lienzo').screenshot({ type: 'png' });
  await page.close();
  return out;
}

module.exports = { anotar, svgAnotaciones, ESTILO };
