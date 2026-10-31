using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.RateLimiting;

namespace Proyecto_Final.Middleware
{
    /// <summary>
    /// Cuando la política "password-recovery" rechaza una solicitud, el usuario
    /// vuelve a «¿Olvidaste tu contraseña?» con un mensaje de espera en lugar de
    /// la página vacía con HTTP 429. Las demás políticas conservan el 429.
    /// </summary>
    public static class PasswordRecoveryRateLimitResponse
    {
        public const string PolicyName = "password-recovery";
        private const int DefaultWaitMinutes = 15;

        public static ValueTask HandleAsync(OnRejectedContext context, CancellationToken cancellationToken)
        {
            var httpContext = context.HttpContext;
            var policy = httpContext.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName;

            if (!string.Equals(policy, PolicyName, StringComparison.Ordinal) || httpContext.Response.HasStarted)
            {
                return ValueTask.CompletedTask;
            }

            var minutes = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
                ? WaitMinutes(retryAfter)
                : DefaultWaitMinutes;

            var tempData = httpContext.RequestServices
                .GetRequiredService<ITempDataDictionaryFactory>()
                .GetTempData(httpContext);
            tempData["ErrorMessage"] = BuildMessage(minutes);
            tempData.Save();

            httpContext.Response.Redirect("/Account/ForgotPassword");
            return ValueTask.CompletedTask;
        }

        public static int WaitMinutes(TimeSpan retryAfter) =>
            Math.Max(1, (int)Math.Ceiling(retryAfter.TotalMinutes));

        public static string BuildMessage(int minutes) =>
            $"Alcanzaste el límite de solicitudes de recuperación de contraseña. Intentá nuevamente en {minutes} {(minutes == 1 ? "minuto" : "minutos")}.";
    }
}
