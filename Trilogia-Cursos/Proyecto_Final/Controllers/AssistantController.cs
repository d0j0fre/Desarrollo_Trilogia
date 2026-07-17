using Microsoft.AspNetCore.Mvc;
using Proyecto_Final.Filters;
using Proyecto_Final.Services;

namespace Proyecto_Final.Controllers
{
    // CU-261 (métricas por lenguaje natural) y CU-263 (ayuda por módulo).
    [SessionAuthorize("Administrador", "Gerente", "Vendedor", "Empleado")]
    public class AssistantController : Controller
    {
        private readonly AssistantService _assistant;
        private readonly ILogger<AssistantController> _logger;

        public AssistantController(AssistantService assistant, ILogger<AssistantController> logger)
        {
            _assistant = assistant;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Index() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Ask(string pregunta, string? contexto)
        {
            if (string.IsNullOrWhiteSpace(pregunta))
                return Json(new { ok = false, message = "Escribe una pregunta." });

            var usuarioId = HttpContext.Session.GetInt32("UserId") ?? 0;
            var usuarioNombre = HttpContext.Session.GetString("UserFullName") ?? "Usuario";
            var rol = HttpContext.Session.GetString("UserRole");

            try
            {
                var r = await _assistant.AskAsync(pregunta, rol, contexto, usuarioId, usuarioNombre);
                return Json(new
                {
                    ok = true,
                    tipo = r.Tipo,
                    intent = r.Intent,
                    interpretado = r.Interpretado,
                    titulo = r.Titulo,
                    respuesta = r.Respuesta,
                    sugerencias = r.Sugerencias
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en el asistente virtual.");
                return Json(new { ok = false, message = "No fue posible procesar la consulta en este momento." });
            }
        }
    }
}
