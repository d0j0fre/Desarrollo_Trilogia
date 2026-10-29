using Proyecto_FinalAPI.Models;
using Proyecto_FinalAPI.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Proyecto_FinalAPI.Controllers;

namespace Proyecto_Final.Tests;

public sealed class PasswordResetConsumptionTests
{
    [Theory]
    [InlineData(false, 42, 1, "opaque", true)]
    [InlineData(true, 42, 1, "opaque", false)]
    [InlineData(false, 43, 1, "opaque", false)]
    [InlineData(false, 42, 0, "opaque", false)]
    [InlineData(false, 42, -1, "opaque", false)]
    [InlineData(false, 42, 1, "other", false)]
    [InlineData(false, 42, 1, "OPAQUE", false)]
    public void TokenMustStillBelongToTheUserAndBeUnusedAndUnexpired(
        bool used, int owner, int remainingSeconds, string token, bool expected)
    {
        var now = new DateTime(2026, 10, 16, 12, 0, 0);
        var info = new PasswordResetTokenInfo
        {
            UsuarioId = owner, Token = token, Usado = used,
            FechaExpiracion = now.AddSeconds(remainingSeconds)
        };
        Assert.Equal(expected, AccountApiDbService.IsResetTokenUsable(info, 42, "opaque", now));
    }

    [Fact]
    public void MissingTokenCannotAuthorizeAWrite() => Assert.False(
        AccountApiDbService.IsResetTokenUsable(null, 42, "opaque", new DateTime(2026, 10, 16)));

    [Fact]
    public void AccountLockIsBoundedTransactionalAndContainsNoCredential()
    {
        using var connection = new Microsoft.Data.SqlClient.SqlConnection();
        using var command = AccountApiDbService.CreatePasswordResetLockCommand(connection, null, 42);
        Assert.Contains("@LockOwner=N'Transaction'", command.CommandText);
        Assert.Contains("@LockTimeout=5000", command.CommandText);
        Assert.Equal("Stage2:PasswordReset:User:42", command.Parameters["@LockResource"].Value);
        Assert.Single(command.Parameters.Cast<Microsoft.Data.SqlClient.SqlParameter>());
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AccountApiDbService.CreatePasswordResetLockCommand(connection, null, 0));
    }

    [Fact]
    public async Task TokenConsumedAfterControllerLookupReturnsAnOpaqueBadRequest()
    {
        const string tokenValue = "opaque";
        var candidate = string.Concat("Valid-", "Recovery-", "Password1!");
        var database = new Mock<IAccountApiDbService>(MockBehavior.Strict);
        database.Setup(x => x.GetValidResetTokenAsync(tokenValue))
            .ReturnsAsync(new PasswordResetTokenInfo { UsuarioId = 42 });
        database.Setup(x => x.ResetPasswordAsync(42, tokenValue, candidate))
            .ThrowsAsync(new InvalidPasswordResetTokenException());
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var configuration = new ConfigurationBuilder().Build();
        var controller = new AuthController(database.Object, new EmailService(configuration),
            new LoginAttemptLimiter(cache), new PasswordRecoveryAttemptLimiter(cache),
            configuration, NullLogger<AuthController>.Instance, Options.Create(new CompanyOptions()))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var result = Assert.IsType<BadRequestObjectResult>(await controller.ResetPassword(
            new ResetPasswordApiRequest { Token = tokenValue, NewPassword = candidate }));
        var response = Assert.IsType<AuthResult>(result.Value);
        Assert.False(response.Success);
        Assert.Equal("El enlace de recuperación no es válido o ya venció.", response.Message);
        database.VerifyAll();
    }
}
