using Microsoft.AspNetCore.Mvc;
using Proyecto_Final.Filters;

namespace Proyecto_Final.Controllers
{
    [AdminAuthorize("Seguridad")]
    public class SecurityController : Controller
    {
        [HttpGet]
        public IActionResult Roles()
        {
            return RedirectToAction("Index", "Roles");
        }

        [HttpGet]
        public IActionResult Auditoria()
        {
            return RedirectToAction("Index", "Audit");
        }

        [HttpGet]
        public IActionResult CrearRol()
        {
            return RedirectToAction("Create", "Roles");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CrearRolPost()
        {
            return RedirectToAction("Create", "Roles");
        }

        [HttpGet]
        public IActionResult EditarRol(int id)
        {
            return RedirectToAction("Edit", "Roles", new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditarRolPost(int perfilId)
        {
            return RedirectToAction("Edit", "Roles", new { id = perfilId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EliminarPerfil(int id)
        {
            return RedirectToAction("Index", "Roles");
        }

        [HttpGet]
        public IActionResult Permisos(int perfilId)
        {
            return RedirectToAction("Edit", "Permissions", new { id = perfilId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult GuardarPermisos(int perfilId)
        {
            return RedirectToAction("Edit", "Permissions", new { id = perfilId });
        }
    }
}
