using System.Security.Cryptography;
using System.Text;
using Proyecto_FinalAPI.Services;

namespace Proyecto_Final.Tests;

// Seguridad / credenciales solo con hash (migración 0310).
public sealed class HashOnlyCredentialTests
{
    // Vector generado por dbo.fn_Auth_HashTransicion en SQL Server: el mismo cálculo que el C# debe verificar.
    private const string TransitionalVector =
        "legacy-sha256$00112233445566778899AABBCCDDEEFF$D0DCC291BE726E814236E7C16667122BB8F711470D8130C58B34632E7DC2E770";
    private const string VectorInput = "Prueba-Unitaria-ñ!1";

    private static readonly PasswordHashService Service = new();

    [Fact]
    public void TransitionalHash_GeneratedBySqlServerVerifiesInCSharp()
    {
        Assert.True(Service.Verify(VectorInput, TransitionalVector));
        Assert.False(Service.Verify("otra clave", TransitionalVector));
        Assert.False(Service.Verify(VectorInput.ToUpperInvariant(), TransitionalVector));
    }

    [Fact]
    public void TransitionalHash_MatchesTheDocumentedAlgorithm()
    {
        var salt = Convert.FromHexString("00112233445566778899AABBCCDDEEFF");
        var hash = SHA256.HashData([.. salt, .. Encoding.Unicode.GetBytes("abc")]);
        var encoded = $"legacy-sha256${Convert.ToHexString(salt)}${Convert.ToHexString(hash)}";

        Assert.Equal("legacy-sha256$00112233445566778899AABBCCDDEEFF$2EEF3D270EDB192145EE8B16F7FF53EA10FA1B4CDAA1A4C1807EFDB0A2DCBADA", encoded);
        Assert.True(Service.Verify("abc", encoded));
    }

    [Theory]
    [InlineData("legacy-sha256$ZZ$ZZ")]
    [InlineData("legacy-sha256$0011$2EEF")]
    [InlineData("legacy-sha256$00112233445566778899AABBCCDDEEFF")]
    [InlineData("legacy-sha256")]
    public void TransitionalHash_MalformedValuesNeverVerify(string encoded)
    {
        Assert.False(Service.Verify("abc", encoded));
    }

    [Fact]
    public void PlainTextIsNeverAValidStoredCredential()
    {
        // Un valor que no es hash (texto plano heredado) no verifica ni siquiera contra sí mismo.
        Assert.False(Service.Verify("texto-plano-heredado", "texto-plano-heredado"));
        Assert.False(Service.Verify("abc", string.Empty));
    }

    [Fact]
    public void NeedsRehash_IsTrueOnlyForNonCurrentHashes()
    {
        Assert.True(Service.NeedsRehash(TransitionalVector));
        Assert.False(Service.NeedsRehash(Service.Hash("una clave segura 1")));
        Assert.False(Service.NeedsRehash(string.Empty));
    }

    [Fact]
    public void Hash_ProducesPbkdf2WithSaltAndWebHasherIsCompatible()
    {
        var api = Service.Hash("una clave segura 1");
        var web = Proyecto_Final.Services.PasswordHasher.Hash("una clave segura 1");

        Assert.StartsWith("pbkdf2-sha256$", api, StringComparison.Ordinal);
        Assert.NotEqual(api, web);
        // El hash que genera la web lo verifica la API (quien autentica).
        Assert.True(Service.Verify("una clave segura 1", web));
        Assert.False(Service.NeedsRehash(web));
    }

    [Fact]
    public void WebServices_NeverSendPlainPasswordsToClientProcedures()
    {
        var admin = File.ReadAllText(SourcePath("Proyecto_Final", "Services", "AdminDbService.cs"));
        var accounts = File.ReadAllText(SourcePath("Proyecto_Final", "Services", "AccountDbService.cs"));

        Assert.DoesNotContain("\"@Contrasena\"", admin, StringComparison.Ordinal);
        Assert.Contains("\"@ContrasenaHash\"", admin, StringComparison.Ordinal);
        Assert.DoesNotContain("u.Contrasena", accounts, StringComparison.Ordinal);
        Assert.DoesNotContain("ValidateUserAsync", accounts, StringComparison.Ordinal);
        Assert.DoesNotContain("RegisterClientAsync", accounts, StringComparison.Ordinal);
    }

    [Fact]
    public void Migration0310_RemovesPlainTextFromEveryAuthenticationPath()
    {
        var sql = File.ReadAllText(Path.Combine(SourcePath("database", "migrations"), "0310_credenciales_solo_hash.sql"));

        Assert.Contains("CREATE OR ALTER TRIGGER dbo.tr_Usuarios_SinTextoPlano", sql, StringComparison.Ordinal);
        Assert.Contains("fn_Auth_HashTransicion", sql, StringComparison.Ordinal);
        // El inicio de sesión ya no compara la contraseña recibida con una columna.
        Assert.DoesNotContain("u.Contrasena = @Contrasena", sql, StringComparison.Ordinal);
        Assert.Contains("THROW 58110", sql, StringComparison.Ordinal);
    }

    private static string SourcePath(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Proyecto_Final.slnx")))
            directory = directory.Parent;
        var root = directory?.FullName ?? throw new DirectoryNotFoundException("No se encontró la raíz del repositorio.");
        return Path.Combine([root, .. parts]);
    }
}
