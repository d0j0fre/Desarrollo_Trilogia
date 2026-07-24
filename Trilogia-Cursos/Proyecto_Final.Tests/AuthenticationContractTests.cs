using System.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Moq;
using Proyecto_FinalAPI.Controllers;
using Proyecto_FinalAPI.Models;
using Proyecto_FinalAPI.Services;

namespace Proyecto_Final.Tests;

public sealed class AuthenticationContractTests
{
    [Fact]
    public void ValidateCommand_UsesExplicitContractAndPreservesPasswordWhitespace()
    {
        using var connection = new SqlConnection();
        using var command = AccountApiDbService.CreateValidateUserCommand(
            connection,
            "  admin@example.com  ",
            " password with spaces ");

        Assert.Equal("dbo.sp_Auth_ValidateUser", command.CommandText);
        Assert.Equal(CommandType.StoredProcedure, command.CommandType);

        var email = Assert.IsType<SqlParameter>(command.Parameters["@Correo"]);
        Assert.Equal(SqlDbType.NVarChar, email.SqlDbType);
        Assert.Equal(150, email.Size);
        Assert.Equal("admin@example.com", email.Value);

        var password = Assert.IsType<SqlParameter>(command.Parameters["@Contrasena"]);
        Assert.Equal(SqlDbType.NVarChar, password.SqlDbType);
        Assert.Equal(255, password.Size);
        Assert.Equal(" password with spaces ", password.Value);
    }

    [Fact]
    public void AuthenticatedUserMapping_UsesColumnNamesInsteadOfPositions()
    {
        var table = new DataTable();
        table.Columns.Add("PerfilNombre", typeof(string));
        table.Columns.Add("Activo", typeof(bool));
        table.Columns.Add("Correo", typeof(string));
        table.Columns.Add("UsuarioId", typeof(int));
        table.Columns.Add("NombreCompleto", typeof(string));
        table.Rows.Add("Administrador", true, "admin@example.com", 42, "Admin de prueba");

        using var reader = table.CreateDataReader();
        Assert.True(reader.Read());
        var user = AccountApiDbService.MapAuthenticatedUser(reader);

        Assert.Equal(42, user.UsuarioId);
        Assert.Equal("Admin de prueba", user.NombreCompleto);
        Assert.Equal("admin@example.com", user.Correo);
        Assert.Equal("Administrador", user.PerfilNombre);
        Assert.True(user.Activo);
    }

    [Fact]
    public async Task Login_ForwardsPasswordUnchangedAndReturnsAdministrator()
    {
        const string password = " value with spaces ";
        string? forwardedPassword = null;
        var database = new Mock<IAccountApiDbService>();
        database
            .Setup(service => service.ValidateUserAsync(
                "admin@example.com",
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, value, _) => forwardedPassword = value)
            .ReturnsAsync(new ApiUser
            {
                UsuarioId = 42,
                NombreCompleto = "Admin de prueba",
                Correo = "admin@example.com",
                PerfilNombre = "Administrador",
                Activo = true
            });
        var controller = CreateController(database.Object);

        var result = await controller.Login(new LoginApiRequest
        {
            Email = "admin@example.com",
            Password = password
        });

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<Proyecto_FinalAPI.Controllers.AuthResult>(ok.Value);
        Assert.True(response.Success);
        Assert.Equal("Administrador", response.Role);
        Assert.Equal(password, forwardedPassword);
    }

    [Fact]
    public async Task Login_InvalidCredentials_ReturnsGenericUnauthorizedResponse()
    {
        var database = new Mock<IAccountApiDbService>();
        database
            .Setup(service => service.ValidateUserAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ApiUser?)null);
        var controller = CreateController(database.Object);

        var result = await controller.Login(new LoginApiRequest
        {
            Email = "unknown@example.com",
            Password = "invalid"
        });

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        var response = Assert.IsType<Proyecto_FinalAPI.Controllers.AuthResult>(unauthorized.Value);
        Assert.False(response.Success);
        Assert.Equal("Correo o contraseña incorrectos.", response.Message);
    }

    [Fact]
    public void LoginAttemptLimiter_BlocksAfterFiveFailuresAndCanReset()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var limiter = new LoginAttemptLimiter(cache);

        for (var attempt = 0; attempt < 5; attempt++)
            limiter.RegisterFailedAttempt("admin@example.com", "127.0.0.1");

        Assert.True(limiter.IsBlocked("admin@example.com", "127.0.0.1", out var remaining));
        Assert.True(remaining > TimeSpan.Zero);

        limiter.Reset("admin@example.com", "127.0.0.1");
        Assert.False(limiter.IsBlocked("admin@example.com", "127.0.0.1", out _));
    }

    private static AuthController CreateController(IAccountApiDbService database)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var cache = new MemoryCache(new MemoryCacheOptions());
        return new AuthController(
            database,
            new EmailService(configuration),
            new LoginAttemptLimiter(cache),
            new PasswordRecoveryAttemptLimiter(cache))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }
}
