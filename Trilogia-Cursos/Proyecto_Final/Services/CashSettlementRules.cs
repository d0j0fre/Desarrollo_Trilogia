using Proyecto_Final.Models.Admin;

namespace Proyecto_Final.Services
{
    /// <summary>
    /// Reglas de la liquidación de cobros que se validan antes de llegar a la base
    /// (la base las repite en sp_LiquidacionCobros_Registrar): G2-38 y los textos de resultado.
    /// </summary>
    public static class CashSettlementRules
    {
        /// <summary>
        /// El detalle de comprobantes debe tener tipo y monto positivo en cada fila escrita y sumar
        /// exactamente «Total en comprobantes». Devuelve el texto del error o null si está bien.
        /// </summary>
        public static string? ValidateVouchers(IEnumerable<CashSettlementVoucher>? comprobantes, decimal totalComprobantes)
        {
            var filas = (comprobantes ?? Enumerable.Empty<CashSettlementVoucher>())
                .Where(c => c.Monto != 0 || !string.IsNullOrWhiteSpace(c.Tipo) || !string.IsNullOrWhiteSpace(c.Referencia))
                .ToList();

            if (filas.Any(c => c.Monto <= 0 || string.IsNullOrWhiteSpace(c.Tipo)))
                return "Cada comprobante necesita su tipo y un monto mayor que cero.";

            var suma = filas.Sum(c => Math.Round(c.Monto, 2));
            var total = Math.Round(totalComprobantes, 2);
            return suma == total
                ? null
                : $"El total en comprobantes (₡{total:N2}) no coincide con la suma del detalle (₡{suma:N2}). Detalle cada comprobante o corrija el total.";
        }

        public static string SuccessMessage(CashSettlementResult result)
        {
            var texto = $"Liquidación registrada. Resultado: {result.Estado} (diferencia ₡{result.Diferencia:N2}).";
            if (result.FaltanteChofer > 0 && !string.IsNullOrWhiteSpace(result.ChoferNombre))
                texto += $" El faltante de ₡{result.FaltanteChofer:N2} quedó a cargo del chofer {result.ChoferNombre}.";
            return texto;
        }
    }
}
