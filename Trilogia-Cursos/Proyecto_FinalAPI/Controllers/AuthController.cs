using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.SqlClient;
using Proyecto_FinalAPI.Models;
using Proyecto_FinalAPI.Services;

namespace Proyecto_FinalAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAccountApiDbService _accountApiDbService;
        private readonly EmailService _emailService;
        private readonly LoginAttemptLimiter _loginAttemptLimiter;
        private readonly PasswordRecoveryAttemptLimiter _passwordRecoveryAttemptLimiter;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthController> _logger;
        private readonly IJwtTokenService _jwtTokenService;
        private readonly IRefreshTokenService _refreshTokenService;
        private readonly IMobileAuthDbService _mobileAuthDbService;

        public AuthController(
            IAccountApiDbService accountApiDbService,
            EmailService emailService,
            LoginAttemptLimiter loginAttemptLimiter,
            PasswordRecoveryAttemptLimiter passwordRecoveryAttemptLimiter,
            IConfiguration configuration,
            ILogger<AuthController> logger,
            IJwtTokenService jwtTokenService,
            IRefreshTokenService refreshTokenService,
            IMobileAuthDbService mobileAuthDbService)
        {
            _accountApiDbService = accountApiDbService;
            _emailService = emailService;
            _loginAttemptLimiter = loginAttemptLimiter;
            _passwordRecoveryAttemptLimiter = passwordRecoveryAttemptLimiter;
            _configuration = configuration;
            _logger = logger;
            _jwtTokenService = jwtTokenService;
            _refreshTokenService = refreshTokenService;
            _mobileAuthDbService = mobileAuthDbService;
        }

        [HttpPost("login")]
        [EnableRateLimiting("authentication")]
        public async Task<IActionResult> Login([FromBody] LoginApiRequest request)
        {
            if (request == null ||
                string.IsNullOrWhiteSpace(request.Email) ||
                string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new AuthResult
                {
                    Success = false,
                    Message = "Debes indicar correo y contraseña."
                });
            }

            var email = request.Email.Trim();
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            if (_loginAttemptLimiter.IsBlocked(email, ipAddress, out var remainingTime))
            {
                return StatusCode(StatusCodes.Status429TooManyRequests, new AuthResult
                {
                    Success = false,
                    Message = $"Demasiados intentos fallidos. Intentá nuevamente en {Math.Ceiling(remainingTime.TotalMinutes)} minuto(s)."
                });
            }

            var user = await _accountApiDbService.ValidateUserAsync(email, request.Password);

            if (user == null)
            {
                _loginAttemptLimiter.RegisterFailedAttempt(email, ipAddress);

                return Unauthorized(new AuthResult
                {
                    Success = false,
                    Message = "Correo o contraseña incorrectos."
                });
            }

            _loginAttemptLimiter.Reset(email, ipAddress);

            var result = new AuthResult
            {
                Success = true,
                Message = "Inicio de sesión correcto.",
                UserId = user.UsuarioId,
                FullName = user.NombreCompleto,
                Email = user.Correo,
                Role = user.PerfilNombre
            };

            // Los tokens son estrictamente aditivos. Solo se emiten cuando el
            // cliente se identifica como dispositivo (la app móvil siempre manda
            // DeviceId, porque lo necesita para su cadena de refresco).
            // El MVC no manda ninguno: su respuesta y su flujo quedan idénticos,
            // y no se escribe una fila de token por cada inicio de sesión web.
            if (!string.IsNullOrWhiteSpace(request.DeviceId))
            {
                try
                {
                    if (!await IssueSessionTokensAsync(result, user, request.DeviceId, request.DeviceName, ipAddress))
                    {
                        // Credenciales correctas, pero el perfil no usa la
                        // aplicación (un cliente, o un perfil al que
                        // administración le quitó el acceso). Sin tokens no hay
                        // sesión a medias que después falle en cada pantalla.
                        return StatusCode(StatusCodes.Status403Forbidden, new AuthResult
                        {
                            Success = false,
                            Message = "Tu perfil no tiene acceso a la aplicación móvil. Podés usar el sitio web."
                        });
                    }
                }
                catch (SqlException exception)
                {
                    _logger.LogError(
                        exception,
                        "No fue posible emitir los tokens del dispositivo para el usuario {UserId}.",
                        user.UsuarioId);
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, new AuthResult
                    {
                        Success = false,
                        Message = "El servicio no está disponible en este momento. Intentá de nuevo."
                    });
                }
            }

            return Ok(result);
        }

        /// <summary>
        /// Canjea un token de refresco por un par nuevo. Rotativo: el token
        /// presentado queda inutilizable en el mismo momento.
        /// </summary>
        [HttpPost("refresh")]
        [EnableRateLimiting("authentication")]
        public async Task<IActionResult> Refresh([FromBody] RefreshTokenApiRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.RefreshToken))
            {
                return BadRequest(new AuthResult
                {
                    Success = false,
                    Message = "Falta el token de refresco."
                });
            }

            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

            RefreshExchangeResult exchange;
            try
            {
                exchange = await _refreshTokenService.ExchangeAsync(
                    request.RefreshToken.Trim(),
                    ipAddress,
                    HttpContext.RequestAborted);
            }
            catch (SqlException exception)
            {
                _logger.LogError(exception, "Falló el canje de un token de refresco.");
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new AuthResult
                {
                    Success = false,
                    Message = "El servicio no está disponible en este momento. Intentá de nuevo."
                });
            }

            // Reutilizado e inválido devuelven exactamente lo mismo al cliente:
            // distinguirlos le confirmaría a un atacante que el token robado era
            // legítimo. La diferencia queda en el log y en la cadena revocada.
            if (exchange.Outcome != RefreshOutcome.Ok || exchange.User is null || exchange.RefreshToken is null)
            {
                return Unauthorized(new AuthResult
                {
                    Success = false,
                    Message = "La sesión expiró. Iniciá sesión de nuevo."
                });
            }

            var user = exchange.User;
            var permissions = await _mobileAuthDbService.GetPermissionsForRoleAsync(
                user.PerfilNombre,
                HttpContext.RequestAborted);

            var access = _jwtTokenService.CreateAccessToken(
                user.UsuarioId,
                user.Correo,
                user.NombreCompleto,
                user.PerfilNombre,
                permissions);

            return Ok(new AuthResult
            {
                Success = true,
                Message = "Sesión renovada.",
                UserId = user.UsuarioId,
                FullName = user.NombreCompleto,
                Email = user.Correo,
                Role = user.PerfilNombre,
                AccessToken = access.Value,
                ExpiresAt = access.ExpiresAt,
                RefreshToken = exchange.RefreshToken.Value,
                RefreshTokenExpiresAt = exchange.RefreshToken.ExpiresAt,
                Permissions = permissions
            });
        }

        /// <summary>
        /// Revoca la cadena de refresco del dispositivo. Un token que solo se
        /// borra del teléfono sigue siendo válido: eso no es cerrar sesión.
        /// Siempre responde igual, exista o no el token.
        /// </summary>
        [HttpPost("logout")]
        [EnableRateLimiting("authentication")]
        public async Task<IActionResult> Logout([FromBody] RefreshTokenApiRequest request)
        {
            if (request != null && !string.IsNullOrWhiteSpace(request.RefreshToken))
            {
                await _refreshTokenService.RevokeAsync(request.RefreshToken.Trim(), HttpContext.RequestAborted);
            }

            return Ok(new AuthResult
            {
                Success = true,
                Message = "Sesión cerrada."
            });
        }

        /// <summary>
        /// Emite los tokens del dispositivo. Devuelve <c>false</c>, sin emitir
        /// nada, si el perfil no tiene permiso para usar la aplicación.
        /// </summary>
        private async Task<bool> IssueSessionTokensAsync(
            AuthResult result,
            ApiUser user,
            string? deviceId,
            string? deviceName,
            string ipAddress)
        {
            var permissions = await _mobileAuthDbService.GetPermissionsForRoleAsync(
                user.PerfilNombre,
                HttpContext.RequestAborted);

            var isAdministrator = string.Equals(user.PerfilNombre, "Administrador", StringComparison.OrdinalIgnoreCase);
            if (!isAdministrator && !permissions.Contains(MobilePermissions.Access, StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }

            var access = _jwtTokenService.CreateAccessToken(
                user.UsuarioId,
                user.Correo,
                user.NombreCompleto,
                user.PerfilNombre,
                permissions);

            var refresh = await _refreshTokenService.IssueAsync(
                user.UsuarioId,
                deviceId,
                deviceName,
                ipAddress,
                HttpContext.RequestAborted);

            result.AccessToken = access.Value;
            result.ExpiresAt = access.ExpiresAt;
            result.RefreshToken = refresh.Value;
            result.RefreshTokenExpiresAt = refresh.ExpiresAt;
            result.Permissions = permissions;
            return true;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterApiRequest request)
        {
            if (request == null ||
                string.IsNullOrWhiteSpace(request.FullName) ||
                string.IsNullOrWhiteSpace(request.Email) ||
                string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new AuthResult
                {
                    Success = false,
                    Message = "Los datos del registro son obligatorios."
                });
            }

            var emailExiste = await _accountApiDbService.EmailExistsAsync(request.Email);

            if (emailExiste)
            {
                return Conflict(new AuthResult
                {
                    Success = false,
                    Message = "Este correo ya está registrado."
                });
            }

            var registerModel = new Proyecto_FinalAPI.Models.RegisterRequest
            {
                FullName = request.FullName,
                Email = request.Email,
                Password = request.Password
            };

            await _accountApiDbService.RegisterClientAsync(registerModel);

            return Ok(new AuthResult
            {
                Success = true,
                Message = "Cuenta creada correctamente."
            });
        }

        [HttpPost("forgot-password")]
        [EnableRateLimiting("password-recovery")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordApiRequest request)
        {
            const string safeRecoveryMessage = "Si la solicitud es válida, se procesará la recuperación de contraseña.";

            if (request == null || string.IsNullOrWhiteSpace(request.Email))
            {
                return BadRequest(new AuthResult
                {
                    Success = false,
                    Message = "Debes indicar un correo."
                });
            }

            var email = request.Email.Trim();
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            if (_passwordRecoveryAttemptLimiter.IsForgotPasswordBlocked(email, ipAddress, out _))
            {
                return StatusCode(StatusCodes.Status429TooManyRequests, new AuthResult
                {
                    Success = false,
                    Message = "No fue posible procesar la solicitud en este momento. Intente nuevamente más tarde."
                });
            }

            _passwordRecoveryAttemptLimiter.RegisterForgotPasswordAttempt(email, ipAddress);

            var user = await _accountApiDbService.GetUserByEmailAsync(email);

            if (user == null)
            {
                return Ok(new AuthResult
                {
                    Success = true,
                    Message = safeRecoveryMessage
                });
            }

            var configuredBaseUrl = _configuration["PasswordRecovery:PublicBaseUrl"]?.TrimEnd('/');
            if (!Uri.TryCreate(configuredBaseUrl, UriKind.Absolute, out var publicBaseUri) ||
                !string.Equals(publicBaseUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("PasswordRecovery:PublicBaseUrl no está configurada con una URL HTTPS válida.");
                return Ok(new AuthResult { Success = true, Message = safeRecoveryMessage });
            }

            var token = await _accountApiDbService.CreatePasswordResetTokenAsync(user.UsuarioId);

            try
            {
                var resetUrl = $"{configuredBaseUrl}/Account/ResetPassword?token={Uri.EscapeDataString(token)}&email={Uri.EscapeDataString(user.Correo)}";

                var asunto = "Recuperación de contraseña - Licorera La Bodega";
                var contenido = EmailTemplateBuilder.BuildPasswordResetEmail(
                    user.NombreCompleto,
                    resetUrl);

                _emailService.SendEmail(user.Correo, asunto, contenido);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "No fue posible enviar el correo de recuperación de contraseña.");
                return Ok(new AuthResult
                {
                    Success = true,
                    Message = safeRecoveryMessage
                });
            }

            return Ok(new AuthResult
            {
                Success = true,
                Message = safeRecoveryMessage
            });
        }

        [HttpPost("reset-password")]
        [EnableRateLimiting("password-recovery")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordApiRequest request)
        {
            if (request == null ||
                string.IsNullOrWhiteSpace(request.Token) ||
                string.IsNullOrWhiteSpace(request.NewPassword))
            {
                return BadRequest(new AuthResult
                {
                    Success = false,
                    Message = "La solicitud de restablecimiento no es válida."
                });
            }

            var token = request.Token.Trim();
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            if (_passwordRecoveryAttemptLimiter.IsResetPasswordBlocked(token, ipAddress, out _))
            {
                return StatusCode(StatusCodes.Status429TooManyRequests, new AuthResult
                {
                    Success = false,
                    Message = "No fue posible procesar la solicitud en este momento. Intente nuevamente más tarde."
                });
            }

            _passwordRecoveryAttemptLimiter.RegisterResetPasswordAttempt(token, ipAddress);

            var tokenInfo = await _accountApiDbService.GetValidResetTokenAsync(token);

            if (tokenInfo == null)
            {
                return BadRequest(new AuthResult
                {
                    Success = false,
                    Message = "El enlace de recuperación no es válido o ya venció."
                });
            }

            await _accountApiDbService.ResetPasswordAsync(tokenInfo.UsuarioId, token, request.NewPassword);
            _passwordRecoveryAttemptLimiter.ResetResetPasswordAttempts(token, ipAddress);

            return Ok(new AuthResult
            {
                Success = true,
                Message = "La contraseña se actualizó correctamente."
            });
        }
    }

    public class LoginApiRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;

        // Opcionales. Solo los envía un cliente nativo; su presencia es lo que
        // hace que el login emita tokens. El MVC no los manda.
        public string? DeviceId { get; set; }
        public string? DeviceName { get; set; }
    }

    public class RefreshTokenApiRequest
    {
        public string RefreshToken { get; set; } = string.Empty;
    }

    public class RegisterApiRequest
    {
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class ForgotPasswordApiRequest
    {
        public string Email { get; set; } = string.Empty;
    }

    public class ResetPasswordApiRequest
    {
        public string Token { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }

    public class AuthResult
    {
        // ── Contrato original. El login del MVC depende de estos seis campos:
        //    no se renombran, no se quitan y no cambian de tipo. ──────────────
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int? UserId { get; set; }
        public string? FullName { get; set; }
        public string? Email { get; set; }
        public string? Role { get; set; }

        // ── Agregados para la aplicación móvil. Nulos cuando el cliente no es
        //    un dispositivo; el MVC simplemente los ignora. ──────────────────
        public string? AccessToken { get; set; }
        public DateTimeOffset? ExpiresAt { get; set; }
        public string? RefreshToken { get; set; }
        public DateTimeOffset? RefreshTokenExpiresAt { get; set; }
        public IReadOnlyList<string>? Permissions { get; set; }
    }
}
