using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Proyecto_FinalAPI.Options;

namespace Proyecto_FinalAPI.Services
{
    public interface IJwtSigningKeyProvider
    {
        SymmetricSecurityKey Key { get; }
    }

    /// <summary>
    /// Resuelve la clave de firma una sola vez al arrancar.
    ///
    /// Fuera de Development la clave es obligatoria: si falta o es demasiado
    /// corta, la API no arranca. Una clave por defecto en produccion es peor que
    /// no tener autenticacion, porque parece segura.
    ///
    /// En Development, si no hay clave configurada se genera una aleatoria por
    /// proceso. Un companero puede correr la API sin configurar nada; los tokens
    /// dejan de servir al reiniciar, que es exactamente lo que se quiere que
    /// pase en una maquina de desarrollo.
    /// </summary>
    public sealed class JwtSigningKeyProvider : IJwtSigningKeyProvider
    {
        public JwtSigningKeyProvider(
            IOptions<JwtOptions> options,
            IHostEnvironment environment,
            ILogger<JwtSigningKeyProvider> logger)
        {
            var configured = options.Value.SigningKey;

            if (!string.IsNullOrWhiteSpace(configured))
            {
                var bytes = Encoding.UTF8.GetBytes(configured);
                if (bytes.Length < JwtOptions.MinimumSigningKeyBytes)
                {
                    throw new InvalidOperationException(
                        $"Jwt:SigningKey debe tener al menos {JwtOptions.MinimumSigningKeyBytes} bytes " +
                        $"({JwtOptions.MinimumSigningKeyBytes} caracteres ASCII). " +
                        "Generá una con: openssl rand -base64 48");
                }

                Key = new SymmetricSecurityKey(bytes);
                return;
            }

            if (!environment.IsDevelopment())
            {
                throw new InvalidOperationException(
                    "Falta la configuración Jwt:SigningKey. En Azure se define como " +
                    "variable de aplicación Jwt__SigningKey en App Service Configuration; " +
                    "nunca en un archivo versionado.");
            }

            var ephemeral = RandomNumberGenerator.GetBytes(JwtOptions.MinimumSigningKeyBytes);
            Key = new SymmetricSecurityKey(ephemeral);

            logger.LogWarning(
                "No hay Jwt:SigningKey configurada. Se generó una clave efímera para este proceso de " +
                "desarrollo: los tokens dejarán de ser válidos al reiniciar la API. Para una clave " +
                "estable: dotnet user-secrets set \"Jwt:SigningKey\" \"<48 bytes en base64>\" " +
                "--project Proyecto_FinalAPI");
        }

        public SymmetricSecurityKey Key { get; }
    }
}
