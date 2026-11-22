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
            var (storedModules, acciones) = await _adminDbService.GetAuditFilterOptionsAsync();
            var registros = await _adminDbService.GetUnifiedAuditLogsByModuleAsync(origin, modulo, accion, buscar, storedModules);

            // M-14: una sola entrada por módulo, con tildes; los registros viejos sin tilde se agrupan con los nuevos.
            var modulos = AuditModuleNames.Options(storedModules);
            var moduloElegido = string.IsNullOrWhiteSpace(modulo)
                ? modulo
                : AuditModuleNames.Options(AuditModuleNames.StoredVariants(modulo, storedModules)).First();
            foreach (var registro in registros)
            {
                registro.Modulo = AuditModuleNames.DisplayName(registro.Modulo);
            }

            var model = new AuditLogFilterViewModel
            {
                Origen = origin,
                Modulo = moduloElegido,
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
