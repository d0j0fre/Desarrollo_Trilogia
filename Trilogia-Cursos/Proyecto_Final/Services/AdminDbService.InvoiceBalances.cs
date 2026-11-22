using System.Data;
using Microsoft.Data.SqlClient;

namespace Proyecto_Final.Services
{
    /// <summary>M-07 (0336): cargo a crédito de una factura y lo que aún está pendiente de pago.</summary>
    public sealed record InvoiceBalance(int FacturaId, int PedidoId, decimal MontoCargo, decimal Pendiente)
    {
        /// <summary>Pagada por completo: tiene cargo a crédito y su saldo es ₡0.</summary>
        public bool Pagada => MontoCargo > 0 && Pendiente <= 0;
    }

    // M-07: «Pagada» es solo una etiqueta de pantalla; dbo.Facturas.Estado no cambia (lo usan los reportes).
    public partial class AdminDbService
    {
        public async Task<List<InvoiceBalance>> GetInvoiceBalancesAsync(int? usuarioId = null, int? facturaId = null, CancellationToken cancellationToken = default)
        {
            var lista = new List<InvoiceBalance>();
            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_Facturas_Saldo", connection) { CommandType = CommandType.StoredProcedure };
            command.Parameters.Add("@UsuarioId", SqlDbType.Int).Value = usuarioId.HasValue ? usuarioId.Value : DBNull.Value;
            command.Parameters.Add("@FacturaId", SqlDbType.Int).Value = facturaId.HasValue ? facturaId.Value : DBNull.Value;
            await connection.OpenAsync(cancellationToken);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                lista.Add(new InvoiceBalance(reader.GetInt32(0), reader.GetInt32(2), reader.GetDecimal(4), reader.GetDecimal(5)));
            }
            return lista;
        }

        /// <summary>Igual que <see cref="GetInvoiceBalancesAsync"/>, pero si falla (por ejemplo, sin la 0336) devuelve una lista vacía.</summary>
        public async Task<List<InvoiceBalance>> TryGetInvoiceBalancesAsync(int? usuarioId, int? facturaId, Microsoft.Extensions.Logging.ILogger logger)
        {
            try
            {
                return await GetInvoiceBalancesAsync(usuarioId, facturaId);
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "No se pudo consultar el saldo de las facturas (¿falta la migración 0336?); se muestra el estado guardado.");
                return new List<InvoiceBalance>();
            }
        }
    }

    public static class InvoiceDisplayStatus
    {
        public const string Generated = "Generada";
        public const string Paid = "Pagada";

        /// <summary>«Generada» con saldo ₡0 se muestra «Pagada»; cualquier otro estado se muestra tal cual.</summary>
        public static string Visible(string? estado, bool pagada) =>
            pagada && string.Equals(estado, Generated, StringComparison.Ordinal) ? Paid : estado ?? string.Empty;

        public static string CssClass(string visible) => visible is Generated or Paid ? "success" : "dark";
    }
}
