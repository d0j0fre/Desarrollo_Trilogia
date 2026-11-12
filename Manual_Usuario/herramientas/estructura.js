// Genera mapa_puestos_modulos.md a partir de mapa.json y de los sondeos en vivo
// (herramientas/salida/sondeo_perfiles.json y sondeo_movil.json).
//   node herramientas/estructura.js
const fs = require('fs');
const path = require('path');
const RAIZ = path.resolve(__dirname, '..');
const mapa = JSON.parse(fs.readFileSync(path.join(RAIZ, 'mapa.json'), 'utf8'));
const leer = f => { try { return JSON.parse(fs.readFileSync(path.join(__dirname, 'salida', f), 'utf8')); } catch { return {}; } };
const web = leer('sondeo_perfiles.json');
const movil = leer('sondeo_movil.json');
const hallazgos = fs.existsSync(path.join(RAIZ, 'hallazgos.md')) ? fs.readFileSync(path.join(RAIZ, 'hallazgos.md'), 'utf8') : '';

const usuarios = m => Object.fromEntries(m.usuarios.split(',').map(s => s.trim().split(':')).map(([p, n]) => [p.trim(), (n || 'completo').trim()]));
const parte = id => mapa.partes.find(p => p.id === id);
const romano = n => ['', 'I', 'II', 'III', 'IV', 'V', 'VI', 'VII', 'VIII', 'IX', 'X', 'XI', 'XII', 'XIII', 'XIV', 'XV', 'XVI', 'XVII'][n];
const nParte = id => romano(mapa.partes.findIndex(p => p.id === id) + 1);
const perfiles = mapa.partes.filter(p => p.perfil);
const L = [];
const w = s => L.push(s);

w('# Mapa de puestos y módulos — Manual de usuario');
w('');
w(`Generado por \`node herramientas/estructura.js\` el ${new Date().toLocaleString('es-CR')}. Fuente: \`mapa.json\` (reparto) y los sondeos en vivo contra el código de la rama \`correcciones-esteban-manual\` (8f8ffcd) y una base v2 con las migraciones 0251 a 0257 (menú visible, incluidas las opciones del panel «Administración», y acceso real de cada perfil).`);
w('');
w('**Regla:** cada módulo tiene un **puesto dueño** (la parte del manual donde se escribe su procedimiento, una sola vez). Los demás puestos que lo usan no lo repiten: el generador del Word pone automáticamente, al inicio de cada parte, la tabla «Módulos de otras partes que también usa este puesto» con un hipervínculo a la sección del dueño. Las funciones de la app móvil tienen su propia parte (XVI), dueña de todas ellas.');
w('');

// ── Reparto en grupos
w('## 1. Reparto propuesto en 3 grupos (3 sesiones)');
w('');
const grupos = { 1: 'Grupo 1 — Administración', 2: 'Grupo 2 — Gestión y oficina', 3: 'Grupo 3 — Operación, clientes y app móvil' };
for (const g of [1, 2, 3]) {
  const partes = mapa.partes.filter(p => p.grupo === g);
  const mods = mapa.modulos.filter(m => partes.some(p => p.id === m.dueno));
  w(`### ${grupos[g]} · ${mods.length} módulos`);
  w('');
  w('| Parte | Carpeta | Módulos que escribe (ancla) |');
  w('|---|---|---|');
  for (const p of partes) {
    const propios = mapa.modulos.filter(m => m.dueno === p.id);
    w(`| ${nParte(p.id)}. ${p.titulo} | \`${p.carpeta}\` | ${propios.length ? propios.map(m => `${m.nombre} (\`#${m.id}\`)`).join('; ') : '— (solo intro del puesto; usa módulos de otras partes)'} |`);
  }
  w('');
}
w('La Parte I (Información general) y los Anexos los escribió la sesión base. Los tres grupos terminaron sus partes.');
w('');

// ── Por perfil
w('## 2. Qué ve cada perfil');
w('');
w('Columnas: **Menú web** = botones visibles tras iniciar sesión (sondeo). **Acceso web real** = módulos que abren sin «Acceso denegado» (sondeo por URL). **App móvil** = funciones habilitadas por la API para ese perfil.');
w('');
for (const p of perfiles) {
  const s = web[p.perfil] || {};
  const menu = [...new Set((s.menu || []).map(x => x.texto))].filter(t => !['Inicio', 'Tienda', 'Carrito', 'Chats privados', 'Departamentos'].includes(t));
  const acceso = Object.entries(s.acceso || {}).filter(([, v]) => v === 'si').map(([k]) => k).filter(k => !['Tienda', 'Carrito', 'Notificaciones', 'Mi perfil'].includes(k));
  const sinMenu = acceso.filter(a => !menu.some(m => m.toLowerCase().includes(a.toLowerCase().split(' (')[0]) || a.toLowerCase().includes(m.toLowerCase())));
  const mv = movil[p.perfil];
  w(`### ${p.titulo} — Parte ${nParte(p.id)}`);
  w('');
  w(`- **Menú web:** ${menu.join(', ') || '—'}`);
  w(`- **Acceso web real:** ${acceso.join(', ') || '—'}`);
  if (sinMenu.length) w(`- **Con acceso pero sin botón en el menú:** ${sinMenu.join(', ')}`);
  w(`- **App móvil:** ${mv ? (mv.acceso ? mv.modulos.join(' · ') : `sin acceso (${mv.mensaje})`) : '(sin sondeo)'}`);
  const propios = mapa.modulos.filter(m => m.dueno === p.id);
  const ajenos = mapa.modulos.filter(m => m.dueno !== p.id && usuarios(m)[p.perfil]);
  w(`- **Escribe en su parte:** ${propios.map(m => m.nombre).join(', ') || '—'}`);
  w(`- **Enlaza a otras partes:** ${ajenos.map(m => `${m.nombre} → ${parte(m.dueno).titulo}`).join('; ') || '—'}`);
  w('');
}

// ── Tabla maestra
w('## 3. Tabla maestra de módulos');
w('');
w('| Ancla | Módulo | Plataforma | Dónde está | Dueño (parte) | Grupo | También lo usan | Nota |');
w('|---|---|---|---|---|---|---|---|');
for (const m of mapa.modulos) {
  const u = usuarios(m);
  const otros = Object.entries(u).filter(([k]) => k !== parte(m.dueno).perfil).map(([k, v]) => `${k} (${v})`).join(', ');
  w(`| \`${m.id}\` | ${m.nombre} | ${m.plataforma === 'app' ? 'App' : 'Web'} | ${m.acceso} | ${nParte(m.dueno)}. ${parte(m.dueno).titulo} | ${parte(m.dueno).grupo} | ${otros || '—'} | ${m.nota || ''} |`);
}
w('');
w('Anclas de la Parte I que los capítulos pueden enlazar: ' + mapa.comunes.map(c => `\`#${c.id}\` (${c.nombre})`).join(', ') + '.');
w('');
if (hallazgos) { w(hallazgos.trim()); w(''); }

fs.writeFileSync(path.join(RAIZ, 'mapa_puestos_modulos.md'), L.join('\n'));
console.log('mapa_puestos_modulos.md generado');
