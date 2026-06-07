using Microsoft.AspNetCore.Mvc;
using Proyecto_Final.Filters;
using Proyecto_Final.Models;
using Proyecto_Final.Services;

namespace Proyecto_Final.Controllers
{
    //[AdminAuthorize("Seguridad")] // Protegemos todo el módulo de seguridad (Descomentar cuando el filtro esté listo)
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

        // HU-043: Vista de la tabla de auditoría
        [HttpGet]
        public async Task<IActionResult> Auditoria()
        {
            var historial = await _dbService.ObtenerAuditoriaAsync();
            return View(historial);
        }

        // Muestra el formulario vacío
        [HttpGet]
        public IActionResult CrearRol()
        {
            return View();
        }

        // Recibe los datos del formulario y los guarda
        [HttpPost]
        public async Task<IActionResult> CrearRol(Perfil modelo)
        {
            if (ModelState.IsValid)
            {
                await _dbService.CrearPerfilAsync(modelo);

                // Integración con HU-043: Registrar la acción en auditoría
                var usuarioIdStr = HttpContext.Session.GetString("UsuarioId");
                if (int.TryParse(usuarioIdStr, out int usuarioId))
                {
                    await _dbService.RegistrarAuditoriaAsync(usuarioId, "CREAR_ROL", "Seguridad", $"Se creó el rol: {modelo.Nombre}");
                }

                return RedirectToAction(nameof(Roles)); // Redirige a la tabla
            }

            return View(modelo); // Si hay error, devuelve a la misma vista con los datos que intentó guardar
        }

        // --- EDITAR ROL ---
        [HttpGet]
        public async Task<IActionResult> EditarRol(int id)
        {
            var perfil = await _dbService.ObtenerPerfilPorIdAsync(id);
            if (perfil == null) return NotFound();
            return View(perfil);
        }

        [HttpPost]
        public async Task<IActionResult> EditarRol(Perfil modelo)
        {
            if (ModelState.IsValid)
            {
                await _dbService.ActualizarPerfilAsync(modelo);

                var usuarioIdStr = HttpContext.Session.GetString("UsuarioId");
                if (int.TryParse(usuarioIdStr, out int usuarioId))
                {
                    await _dbService.RegistrarAuditoriaAsync(usuarioId, "EDITAR_ROL", "Seguridad", $"Se editó el rol ID: {modelo.PerfilId}");
                }

                return RedirectToAction(nameof(Roles));
            }
            return View(modelo);
        }

        // --- ELIMINAR ROL ---
        [HttpPost]
        public async Task<IActionResult> EliminarPerfil(int id)
        {
            try
            {
                await _dbService.EliminarPerfilAsync(id);

                var usuarioIdStr = HttpContext.Session.GetString("UsuarioId");
                if (int.TryParse(usuarioIdStr, out int usuarioId))
                {
                    await _dbService.RegistrarAuditoriaAsync(usuarioId, "ELIMINAR_ROL", "Seguridad", $"Se eliminó el rol ID: {id}");
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMsg"] = ex.Message;
            }
            return RedirectToAction(nameof(Roles));
        }

        // --- PERMISOS (HU-042) ---
        [HttpGet]
        public async Task<IActionResult> Permisos(int perfilId)
        {
            ViewBag.Perfil = await _dbService.ObtenerPerfilPorIdAsync(perfilId);
            ViewBag.TodosModulos = await _dbService.ObtenerTodosLosModulosAsync();
            ViewBag.Asignados = await _dbService.ObtenerModulosPorPerfilAsync(perfilId);
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> GuardarPermisos(int perfilId, List<int> modulosSeleccionados)
        {
            await _dbService.ActualizarPermisosAsync(perfilId, modulosSeleccionados);

            var usuarioIdStr = HttpContext.Session.GetString("UsuarioId");
            if (int.TryParse(usuarioIdStr, out int usuarioId))
            {
                await _dbService.RegistrarAuditoriaAsync(usuarioId, "EDITAR_PERMISOS", "Seguridad", $"Se actualizaron permisos del rol ID: {perfilId}");
            }

            return RedirectToAction(nameof(Roles));
        }
    }
}