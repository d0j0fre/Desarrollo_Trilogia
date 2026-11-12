// Datos de ejemplo del grupo 2 que conviene crear por pantalla (como Administrador):
// cálculos de planilla del Empleado Demo y del Vendedor Demo en el período 1, para que haya boletas.
// Requiere haber corrido antes: node capitulos/10_administrador/_preparar_datos.js planilla calculos
//   $env:MANUAL_WEB='http://localhost:7150'; node capitulos/21_supervisor/_preparar_datos_g2.js
// Después: _datos_grupo2.sql (deja los cálculos en Pagada y crea licencias y jornadas de ejemplo).
const { chromium } = require('playwright-core');
const cfg = require('../../herramientas/config');

(async () => {
  const browser = await chromium.launch({ channel: 'chrome', headless: true });
  const page = await (await browser.newContext({ viewport: { width: 1400, height: 900 }, locale: 'es-CR' })).newPage();
  await cfg.login(page, 'admin');
  for (const empleado of [25, 16]) {
    await page.goto(cfg.web + '/Payroll', { waitUntil: 'networkidle' });
    await page.fill('#period-id', '1'); await page.fill('#employee-id', String(empleado));
    await Promise.all([page.waitForNavigation({ waitUntil: 'networkidle' }).catch(() => {}), page.click('form[action*="Calculate"] button[type=submit]')]);
    console.log('cálculo empleado', empleado, '→', (await page.locator('.alert-success, .alert-danger').first().innerText().catch(() => page.url())).trim());
  }
  await browser.close();
})();
