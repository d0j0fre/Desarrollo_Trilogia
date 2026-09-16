using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Proyecto_FinalAPI.Services;

namespace Proyecto_FinalAPI.Authorization
{
    /// <summary>
    /// Exige un código de permiso concreto sobre un endpoint de la API.
    ///
    /// Espeja <c>AdminAuthorizeAttribute</c> del MVC a propósito: misma idea, misma
    /// tabla <c>PerfilPermisos</c>, mismo procedimiento <c>sp_Admin_HasPermissionByCode</c>.
    ///
    /// El permiso se revalida contra la base en cada request. El claim <c>perm</c>
    /// del token existe para que la aplicación sepa qué dibujar, no para decidir
    /// accesos: si a un usuario se le revoca un permiso, deja de pasar de inmediato
    /// aunque su token siga vigente.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public sealed class RequirePermissionAttribute : TypeFilterAttribute
    {
        public RequirePermissionAttribute(string permissionCode) : base(typeof(RequirePermissionFilter))
        {
            Arguments = new object[] { permissionCode };
        }
    }

    public sealed class RequirePermissionFilter : IAsyncAuthorizationFilter
    {
        private readonly string _permissionCode;
        private readonly IMobileAuthDbService _authDb;
        private readonly ILogger<RequirePermissionFilter> _logger;

        public RequirePermissionFilter(
            string permissionCode,
            IMobileAuthDbService authDb,
            ILogger<RequirePermissionFilter> logger)
        {
            _permissionCode = permissionCode;
            _authDb = authDb;
            _logger = logger;
        }

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var user = context.HttpContext.User;

            if (user?.Identity?.IsAuthenticated != true)
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            var role = user.FindFirstValue(ClaimTypes.Role);

            if (string.Equals(role, "Administrador", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            bool granted;
            try
            {
                granted = await _authDb.HasPermissionAsync(
                    role,
                    _permissionCode,
                    context.HttpContext.RequestAborted);
            }
            catch (Exception exception)
            {
                // Ante una falla al consultar permisos se niega el acceso. Fallar
                // abierto en autorización nunca es una opción.
                _logger.LogError(
                    exception,
                    "No se pudo verificar el permiso {Permiso} para el rol {Rol}.",
                    _permissionCode,
                    role);
                context.Result = new ObjectResult(new { error = "servicio_no_disponible" })
                {
                    StatusCode = StatusCodes.Status503ServiceUnavailable
                };
                return;
            }

            if (!granted)
            {
                _logger.LogInformation(
                    "Acceso denegado: el rol {Rol} no tiene el permiso {Permiso}.",
                    role,
                    _permissionCode);
                context.Result = new ObjectResult(new { error = "permiso_insuficiente" })
                {
                    StatusCode = StatusCodes.Status403Forbidden
                };
            }
        }
    }
}
