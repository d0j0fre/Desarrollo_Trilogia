using System.ComponentModel.DataAnnotations;

namespace Proyecto_FinalAPI.Options
{
    // Configuracion de emision y validacion de tokens para la aplicacion movil.
    // La clave de firma NUNCA llega por appsettings versionado: en local viene de
    // User Secrets y en Azure de App Service Configuration (Jwt__SigningKey).
    public sealed class JwtOptions
    {
        public const string SectionName = "Jwt";

        // Minimo de bytes de la clave HMAC-SHA256. Menos que esto y la firma es
        // mas debil que el algoritmo que la usa.
        public const int MinimumSigningKeyBytes = 32;

        [Required(AllowEmptyStrings = false)]
        public string Issuer { get; set; } = string.Empty;

        [Required(AllowEmptyStrings = false)]
        public string Audience { get; set; } = string.Empty;

        // Vacia solo se tolera en Development, donde se genera una clave efimera
        // por proceso. Ver JwtSigningKeyProvider.
        public string SigningKey { get; set; } = string.Empty;

        [Range(5, 240)]
        public int AccessTokenMinutes { get; set; } = 30;

        [Range(1, 90)]
        public int RefreshTokenDays { get; set; } = 14;

        // Tolerancia de reloj al validar la expiracion. Los telefonos derivan.
        [Range(0, 300)]
        public int ClockSkewSeconds { get; set; } = 30;
    }
}
