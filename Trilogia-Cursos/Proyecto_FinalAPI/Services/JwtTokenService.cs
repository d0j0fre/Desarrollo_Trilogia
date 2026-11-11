using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Proyecto_FinalAPI.Options;

namespace Proyecto_FinalAPI.Services
{
    public sealed record IssuedAccessToken(string Value, DateTimeOffset ExpiresAt);

    public interface IJwtTokenService
    {
        IssuedAccessToken CreateAccessToken(
            int userId,
            string email,
            string fullName,
            string role,
            IReadOnlyCollection<string> permissions,
            string? securityStamp = null);
    }

    /// <summary>
    /// Emite el access token de vida corta que acompaña cada request de la app.
    ///
    /// Los códigos de permiso viajan como claims <c>perm</c> para que la
    /// aplicación decida qué dibujar sin una consulta extra. No son autorización:
    /// cada endpoint revalida contra PerfilPermisos. Si a alguien se le revoca un
    /// permiso, el efecto es inmediato aunque su token siga vigente.
    /// </summary>
    public sealed class JwtTokenService : IJwtTokenService
    {
        public const string PermissionClaimType = "perm";

        /// <summary>
        /// Sello de seguridad del usuario al emitir el token. La API compara este claim con el sello vigente en cada
        /// request: si el usuario se inactivó, cambió de perfil o de contraseña, el token deja de valer aunque no haya vencido.
        /// </summary>
        public const string SecurityStampClaimType = "stamp";

        private readonly JwtOptions _options;
        private readonly IJwtSigningKeyProvider _signingKey;
        private readonly TimeProvider _timeProvider;

        public JwtTokenService(
            IOptions<JwtOptions> options,
            IJwtSigningKeyProvider signingKey,
            TimeProvider timeProvider)
        {
            _options = options.Value;
            _signingKey = signingKey;
            _timeProvider = timeProvider;
        }

        public IssuedAccessToken CreateAccessToken(
            int userId,
            string email,
            string fullName,
            string role,
            IReadOnlyCollection<string> permissions,
            string? securityStamp = null)
        {
            if (userId <= 0) throw new ArgumentOutOfRangeException(nameof(userId));

            var issuedAt = _timeProvider.GetUtcNow();
            var expiresAt = issuedAt.AddMinutes(_options.AccessTokenMinutes);

            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
                new(JwtRegisteredClaimNames.Email, email ?? string.Empty),
                new(ClaimTypes.NameIdentifier, userId.ToString()),
                new(ClaimTypes.Name, fullName ?? string.Empty),
                new(ClaimTypes.Role, role ?? string.Empty)
            };

            if (!string.IsNullOrWhiteSpace(securityStamp))
            {
                claims.Add(new Claim(SecurityStampClaimType, securityStamp));
            }

            foreach (var permission in permissions ?? Array.Empty<string>())
            {
                if (!string.IsNullOrWhiteSpace(permission))
                {
                    claims.Add(new Claim(PermissionClaimType, permission));
                }
            }

            var token = new JwtSecurityToken(
                issuer: _options.Issuer,
                audience: _options.Audience,
                claims: claims,
                notBefore: issuedAt.UtcDateTime,
                expires: expiresAt.UtcDateTime,
                signingCredentials: new SigningCredentials(_signingKey.Key, SecurityAlgorithms.HmacSha256));

            var value = new JwtSecurityTokenHandler().WriteToken(token);
            return new IssuedAccessToken(value, expiresAt);
        }
    }
}
