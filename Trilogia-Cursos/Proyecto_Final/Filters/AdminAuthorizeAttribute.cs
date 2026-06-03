using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Proyecto_Final.Services; // Ajusta al namespace de tu servicio

namespace Proyecto_Final.Filters
{
    // El atributo que decorarás en los controladores
    public class AdminAuthorizeAttribute : TypeFilterAttribute
    {
        public AdminAuthorizeAttribute(string modulo) : base(typeof(AdminAuthorizeFilter))
        {
            Arguments = new object[] { modulo };
        }
    }

    // La lógica de validación
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
            // 1. Validar sesión activa
            var perfilIdStr = context.HttpContext.Session.GetString("PerfilId");

            if (string.IsNullOrEmpty(perfilIdStr) || !int.TryParse(perfilIdStr, out int perfilId))
            {
                context.Result = new RedirectToActionResult("Login", "Account", null);
                return;
            }

            // 2. Validar permiso contra la base de datos
            bool tienePermiso = await _dbService.TienePermisoAsync(perfilId, _modulo);

            if (!tienePermiso)
            {
                // Retorna un error 403 o redirige a una vista "AccesoDenegado"
                context.Result = new RedirectToActionResult("AccesoDenegado", "Home", null);
            }
        }
    }
}