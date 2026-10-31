using System.Data;
using Microsoft.Data.SqlClient;
using Proyecto_Final.Models.Admin;

namespace Proyecto_Final.Services
{
    // BUG-06-03 / BUG-12-01 / BUG-12-02 — estado de cuenta del cliente (sp_Cliente_EstadoCuenta, migración 0232).
    public class ClientAccountDbService
    {
        private readonly string _connectionString;

        public ClientAccountDbService(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("No se encontró la cadena de conexión DefaultConnection.");
        }

        public async Task<ClientAccountStatementViewModel?> GetStatementAsync(int usuarioId, CancellationToken cancellationToken = default)
        {
            if (usuarioId <= 0) return null;

            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_Cliente_EstadoCuenta", connection) { CommandType = CommandType.StoredProcedure };
            command.Parameters.Add("@UsuarioId", SqlDbType.Int).Value = usuarioId;

            await connection.OpenAsync(cancellationToken);
            try
            {
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                if (!await reader.ReadAsync(cancellationToken)) return null;

                var model = new ClientAccountStatementViewModel
                {
                    UsuarioId = reader.GetInt32(0),
                    NombreCompleto = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                    Correo = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                    Telefono = reader.IsDBNull(3) ? null : reader.GetString(3),
                    Direccion = reader.IsDBNull(4) ? null : reader.GetString(4),
                    LimiteCredito = reader.GetDecimal(5),
                    CreditoActivo = reader.GetBoolean(6),
                    CreditoBloqueado = reader.GetBoolean(7),
                    PlazoCreditoDias = reader.GetInt32(8),
                    Saldo = reader.GetDecimal(9),
                    DeudaVencida = reader.GetDecimal(10),
                    VencimientoMasAntiguo = reader.IsDBNull(11) ? null : reader.GetDateTime(11),
                    CreditoDisponible = reader.GetDecimal(12),
                    CantidadFacturas = reader.GetInt32(13),
                    TotalFacturado = reader.GetDecimal(14),
                    TotalPagado = reader.GetDecimal(15),
                    FechaCorte = reader.GetDateTime(16)
                };

                await reader.NextResultAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    model.Facturas.Add(new ClientAccountInvoiceViewModel
                    {
                        FacturaId = reader.GetInt32(0),
                        NumeroFactura = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                        PedidoId = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                        FechaFactura = reader.GetDateTime(3),
                        Subtotal = reader.GetDecimal(4),
                        Impuesto = reader.GetDecimal(5),
                        Total = reader.GetDecimal(6),
                        Estado = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                        MetodoPago = reader.IsDBNull(8) ? null : reader.GetString(8)
                    });
                }

                await reader.NextResultAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    model.Movimientos.Add(new ClientAccountMovementViewModel
                    {
                        CreditoMovimientoId = reader.GetInt32(0),
                        FechaMovimiento = reader.GetDateTime(1),
                        TipoMovimiento = reader.GetString(2),
                        Descripcion = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                        Referencia = reader.IsDBNull(4) ? null : reader.GetString(4),
                        PedidoId = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                        Cargo = reader.GetDecimal(6),
                        Abono = reader.GetDecimal(7),
                        SaldoAcumulado = reader.GetDecimal(8)
                    });
                }

                return model;
            }
            catch (SqlException ex) when (ex.Number == 52030)
            {
                return null;
            }
        }
    }
}
