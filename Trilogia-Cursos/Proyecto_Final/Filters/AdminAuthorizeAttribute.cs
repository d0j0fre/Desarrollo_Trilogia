using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Proyecto_Final.Services;

namespace Proyecto_Final.Filters
{
    public class AdminAuthorizeAttribute : TypeFilterAttribute
    {
        public AdminAuthorizeAttribute(string modulo) : base(typeof(AdminAuthorizeFilter))
        {
            Arguments = new object[] { modulo };
        }
    }

    public class AdminAuthorizeFilter : IAsyncAuthorizationFilter
    {
        private readonly string _modulo;
        private readonly AdminDbService _dbService;

        public AdminAuthorizeFilter(string modulo, AdminDbService dbService)
        {
            _modulo = modulo;
            _dbService = dbService;
        }

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var userId = context.HttpContext.Session.GetInt32("UserId");
            var userEmail = context.HttpContext.Session.GetString("UserEmail");
            var userRole = context.HttpContext.Session.GetString("UserRole");

            if (!userId.HasValue || userId.Value <= 0 || string.IsNullOrWhiteSpace(userEmail))
            {
                context.Result = new RedirectToActionResult("Login", "Account", null);
                return;
            }

            if (string.Equals(userRole, "Administrador", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var tienePermiso = await _dbService.TienePermisoPorRolAsync(userRole, _modulo);

            if (!tienePermiso)
            {
                context.Result = new RedirectToActionResult("AccesoDenegado", "Home", null);
            }
        }
    }
}
