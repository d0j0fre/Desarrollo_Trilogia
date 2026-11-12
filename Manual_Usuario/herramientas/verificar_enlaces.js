// Revisa los enlaces internos del PDF exportado: cuántos hay y a qué página llevan.
//   node herramientas/verificar_enlaces.js salida/Manual.pdf [paginaDeMuestra]
const pdfjs = require('pdfjs-dist/legacy/build/pdf.js');
const fs = require('fs');
(async () => {
  const [archivo, muestra = '3'] = process.argv.slice(2);
  const doc = await pdfjs.getDocument({ data: new Uint8Array(fs.readFileSync(archivo)) }).promise;
  let internos = 0, rotos = 0, externos = 0;
  const ejemplos = [];
  for (let n = 1; n <= doc.numPages; n++) {
    const pg = await doc.getPage(n);
    const tc = await pg.getTextContent();
    for (const a of await pg.getAnnotations()) {
      if (a.subtype !== 'Link') continue;
      if (a.url) { externos++; continue; }
      let dest = a.dest;
      if (typeof dest === 'string') dest = await doc.getDestination(dest);
      if (!dest) { rotos++; continue; }
      internos++;
      const destino = (await doc.getPageIndex(dest[0])) + 1;
      if (String(n) === muestra && ejemplos.length < 8) {
        const [x1, y1, x2, y2] = a.rect;
        const txt = tc.items.filter(i => i.transform[5] >= y1 - 2 && i.transform[5] <= y2 + 2 && i.transform[4] >= x1 - 2 && i.transform[4] <= x2).map(i => i.str).join(' ').trim();
        ejemplos.push(`  pág. ${n} «${txt.slice(0, 60)}» → pág. ${destino}`);
      }
    }
  }
  console.log(`Enlaces internos: ${internos} · sin destino: ${rotos} · externos: ${externos}`);
  console.log(ejemplos.join('\n'));
})();
