// Crea, con el propio sistema (como Administrador), los datos de ejemplo de la Parte II que
// conviene registrar por pantalla: proveedores, órdenes de compra, combos, departamentos del chat,
// documentos digitales y planilla. Complementa _datos_ejemplo.sql.
//   $env:MANUAL_WEB='http://localhost:7180'; node capitulos/10_administrador/_preparar_datos.js [paso ...]
// Pasos: proveedores ordenes combos chat documentos planilla (sin argumentos: todos).
const { chromium } = require('playwright-core');
const path = require('path');
const fs = require('fs');
const cfg = require('../../herramientas/config');

const pasos = process.argv.slice(2);
const toca = p => !pasos.length || pasos.includes(p);
const ir = (page, ruta) => page.goto(cfg.web + ruta, { waitUntil: 'networkidle' });
async function enviar(page, selector) {
  await Promise.all([page.waitForNavigation({ waitUntil: 'networkidle' }).catch(() => {}), page.click(selector)]);
  const ok = await page.locator('.alert-success').first().innerText().catch(() => '');
  const err = await page.locator('.alert-danger, .validation-summary-errors, .text-danger:not(:empty)').allInnerTexts().catch(() => []);
  console.log('   →', (ok || err.filter(Boolean).join(' / ') || page.url()).replace(/\s+/g, ' ').slice(0, 160));
}

(async () => {
  const browser = await chromium.launch({ channel: 'chrome', headless: true });
  const page = await (await browser.newContext({ viewport: { width: 1400, height: 900 }, locale: 'es-CR' })).newPage();
  await cfg.login(page, 'admin');

  if (toca('proveedores')) {
    const proveedores = [
      ['Distribuidora Central de Licores', 'Marta Solano', '2222-4100', 'ventas@licorescentral.example'],
      ['Hielo y Bebidas del Valle', 'Jorge Arce', '2440-1818', 'pedidos@hielovalle.example'],
      ['Importadora Vinos del Sur', 'Lucía Brenes', '2290-7733', 'lucia@vinosdelsur.example'],
      ['Snacks La Cosecha', 'Andrés Mora', '2550-3030', 'ventas@lacosecha.example'],
    ];
    for (const [nombre, contacto, tel, mail] of proveedores) {
      await ir(page, '/Suppliers?filtro=' + encodeURIComponent(nombre));
      if (await page.locator('main table tbody tr', { hasText: nombre }).count()) { console.log('   (ya existe)', nombre); continue; }
      await ir(page, '/Suppliers');
      await page.fill('#Nombre', nombre); await page.fill('#Contacto', contacto);
      await page.fill('#Telefono', tel); await page.fill('#Email', mail);
      console.log('proveedor', nombre); await enviar(page, 'form[action*="Suppliers/Save"] button[type=submit]');
    }
  }

  if (toca('ordenes')) {
    // Dos órdenes: una se recibe completa y otra queda pendiente.
    const ordenes = [
      { prov: 'Distribuidora Central de Licores', notas: 'Reposición quincenal de licores', items: { 'Cacique Superior 1L': [24, 5200], 'Flor de Caña 7 Años 750ml': [12, 10400], 'Smirnoff Vodka 750ml': [12, 7600] } },
      { prov: 'Importadora Vinos del Sur', notas: 'Vinos para temporada de fin de año', items: { 'Casillero del Diablo Cabernet 750ml': [24, 6300], 'Vino Tinto Reserva 750ml': [18, 5600] } },
      { prov: 'Hielo y Bebidas del Valle', notas: 'Hielo y refrescos para eventos', items: { 'Bolsa de Hielo 2kg': [60, 650], 'Coca-Cola 2.5L': [36, 1150] } },
    ];
    for (const o of ordenes) {
      await ir(page, '/PurchaseOrders/Create');
      await page.selectOption('#ProveedorId', { label: o.prov });
      await page.fill('#Notas', o.notas);
      for (const [producto, [cant, precio]] of Object.entries(o.items)) {
        const fila = page.locator('main table tbody tr', { hasText: producto }).first();
        await fila.locator('input[type=checkbox]').check();
        await fila.locator('input[name$=".Cantidad"]').fill(String(cant));
        await fila.locator('input[name$=".PrecioUnitario"]').fill(String(precio));
      }
      console.log('orden', o.prov); await enviar(page, 'main form button[type=submit]');
    }
  }

  if (toca('combos')) {
    const combos = [
      { nombre: 'Combo Fiesta Cacique', desc: 'Cacique 750ml, 2 Coca-Cola 2.5L y 1 bolsa de hielo.', precio: 11500, items: { 'Cacique 750ml': 1, 'Coca-Cola 2.5L': 2, 'Bolsa de Hielo 2kg': 1 } },
      { nombre: 'Combo Cervecero', desc: '2 six packs Imperial Silver y Yucas Tostadas.', precio: 14900, items: { 'Imperial Silver 6 Pack': 2, 'Yucas Tostadas 150g': 1 } },
    ];
    for (const c of combos) {
      await ir(page, '/Combos');
      if (await page.locator('main', { hasText: c.nombre }).count()) { console.log('   (ya existe)', c.nombre); continue; }
      await ir(page, '/Combos/Create');
      await page.fill('#Nombre', c.nombre); await page.fill('#Descripcion', c.desc); await page.fill('#Precio', String(c.precio));
      for (const [producto, cant] of Object.entries(c.items)) {
        const fila = page.locator('main table tbody tr', { hasText: producto }).first();
        await fila.locator('input[type=checkbox]').check();
        await fila.locator('input[name$=".Cantidad"]').fill(String(cant));
      }
      console.log('combo', c.nombre); await enviar(page, 'main form button[type=submit]');
    }
  }

  if (toca('chat')) {
    for (const [nombre, desc] of [['Bodega', 'Preparación de pedidos, recepción de compras e inventario'], ['Ventas', 'Vendedores de ruta, cotizaciones y clientes'], ['Cobros', 'Crédito, abonos y liquidación de rutas']]) {
      await ir(page, '/Chat');
      if (await page.locator(`input[value="${nombre}"]`).count()) { console.log('   (ya existe)', nombre); continue; }
      await page.fill('#departmentName', nombre); await page.fill('#departmentDescription', desc);
      console.log('departamento', nombre); await enviar(page, 'form[action*="CreateDepartment"] button[type=submit]');
    }
  }

  if (toca('recepcion')) {
    // Orden 1: se recibe completa. Orden 2: solo una línea (queda RecibidaParcial).
    const recibir = { 1: ['Cacique Superior 1L', 'Flor de Caña 7 Años 750ml', 'Smirnoff Vodka 750ml'], 2: ['Casillero del Diablo Cabernet 750ml'] };
    for (const [orden, productos] of Object.entries(recibir)) {
      for (const producto of productos) {
        await ir(page, '/PurchaseOrders/Detail/' + orden);
        const fila = page.locator('main table tbody tr', { hasText: producto }).first();
        if (!(await fila.locator('input[name=CantidadRecibidaAhora]').count())) { console.log('   (ya recibido)', producto); continue; }
        const pendiente = (await fila.locator('td').nth(3).innerText()).trim();
        await fila.locator('input[name=CantidadRecibidaAhora]').fill(pendiente);
        console.log('recibir', orden, producto, pendiente);
        await Promise.all([page.waitForNavigation({ waitUntil: 'networkidle' }), fila.locator('button').first().click()]);
        console.log('   →', (await page.locator('.alert-success, .alert-danger').first().innerText().catch(() => page.url())).replace(/\s+/g, ' '));
      }
    }
  }

  if (toca('precios')) {
    // Segunda compra de Cacique Superior 1L a otro precio, para que el histórico muestre la variación.
    await ir(page, '/PurchaseOrders/Create');
    await page.selectOption('#ProveedorId', { label: 'Distribuidora Central de Licores' });
    await page.fill('#Notas', 'Reposición por promoción de fin de mes');
    const fila = page.locator('main table tbody tr', { hasText: 'Cacique Superior 1L' }).first();
    await fila.locator('input[type=checkbox]').check();
    await fila.locator('input[name$=".Cantidad"]').fill('24');
    await fila.locator('input[name$=".PrecioUnitario"]').fill('6100');
    console.log('orden precio'); await enviar(page, 'main form button[type=submit]');
    await ir(page, '/PurchaseOrders?estado=Pendiente');
    const id = (await page.locator('main table tbody tr', { hasText: 'Distribuidora Central de Licores' }).first().locator('td').first().innerText()).trim();
    await ir(page, '/PurchaseOrders/Detail/' + id);
    const f = page.locator('main table tbody tr', { hasText: 'Cacique Superior 1L' }).first();
    await f.locator('input[name=CantidadRecibidaAhora]').fill('24');
    await Promise.all([page.waitForNavigation({ waitUntil: 'networkidle' }), f.locator('button').first().click()]);
    console.log('   → recibida orden', id);
  }

  if (toca('miembros')) {
    const miembros = { Bodega: ['bodeguero@labodega.cr', 'compras@labodega.cr', 'supervisor@labodega.cr'], Ventas: ['vendedor@labodega.cr', 'gerente@labodega.cr', 'facturador@labodega.cr'], Cobros: ['credito@labodega.cr', 'cajero@labodega.cr'] };
    for (const [depto, correos] of Object.entries(miembros)) {
      for (const correo of correos) {
        await ir(page, '/Chat');
        const tarjeta = page.locator('form[action*="UpdateDepartment"]', { has: page.locator(`input[value="${depto}"]`) }).locator('xpath=ancestor::*[.//form[contains(@action,"AddMember")]][1]');
        if (await tarjeta.locator('td', { hasText: correo }).count()) { console.log('   (ya es miembro)', depto, correo); continue; }
        const select = tarjeta.locator('form[action*="AddMember"] select');
        const opcion = await select.locator('option', { hasText: correo }).first().getAttribute('value');
        await select.selectOption(opcion);
        console.log('miembro', depto, correo);
        await Promise.all([page.waitForNavigation({ waitUntil: 'networkidle' }), tarjeta.locator('form[action*="AddMember"] button[type=submit]').click()]);
      }
    }
  }

  if (toca('documentos')) {
    const hoy = new Date('2026-11-02');
    const dia = n => { const d = new Date(hoy); d.setDate(d.getDate() + n); return d.toISOString().slice(0, 10); };
    const docs = [
      { tipo: 'Licencia', depto: 'Administración', titulo: 'Patente municipal de licores', ref: 'PAT-2026-0457', emision: '2026-01-10', vence: dia(12), archivo: 'patente_municipal.pdf', desc: 'Licencia clase B para la venta de licores en la bodega central.' },
      { tipo: 'Póliza', depto: 'Operaciones', titulo: 'Póliza de seguro de la flota de reparto', ref: 'POL-AUTO-88213', emision: '2025-11-15', vence: dia(-5), archivo: 'poliza_flota.pdf', desc: 'Cobertura de los camiones C-01 a C-04.' },
      { tipo: 'Permiso', depto: 'Operaciones', titulo: 'Permiso sanitario de funcionamiento', ref: 'PSF-11-2025-0932', emision: '2025-11-20', vence: dia(27), archivo: 'permiso_sanitario.pdf', desc: 'Permiso del Ministerio de Salud para la bodega central.' },
      { tipo: 'Contrato', depto: 'Administración', titulo: 'Contrato de alquiler de la bodega', ref: 'CT-ALQ-2024-01', emision: '2024-03-01', vence: '2027-02-28', archivo: 'contrato_alquiler.pdf', desc: 'Alquiler del local de bodega por 3 años.' },
    ];
    for (const d of docs) {
      await ir(page, '/Documents?Search=' + encodeURIComponent(d.ref));
      if (await page.locator('main table tbody tr', { hasText: d.titulo }).count()) { console.log('   (ya existe)', d.titulo); continue; }
      await ir(page, '/Documents/Create');
      await page.selectOption('#TypeId', { label: d.tipo });
      await page.selectOption('#DepartmentId', { label: d.depto });
      await page.fill('#Title', d.titulo); await page.fill('#Description', d.desc); await page.fill('#ReferenceNumber', d.ref);
      await page.fill('#IssueDate', d.emision); await page.fill('#ExpirationDate', d.vence);
      await page.setInputFiles('#File', path.join(__dirname, '_archivos', d.archivo));
      console.log('documento', d.titulo); await enviar(page, 'main form[enctype] button');
    }
  }

  if (toca('version')) {
    // Segunda versión del contrato de alquiler (para el historial de versiones).
    await ir(page, '/Documents?Search=CT-ALQ-2024-01');
    await page.locator("main table tbody tr:has-text('Contrato de alquiler') a:has-text('Ver')").first().click();
    await page.waitForLoadState('networkidle');
    if (await page.locator('main', { hasText: 'Versión 2' }).count()) console.log('   (ya tiene versión 2)');
    else {
      await page.setInputFiles('#replacement-file', path.join(__dirname, '_archivos', 'contrato_alquiler_v2.pdf'));
      console.log('versión 2 del contrato'); await enviar(page, 'main form[action*=ReplaceFile] button');
    }
  }

  if (toca('app')) {
    // Versión de la aplicación móvil (la página Aplicación móvil muestra la descarga).
    await ir(page, '/MobileApp/Index');
    if (await page.locator('main', { hasText: '1.2.0' }).count()) console.log('   (ya publicada)');
    else {
      await page.setInputFiles('#archivo', path.join(__dirname, '..', '..', 'herramientas', 'movil', 'app-manual.apk'));
      await page.fill('#VersionNombre', '1.2.0'); await page.fill('#BuildNumero', '5'); await page.fill('#MinBuildSoportado', '5');
      await page.fill('#Notas', 'Liquidación de rutas y recepción de compras desde el teléfono.');
      console.log('publicar app'); await enviar(page, 'main form[action*=Publish] button[type=submit]');
    }
  }

  if (toca('planilla')) {
    // Los porcentajes se escriben como se leen (2 = 2 %, 10,67 = 10,67 %). Pensado para una base limpia: las reglas no se repiten y el período no puede traslaparse.
    const reglas = [
      ['BONO_PUNTUALIDAD', 'Bono por puntualidad', 'Ingreso', '% de salario', '2', 'Acuerdo de Gerencia del 15/12/2025'],
      ['CCSS_OBRERO', 'Cuota obrera CCSS', 'Deducción', '% bruto', '10,67', 'Tabla de cuotas vigente, aprobada por Contabilidad'],
    ];
    for (const [codigo, nombre, tipo, calculo, valor, fuente] of reglas) {
      await ir(page, '/Payroll');
      await page.fill('#rule-code', codigo); await page.fill('#rule-name', nombre);
      await page.selectOption('#rule-type', { label: tipo }); await page.selectOption('#rule-calculation', { label: calculo });
      await page.fill('#rule-value', valor); await page.fill('#rule-from', '2026-01-01'); await page.fill('#rule-source', fuente);
      console.log('regla', codigo); await enviar(page, 'form[action*="ConfigureRule"] button[type=submit]');
    }
    await ir(page, '/Payroll');
    await page.selectOption('#period-type', { label: 'Quincenal' });
    await page.fill('#period-from', '2026-10-16'); await page.fill('#period-to', '2026-10-31');
    await page.fill('#salary-factor', '0,5'); await page.fill('#factor-source', 'Política de pago quincenal aprobada por Gerencia');
    console.log('período'); await enviar(page, 'form[action*="CreatePeriod"] button[type=submit]');
  }

  if (toca('calculos')) {
    // Período 1 (16–31/10/2026): vendedor de ruta con comisiones y encargado de bodega sin comisiones.
    for (const [empleado, comisiones] of [[3, '25000'], [6, '0'], [9, '0']]) {
      await ir(page, '/Payroll');
      await page.selectOption('#period-id', '1'); await page.selectOption('#employee-id', String(empleado));
      // Desde el PR #131 las comisiones se calculan solas (regla COMISION_VENTAS) y el campo ya no existe.
      if (await page.locator('#commission').count()) await page.fill('#commission', comisiones);
      console.log('cálculo empleado', empleado); await enviar(page, 'form[action*="Calculate"] button[type=submit]');
    }
  }
  await browser.close();
})();
