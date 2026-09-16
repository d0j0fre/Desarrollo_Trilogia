using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.SqlClient;
using Proyecto_FinalAPI.Authorization;
using Proyecto_FinalAPI.Models;
using Proyecto_FinalAPI.Services;

namespace Proyecto_FinalAPI.Controllers
{
    /// <summary>
    /// Qué puede hacer quien está usando la aplicación.
    ///
    /// La app no lleva cableado "si el rol es Chofer, muestro estas tarjetas":
    /// le pregunta al servidor y dibuja lo que reciba. El día que a un chofer le
    /// asignen además un permiso de bodega, su pantalla cambia sin publicar una
    /// versión nueva del APK — que es justo lo que no se puede hacer rápido
    /// cuando la distribución es por archivo y no por tienda.
    /// </summary>
    [Route("api/mobile/v1/me")]
    [Authorize]
    [RequirePermission(MobilePermissions.Access)]
    [EnableRateLimiting("mobile-read")]
    public sealed class MobileMeController : MobileControllerBase
    {
        private readonly IMobileAuthDbService _authDb;
        private readonly ILogger<MobileMeController> _logger;

        public MobileMeController(IMobileAuthDbService authDb, ILogger<MobileMeController> logger)
        {
            _authDb = authDb;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Me(CancellationToken cancellationToken) =>
            await BuildCapabilitiesAsync(cancellationToken);

        [HttpGet("capabilities")]
        public async Task<IActionResult> Capabilities(CancellationToken cancellationToken) =>
            await BuildCapabilitiesAsync(cancellationToken);

        private async Task<IActionResult> BuildCapabilitiesAsync(CancellationToken cancellationToken)
        {
            IReadOnlyList<string> permissions;
            try
            {
                // Se releen de la base, no se toman del token: si a alguien le
                // revocaron un permiso hace un minuto, su pantalla lo refleja.
                permissions = await _authDb.GetPermissionsForRoleAsync(CurrentRole, cancellationToken);
            }
            catch (SqlException exception)
            {
                _logger.LogError(exception, "No se pudieron leer los permisos del rol {Rol}.", CurrentRole);
                return Unavailable();
            }

            return Ok(new MobileCapabilitiesResponse
            {
                UserId = CurrentUserId,
                FullName = CurrentUserName,
                Email = CurrentUserEmail,
                Role = CurrentRole,
                Permissions = permissions,
                Modules = MobileModuleCatalog.For(CurrentRole, permissions)
            });
        }
    }

    /// <summary>
    /// Catálogo de módulos de la aplicación. Vive en un solo lugar para que la
    /// pantalla de inicio y los permisos no se puedan contradecir.
    ///
    /// Cada módulo se habilita por el permiso que exige su endpoint, no por el
    /// nombre del rol: si administración le da a un vendedor el permiso de
    /// inventario, la tarjeta aparece sin publicar otra versión. La única
    /// excepción es la operación del chofer, que depende de tener rutas propias
    /// y por eso sigue atada al perfil Chofer.
    /// </summary>
    public static class MobileModuleCatalog
    {
        public const string SectionField = "Operación en ruta";
        public const string SectionWarehouse = "Bodega";
        public const string SectionManagement = "Gestión";
        public const string SectionSales = "Ventas";
        public const string SectionOffice = "Oficina";
        public const string SectionPeople = "Personal";

        private sealed record Definition(
            string Key,
            string Title,
            string Description,
            string Icon,
            string Section,
            Func<bool, IReadOnlyCollection<string>, bool> Enabled);

        private static bool Has(bool isAdmin, IReadOnlyCollection<string> permissions, string code) =>
            isAdmin || permissions.Contains(code, StringComparer.OrdinalIgnoreCase);

        private static readonly Definition[] Definitions =
        [
            // ── Chofer ─────────────────────────────────────────────────────
            new("driver.routes", "Mis rutas", "Paradas, navegación y estado de cada entrega", "route", SectionField,
                (_, _) => false),
            new("driver.mileage", "Kilometraje", "Iniciá o cerrá la jornada del vehículo", "speedometer", SectionField,
                (_, _) => false),
            new("driver.summary", "Por enviar", "Acciones guardadas que faltan por enviar", "clipboard", SectionField,
                (_, _) => false),

            // ── Bodega ─────────────────────────────────────────────────────
            new("warehouse.picking", "Pedidos por preparar", "Alistá los pedidos y marcá los que quedan listos", "picking", SectionWarehouse,
                (admin, p) => Has(admin, p, MobilePermissions.PrepareOrders)),
            new("warehouse.receiving", "Recepción de compras", "Recibí la mercadería de las órdenes de compra", "receiving", SectionWarehouse,
                (admin, p) => Has(admin, p, MobilePermissions.ReceivePurchases)),
            new("warehouse.movements", "Movimientos de inventario", "Registrá entradas, salidas y ajustes", "movements", SectionWarehouse,
                (admin, p) => Has(admin, p, MobilePermissions.InventoryMovements)),
            new("warehouse.stock", "Consulta de inventario", "Existencias, alertas de stock e historial", "inventory", SectionWarehouse,
                (admin, p) => Has(admin, p, MobilePermissions.InventoryView)),

            // ── Gestión ────────────────────────────────────────────────────
            new("management.dashboard", "Métricas del negocio", "Ventas, pedidos, entregas y existencias", "chart", SectionManagement,
                (admin, p) => Has(admin, p, MobilePermissions.Dashboard)),
            new("management.approvals", "Pedidos retenidos", "Aprobá o rechazá pedidos que superan el umbral", "approval", SectionManagement,
                (admin, p) => Has(admin, p, MobilePermissions.AuthorizeOrders)),
            new("management.routes", "Rutas y choferes", "Seguimiento, despacho y reasignación de rutas", "fleet", SectionManagement,
                (admin, p) => Has(admin, p, MobilePermissions.ManageRoutes)),
            // Bodega consulta pedidos desde su lista de preparación: repetirlos
            // aquí le duplicaría la pantalla de inicio.
            new("management.orders", "Pedidos", "Consulta de pedidos y cambios de estado", "orders", SectionManagement,
                (admin, p) => admin || (Has(admin, p, MobilePermissions.OrdersView) && !Has(admin, p, MobilePermissions.PrepareOrders))),
            new("management.products", "Productos", "Activá o inactivá productos del catálogo", "product", SectionManagement,
                (admin, p) => Has(admin, p, MobilePermissions.ProductsEdit)),

            // ── Ventas ─────────────────────────────────────────────────────
            new("sales.orders", "Ventas en campo", "Tomá pedidos de clientes y seguí su estado", "cart", SectionSales,
                (admin, p) => Has(admin, p, MobilePermissions.SellerOrders)),

            // ── Oficina ────────────────────────────────────────────────────
            new("office.settlements", "Liquidación de rutas", "Cobros esperados y recibidos por ruta", "cash", SectionOffice,
                (admin, p) => Has(admin, p, MobilePermissions.Settlements)),
            new("office.invoices", "Facturas", "Facturas emitidas y su detalle", "receipt", SectionOffice,
                (admin, p) => Has(admin, p, MobilePermissions.InvoicesView)),
            new("office.credit", "Crédito de clientes", "Límites, deuda y movimientos por cliente", "credit", SectionOffice,
                (admin, p) => Has(admin, p, MobilePermissions.CreditView)),
            // Igual con compras: quien recibe mercadería ya ve las órdenes en su recepción.
            new("office.purchasing", "Compras", "Órdenes de compra y sugerencias de reposición", "purchasing", SectionOffice,
                (admin, p) => admin || (Has(admin, p, MobilePermissions.PurchaseOrdersView) && !Has(admin, p, MobilePermissions.ReceivePurchases))),
            new("office.support", "Consultas de clientes", "Atendé las consultas recibidas", "support", SectionOffice,
                (admin, p) => Has(admin, p, MobilePermissions.ConsultationsView)),
            new("office.audit", "Bitácora", "Acciones registradas en el sistema", "audit", SectionOffice,
                (admin, p) => Has(admin, p, MobilePermissions.AuditView)),

            // ── Personal ───────────────────────────────────────────────────
            new("staff.approvals", "Aprobar jornadas", "Revisá las jornadas enviadas por el personal", "approve-hours", SectionPeople,
                (admin, p) => Has(admin, p, MobilePermissions.AttendanceApprove)),
            new("staff.attendance", "Mis jornadas", "Registrá tus horas y seguí su aprobación", "clock", SectionPeople,
                (admin, p) => Has(admin, p, MobilePermissions.AttendanceRegister)),
        ];

        public static IReadOnlyList<MobileModule> For(string role, IReadOnlyList<string> permissions)
        {
            var isDriver = string.Equals(role, "Chofer", StringComparison.OrdinalIgnoreCase);
            var isAdmin = string.Equals(role, "Administrador", StringComparison.OrdinalIgnoreCase);

            return Definitions.Select(definition =>
            {
                var enabled = definition.Key switch
                {
                    // Las rutas del chofer son las suyas: un administrador no
                    // tiene rutas propias y ve la gestión de todas en su lugar.
                    "driver.routes" or "driver.summary" => isDriver,
                    "driver.mileage" => isDriver && permissions.Contains(MobilePermissions.OwnMileage, StringComparer.OrdinalIgnoreCase),
                    _ => definition.Enabled(isAdmin, permissions)
                };

                return new MobileModule
                {
                    Key = definition.Key,
                    Title = definition.Title,
                    Description = definition.Description,
                    Icon = definition.Icon,
                    Section = definition.Section,
                    Enabled = enabled
                };
            }).ToList();
        }
    }

    public static class MobilePermissions
    {
        public const string Access = "MOVIL_ACCESO";
        public const string OwnMileage = "FLOTA_KILOMETRAJE_PROPIO";

        public const string InventoryView = "INVENTARIO_VER";
        public const string InventoryMovements = "INVENTARIO_MOVIMIENTOS";
        public const string ProductsEdit = "INVENTARIO_EDITAR";
        public const string PurchaseOrdersView = "COMPRAS_ORDENES_VER";
        public const string ReceivePurchases = "COMPRAS_ORDENES_RECIBIR";
        public const string PurchaseSuggestions = "COMPRAS_SUGERENCIAS_VER";

        public const string OrdersView = "PEDIDOS_VER";
        public const string OrdersChangeStatus = "PEDIDOS_CAMBIAR_ESTADO";
        public const string PrepareOrders = "PEDIDOS_PREPARAR";
        public const string AuthorizeOrders = "PEDIDOS_AUTORIZAR_RECHAZAR";
        public const string SellerOrders = "VENTA_MOVIL_CREAR_PEDIDO";

        public const string Dashboard = "REPORTES_DASHBOARD";
        public const string ManageRoutes = "RUTAS_GESTIONAR";

        public const string AttendanceRegister = "RRHH_JORNADAS_REGISTRAR";
        public const string AttendanceOwn = "RRHH_JORNADAS_VER_PROPIAS";
        public const string AttendanceApprove = "RRHH_JORNADAS_APROBAR";

        public const string Settlements = "LIQUIDACION_FINANCIERA";
        public const string InvoicesView = "FACTURACION_VER";
        public const string CreditView = "CREDITOS_VER";
        public const string ConsultationsView = "CONSULTAS_VER";
        public const string ConsultationsAttend = "CONSULTAS_ATENDER";
        public const string AuditView = "AUDITORIA_VER";
    }
}
