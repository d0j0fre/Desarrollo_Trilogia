// Control del emulador Android por adb para las capturas móviles.
const { execFileSync } = require('child_process');
const fs = require('fs');
const path = require('path');
const os = require('os');

const ADB = process.env.ADB || path.join(process.env.LOCALAPPDATA || '', 'Android', 'Sdk', 'platform-tools', 'adb.exe');
const PKG = 'cr.distribuidorajj.proyecto_movil';
const ESTADO = path.join(__dirname, 'salida', 'movil_sesion.txt'); // usuario con sesión abierta en el emulador

function adb(args, opts = {}) {
  return execFileSync(ADB, args, { maxBuffer: 64 * 1024 * 1024, stdio: ['ignore', 'pipe', 'ignore'], ...opts });
}
const sh = cmd => adb(['shell', cmd]).toString();
const dormir = ms => new Promise(r => setTimeout(r, ms));

function dispositivo() {
  const lista = adb(['devices']).toString().split(/\r?\n/).slice(1).filter(l => /\tdevice$/.test(l));
  if (!lista.length) throw new Error('No hay emulador conectado. Iniciá Medium_Phone_API_36.1 (ver HERRAMIENTAS.md).');
}

// Barra de estado limpia: hora fija, batería llena, sin notificaciones.
function barraDemo() {
  try {
    sh('settings put global sysui_demo_allowed 1');
    const b = c => sh(`am broadcast -a com.android.systemui.demo -e command ${c}`);
    b('enter');
    b(`clock -e hhmm ${sh('date +%H%M').trim()}`); // hora real: el saludo de la app depende de ella
    b('battery -e level 100 -e plugged false');
    b('network -e wifi show -e level 4 -e mobile show -e datatype none -e level 4');
    b('notifications -e visible false');
  } catch { /* opcional */ }
}

function captura() { return adb(['exec-out', 'screencap', '-p']); }

function tamano() {
  const m = sh('wm size').match(/(\d+)x(\d+)/);
  return { w: +m[1], h: +m[2] };
}

// Árbol de accesibilidad (Flutter publica sus textos aquí): [{texto, desc, clase, caja}]
function nodos() {
  // uiautomator falla a veces con el emulador cargado (arbol nulo): se reintenta.
  for (let i = 0; ; i++) {
    try { sh('uiautomator dump /sdcard/ui.xml'); break; }
    catch (e) { if (i >= 5) throw e; Atomics.wait(new Int32Array(new SharedArrayBuffer(4)), 0, 0, 1500); }
  }
  const xml = adb(['exec-out', 'cat', '/sdcard/ui.xml']).toString();
  const res = [];
  const re = /<node [^>]*>/g;
  let m;
  while ((m = re.exec(xml))) {
    const at = n => { const r = new RegExp(` ${n}="([^"]*)"`).exec(m[0]); return r ? r[1].replace(/&#10;/g, '\n').replace(/&amp;/g, '&').replace(/&quot;/g, '"') : ''; };
    const b = at('bounds').match(/\[(\d+),(\d+)\]\[(\d+),(\d+)\]/);
    if (!b) continue;
    res.push({ texto: at('text') || at('hint'), desc: at('content-desc'), pista: at('hint'), clase: at('class'), clic: at('clickable') === 'true',
      caja: { x: +b[1], y: +b[2], w: +b[3] - +b[1], h: +b[4] - +b[2] } });
  }
  return res;
}

// Busca un elemento por texto visible (o descripción). Coincidencia exacta primero, luego parcial.
// "indice" elige entre varias coincidencias (0 = la primera de arriba hacia abajo).
function buscar(texto, { indice = 0, clase } = {}) {
  const t = String(texto).toLowerCase();
  const todos = nodos().filter(n => n.caja.w > 0 && n.caja.h > 0 && (!clase || n.clase.includes(clase)));
  const etiqueta = n => `${n.texto}\n${n.desc}`.toLowerCase();
  let hits = todos.filter(n => n.texto.toLowerCase() === t || n.pista.toLowerCase() === t || n.desc.toLowerCase() === t || n.desc.toLowerCase().split('\n')[0] === t);
  if (!hits.length) hits = todos.filter(n => etiqueta(n).includes(t));
  // Entre contenedores anidados con el mismo texto, el más pequeño es el elemento.
  hits.sort((a, b) => a.caja.y - b.caja.y || a.caja.w * a.caja.h - b.caja.w * b.caja.h);
  const unicos = hits.filter((n, i) => !hits.slice(0, i).some(p => p.caja.y === n.caja.y && p.caja.x <= n.caja.x && p.caja.x + p.caja.w >= n.caja.x + n.caja.w));
  return unicos[indice] || null;
}

function centro(c) { return [Math.round(c.x + c.w / 2), Math.round(c.y + c.h / 2)]; }
function tocarEn(x, y) { sh(`input tap ${x} ${y}`); }
function escribir(texto) {
  // input text no acepta espacios ni algunos símbolos sin escapar.
  // Un carácter por llamada: con el emulador lento (renderizado por software) una cadena
  // larga pierde o duplica caracteres.
  for (const ch of String(texto)) {
    const t = ch.replace(/([\\"'`$&|;<>()*?!#~])/g, '\\$1').replace(/ /g, '%s');
    sh(`input text "${t}"`);
  }
}

async function esperarTexto(texto, ms = 30000) {
  const fin = Date.now() + ms;
  while (Date.now() < fin) {
    const n = buscar(texto);
    if (n) return n;
    await dormir(800);
  }
  throw new Error(`No apareció "${texto}" en el emulador.`);
}

// Cierra el teclado en pantalla solo si está visible ("atrás" sin teclado saldría de la app).
function ocultarTeclado() {
  if (/mInputShown=true/.test(sh('dumpsys input_method'))) sh('input keyevent 4');
}

function abrirApp() { sh(`monkey -p ${PKG} -c android.intent.category.LAUNCHER 1`); }

// Inicia sesión en la app con datos limpios. Reutiliza la sesión si ya es del mismo usuario.
async function sesion(correo, clave) {
  const actual = fs.existsSync(ESTADO) ? fs.readFileSync(ESTADO, 'utf8').trim() : '';
  if (actual === (correo || '(ninguno)')) {
    sh(`am force-stop ${PKG}`); abrirApp();
    // La pantalla de inicio está lista cuando aparece el botón de la cuenta (o el login, si no hay sesión).
    await esperarTexto(correo ? 'Mi cuenta y ajustes' : 'Ingresar', 120000).catch(() => {});
    await dormir(1500); return;
  }
  sh(`pm clear ${PKG}`);
  try { sh(`pm grant ${PKG} android.permission.POST_NOTIFICATIONS`); } catch { }
  abrirApp();
  fs.mkdirSync(path.dirname(ESTADO), { recursive: true });
  if (!correo) { await esperarTexto('Ingresar'); fs.writeFileSync(ESTADO, '(ninguno)'); return; }
  await esperarTexto('Ingresar');
  let campos = [];
  for (let i = 0; i < 20 && campos.length < 2; i++) { campos = nodos().filter(n => n.clase.includes('EditText')); if (campos.length < 2) await dormir(750); }
  if (campos.length < 2) throw new Error('No aparecieron los campos de inicio de sesión de la app.');
  tocarEn(...centro(campos[0].caja)); await dormir(300); escribir(correo);
  // A veces el primer carácter se repite (cchofer@…): se verifica y, si hace falta, se borra y se vuelve a escribir.
  for (let intento = 0; intento < 3; intento++) {
    await dormir(400);
    const escrito = (nodos().filter(n => n.clase.includes('EditText'))[0] || {}).texto;
    if (escrito === correo) break;
    sh('input keyevent KEYCODE_MOVE_END');
    for (let i = 0; i < 40; i++) sh('input keyevent KEYCODE_DEL');
    escribir(correo);
  }
  tocarEn(...centro(campos[1].caja)); await dormir(300); escribir(clave);
  ocultarTeclado();
  await dormir(500);
  tocarEn(...centro((await esperarTexto('Ingresar')).caja));
  // La pantalla de inicio saluda al usuario; esperamos a que desaparezca el login.
  const fin = Date.now() + 60000;
  while (Date.now() < fin) {
    await dormir(1500);
    if (!buscar('Ingresar') && !buscar('Contraseña')) break;
  }
  await esperarTexto('Mi cuenta y ajustes').catch(() => {});
  await dormir(1500);
  fs.writeFileSync(ESTADO, correo);
}

module.exports = { ocultarTeclado, adb, sh, dispositivo, barraDemo, captura, tamano, nodos, buscar, centro, tocarEn, escribir, esperarTexto, sesion, dormir, PKG };
