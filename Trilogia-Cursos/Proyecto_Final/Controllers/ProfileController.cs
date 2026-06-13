using Microsoft.AspNetCore.Mvc;
using Proyecto_Final.Filters;
using Proyecto_Final.Services;

namespace Proyecto_Final.Controllers
{
    [SessionAuthorize]
    public class ProfileController : Controller
    {
        private readonly AccountDbService _accountDbService;

        public ProfileController(AccountDbService accountDbService)
        {
            _accountDbService = accountDbService;
        }

        [HttpGet]
        public IActionResult Edit()
        {
            var usuarioId = HttpContext.Session.GetInt32("UserId");

            if (usuarioId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            ViewBag.NombreCompleto = HttpContext.Session.GetString("UserFullName");
            ViewBag.Correo = HttpContext.Session.GetString("UserEmail");

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string fullName, string email)
        {
            var usuarioId = HttpContext.Session.GetInt32("UserId");

            if (usuarioId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            if (string.IsNullOrWhiteSpace(fullName) ||
                string.IsNullOrWhiteSpace(email))
            {
                TempData["ErrorMessage"] = "Todos los campos son obligatorios.";
                return View();
            }

            try
            {
                await _accountDbService.UpdateProfileAsync(
                    usuarioId.Value,
                    fullName,
                    email);

                HttpContext.Session.SetString("UserFullName", fullName);
                HttpContext.Session.SetString("UserEmail", email);

                TempData["SuccessMessage"] = "Perfil actualizado correctamente.";

                return RedirectToAction(nameof(Edit));
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "No fue posible actualizar el perfil en este momento. Intente nuevamente.";
                return View();
            }
        }
    }
}
