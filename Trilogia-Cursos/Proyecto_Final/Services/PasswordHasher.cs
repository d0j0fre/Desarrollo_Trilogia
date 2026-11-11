using System.Security.Cryptography;

namespace Proyecto_Final.Services;

/// <summary>
/// Genera el hash PBKDF2 de una contraseña antes de enviarla a la base de datos. La web nunca envía
/// contraseñas en texto plano: el formato es idéntico al de la API (<c>pbkdf2-sha256$iter$sal$hash</c>),
/// que es quien verifica el inicio de sesión.
/// </summary>
public static class PasswordHasher
{
    private const int Iterations = 210_000;
    private const int SaltBytes = 16;
    private const int HashBytes = 32;
    private const string Prefix = "pbkdf2-sha256";

    public static string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashBytes);
        return $"{Prefix}${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }
}
