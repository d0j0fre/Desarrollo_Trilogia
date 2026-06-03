using Microsoft.AspNetCore.Mvc;
using Proyecto_Final.Filters;
using Proyecto_Final.Services;

namespace Proyecto_Final.Controllers
{
    //[AdminAuthorize("Seguridad")] // Protegemos todo el módulo de seguridad
    public class SecurityController : Controller
    {
        private readonly AdminDbService _dbService;

        public SecurityController(AdminDbService dbService)
        {
            _dbService = dbService;
        }

        // HU-041: Vista principal de Roles
        [HttpGet]
        public async Task<IActionResult> Roles()
        {
            var perfiles = await _dbService.ObtenerPerfilesAsync();
            return View(perfiles);
        }

        // HU-042: Vista para asignar permisos a un Rol específico
        [HttpGet]
        public async Task<IActionResult> Permisos(int perfilId)
        {
            // Aquí luego inyectaremos los módulos asignados y no asignados
            ViewBag.PerfilId = perfilId;
            return View();
        }

        // HU-043: Vista de la tabla de auditoría
        [HttpGet]
        public async Task<IActionResult> Auditoria()
        {
            var historial = await _dbService.ObtenerAuditoriaAsync();
            return View(historial);
        }
    }
}