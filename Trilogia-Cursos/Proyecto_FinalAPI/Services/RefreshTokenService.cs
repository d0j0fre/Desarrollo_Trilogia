using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Proyecto_FinalAPI.Models;
using Proyecto_FinalAPI.Options;

namespace Proyecto_FinalAPI.Services
{
    public sealed record IssuedRefreshToken(string Value, DateTimeOffset ExpiresAt, Guid ChainId);

    public enum RefreshOutcome
    {
        Ok,
        Reused,
        Invalid
    }

    public sealed record RefreshExchangeResult(
        RefreshOutcome Outcome,
        ApiUser? User,
        IssuedRefreshToken? RefreshToken);

    public interface IRefreshTokenService
    {
        Task<IssuedRefreshToken> IssueAsync(
            int userId,
            string? deviceId,
            string? deviceName,
            string? ipAddress,
            CancellationToken cancellationToken = default);

        Task<RefreshExchangeResult> ExchangeAsync(
            string presentedToken,
            string? ipAddress,
            CancellationToken cancellationToken = default);

        Task RevokeAsync(string presentedToken, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Emite y rota los tokens de refresco.
    ///
    /// El token viaja al cliente en claro una sola vez; en la base solo queda su
    /// SHA-256. Quien lea la tabla no puede suplantar a nadie.
    ///
    /// Cada canje invalida el token presentado y emite uno nuevo en la misma
    /// cadena. Si llega un token ya canjeado, el procedimiento revoca la cadena
    /// completa: es la señal clásica de que alguien copió el token de un
    /// dispositivo, y a partir de ahí ambos tienen que volver a autenticarse.
    /// </summary>
    public sealed class RefreshTokenService : IRefreshTokenService
    {
        private const int TokenBytes = 32;

        private readonly IMobileAuthDbService _db;
        private readonly JwtOptions _options;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<RefreshTokenService> _logger;

        public RefreshTokenService(
            IMobileAuthDbService db,
            IOptions<JwtOptions> options,
            TimeProvider timeProvider,
            ILogger<RefreshTokenService> logger)
        {
            _db = db;
            _options = options.Value;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task<IssuedRefreshToken> IssueAsync(
            int userId,
            string? deviceId,
            string? deviceName,
            string? ipAddress,
            CancellationToken cancellationToken = default)
        {
            var token = CreateToken();
            var expiresAt = _timeProvider.GetUtcNow().AddDays(_options.RefreshTokenDays);

            var chainId = await _db.IssueRefreshTokenAsync(
                new RefreshTokenIssueRequest(
                    userId,
                    Hash(token),
                    expiresAt.UtcDateTime,
                    ChainId: null,
                    deviceId,
                    deviceName,
                    ipAddress),
                cancellationToken);

            return new IssuedRefreshToken(token, expiresAt, chainId);
        }

        public async Task<RefreshExchangeResult> ExchangeAsync(
            string presentedToken,
            string? ipAddress,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(presentedToken))
            {
                return new RefreshExchangeResult(RefreshOutcome.Invalid, null, null);
            }

            var replacement = CreateToken();
            var expiresAt = _timeProvider.GetUtcNow().AddDays(_options.RefreshTokenDays);

            var row = await _db.ExchangeRefreshTokenAsync(
                new RefreshTokenExchangeRequest(
                    Hash(presentedToken),
                    Hash(replacement),
                    expiresAt.UtcDateTime,
                    ipAddress),
                cancellationToken);

            switch (row.Outcome)
            {
                case "Ok":
                    if (row.UserId is not { } userId || row.ChainId is not { } chainId)
                    {
                        return new RefreshExchangeResult(RefreshOutcome.Invalid, null, null);
                    }

                    var user = await _db.GetActiveUserAsync(userId, cancellationToken);
                    if (user is null)
                    {
                        // Carrera improbable: el usuario se desactivó entre el canje
                        // y la lectura. El token nuevo se corta antes de entregarlo.
                        await _db.RevokeChainAsync(Hash(replacement), "Usuario inactivo.", cancellationToken);
                        return new RefreshExchangeResult(RefreshOutcome.Invalid, null, null);
                    }

                    return new RefreshExchangeResult(
                        RefreshOutcome.Ok,
                        user,
                        new IssuedRefreshToken(replacement, expiresAt, chainId));

                case "Reutilizado":
                    _logger.LogWarning(
                        "Se presentó un token de refresco ya canjeado del usuario {UserId}. " +
                        "Se revocó la cadena completa.",
                        row.UserId);
                    return new RefreshExchangeResult(RefreshOutcome.Reused, null, null);

                default:
                    return new RefreshExchangeResult(RefreshOutcome.Invalid, null, null);
            }
        }

        public Task RevokeAsync(string presentedToken, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(presentedToken)) return Task.CompletedTask;
            return _db.RevokeChainAsync(Hash(presentedToken), "Cierre de sesión.", cancellationToken);
        }

        private static string CreateToken() =>
            Base64UrlEncode(RandomNumberGenerator.GetBytes(TokenBytes));

        // SHA-256 en hexadecimal mayúscula, que es lo que espera la columna
        // CHAR(64) de dbo.UsuarioTokensRefresco.
        internal static string Hash(string token) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

        private static string Base64UrlEncode(byte[] value) =>
            Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
