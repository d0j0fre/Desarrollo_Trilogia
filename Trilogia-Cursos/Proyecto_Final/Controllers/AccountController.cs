using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Proyecto_Final.Services;

namespace Proyecto_Final.Controllers
{
    public class AccountController : Controller
    {
        private readonly AccountApiService _accountApiService;
        private readonly AdminDbService _adminDbService;

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
        [EnableRateLimiting("authentication")]
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

                HttpContext.Session.SetInt32("UserId", response.UserId ?? 0);
                HttpContext.Session.SetString("UserEmail", response.Email ?? string.Empty);
                HttpContext.Session.SetString("UserFullName", response.FullName ?? string.Empty);
                HttpContext.Session.SetString("UserRole", response.Role ?? string.Empty);

                await RegistrarAuditoriaAsync(
                    response.UserId ?? 0,
                    response.FullName,
                    response.Email,
                    response.Role,
                    "Login",
                    "Autenticación",
                    "El usuario inició sesión correctamente.");

                TempData["LoginSuccess"] = $"Bienvenido, {response.FullName}.";
                return RedirectToAction("Index", "Home");
            }
            catch (Exception)
            {
                ModelState.AddModelError(string.Empty, "No fue posible iniciar sesión en este momento. Intentá nuevamente.");
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

                await RegistrarAuditoriaAsync(
                    null,
                    model.FullName,
                    model.Email,
                    "Cliente",
                    "Registro",
                    "Usuarios",
                    $"Se registró una nueva cuenta de cliente para el correo {model.Email}.");

                TempData["SuccessMessage"] = "Tu cuenta fue creada correctamente. Ahora puedes iniciar sesión.";
                return RedirectToAction(nameof(Login));
            }
            catch (Exception)
            {
                ModelState.AddModelError(string.Empty, "No fue posible completar el registro en este momento. Intentá nuevamente.");
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
        [EnableRateLimiting("password-recovery")]
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

                await RegistrarAuditoriaAsync(
                    null,
                    "Usuario no autenticado",
                    model.Email,
                    "No disponible",
                    "Recuperación de contraseña",
                    "Autenticación",
                    $"Se solicitó recuperación de contraseña para el correo {model.Email}.");

                TempData["SuccessMessage"] = response.Message ?? "Se procesó la recuperación de contraseña correctamente.";
                return RedirectToAction(nameof(Login));
            }
            catch (Exception)
            {
                ModelState.AddModelError(string.Empty, "No fue posible procesar la solicitud en este momento. Intentá nuevamente.");
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
        [EnableRateLimiting("password-recovery")]
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

                await RegistrarAuditoriaAsync(
                    null,
                    "Usuario no autenticado",
                    "No disponible",
                    "No disponible",
                    "Cambio de contraseña",
                    "Autenticación",
                    "Se restableció una contraseña mediante token de recuperación.");

                TempData["SuccessMessage"] = response.Message ?? "La contraseña se restableció correctamente.";
                return RedirectToAction(nameof(Login));
            }
            catch (Exception)
            {
                ModelState.AddModelError(string.Empty, "No fue posible restablecer la contraseña en este momento. Intentá nuevamente.");
                return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            var usuarioId = HttpContext.Session.GetInt32("UserId");
            var usuarioNombre = HttpContext.Session.GetString("UserFullName");
            var usuarioCorreo = HttpContext.Session.GetString("UserEmail");
            var rol = HttpContext.Session.GetString("UserRole");

            await RegistrarAuditoriaAsync(
                usuarioId,
                usuarioNombre,
                usuarioCorreo,
                rol,
                "Logout",
                "Autenticación",
                "El usuario cerró sesión correctamente.");

            HttpContext.Session.Clear();
            TempData["SuccessMessage"] = "Sesión cerrada correctamente.";
            return RedirectToAction("Index", "Home");
        }

        private async Task RegistrarAuditoriaAsync(int? usuarioId, string? usuarioNombre, string? usuarioCorreo, string? rol, string accion, string modulo, string descripcion)
        {
            await _adminDbService.CreateAuditLogAsync(
                usuarioId,
                usuarioNombre,
                usuarioCorreo,
                rol,
                accion,
                modulo,
                descripcion,
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                Request.Headers.UserAgent.ToString());
        }
    }
}
