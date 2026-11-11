using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Proyecto_FinalAPI.Options;
using Proyecto_FinalAPI.Services;

namespace Proyecto_FinalAPI.Authorization
{
    /// <summary>
    /// Configura la validación del esquema Bearer resolviendo la clave de firma
    /// por inyección de dependencias, no construyendo un proveedor dentro del
    /// pipeline de validación.
    /// </summary>
    public sealed class ConfigureJwtBearerOptions : IConfigureNamedOptions<JwtBearerOptions>
    {
        private readonly JwtOptions _jwt;
        private readonly IJwtSigningKeyProvider _signingKey;
        private readonly IHostEnvironment _environment;

        public ConfigureJwtBearerOptions(
            IOptions<JwtOptions> jwt,
            IJwtSigningKeyProvider signingKey,
            IHostEnvironment environment)
        {
            _jwt = jwt.Value;
            _signingKey = signingKey;
            _environment = environment;
        }

        public void Configure(string? name, JwtBearerOptions options)
        {
            if (!string.Equals(name, JwtBearerDefaults.AuthenticationScheme, StringComparison.Ordinal))
            {
                return;
            }

            Configure(options);
        }

        public void Configure(JwtBearerOptions options)
        {
            // Los claims se usan tal como se emiten. Sin este flag, el handler
            // reescribe `sub` a la URI larga de WS-Federation y el código que lee
            // el identificador deja de encontrarlo.
            options.MapInboundClaims = false;
            options.RequireHttpsMetadata = !_environment.IsDevelopment();
            options.SaveToken = false;

            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = _jwt.Issuer,
                ValidateAudience = true,
                ValidAudience = _jwt.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = _signingKey.Key,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(_jwt.ClockSkewSeconds),
                RoleClaimType = ClaimTypes.Role,
                NameClaimType = ClaimTypes.Name,
                ValidAlgorithms = [SecurityAlgorithms.HmacSha256]
            };

            options.Events = new JwtBearerEvents
            {
                // Respuesta uniforme: el cliente nunca sabe por qué falló, solo
                // que tiene que renovar o volver a autenticarse.
                // Revocación de sesiones: aunque la firma y el vencimiento sean válidos, el token vale solo
                // mientras el usuario siga activo, con el mismo perfil y con el mismo sello de seguridad.
                OnTokenValidated = async context =>
                {
                    var services = context.HttpContext.RequestServices;
                    var result = await SessionStateValidator.ValidateAsync(
                        context.Principal ?? new ClaimsPrincipal(),
                        services.GetRequiredService<IMobileAuthDbService>(),
                        services.GetService<ILoggerFactory>()?.CreateLogger("Proyecto_FinalAPI.SessionValidation"),
                        context.HttpContext.RequestAborted);

                    if (result == SessionValidationResult.Revoked)
                    {
                        context.Fail(SessionStateValidator.RevokedFailure);
                    }
                    else if (result == SessionValidationResult.Unavailable)
                    {
                        context.Fail(SessionStateValidator.UnavailableFailure);
                    }
                },
                OnChallenge = context =>
                {
                    context.HandleResponse();
                    context.Response.ContentType = "application/json";

                    if (string.Equals(
                            context.AuthenticateFailure?.Message,
                            SessionStateValidator.UnavailableFailure,
                            StringComparison.Ordinal))
                    {
                        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                        return context.Response.WriteAsync("{\"error\":\"servicio_no_disponible\"}");
                    }

                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return context.Response.WriteAsync("{\"error\":\"no_autenticado\"}");
                },
                OnForbidden = context =>
                {
                    context.Response.ContentType = "application/json";
                    return context.Response.WriteAsync("{\"error\":\"permiso_insuficiente\"}");
                }
            };
        }
    }
}
