using System.Data;
using System.Security.Cryptography;
using JJ.Commerce;
using Microsoft.Data.SqlClient;
using Proyecto_Final.Models.Admin;

namespace Proyecto_Final.Services;

public interface ICommercialOrderService
{
    bool Enabled { get; }
    Task<ServerQuoteResult> ReviewAsync(SellerOrderCreateViewModel draft, CancellationToken ct);
    Task<SellerOrderResultViewModel> CreateAsync(SellerOrderCreateViewModel draft, bool offline, CancellationToken ct);
    Task<List<SellerOrderClientViewModel>> ClientsAsync(CancellationToken ct);
    Task<List<SellerMyOrderViewModel>> HistoryAsync(CancellationToken ct);
    Task<(OrderDetailViewModel Order, long Revision)?> DetailsAsync(int order, bool supervisor, CancellationToken ct);
    Task<List<RetainedOrderListItemViewModel>> RetainedAsync(string? search, CancellationToken ct);
    Task<ManagerApproveResultViewModel> DecideAsync(int order, Guid operation, long revision, bool approve, string? reason, CancellationToken ct);
}

public sealed class CommercialOrderService(IConfiguration configuration, IHttpContextAccessor accessor,
    ICommercialQuoteClient prices) : ICommercialOrderService
{
    public bool Enabled => configuration.GetValue<bool>("Commerce:Enabled") && configuration.GetValue<bool>("Commerce:OrdersEnabled");
    private OriginalLoginContext Login()
    {
        if (!Enabled) throw new CommercialAssignmentsUnavailableException();
        return OriginalLoginSession.ValidatedRequest(accessor.HttpContext ?? throw new UnauthorizedAccessException());
    }
    private SqlConnection Connection()
    {
        _ = Login();
        return new(configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Falta la identidad APP restringida."));
    }
    private SqlCommand Command(SqlConnection connection, string operation)
    {
        var command = OriginalLoginService.ContextCommand(connection, "CommercialOrders." + operation, Login());
        command.CommandTimeout = 10;
        return command;
    }
    private static SellerQuoteIntent Intent(SellerOrderCreateViewModel draft, bool offline)
    {
        if (draft.RequestId == Guid.Empty || draft.Productos is null) throw new ArgumentException("Falta el intento original.");
        return new SellerQuoteIntent(draft.RequestId, draft.ClienteUsuarioId, draft.TipoEntrega,
            draft.DireccionEntrega ?? "", draft.Observaciones, draft.IdentificacionCliente,
            offline ? "Venta móvil offline" : "Venta móvil",
            draft.Productos.Where(x => x.Cantidad > 0).Select(x => new OrderQuantity(x.ProductoId, x.Cantidad)).ToArray()).Normalize();
    }
    public Task<ServerQuoteResult> ReviewAsync(SellerOrderCreateViewModel draft, CancellationToken ct)
    {
        _ = Login();
        return prices.IssueAsync(Intent(draft, false), ct);
    }
    public async Task<SellerOrderResultViewModel> CreateAsync(SellerOrderCreateViewModel draft, bool offline, CancellationToken ct)
    {
        _ = Login();
        var intent = Intent(draft, offline);
        if (!offline && (!Hash(draft.QuoteFingerprint) || !Hash(draft.QuoteSourceHash)))
            throw new ArgumentException("Revisá el importe final antes de confirmar.");
        var quote = await prices.IssueAsync(intent, ct);
        if (!offline && (!SameHash(draft.QuoteFingerprint!, quote.IntentFingerprint) || !SameHash(draft.QuoteSourceHash!, quote.SourceHash)))
            throw new CommercialQuoteTransportException(409);
        await using var connection = Connection();
        using var command = Command(connection, "Consume");
        command.Parameters.Add("@RequestId", SqlDbType.UniqueIdentifier).Value = quote.RequestId;
        command.Parameters.Add("@Fingerprint", SqlDbType.Binary, 32).Value = Convert.FromHexString(quote.IntentFingerprint);
        await connection.OpenAsync(ct);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) throw new InvalidOperationException("Falta el resultado del pedido.");
        var result = SellerOrderResultReader.Read(reader);
        await Complete(reader, ct);
        return result;
    }
    public async Task<List<SellerOrderClientViewModel>> ClientsAsync(CancellationToken ct)
    {
        await using var connection = Connection(); using var command = Command(connection, "Clients");
        await connection.OpenAsync(ct); await using var reader = await command.ExecuteReaderAsync(ct);
        var rows = new List<SellerOrderClientViewModel>();
        while (await reader.ReadAsync(ct)) rows.Add(new() { UsuarioId = reader.GetInt32(0), NombreCompleto = reader.GetString(1),
            Correo = reader.GetString(2), Telefono = reader.GetString(3), Direccion = reader.GetString(4) });
        await Complete(reader, ct); return rows;
    }
    public async Task<List<SellerMyOrderViewModel>> HistoryAsync(CancellationToken ct)
    {
        await using var connection = Connection(); using var command = Command(connection, "HistoryV2");
        await connection.OpenAsync(ct); await using var reader = await command.ExecuteReaderAsync(ct);
        var rows = new List<SellerMyOrderViewModel>();
        while (await reader.ReadAsync(ct)) rows.Add(new() { PedidoId = reader.GetInt32(0), Cliente = reader.GetString(1),
            FechaPedido = reader.GetDateTime(2), Estado = reader.GetString(3), Total = reader.GetDecimal(4),
            MotivoRechazo = reader.GetString(5), NumeroFactura = reader.GetString(6), FechaActualizacion = reader.IsDBNull(7) ? null : reader.GetDateTime(7) });
        await Complete(reader, ct); return rows;
    }
    public async Task<(OrderDetailViewModel Order, long Revision)?> DetailsAsync(int order, bool supervisor, CancellationToken ct)
    {
        await using var connection = Connection(); using var command = Command(connection, "DetailsV2");
        command.Parameters.Add("@PedidoId", SqlDbType.Int).Value = order;
        command.Parameters.Add("@Scope", SqlDbType.VarChar, 20).Value = supervisor ? "Supervisor" : "Seller";
        await connection.OpenAsync(ct); await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        var model = new OrderDetailViewModel { PedidoId = reader.GetInt32(0), UsuarioId = reader.GetInt32(1), Cliente = reader.GetString(2),
            Correo = reader.GetString(3), FechaPedido = reader.GetDateTime(4), Estado = reader.GetString(5),
            TipoEntrega = Text(reader, 6), DireccionEntrega = Text(reader, 7), Observaciones = Text(reader, 8), Total = reader.GetDecimal(9),
            MetodoPago = reader.GetString(10), EstadoPago = reader.GetString(11), ReferenciaPago = Text(reader, 12), FechaPago = reader.IsDBNull(13) ? null : reader.GetDateTime(13),
            MotivoRechazo = Text(reader, 14), InventoryDebited = reader.GetBoolean(15), FacturaId = reader.IsDBNull(16) ? null : reader.GetInt32(16),
            HasInvoice = !reader.IsDBNull(16), NumeroFactura = reader.GetString(17) };
        long revision = reader.IsDBNull(18) ? 0 : reader.GetInt64(18);
        if (!await reader.NextResultAsync(ct)) throw new InvalidOperationException("Faltan líneas del pedido.");
        while (await reader.ReadAsync(ct)) model.Detalles.Add(new() { PedidoDetalleId = reader.GetInt32(0), PedidoId = order,
            Producto = reader.GetString(1), ProductoId = reader.GetInt32(2), Cantidad = reader.GetInt32(3), PrecioUnitario = reader.GetDecimal(4),
            Subtotal = reader.GetDecimal(5), StockActual = reader.GetInt32(6), IsCombo = reader.GetBoolean(7) });
        if (!await reader.NextResultAsync(ct)) throw new InvalidOperationException("Falta la acreditación de importes.");
        if (await reader.ReadAsync(ct))
        {
            var amounts = new QuoteMoney(reader.GetDecimal(0), reader.GetDecimal(1), reader.GetDecimal(2), reader.GetDecimal(3), reader.GetDecimal(4));
            var lines = model.Detalles.Select(x => new PricedQuantity(x.ProductoId, x.Cantidad, x.PrecioUnitario,
                x.Cantidad * x.PrecioUnitario - x.Subtotal)).ToArray();
            if (amounts != ServerPricing.Calculate(lines) || amounts.FinalTotal != model.Total)
                throw new InvalidOperationException("Los importes del pedido no coinciden.");
            model.ServerAmounts = amounts;
        }
        await Complete(reader, ct); return (model, revision);
    }
    public async Task<List<RetainedOrderListItemViewModel>> RetainedAsync(string? search, CancellationToken ct)
    {
        if (search?.Length > 150) throw new ArgumentException("La búsqueda excede el contrato.");
        await using var connection = Connection(); using var command = Command(connection, "Retained");
        command.Parameters.Add("@Buscar", SqlDbType.NVarChar, 150).Value = string.IsNullOrWhiteSpace(search) ? DBNull.Value : search.Trim();
        await connection.OpenAsync(ct); await using var reader = await command.ExecuteReaderAsync(ct);
        var rows = new List<RetainedOrderListItemViewModel>();
        while (await reader.ReadAsync(ct)) rows.Add(new() { PedidoId = reader.GetInt32(0), Cliente = reader.GetString(1), Correo = reader.GetString(2),
            FechaPedido = reader.GetDateTime(3), VendedorNombre = reader.GetString(4), Total = reader.GetDecimal(5), TipoEntrega = reader.GetString(6),
            CanalPedido = reader.GetString(7), TotalLineas = reader.GetInt32(8) });
        await Complete(reader, ct); return rows;
    }
    public async Task<ManagerApproveResultViewModel> DecideAsync(int order, Guid operation, long revision, bool approve, string? reason, CancellationToken ct)
    {
        if (order <= 0 || operation == Guid.Empty || revision <= 0 || (!approve && string.IsNullOrWhiteSpace(reason)) || reason?.Length > 500)
            throw new ArgumentException("La decisión no cumple el contrato.");
        await using var connection = Connection(); using var command = Command(connection, "Decide");
        command.Parameters.Add("@PedidoId", SqlDbType.Int).Value = order; command.Parameters.Add("@OperationId", SqlDbType.UniqueIdentifier).Value = operation;
        command.Parameters.Add("@ExpectedRevision", SqlDbType.BigInt).Value = revision;
        command.Parameters.Add("@Decision", SqlDbType.NVarChar, 20).Value = approve ? "Aprobado" : "Rechazado";
        command.Parameters.Add("@Reason", SqlDbType.NVarChar, 500).Value = approve ? DBNull.Value : reason!.Trim();
        await connection.OpenAsync(ct); await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) throw new InvalidOperationException("Falta el resultado de la decisión.");
        var result = new ManagerApproveResultViewModel { PedidoId = reader.GetInt32(0), Estado = reader.GetString(1), FacturaId = reader.GetInt32(2), NumeroFactura = reader.GetString(3) };
        if (result.PedidoId != order || (approve && (result.Estado != "Facturado" || result.FacturaId <= 0))
            || (!approve && (result.Estado != "Rechazado" || result.FacturaId != 0))) throw new InvalidOperationException("La decisión no confirmó sus efectos.");
        await Complete(reader, ct); return result;
    }
    private static bool Hash(string? value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);
    private static bool SameHash(string first, string second) => Hash(second) && CryptographicOperations.FixedTimeEquals(Convert.FromHexString(first), Convert.FromHexString(second));
    private static string? Text(SqlDataReader reader, int index) => reader.IsDBNull(index) ? null : reader.GetString(index);
    private static async Task Complete(SqlDataReader reader, CancellationToken ct)
    { while (await reader.NextResultAsync(ct)) while (await reader.ReadAsync(ct)) { } }
}
