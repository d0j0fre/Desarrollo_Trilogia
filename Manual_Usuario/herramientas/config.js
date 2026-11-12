// Configuración común de las herramientas del manual.
// Las direcciones se pueden cambiar con variables de entorno MANUAL_WEB y MANUAL_API.
const web = process.env.MANUAL_WEB || 'http://localhost:7170';
const api = process.env.MANUAL_API || 'http://localhost:7171';
// Clave de los usuarios demo de la base local (variable de entorno MANUAL_CLAVE). Nunca aparece en capturas:
// el script la escribe y la tapa antes de fotografiar.
const CLAVE_DEMO = process.env.MANUAL_CLAVE;
if (!CLAVE_DEMO) throw new Error('Defina MANUAL_CLAVE con la clave de los usuarios demo de la base local.');

// Un usuario por perfil (base demo).
const usuarios = [
  { perfil: 'Administrador', correo: 'admin@labodega.cr' },
  { perfil: 'Gerente', correo: 'gerente@labodega.cr' },
  { perfil: 'Supervisor', correo: 'supervisor@labodega.cr' },
  { perfil: 'Auditor Interno', correo: 'auditor@labodega.cr' },
  { perfil: 'Empleado', correo: 'empleado@labodega.cr' },
  { perfil: 'Cajero', correo: 'cajero@labodega.cr' },
  { perfil: 'Bodeguero', correo: 'bodeguero@labodega.cr' },
  { perfil: 'Facturador', correo: 'facturador@labodega.cr' },
  { perfil: 'Crédito y Cobro', correo: 'credito@labodega.cr' },
  { perfil: 'Compras', correo: 'compras@labodega.cr' },
  { perfil: 'Soporte', correo: 'soporte@labodega.cr' },
  { perfil: 'Vendedor', correo: 'vendedor@labodega.cr' },
  { perfil: 'Chofer', correo: 'chofer@labodega.cr' },
  { perfil: 'Cliente', correo: 'cliente@labodega.cr' },
];

// Alias cortos para las especificaciones de capturas: "usuario": "gerente".
const alias = Object.fromEntries(usuarios.map(u => [u.correo.split('@')[0], u.correo]));

function correoDe(usuario) {
  if (!usuario) return null;
  return usuario.includes('@') ? usuario : alias[usuario] || `${usuario}@labodega.cr`;
}

// El sitio acepta 10 inicios de sesión por minuto desde la misma IP: si se pasa, espera y reintenta.
async function login(page, usuario) {
  const correo = correoDe(usuario);
  for (let intento = 1; intento <= 4; intento++) {
    await page.goto(web + '/Account/Login', { waitUntil: 'domcontentloaded' });
    await page.fill('#Email', correo);
    await page.fill('#Password', CLAVE_DEMO);
    const [resp] = await Promise.all([
      page.waitForNavigation({ waitUntil: 'domcontentloaded' }).catch(() => null),
      page.click('form[action*="Login"] button[type=submit]'),
    ]);
    if (resp && resp.status() < 400 && !page.url().includes('/Account/Login')) return;
    if (intento < 4) { console.log(`  (límite de inicios de sesión; espero 30 s para ${correo})`); await page.waitForTimeout(30000); }
  }
  throw new Error(`No se pudo iniciar sesión con ${correo}`);
}

module.exports = { web, api, usuarios, alias, correoDe, login, CLAVE_DEMO };

// Ejecuta SQL en la base local de capturas (LocalDB). Instancia: MANUAL_INSTANCIA (por defecto Trilogia_manual).
// Lo usa el campo "sql" de las especificaciones para dejar los datos en el estado que la captura necesita.
function ejecutarSql(texto) {
  const { execFileSync } = require('child_process');
  const instancia = process.env.MANUAL_INSTANCIA || 'Trilogia_manual';
  const info = execFileSync('SqlLocalDB.exe', ['info', instancia]).toString();
  if (!/Running|En ejecuci/i.test(info)) execFileSync('SqlLocalDB.exe', ['start', instancia]);
  const pipe = execFileSync('SqlLocalDB.exe', ['info', instancia]).toString().match(/(np:\S+)/)[1];
  const sqlcmd = process.env.SQLCMD || 'C:\\Program Files\\SqlCmd\\sqlcmd.exe';
  execFileSync(sqlcmd, ['-S', pipe, '-d', 'DistribuidoraJJ_DB', '-E', '-I', '-b', '-Q', 'SET NOCOUNT ON; ' + texto], { stdio: ['ignore', 'ignore', 'pipe'] });
}
module.exports.ejecutarSql = ejecutarSql;
