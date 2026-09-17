using System.Data;
using Proyecto_FinalAPI.Models;

namespace Proyecto_FinalAPI.Services.Mobile
{
    public interface IManagementMobileDbService
    {
        Task<MobileDashboard> GetDashboardAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<MobileManagedRoute>> GetRoutesAsync(string? status, string? search, CancellationToken cancellationToken = default);
        Task<MobileManagedRouteDetail?> GetRouteAsync(int routeId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<MobileDriverOption>> GetDriversAsync(CancellationToken cancellationToken = default);
        Task<IReadOnlyList<MobileVehicleOption>> GetVehiclesAsync(CancellationToken cancellationToken = default);
        Task<ReassignRouteResponse?> ReassignRouteAsync(int routeId, int driverId, int? vehicleId, string reason, Guid syncGuid, int userId, string userName, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<(int PedidoId, string Cliente, string Correo)>> DispatchRouteAsync(int routeId, int userId, string userName, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Tablero de métricas y gestión de rutas para administración y gerencia.
    /// </summary>
    public sealed class ManagementMobileDbService : MobileDbServiceBase, IManagementMobileDbService
    {
        public ManagementMobileDbService(IConfiguration configuration) : base(configuration) { }

        public async Task<MobileDashboard> GetDashboardAsync(
            DateTime from, DateTime to, CancellationToken cancellationToken = default)
        {
            var dashboard = new MobileDashboard { Desde = from.Date, Hasta = to.Date };

            void Range(Microsoft.Data.SqlClient.SqlParameterCollection p)
            {
                p.Add("@Desde", SqlDbType.Date).Value = from.Date;
                p.Add("@Hasta", SqlDbType.Date).Value = to.Date;
            }

            // Los mismos indicadores que el tablero gerencial del sitio web.
            await ReadAsync("dbo.sp_Reportes_DashboardKpis", Range, async r =>
            {
                if (!await r.ReadAsync(cancellationToken)) return;
                dashboard.VentasPeriodo = r.Dec("VentasPeriodo");
                dashboard.FacturasPeriodo = r.Int("FacturasPeriodo");
                dashboard.PedidosPeriodo = r.Int("PedidosPeriodo");
                dashboard.TicketPromedio = r.Dec("TicketPromedio");
                dashboard.StockBajo = r.Int("StockBajo");
                dashboard.ProductosAgotados = r.Int("ProductosAgotados");
                dashboard.PedidosEnRuta = r.Int("PedidosEnRuta");
                dashboard.CobrosPendientes = r.Dec("CobrosPendientes");
            }, cancellationToken);

            dashboard.SerieVentas = await QueryAsync("dbo.sp_Reportes_DashboardVentasSerie", Range, r => new MobileSalesPoint
            {
                Dia = r.Date("Dia") ?? from.Date,
                Total = r.Dec("Total"),
                Facturas = r.Int("Facturas")
            }, cancellationToken);

            await ReadAsync("dbo.sp_Movil_Gestion_Operacion", Range, async r =>
            {
                while (await r.ReadAsync(cancellationToken))
                {
                    dashboard.PedidosPorEstado.Add(new MobileCount { Etiqueta = r.Str("Estado"), Cantidad = r.Int("Cantidad") });
                }

                if (await r.NextResultAsync(cancellationToken) && await r.ReadAsync(cancellationToken))
                {
                    dashboard.RutasPlanificadas = r.Int("RutasPlanificadas");
                    dashboard.RutasDespachadas = r.Int("RutasDespachadas");
                    dashboard.EntregasPendientes = r.Int("EntregasPendientes");
                    dashboard.EntregasCompletadas = r.Int("EntregasCompletadas");
                    dashboard.EntregasFallidas = r.Int("EntregasFallidas");
                    dashboard.PedidosRetenidos = r.Int("PedidosRetenidos");
                }

                if (await r.NextResultAsync(cancellationToken))
                {
                    while (await r.ReadAsync(cancellationToken))
                    {
                        dashboard.ProductosTop.Add(new MobileTopProduct
                        {
                            ProductoId = r.Int("ProductoId"),
                            Nombre = r.Str("ProductoNombre"),
                            Unidades = r.Int("Unidades"),
                            Monto = r.Dec("Monto")
                        });
                    }
                }

                if (await r.NextResultAsync(cancellationToken))
                {
                    while (await r.ReadAsync(cancellationToken))
                    {
                        dashboard.ExistenciasEnRiesgo.Add(new MobileProductRisk
                        {
                            ProductoId = r.Int("ProductoId"),
                            Nombre = r.Str("Nombre"),
                            Stock = r.Int("Stock"),
                            EstadoStock = r.Str("EstadoStock")
                        });
                    }
                }
            }, cancellationToken);

            return dashboard;
        }

        public async Task<IReadOnlyList<MobileManagedRoute>> GetRoutesAsync(
            string? status, string? search, CancellationToken cancellationToken = default) =>
            (await QueryAsync("dbo.sp_Rutas_List", p =>
            {
                p.Add("@Estado", SqlDbType.NVarChar, 20).Value = DbText(status, 20);
                p.Add("@Buscar", SqlDbType.NVarChar, 150).Value = DbText(search, 150);
            }, r => new MobileManagedRoute
            {
                RutaId = r.Int("RutaId"),
                Codigo = r.Str("Codigo"),
                Zona = r.Str("Zona"),
                Estado = r.Str("Estado"),
                Chofer = r.Str("Chofer"),
                VehiculoPlaca = r.Str("VehiculoPlaca"),
                FechaCreacion = r.Date("FechaCreacion"),
                FechaDespacho = r.Date("FechaDespacho"),
                TotalPedidos = r.Int("TotalPedidos"),
                Entregados = r.Int("Entregados"),
                Fallidos = r.Int("Fallidos"),
                Pendientes = r.Int("Pendientes")
            }, cancellationToken)).Take(100).ToList();

        public async Task<MobileManagedRouteDetail?> GetRouteAsync(int routeId, CancellationToken cancellationToken = default)
        {
            var route = await QuerySingleAsync("dbo.sp_Rutas_GetHeader",
                p => p.Add("@RutaId", SqlDbType.Int).Value = routeId,
                r =>
                {
                    var status = r.Str("Estado");
                    return new MobileManagedRouteDetail
                    {
                        RutaId = r.Int("RutaId"),
                        Codigo = r.Str("Codigo"),
                        Zona = r.Str("Zona"),
                        Estado = status,
                        ChoferUsuarioId = r.Int("ChoferUsuarioId"),
                        Chofer = r.Str("Chofer"),
                        VehiculoId = r.Int("VehiculoId"),
                        VehiculoPlaca = r.Str("VehiculoPlaca"),
                        VehiculoDescripcion = r.Str("VehiculoDescripcion"),
                        Observaciones = r.Str("Observaciones"),
                        FechaCreacion = r.Date("FechaCreacion"),
                        FechaDespacho = r.Date("FechaDespacho"),
                        PuedeReasignar = status is "Planificada" or "Despachada",
                        PuedeDespachar = status == "Planificada"
                    };
                }, cancellationToken);

            if (route is null) return null;

            route.Paradas = await QueryAsync("dbo.sp_Rutas_GetOrders",
                p => p.Add("@RutaId", SqlDbType.Int).Value = routeId,
                r => new MobileManagedStop
                {
                    PedidoId = r.Int("PedidoId"),
                    Secuencia = r.Int("Secuencia"),
                    EstadoEntrega = r.Str("EstadoEntrega"),
                    MotivoFallo = r.Str("MotivoFallo"),
                    Cliente = r.Str("Cliente"),
                    DireccionEntrega = r.Str("DireccionEntrega"),
                    Total = r.Dec("Total")
                }, cancellationToken);

            route.PuedeDespachar = route.PuedeDespachar && route.Paradas.Count > 0;
            return route;
        }

        public async Task<IReadOnlyList<MobileDriverOption>> GetDriversAsync(CancellationToken cancellationToken = default) =>
            await QueryAsync("dbo.sp_Rutas_GetAvailableDrivers", null, r => new MobileDriverOption
            {
                ChoferId = r.Int("UsuarioId"),
                Nombre = r.Str("NombreCompleto"),
                Telefono = r.Str("Telefono"),
                RutasAbiertas = r.Int("RutasAbiertas")
            }, cancellationToken);

        public async Task<IReadOnlyList<MobileVehicleOption>> GetVehiclesAsync(CancellationToken cancellationToken = default) =>
            await QueryAsync("dbo.sp_Rutas_GetAvailableVehicles", null, r => new MobileVehicleOption
            {
                VehiculoId = r.Int("VehiculoId"),
                Placa = r.Str("Placa"),
                Descripcion = r.Str("Descripcion"),
                RutasAbiertas = r.Int("RutasAbiertas")
            }, cancellationToken);

        public Task<ReassignRouteResponse?> ReassignRouteAsync(
            int routeId, int driverId, int? vehicleId, string reason, Guid syncGuid,
            int userId, string userName, CancellationToken cancellationToken = default) =>
            QuerySingleAsync("dbo.sp_Movil_Rutas_Reasignar", p =>
            {
                p.Add("@RutaId", SqlDbType.Int).Value = routeId;
                p.Add("@ChoferUsuarioId", SqlDbType.Int).Value = driverId;
                p.Add("@VehiculoId", SqlDbType.Int).Value = vehicleId is > 0 ? vehicleId.Value : DBNull.Value;
                p.Add("@Motivo", SqlDbType.NVarChar, 200).Value = DbText(reason, 200);
                p.Add("@SyncGuid", SqlDbType.UniqueIdentifier).Value = syncGuid;
                p.Add("@UsuarioId", SqlDbType.Int).Value = userId;
                p.Add("@UsuarioNombre", SqlDbType.NVarChar, 150).Value = ActorName(userName);
            }, r => new ReassignRouteResponse
            {
                RutaId = r.Int("RutaId"),
                Codigo = r.Str("Codigo"),
                Chofer = r.Str("Chofer"),
                ChoferAnterior = r.Str("ChoferAnterior"),
                VehiculoPlaca = r.Str("VehiculoPlaca"),
                Duplicado = r.Bool("Duplicado")
            }, cancellationToken);

        public async Task<IReadOnlyList<(int PedidoId, string Cliente, string Correo)>> DispatchRouteAsync(
            int routeId, int userId, string userName, CancellationToken cancellationToken = default) =>
            await QueryAsync("dbo.sp_Rutas_Dispatch", p =>
            {
                p.Add("@RutaId", SqlDbType.Int).Value = routeId;
                p.Add("@UsuarioId", SqlDbType.Int).Value = userId;
                p.Add("@UsuarioNombre", SqlDbType.NVarChar, 150).Value = ActorName(userName);
            }, r => (r.Int("PedidoId"), r.Str("ClienteNombre"), r.Str("ClienteCorreo")), cancellationToken);
    }
}
