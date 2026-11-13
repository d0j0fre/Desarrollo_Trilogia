using Microsoft.AspNetCore.Mvc;
using Proyecto_Final.Filters;
using Proyecto_Final.Models.Admin;
using Proyecto_Final.Services;

namespace Proyecto_Final.Controllers
{
    [AdminAuthorize("Auditoria")]
    public class AuditController : Controller
    {
        private readonly AdminDbService _adminDbService;

        public AuditController(AdminDbService adminDbService)
        {
            _adminDbService = adminDbService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? origen, string? modulo, string? accion, string? buscar)
        {
            var origin = AuditOrigins.Normalize(origen);
            var registros = await _adminDbService.GetUnifiedAuditLogsAsync(origin, modulo, accion, buscar);
            var (modulos, acciones) = await _adminDbService.GetAuditFilterOptionsAsync();

            var model = new AuditLogFilterViewModel
            {
                Origen = origin,
                Modulo = modulo,
                Accion = accion,
                Buscar = buscar,
                Modulos = modulos,
                Acciones = acciones,
                Registros = registros
            };

            return View(model);
        }
    }
}
