using System.Security.Cryptography;

namespace Proyecto_FinalAPI.Services;

public interface IPasswordHashService
{
    string Hash(string password);
    bool Verify(string password, string encodedHash);

    /// <summary>
    /// Verdadero si el hash guardado no es PBKDF2 actual (por ejemplo el hash con sal de transición que la
    /// migración 0310 genera para las claves que antes estaban en texto plano) y conviene reemplazarlo
    /// después de una verificación correcta.
    /// </summary>
    bool NeedsRehash(string encodedHash);
}

public sealed class PasswordHashService : IPasswordHashService
{
    private const int Iterations = 210_000;
    private const int SaltBytes = 16;
    private const int HashBytes = 32;
    private const string Prefix = "pbkdf2-sha256";
    // Formato de transición de la migración 0310: legacy-sha256$<sal hex>$<SHA-256(sal + contraseña UTF-16LE) hex>.
    private const string LegacyPrefix = "legacy-sha256";

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashBytes);
        return $"{Prefix}${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public bool Verify(string password, string encodedHash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrWhiteSpace(encodedHash))
            return false;

        var parts = encodedHash.Split('$');
        if (parts.Length == 3 && string.Equals(parts[0], LegacyPrefix, StringComparison.Ordinal))
            return VerifyTransitional(password, parts[1], parts[2]);

        if (parts.Length != 4 || !string.Equals(parts[0], Prefix, StringComparison.Ordinal) ||
            !int.TryParse(parts[1], out var iterations) || iterations < 100_000 || iterations > 1_000_000)
            return false;

        try
        {
            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);
            if (salt.Length < SaltBytes || expected.Length != HashBytes)
                return false;

            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public bool NeedsRehash(string encodedHash)
    {
        if (string.IsNullOrWhiteSpace(encodedHash)) return false;
        var parts = encodedHash.Split('$');
        return !(parts.Length == 4
            && string.Equals(parts[0], Prefix, StringComparison.Ordinal)
            && int.TryParse(parts[1], out var iterations)
            && iterations >= Iterations);
    }

    private static bool VerifyTransitional(string password, string saltHex, string hashHex)
    {
        try
        {
            var salt = Convert.FromHexString(saltHex);
            var expected = Convert.FromHexString(hashHex);
            if (salt.Length != SaltBytes || expected.Length != HashBytes)
                return false;

            var passwordBytes = System.Text.Encoding.Unicode.GetBytes(password);
            var actual = SHA256.HashData([.. salt, .. passwordBytes]);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
