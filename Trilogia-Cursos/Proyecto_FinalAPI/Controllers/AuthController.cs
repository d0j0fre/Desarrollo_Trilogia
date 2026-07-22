using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Proyecto_FinalAPI.Models;
using Proyecto_FinalAPI.Services;

namespace Proyecto_FinalAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AccountApiDbService _accountApiDbService;
        private readonly EmailService _emailService;
        private readonly LoginAttemptLimiter _loginAttemptLimiter;
        private readonly PasswordRecoveryAttemptLimiter _passwordRecoveryAttemptLimiter;

        public AuthController(
            AccountApiDbService accountApiDbService,
            EmailService emailService,
            LoginAttemptLimiter loginAttemptLimiter,
            PasswordRecoveryAttemptLimiter passwordRecoveryAttemptLimiter)
        {
            _accountApiDbService = accountApiDbService;
            _emailService = emailService;
            _loginAttemptLimiter = loginAttemptLimiter;
            _passwordRecoveryAttemptLimiter = passwordRecoveryAttemptLimiter;
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

            return Ok(new AuthResult
            {
                Success = true,
                Message = "Inicio de sesión correcto.",
                UserId = user.UsuarioId,
                FullName = user.NombreCompleto,
                Email = user.Correo,
                Role = user.PerfilNombre
            });
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

            var token = await _accountApiDbService.CreatePasswordResetTokenAsync(user.UsuarioId);

            try
            {
                var baseUrl = "https://localhost:7013";
                var resetUrl = $"{baseUrl}/Account/ResetPassword?token={token}&email={Uri.EscapeDataString(user.Correo)}";

                var asunto = "Recuperación de contraseña - Licorera La Bodega";
                var contenido = EmailTemplateBuilder.BuildPasswordResetEmail(
                    user.NombreCompleto,
                    resetUrl);

                _emailService.SendEmail(user.Correo, asunto, contenido);
            }
            catch
            {
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
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int? UserId { get; set; }
        public string? FullName { get; set; }
        public string? Email { get; set; }
        public string? Role { get; set; }
    }
}
