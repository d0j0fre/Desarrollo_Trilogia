namespace Proyecto_Final.Middleware
{
    public class SecurityHeadersMiddleware
    {
        private readonly RequestDelegate _next;

        public SecurityHeadersMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;

                SetHeaderIfMissing(headers, "X-Content-Type-Options", "nosniff");
                SetHeaderIfMissing(headers, "X-Frame-Options", "DENY");
                SetHeaderIfMissing(headers, "Referrer-Policy", "strict-origin-when-cross-origin");
                SetHeaderIfMissing(headers, "X-XSS-Protection", "0");
                SetHeaderIfMissing(headers, "Permissions-Policy", "camera=(), microphone=(), geolocation=()");

                if (IsSensitivePath(context.Request.Path))
                {
                    headers["Cache-Control"] = "no-store, no-cache, must-revalidate, max-age=0";
                    headers["Pragma"] = "no-cache";
                    headers["Expires"] = "0";
                }

                return Task.CompletedTask;
            });

            await _next(context);
        }

        private static void SetHeaderIfMissing(IHeaderDictionary headers, string name, string value)
        {
            if (!headers.ContainsKey(name))
            {
                headers[name] = value;
            }
        }

        private static bool IsSensitivePath(PathString path)
        {
            return path.StartsWithSegments("/Account")
                || path.StartsWithSegments("/Admin")
                || path.StartsWithSegments("/Inventory")
                || path.StartsWithSegments("/OrdersAdmin")
                || path.StartsWithSegments("/Billing")
                || path.StartsWithSegments("/Clients")
                || path.StartsWithSegments("/Credits")
                || path.StartsWithSegments("/Employees")
                || path.StartsWithSegments("/EmployeePortal")
                || path.StartsWithSegments("/Roles")
                || path.StartsWithSegments("/Permissions")
                || path.StartsWithSegments("/Audit")
                || path.StartsWithSegments("/SellerOrders");
        }
    }
}
