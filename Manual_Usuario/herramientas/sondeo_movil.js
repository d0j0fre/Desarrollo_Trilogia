// Pregunta a la API qué módulos de la app móvil ve cada perfil. Salida: herramientas/salida/sondeo_movil.json
const cfg = require('./config');
const fs = require('fs'); const path = require('path');
(async () => {
  const out = {};
  for (const u of cfg.usuarios) {
    // La API limita los inicios de sesión por minuto: ante un 429 espera y reintenta (no es falta de acceso).
    let r;
    for (let i = 0; i < 8; i++) {
      r = await fetch(cfg.api + '/api/auth/login', { method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email: u.correo, password: cfg.CLAVE_DEMO, deviceId: 'manual-sondeo', deviceName: 'Sondeo manual' }) });
      if (r.status !== 429) break;
      console.log(`  (límite de inicios de sesión; espero 60 s para ${u.correo})`);
      await new Promise(res => setTimeout(res, 60000));
    }
    const j = await r.json().catch(() => ({}));
    if (!j.accessToken) { out[u.perfil] = { acceso: false, mensaje: j.message || r.status }; console.log(u.perfil, '→ sin acceso:', j.message); continue; }
    const c = await (await fetch(cfg.api + '/api/mobile/v1/me/capabilities', { headers: { Authorization: 'Bearer ' + j.accessToken } })).json();
    const mods = (c.modules || c.modulos || c).filter ? (c.modules || c.modulos || c).filter(m => m.enabled).map(m => `${m.section}: ${m.title}`) : c;
    out[u.perfil] = { acceso: true, modulos: mods };
    console.log(u.perfil, '→', Array.isArray(mods) ? mods.join(' | ') : JSON.stringify(c).slice(0, 200));
  }
  fs.writeFileSync(path.join(__dirname, 'salida', 'sondeo_movil.json'), JSON.stringify(out, null, 2));
})();
