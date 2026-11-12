// Sondeo de acceso por perfil: inicia sesión con cada usuario demo, lee el menú
// visible y prueba cada módulo web. Resultado: herramientas/salida/sondeo_perfiles.json
// Uso: node herramientas/sondeo_perfiles.js
const { chromium } = require('playwright-core');
const fs = require('fs');
const path = require('path');
const cfg = require('./config');

const MODULOS = [
  ['Dashboard administrativo', '/Admin/Index'],
  ['Inventario', '/Inventory/Index'], ['Movimientos de inventario', '/Inventory/Movements'],
  ['Registrar movimiento', '/Inventory/RegisterMovement'],
  ['Transformar producto', '/Inventory/TransformStock'], ['Combos', '/Combos/Index'],
  ['Devoluciones', '/Returns/Index'], ['Cuarentena', '/Returns/Quarantine'], ['Activos', '/Assets/Index'],
  ['Pedidos', '/OrdersAdmin/Index'], ['Facturación', '/Billing/Index'],
  ['Cuentas por cobrar', '/AccountsReceivableAdmin/Index'], ['Promociones', '/Promotions/Index'],
  ['Solicitudes de garantía', '/WarrantyRequestsAdmin/Index'],
  ['Rutas y entregas', '/RoutesAdmin/Index'], ['Tablero de entregas', '/DeliveryBoard/Index'],
  ['Vehículos', '/Vehicles/Index'], ['Kilometraje', '/Fleet/Mileage'], ['Mantenimiento', '/Fleet/Maintenance'],
  ['Alertas de flota', '/Fleet/Alerts'], ['Comodatos', '/Comodatos/Index'], ['Aplicación móvil', '/MobileApp/Index'],
  ['Liquidación de cobros', '/Finance/Index'], ['Presupuestos anuales', '/Budgets/Index'],
  ['Gastos operativos', '/Expenses/Index'], ['Presupuesto vs. real', '/BudgetComparison/Index'],
  ['Ingresos y egresos', '/FinancialReportAdmin/Index'], ['Proveedores', '/Suppliers/Index'],
  ['Órdenes de compra', '/PurchaseOrders/Index'], ['Histórico de precios', '/PriceHistory/Index'],
  ['Tablero gerencial', '/ManagementDashboard/Index'], ['Reporte de ventas', '/SalesReports/Index'],
  ['Desempeño de vendedores', '/SellerPerformance/Index'], ['Metas de ventas', '/Kpis/Index'],
  ['Reporte de KPIs', '/Kpis/Report'], ['Inteligencia de inventario', '/InventoryIntelligence/Index'],
  ['Clientes', '/Clients/Index'], ['Créditos', '/Credits/Index'], ['Reclamos', '/Reclamos/Index'],
  ['Consultas', '/Consultations/Index'], ['Empleados', '/Employees/Index'],
  ['Solicitudes (licencias)', '/Employees/LeaveRequests'], ['Aprobar jornadas', '/Attendance/Index'],
  ['Planilla', '/Payroll/Index'], ['Boletas de pago', '/PaySlips/Index'], ['Roles', '/Roles/Index'],
  ['Permisos', '/Permissions/Index'], ['Documentos digitales', '/Documents/Index'],
  ['Alertas documentales', '/Documents/Alerts'], ['Auditoría', '/Audit/Index'],
  ['Administrar departamentos de chat', '/Chat/Index'], ['Pedidos retenidos', '/ManagerOrders/Index'],
  ['Venta móvil', '/SellerOrders/Index'], ['Mi meta', '/MyGoal/Index'], ['Asistente', '/Assistant/Index'],
  ['Mis entregas', '/DriverDeliveries/Index'], ['Mi portal', '/EmployeePortal/Index'],
  ['Mis jornadas', '/MyAttendance/Index'], ['Mis boletas', '/MyPaySlips/Index'],
  ['Notificaciones', '/Notifications/Index'], ['Mi perfil', '/Profile/Edit'],
  ['Mis pedidos (cliente)', '/ClientPortal/Index'], ['Mis garantías (cliente)', '/ClientPortal/Warranties'],
  ['Estado de cuenta (cliente)', '/ClientPortal/Statement'], ['Tienda', '/Home/Shop'], ['Carrito', '/Cart/Index'],
];

(async () => {
  const browser = await chromium.launch({ channel: 'chrome', headless: true });
  const salida = {};
  for (const u of cfg.usuarios) {
    const ctx = await browser.newContext();
    const page = await ctx.newPage();
    await cfg.login(page, u.correo);
    // Menú visible (incluye submenús desplegables, aunque estén cerrados).
    const menu = await page.$$eval('.main-nav-2027 a, .main-nav-2027 button', els => els
      .map(e => ({ texto: e.textContent.replace(/\s+/g, ' ').trim(), href: e.getAttribute('href') || '' }))
      .filter(e => e.texto));
    const acceso = {};
    for (const [nombre, ruta] of MODULOS) {
      const r = await page.goto(cfg.web + ruta, { waitUntil: 'domcontentloaded' }).catch(() => null);
      const st = r ? r.status() : 0;
      const url = page.url();
      acceso[nombre] = url.includes('/Account/Login') ? 'login' : st === 403 ? 'denegado' : st >= 400 || st === 0 ? `error ${st}` : 'si';
    }
    salida[u.perfil] = { correo: u.correo, menu, acceso };
    console.log(u.perfil, Object.values(acceso).filter(v => v === 'si').length, 'módulos con acceso');
    await ctx.close();
  }
  await browser.close();
  fs.mkdirSync(path.join(__dirname, 'salida'), { recursive: true });
  fs.writeFileSync(path.join(__dirname, 'salida', 'sondeo_perfiles.json'), JSON.stringify(salida, null, 2));
})();
