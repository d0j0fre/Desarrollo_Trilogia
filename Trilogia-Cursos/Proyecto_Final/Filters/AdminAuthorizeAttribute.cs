using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Proyecto_Final.Filters
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class AdminAuthorizeAttribute : Attribute, IAuthorizationFilter
    {
        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var session = context.HttpContext.Session;

            var userEmail = session.GetString("UserEmail");
            var userRole = session.GetString("UserRole");

            if (string.IsNullOrWhiteSpace(userEmail))
            {
                context.Result = new RedirectToActionResult("Login", "Account", null);
                return;
            }

            if (!string.Equals(userRole, "Administrador", StringComparison.OrdinalIgnoreCase))
            {
                context.Result = new RedirectToActionResult("Index", "Home", null);
                return;
            }
        }
    }
}