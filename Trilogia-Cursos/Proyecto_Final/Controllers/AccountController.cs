using Microsoft.AspNetCore.Mvc;
using Proyecto_Final.Services;

namespace Proyecto_Final.Controllers
{
    public class AccountController : Controller
    {
        private readonly AccountApiService _accountApiService;
        private readonly AdminDbService _adminDbService; // Inyectado para permisos y auditoría

        public AccountController(AccountApiService accountApiService, AdminDbService adminDbService)
        {
            _accountApiService = accountApiService;
            _adminDbService = adminDbService;
        }

        [HttpGet]
        public IActionResult Login()
        {
            if (!string.IsNullOrWhiteSpace(HttpContext.Session.GetString("UserEmail")))
            {
                return RedirectToAction("Index", "Home");
            }

            return View(new Proyecto_Final.Models.LoginViewModel
            {
                Email = string.Empty,
                Password = string.Empty
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(Proyecto_Final.Models.LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                var response = await _accountApiService.LoginAsync(model);

                if (response == null || !response.Success)
                {
                    ModelState.AddModelError(string.Empty, response?.Message ?? "No fue posible iniciar sesión.");
                    return View(model);
                }

                int userId = response.UserId ?? 0;
                HttpContext.Session.SetInt32("UserId", userId);
                HttpContext.Session.SetString("UserEmail", response.Email ?? string.Empty);
                HttpContext.Session.SetString("UserFullName", response.FullName ?? string.Empty);
                HttpContext.Session.SetString("UserRole", response.Role ?? string.Empty);

                // --- INTEGRACIÓN HU-42 y HU-43 ---
                if (userId > 0)
                {
                    // 1. Obtener y guardar los módulos a los que tiene acceso
                    var permisos = await _adminDbService.ObtenerNombresModulosPorUsuarioIdAsync(userId);
                    HttpContext.Session.SetString("PermisosUsuario", string.Join(",", permisos));

                    // 2. Registrar en auditoría el ingreso
                    await _adminDbService.RegistrarAuditoriaAsync(userId, "LOGIN_EXITOSO", "Seguridad", $"Inicio de sesión correcto de: {response.Email}");
                }

                TempData["LoginSuccess"] = $"Bienvenido, {response.FullName}.";
                return RedirectToAction("Index", "Home");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, $"Ocurrió un error al iniciar sesión: {ex.Message}");
                return View(model);
            }
        }

        [HttpGet]
        public IActionResult Register()
        {
            if (!string.IsNullOrWhiteSpace(HttpContext.Session.GetString("UserEmail")))
            {
                return RedirectToAction("Index", "Home");
            }

            return View("Registro", new Proyecto_Final.Models.RegistroViewModel
            {
                FullName = string.Empty,
                Email = string.Empty,
                Password = string.Empty,
                ConfirmPassword = string.Empty
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(Proyecto_Final.Models.RegistroViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View("Registro", model);
            }

            try
            {
                var response = await _accountApiService.RegisterAsync(model);

                if (response == null || !response.Success)
                {
                    ModelState.AddModelError(string.Empty, response?.Message ?? "No fue posible completar el registro.");
                    return View("Registro", model);
                }

                TempData["SuccessMessage"] = "Tu cuenta fue creada correctamente. Ahora puedes iniciar sesión.";
                return RedirectToAction(nameof(Login));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, $"Ocurrió un error durante el registro: {ex.Message}");
                return View("Registro", model);
            }
        }

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View(new Proyecto_Final.Models.ForgotPasswordViewModel
            {
                Email = string.Empty
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(Proyecto_Final.Models.ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                var response = await _accountApiService.ForgotPasswordAsync(model);

                if (response == null || !response.Success)
                {
                    ModelState.AddModelError(string.Empty, response?.Message ?? "No fue posible procesar la solicitud.");
                    return View(model);
                }

                TempData["SuccessMessage"] = response.Message ?? "Se procesó la recuperación de contraseña correctamente.";
                return RedirectToAction(nameof(Login));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, $"Ocurrió un error al procesar la solicitud: {ex.Message}");
                return View(model);
            }
        }

        [HttpGet]
        public IActionResult ResetPassword(string token, string email)
        {
            var model = new Proyecto_Final.Models.ResetPasswordViewModel
            {
                Token = token ?? string.Empty,
                Password = string.Empty,
                ConfirmPassword = string.Empty
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(Proyecto_Final.Models.ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                var response = await _accountApiService.ResetPasswordAsync(model);

                if (response == null || !response.Success)
                {
                    ModelState.AddModelError(string.Empty, response?.Message ?? "No fue posible restablecer la contraseña.");
                    return View(model);
                }

                TempData["SuccessMessage"] = response.Message ?? "La contraseña se restableció correctamente.";
                return RedirectToAction(nameof(Login));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, $"Ocurrió un error al restablecer la contraseña: {ex.Message}");
                return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId.HasValue && userId.Value > 0)
            {
                await _adminDbService.RegistrarAuditoriaAsync(userId.Value, "LOGOUT", "Seguridad", "El usuario cerró sesión manualmente.");
            }

            HttpContext.Session.Clear();
            TempData["SuccessMessage"] = "Sesión cerrada correctamente.";
            return RedirectToAction("Index", "Home");
        }

        // Endpoint para cuando el Filtro AdminAuthorize bloquea el acceso
        [HttpGet]
        public IActionResult AccesoDenegado()
        {
            return View();
        }
    }
}