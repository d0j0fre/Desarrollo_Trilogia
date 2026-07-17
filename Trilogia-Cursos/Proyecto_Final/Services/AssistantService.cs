using Microsoft.Data.SqlClient;
using Proyecto_Final.Models.Admin;
using System.Data;
using System.Text;

namespace Proyecto_Final.Services
{
    // CU-261 / CU-263 — Asistente virtual basado en reglas (sin IA externa).
    // Interpreta la intención por palabras clave y reutiliza los KPIs de CU-131.
    public class AssistantService
    {
        private readonly string _connectionString;
        private readonly ReportsDbService _reports;

        public AssistantService(IConfiguration configuration, ReportsDbService reports)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("No se encontró la cadena de conexión DefaultConnection.");
            _reports = reports;
        }

        private static readonly string[] DetalleWords = { "detalle", "mas", "más", "amplia", "amplía", "ampliar", "profundiza" };

        // Base de conocimiento de ayuda por módulo (CU-263 E1).
        private static readonly Dictionary<string, (string Titulo, string Texto)> Ayuda =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["inventario"] = ("Módulo de Inventario", "Ve a Operación → Inventario para ver productos y stock. Usa 'Nuevo producto' para crear, el lápiz para editar y 'Movimientos' para registrar entradas, salidas o ajustes de stock."),
                ["pedidos"] = ("Módulo de Pedidos", "En Operación → Pedidos revisas los pedidos por estado. Puedes cambiar el estado, retener o facturar un pedido desde su detalle."),
                ["facturacion"] = ("Módulo de Facturación", "En Operación → Facturación generas la factura de un pedido confirmado. El inventario ya se descontó en el checkout, no se vuelve a descontar al facturar."),
                ["clientes"] = ("Módulo de Clientes", "En Clientes → Clientes administras la información de los clientes. Desde Créditos gestionas su línea de crédito y movimientos."),
                ["creditos"] = ("Módulo de Créditos", "En Clientes → Créditos defines el límite de crédito, bloqueas o registras cargos y abonos de cada cliente."),
                ["rutas"] = ("Módulo de Rutas y Entregas", "En Operación → Rutas creas una ruta con chofer y vehículo, agregas pedidos, la secuencias automáticamente, la despachas y al final del día la liquidas."),
                ["vehiculos"] = ("Módulo de Vehículos", "En Operación → Vehículos registras la flota (placa, marca, capacidad). Desde Kilometraje y Mantenimiento controlas el desgaste y los gastos del taller."),
                ["mantenimiento"] = ("Mantenimiento de flota", "En Operación → Mantenimiento registras órdenes preventivas o correctivas. En Alertas de flota ves los vencimientos y mantenimientos próximos."),
                ["activos"] = ("Módulo de Activos", "En Operación → Activos registras neveras y exhibidores con su código de activo y controlas su estado (disponible, prestado, etc.)."),
                ["devoluciones"] = ("Devoluciones", "En Operación → Devoluciones registras la mercadería devuelta. Todo producto devuelto pasa a cuarentena antes de reintegrarse al inventario."),
                ["cuarentena"] = ("Cuarentena", "En Operación → Cuarentena liberas (reintegras al stock) o descartas los productos devueltos."),
                ["empleados"] = ("Módulo de Empleados", "En Personal → Empleados administras el personal, asignas tareas y apruebas solicitudes de licencia."),
                ["roles"] = ("Roles y permisos", "En Seguridad → Roles defines perfiles y en Permisos asignas los permisos granulares a cada rol."),
            };

        public async Task<AssistantAnswerViewModel> AskAsync(string pregunta, string? rol, string? contextoIntent, int usuarioId, string usuarioNombre)
        {
            var norm = Normalizar(pregunta);
            var esGerencial = string.Equals(rol, "Administrador", StringComparison.OrdinalIgnoreCase)
                           || string.Equals(rol, "Gerente", StringComparison.OrdinalIgnoreCase);
            var pideDetalle = DetalleWords.Any(w => norm.Contains(w));

            AssistantAnswerViewModel respuesta;

            // CU-261 E3 — ampliar sobre una métrica ya mostrada
            if (pideDetalle && !string.IsNullOrWhiteSpace(contextoIntent) && esGerencial)
            {
                respuesta = await ResponderMetricaAsync(contextoIntent!, true);
            }
            else
            {
                var intentMetrica = DetectarMetrica(norm);
                var intentAyuda = DetectarAyuda(norm);

                if (intentMetrica != null)
                {
                    respuesta = esGerencial
                        ? await ResponderMetricaAsync(intentMetrica, pideDetalle)
                        : NoAutorizado();
                }
                else if (intentAyuda != null)
                {
                    respuesta = ResponderAyuda(intentAyuda);   // CU-263 E1
                }
                else
                {
                    respuesta = NoInterpretado();               // CU-261 E2 / CU-263 E2
                }
            }

            // CU-263 E3 — registrar la consulta
            await LogAsync(usuarioId, usuarioNombre, pregunta, respuesta.Intent, respuesta.Tipo, respuesta.Interpretado);
            return respuesta;
        }

        private async Task<AssistantAnswerViewModel> ResponderMetricaAsync(string intent, bool detalle)
        {
            var hasta = DateTime.Today;
            var desde = new DateTime(hasta.Year, hasta.Month, 1);
            var kpi = await _reports.GetDashboardAsync(desde, hasta, "Mes actual");

            var r = new AssistantAnswerViewModel { Tipo = "metrica", Intent = intent, Interpretado = true };
            var periodo = $"{desde:dd/MM/yyyy} al {hasta:dd/MM/yyyy}";

            switch (intent)
            {
                case "ventas":
                    r.Titulo = "Ventas del mes";
                    r.Respuesta = $"Las ventas del período ({periodo}) suman <strong>₡{kpi.VentasPeriodo:N2}</strong> en {kpi.FacturasPeriodo} factura(s).";
                    if (detalle) r.Respuesta += $" El ticket promedio es de ₡{kpi.TicketPromedio:N2} y se registraron {kpi.PedidosPeriodo} pedido(s).";
                    break;
                case "pedidos":
                    r.Titulo = "Pedidos del mes";
                    r.Respuesta = $"Se registraron <strong>{kpi.PedidosPeriodo}</strong> pedido(s) en el período ({periodo}).";
                    if (detalle) r.Respuesta += $" De ellos, {kpi.PedidosEnRuta} está(n) actualmente en ruta.";
                    break;
                case "facturas":
                    r.Titulo = "Facturación del mes";
                    r.Respuesta = $"Se emitieron <strong>{kpi.FacturasPeriodo}</strong> factura(s) por ₡{kpi.VentasPeriodo:N2}.";
                    break;
                case "ticket":
                    r.Titulo = "Ticket promedio";
                    r.Respuesta = $"El ticket promedio del período es <strong>₡{kpi.TicketPromedio:N2}</strong>.";
                    break;
                case "stock":
                    r.Titulo = "Estado del inventario";
                    r.Respuesta = $"Hay <strong>{kpi.StockBajo}</strong> producto(s) con stock bajo y <strong>{kpi.ProductosAgotados}</strong> agotado(s).";
                    break;
                case "cobros":
                    r.Titulo = "Cobros pendientes";
                    r.Respuesta = $"El saldo por cobrar asciende a <strong>₡{kpi.CobrosPendientes:N2}</strong>.";
                    break;
                case "rutas":
                    r.Titulo = "Entregas en ruta";
                    r.Respuesta = $"Actualmente hay <strong>{kpi.PedidosEnRuta}</strong> pedido(s) en ruta.";
                    break;
                default:
                    return NoInterpretado();
            }

            if (!detalle)
                r.Sugerencias.Add("¿Quieres más detalle?");
            r.Sugerencias.Add("Ver ventas");
            r.Sugerencias.Add("Ver cobros pendientes");
            r.Sugerencias.Add("Ver estado del stock");
            return r;
        }

        private AssistantAnswerViewModel ResponderAyuda(string modulo)
        {
            var info = Ayuda[modulo];
            return new AssistantAnswerViewModel
            {
                Tipo = "ayuda",
                Intent = $"ayuda:{modulo}",
                Interpretado = true,
                Titulo = info.Titulo,
                Respuesta = info.Texto,
                Sugerencias = { "¿Cómo uso Inventario?", "¿Cómo creo una ruta?", "¿Cómo facturo un pedido?" }
            };
        }

        private static AssistantAnswerViewModel NoInterpretado() => new()
        {
            Tipo = "no_interpretado",
            Intent = "desconocido",
            Interpretado = false,
            Titulo = "No entendí la solicitud",
            Respuesta = "No pude interpretar tu pregunta. Intenta reformularla; por ejemplo: “¿cuánto vendimos este mes?” o “¿cómo uso el módulo de rutas?”. Si el tema es de soporte técnico, contacta a soporte.",
            Sugerencias = { "¿Cuánto vendimos este mes?", "¿Cuántos pedidos en ruta hay?", "¿Cómo uso el inventario?" }
        };

        private static AssistantAnswerViewModel NoAutorizado() => new()
        {
            Tipo = "ayuda",
            Intent = "no_autorizado",
            Interpretado = true,
            Titulo = "Métrica no disponible para tu rol",
            Respuesta = "Las métricas del negocio están disponibles solo para gerencia/administración. Puedo ayudarte con el uso de los módulos.",
            Sugerencias = { "¿Cómo uso el inventario?", "¿Cómo creo una ruta?" }
        };

        private static string? DetectarMetrica(string norm)
        {
            if (Contiene(norm, "venta", "vendimos", "vendido", "ingreso")) return "ventas";
            if (Contiene(norm, "factura", "facturacion", "facturamos")) return "facturas";
            if (Contiene(norm, "ticket", "promedio")) return "ticket";
            if (Contiene(norm, "cobro", "cobrar", "pendiente de pago", "cuentas por cobrar", "saldo")) return "cobros";
            if (Contiene(norm, "en ruta", "entrega", "reparto")) return "rutas";
            if (Contiene(norm, "stock", "inventario", "agotado", "existencia")) return "stock";
            if (Contiene(norm, "pedido", "orden")) return "pedidos";
            return null;
        }

        private static string? DetectarAyuda(string norm)
        {
            // Debe sonar a "cómo/ayuda/usar" + un módulo
            var pideAyuda = Contiene(norm, "como", "ayuda", "usar", "uso", "funciona", "hago");
            if (!pideAyuda) return null;
            foreach (var clave in Ayuda.Keys)
            {
                if (norm.Contains(clave)) return clave;
            }
            if (norm.Contains("factura")) return "facturacion";
            if (norm.Contains("vehiculo")) return "vehiculos";
            return null;
        }

        private static bool Contiene(string norm, params string[] terminos) => terminos.Any(t => norm.Contains(t));

        private async Task LogAsync(int usuarioId, string usuarioNombre, string pregunta, string intent, string tipo, bool interpretado)
        {
            try
            {
                await using var connection = new SqlConnection(_connectionString);
                await using var command = new SqlCommand("dbo.sp_Asistente_Log", connection) { CommandType = CommandType.StoredProcedure };
                command.Parameters.Add("@UsuarioId", SqlDbType.Int).Value = usuarioId > 0 ? usuarioId : DBNull.Value;
                command.Parameters.Add("@UsuarioNombre", SqlDbType.NVarChar, 150).Value = string.IsNullOrWhiteSpace(usuarioNombre) ? DBNull.Value : usuarioNombre;
                command.Parameters.Add("@Pregunta", SqlDbType.NVarChar, 500).Value = pregunta.Length > 500 ? pregunta[..500] : pregunta;
                command.Parameters.Add("@IntentDetectado", SqlDbType.NVarChar, 60).Value = string.IsNullOrWhiteSpace(intent) ? DBNull.Value : intent;
                command.Parameters.Add("@Modulo", SqlDbType.NVarChar, 60).Value = tipo == "ayuda" ? intent.Replace("ayuda:", "") : DBNull.Value;
                command.Parameters.Add("@Interpretado", SqlDbType.Bit).Value = interpretado;
                await connection.OpenAsync();
                await command.ExecuteNonQueryAsync();
            }
            catch
            {
                // La bitácora es best-effort: nunca debe romper la respuesta al usuario.
            }
        }

        private static string Normalizar(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return string.Empty;
            var sb = new StringBuilder(texto.Trim().ToLowerInvariant());
            sb.Replace("á", "a").Replace("é", "e").Replace("í", "i").Replace("ó", "o").Replace("ú", "u").Replace("ñ", "n");
            return sb.ToString();
        }
    }
}
