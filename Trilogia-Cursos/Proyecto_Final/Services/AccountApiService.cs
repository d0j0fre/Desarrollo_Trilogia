using System.Net;
using System.Net.Http.Json;
using Proyecto_Final.Models;

namespace Proyecto_Final.Services
{
    public class AccountApiService
    {
        private readonly HttpClient _httpClient;
        public bool RemoteOriginalLogin { get; }

        public AccountApiService(HttpClient httpClient, IConfiguration? configuration = null)
        {
            _httpClient = httpClient;
            RemoteOriginalLogin = configuration?.GetValue<bool>("OriginalLogin:RemoteAuthority") == true;
        }

        public async Task<AuthResult> LoginAsync(LoginViewModel model, byte[]? bindingHash = null)
        {
            if (RemoteOriginalLogin && (bindingHash is null || bindingHash.Length != 32))
                throw new InvalidOperationException("Falta el vínculo de sesión de MVC.");
            using var response = await _httpClient.PostAsJsonAsync("api/auth/login", new
            {
                Email = model.Email, Password = model.Password,
                BindingHash = RemoteOriginalLogin ? Convert.ToBase64String(bindingHash!) : null
            });

            if (response.StatusCode == HttpStatusCode.Unauthorized)
                return new AuthResult { Success = false, Message = "Correo o contraseña incorrectos." };

            if (!response.IsSuccessStatusCode)
                return new AuthResult { Success = false, Message = "No fue posible iniciar sesión en este momento." };

            var result = await response.Content.ReadFromJsonAsync<AuthResult>();
            return result ?? new AuthResult { Success = false, Message = "Respuesta inválida del API." };
        }

        public async Task<AuthResult> RegisterAsync(RegistroViewModel model)
        {
            var response = await _httpClient.PostAsJsonAsync("api/auth/register", new { FullName = model.FullName, Email = model.Email, Password = model.Password });

            if (response.StatusCode == HttpStatusCode.Conflict)
                return new AuthResult { Success = false, Message = "Este correo ya está registrado." };

            if (!response.IsSuccessStatusCode)
                return new AuthResult { Success = false, Message = "No fue posible registrar la cuenta en este momento." };

            var result = await response.Content.ReadFromJsonAsync<AuthResult>();
            return result ?? new AuthResult { Success = false, Message = "Respuesta inválida del API." };
        }

        public async Task<AuthResult> ForgotPasswordAsync(ForgotPasswordViewModel model)
        {
            var response = await _httpClient.PostAsJsonAsync("api/auth/forgot-password", new { Email = model.Email });
            if (!response.IsSuccessStatusCode)
                return new AuthResult { Success = false, Message = "No fue posible procesar la recuperación en este momento." };

            var result = await response.Content.ReadFromJsonAsync<AuthResult>();
            return result ?? new AuthResult { Success = true, Message = "Si el correo existe, se enviará un enlace de recuperación." };
        }

        public async Task<AuthResult> ResetPasswordAsync(ResetPasswordViewModel model)
        {
            var response = await _httpClient.PostAsJsonAsync("api/auth/reset-password", new { Token = model.Token, NewPassword = model.Password });
            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                var bad = await response.Content.ReadFromJsonAsync<AuthResult>();
                return bad ?? new AuthResult { Success = false, Message = "El enlace de recuperación no es válido o ya venció." };
            }

            if (!response.IsSuccessStatusCode)
                return new AuthResult { Success = false, Message = "No fue posible actualizar la contraseña." };

            var result = await response.Content.ReadFromJsonAsync<AuthResult>();
            return result ?? new AuthResult { Success = false, Message = "Respuesta inválida del API." };
        }
    }
}
