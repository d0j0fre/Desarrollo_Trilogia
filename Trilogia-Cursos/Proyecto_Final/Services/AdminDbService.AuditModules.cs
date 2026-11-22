using Proyecto_Final.Models.Admin;

namespace Proyecto_Final.Services
{
    // M-14: filtro de Auditoría por módulo sin importar cómo se haya escrito el nombre (con o sin tilde).
    public partial class AdminDbService
    {
        /// <summary>
        /// Igual que <see cref="GetUnifiedAuditLogsAsync"/>, pero un módulo elegido en la lista incluye todas sus grafías
        /// guardadas (Facturacion y Facturación). El máximo de 200 registros se aplica al conjunto.
        /// </summary>
        public async Task<List<AuditLogViewModel>> GetUnifiedAuditLogsByModuleAsync(
            string? origen, string? modulo, string? accion, string? buscar, IReadOnlyCollection<string> storedModules)
        {
            if (string.IsNullOrWhiteSpace(modulo))
            {
                return await GetUnifiedAuditLogsAsync(origen, null, accion, buscar);
            }

            var variants = AuditModuleNames.StoredVariants(modulo, storedModules);
            if (variants.Count == 1)
            {
                return await GetUnifiedAuditLogsAsync(origen, variants[0], accion, buscar);
            }

            var all = new List<AuditLogViewModel>();
            foreach (var variant in variants)
            {
                all.AddRange(await GetUnifiedAuditLogsAsync(origen, variant, accion, buscar));
            }

            return all.OrderByDescending(r => r.FechaRegistro).Take(200).ToList();
        }
    }
}
