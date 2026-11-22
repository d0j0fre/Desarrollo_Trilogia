using System.Data;
using Microsoft.Data.SqlClient;
using Proyecto_Final.Models.Admin;

namespace Proyecto_Final.Services
{
    // POST-qa2-03 · CU-093-04 (0326): facturas a crédito vencidas y su aviso diario.
    public partial class AdminDbService
    {
        /// <summary>Facturas vencidas de un cliente (o de todos si no se indica) según el FIFO de la 0230.</summary>
        public async Task<List<OverdueInvoiceViewModel>> GetOverdueInvoicesAsync(int? usuarioId = null, CancellationToken cancellationToken = default)
        {
            var lista = new List<OverdueInvoiceViewModel>();
            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_Facturas_Vencidas", connection) { CommandType = CommandType.StoredProcedure };
            command.Parameters.Add("@UsuarioId", SqlDbType.Int).Value = usuarioId.HasValue ? usuarioId.Value : DBNull.Value;
            await connection.OpenAsync(cancellationToken);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                lista.Add(new OverdueInvoiceViewModel
                {
                    FacturaId = reader.GetInt32(0),
                    NumeroFactura = reader.GetString(1),
                    PedidoId = reader.GetInt32(2),
                    UsuarioId = reader.GetInt32(3),
                    MontoCargo = reader.GetDecimal(4),
                    Pendiente = reader.GetDecimal(5),
                    Vencimiento = reader.GetDateTime(6),
                    DiasAtraso = reader.GetInt32(7)
                });
            }
            return lista;
        }

        /// <summary>
        /// Igual que <see cref="GetOverdueInvoicesAsync"/>, pero si la consulta falla devuelve una lista vacía:
        /// la insignia «Vencida» es un complemento y no debe impedir abrir la pantalla.
        /// </summary>
        public async Task<List<OverdueInvoiceViewModel>> TryGetOverdueInvoicesAsync(int? usuarioId, Microsoft.Extensions.Logging.ILogger logger)
        {
            try
            {
                return await GetOverdueInvoicesAsync(usuarioId);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "No se pudieron consultar las facturas vencidas.");
                return new List<OverdueInvoiceViewModel>();
            }
        }

        /// <summary>Crea los avisos FacturaVencida del día (uno por cliente y destinatario). Devuelve cuántos nuevos.</summary>
        public async Task<int> GenerateOverdueInvoiceAlertsAsync(CancellationToken cancellationToken = default)
        {
            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_FacturasVencidas_GenerarAvisos", connection) { CommandType = CommandType.StoredProcedure };
            await connection.OpenAsync(cancellationToken);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            return await reader.ReadAsync(cancellationToken) ? reader.GetInt32(0) : 0;
        }
    }
}
