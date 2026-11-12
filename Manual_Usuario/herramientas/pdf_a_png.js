// Renderiza páginas de un PDF a PNG para revisarlas (usa pdf.js dentro de Chrome).
//   node herramientas/pdf_a_png.js salida/Manual.pdf 1,2,5-8 [carpetaSalida] [escala]
const { chromium } = require('playwright-core');
const fs = require('fs'); const path = require('path');
const [pdf, rangos = '1', dirSal = 'salida/paginas', escala = '1.3'] = process.argv.slice(2);
const paginas = rangos.split(',').flatMap(r => { const [a, b] = r.split('-').map(Number); return b ? Array.from({ length: b - a + 1 }, (_, i) => a + i) : [a]; });
(async () => {
  const b = await chromium.launch({ channel: 'chrome' });
  const p = await b.newPage();
  await p.setContent('<body style="margin:0;background:#888"><canvas id="c"></canvas></body>');
  await p.addScriptTag({ path: require.resolve('pdfjs-dist/build/pdf.min.js') });
  const worker = fs.readFileSync(require.resolve('pdfjs-dist/build/pdf.worker.min.js'), 'utf8');
  const datos = fs.readFileSync(pdf).toString('base64');
  const total = await p.evaluate(async ({ worker, datos }) => {
    pdfjsLib.GlobalWorkerOptions.workerSrc = URL.createObjectURL(new Blob([worker], { type: 'text/javascript' }));
    window.doc = await pdfjsLib.getDocument({ data: Uint8Array.from(atob(datos), c => c.charCodeAt(0)) }).promise;
    return doc.numPages;
  }, { worker, datos });
  fs.mkdirSync(dirSal, { recursive: true });
  for (const n of paginas.filter(n => n <= total)) {
    await p.evaluate(async ({ n, escala }) => {
      const pg = await doc.getPage(n); const vp = pg.getViewport({ scale: escala });
      const c = document.getElementById('c'); c.width = vp.width; c.height = vp.height;
      await pg.render({ canvasContext: c.getContext('2d'), viewport: vp }).promise;
    }, { n, escala: +escala });
    await p.locator('#c').screenshot({ path: path.join(dirSal, `p${String(n).padStart(3, '0')}.png`) });
  }
  console.log(`${total} páginas; renderizadas: ${paginas.filter(n => n <= total).join(', ')}`);
  await b.close();
})();
