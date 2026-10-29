using System.Reflection;
using Proyecto_Final.Controllers;

namespace Proyecto_Final.Tests;

public sealed class RestrictedSqlGateContractTests
{
    private static bool ExpectedPermission(string role, string procedure)
    {
        var gate = typeof(AccountController).Assembly.GetType("DistribuidoraJJ.Security.RestrictedSqlGate", throwOnError: true)!;
        var method = gate.GetMethod("ExpectedPermission", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        return Assert.IsType<bool>(method!.Invoke(null, new object?[] { role, procedure }));
    }

    [Theory]
    [InlineData("App", true)]
    [InlineData("MvcApp", true)]
    [InlineData("Reader", false)]
    [InlineData("BrokerReader", false)]
    [InlineData("BrokerApp", false)]
    [InlineData("Auth", false)]
    [InlineData("Issuer", false)]
    [InlineData("ArchiveReader", false)]
    [InlineData("Pricing", false)]
    public void Original_login_validation_and_revocation_follow_the_APP_authority(string role, bool expected)
    {
        Assert.Equal(expected, ExpectedPermission(role, "dbo.sp_Stage2_ValidateOriginalLogin"));
        Assert.Equal(expected, ExpectedPermission(role, "dbo.sp_Stage2_RevokeOriginalLogin"));
    }

    [Theory]
    [InlineData("App", false, false)]
    [InlineData("MvcApp", false, false)]
    [InlineData("Reader", false, false)]
    [InlineData("BrokerReader", false, false)]
    [InlineData("BrokerApp", false, false)]
    [InlineData("Auth", false, true)]
    [InlineData("Issuer", true, false)]
    [InlineData("ArchiveReader", false, false)]
    [InlineData("Pricing", false, false)]
    public void Login_creation_and_credential_lookup_remain_separate(string role, bool create, bool credentialLookup)
    {
        Assert.Equal(create, ExpectedPermission(role, "dbo.sp_Stage2_CreateOriginalLogin"));
        Assert.Equal(credentialLookup, ExpectedPermission(role, "dbo.sp_Auth_GetLoginCredential"));
    }

    [Theory]
    [InlineData("App", false)]
    [InlineData("MvcApp", false)]
    [InlineData("Auth", false)]
    [InlineData("Issuer", false)]
    [InlineData("Reader", false)]
    [InlineData("BrokerReader", false)]
    [InlineData("ArchiveReader", false)]
    [InlineData("Pricing", false)]
    [InlineData("Unknown", false)]
    public void Unknown_procedures_fail_closed(string role, bool expected) =>
        Assert.Equal(expected, ExpectedPermission(role, "dbo.sp_Stage2_Unexpected"));
}
