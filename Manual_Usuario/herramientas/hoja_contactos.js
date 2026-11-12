// Une varias páginas renderizadas en una sola imagen (para revisar rápido).
//   node herramientas/hoja_contactos.js salida/paginas salida/hoja.png [columnas] [desde] [hasta]
const { chromium } = require('playwright-core'); const fs = require('fs'); const path = require('path');
const [dir, sal, cols = '3', desde = '1', hasta = '999'] = process.argv.slice(2);
const files = fs.readdirSync(dir).filter(f => /^p\d+\.png$/.test(f)).filter(f => { const n = +f.slice(1, 4); return n >= +desde && n <= +hasta; }).sort();
(async () => {
  const b = await chromium.launch({ channel: 'chrome' }); const p = await b.newPage({ viewport: { width: 1800, height: 1000 } });
  const imgs = files.map(f => `<figure style="margin:0"><img src="data:image/png;base64,${fs.readFileSync(path.join(dir, f)).toString('base64')}" style="width:100%;border:1px solid #444"><figcaption style="font:14px sans-serif;text-align:center">${f}</figcaption></figure>`).join('');
  await p.setContent(`<body style="margin:8px;background:#ccc"><div id="g" style="display:grid;grid-template-columns:repeat(${cols},1fr);gap:10px;width:1780px">${imgs}</div></body>`);
  await p.locator('#g').screenshot({ path: sal }); await b.close();
})();
