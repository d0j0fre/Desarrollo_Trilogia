using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Proyecto_Final.Filters
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class SessionAuthorizeAttribute : Attribute, IAuthorizationFilter
    {
        private readonly string[] _allowedRoles;

        public SessionAuthorizeAttribute(params string[] allowedRoles)
        {
            _allowedRoles = allowedRoles ?? Array.Empty<string>();
        }

        // El menú la lee para no mostrar opciones que el rol no puede abrir.
        public IReadOnlyList<string> AllowedRoles => _allowedRoles;

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var session = context.HttpContext.Session;
            var userId = session.GetInt32("UserId");
            var userEmail = session.GetString("UserEmail");
            var userRole = session.GetString("UserRole");

            if (!userId.HasValue || userId.Value <= 0 || string.IsNullOrWhiteSpace(userEmail))
            {
                // En una consulta GET se recuerda la página pedida para volver a ella
                // después de iniciar sesión (por ejemplo, el enlace de descarga que
                // abre la aplicación móvil). Account/Login solo sigue direcciones locales.
                var request = context.HttpContext.Request;
                var returnUrl = HttpMethods.IsGet(request.Method)
                    ? $"{request.PathBase}{request.Path}{request.QueryString}"
                    : null;
                context.Result = new RedirectToActionResult(
                    "Login", "Account", returnUrl is null ? null : new { returnUrl });
                return;
            }

            if (_allowedRoles.Length == 0)
            {
                return;
            }

            var roleAllowed = _allowedRoles.Any(role =>
                string.Equals(role, userRole, StringComparison.OrdinalIgnoreCase));

            if (!roleAllowed)
            {
                context.Result = AuthorizationResults.AccessDenied();
            }
        }
    }
}
