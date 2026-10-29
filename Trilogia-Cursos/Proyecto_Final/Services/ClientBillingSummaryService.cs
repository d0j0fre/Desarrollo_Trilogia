using System.Data;
using Microsoft.Data.SqlClient;

namespace Proyecto_Final.Services;

public sealed record ClientBillingSummary(int InvoiceCount, decimal TotalBilled);
public interface IClientBillingSummaryService
{
    Task<ClientBillingSummary?> ReadOwnAsync(CancellationToken cancellationToken);
}

public sealed class ClientBillingSummaryService(IConfiguration configuration, IHttpContextAccessor accessor)
    : IClientBillingSummaryService
{
    public async Task<ClientBillingSummary?> ReadOwnAsync(CancellationToken cancellationToken)
    {
        if (!configuration.GetValue<bool>("Commerce:OrdersEnabled")) return null;
        var login = OriginalLoginSession.ValidatedRequest(accessor.HttpContext ?? throw new UnauthorizedAccessException());
        await using var connection = new SqlConnection(configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Falta la identidad APP."));
        using var command = OriginalLoginService.ContextCommand(connection, "dbo.sp_Stage2_ClientBillingSummary", login);
        command.CommandTimeout = 10;
        await connection.OpenAsync(cancellationToken);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) throw new InvalidOperationException("Falta el resumen de facturación.");
        var result = new ClientBillingSummary(reader.GetInt32(0), reader.GetDecimal(1));
        while (await reader.NextResultAsync(cancellationToken))
            while (await reader.ReadAsync(cancellationToken)) { }
        if (result.InvoiceCount < 0 || result.TotalBilled < 0)
            throw new InvalidOperationException("El resumen de facturación no es válido.");
        return result;
    }
}
