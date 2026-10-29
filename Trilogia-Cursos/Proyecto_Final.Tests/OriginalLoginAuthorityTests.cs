using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Proyecto_FinalAPI.Controllers;
using Proyecto_FinalAPI.Models;
using Proyecto_FinalAPI.Services;
using MvcLogin = Proyecto_Final.Services.OriginalLoginService;

namespace Proyecto_Final.Tests;

public sealed class OriginalLoginAuthorityTests
{
    private static readonly byte[] Binding = Enumerable.Repeat((byte)42, 32).ToArray();
    private static ApiUser User() => new()
    {
        UsuarioId = 42, PerfilId = 7, Activo = true, PerfilActivo = true,
        SecurityStamp = Guid.NewGuid().ToString("D"), Correo = "synthetic@example.invalid", PerfilNombre = "Interno"
    };
    private static AuthController Controller(IAccountApiDbService database, IOriginalLoginIssuer? issuer)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["OriginalLogin:AuthorityEnabled"] = "true" }).Build();
        var cache = new MemoryCache(new MemoryCacheOptions());
        return new AuthController(database, new EmailService(config), new LoginAttemptLimiter(cache),
            new PasswordRecoveryAttemptLimiter(cache), config, NullLogger<AuthController>.Instance,
            Options.Create(new CompanyOptions()), issuer)
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
    }
    private static LoginApiRequest Request(string? binding = null) => new()
    { Email = "synthetic@example.invalid", Password = "<credential-input>", BindingHash = binding };

    [Fact] public async Task Issuance_uses_only_the_credential_verified_actor_and_binding()
    {
        var user = User(); var database = new Mock<IAccountApiDbService>(); var issuer = new Mock<IOriginalLoginIssuer>();
        var now = DateTime.UtcNow; var ticket = new OriginalLoginTicket(Guid.NewGuid(), now, now.AddHours(8));
        database.Setup(x => x.ValidateUserAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(user);
        issuer.Setup(x => x.IssueAsync(user, It.Is<byte[]>(x => x.SequenceEqual(Binding)), It.IsAny<CancellationToken>())).ReturnsAsync(ticket);
        var result = Assert.IsType<OkObjectResult>(await Controller(database.Object, issuer.Object).Login(Request(Convert.ToBase64String(Binding))));
        Assert.Same(ticket, Assert.IsType<AuthResult>(result.Value).OriginalLogin);
        issuer.Verify(x => x.IssueAsync(user, It.Is<byte[]>(x => x.SequenceEqual(Binding)), It.IsAny<CancellationToken>()), Times.Once);
    }
    [Fact] public async Task Wrong_credentials_cannot_issue_even_with_a_valid_binding()
    {
        var database = new Mock<IAccountApiDbService>(); var issuer = new Mock<IOriginalLoginIssuer>(MockBehavior.Strict);
        database.Setup(x => x.ValidateUserAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((ApiUser?)null);
        Assert.IsType<UnauthorizedObjectResult>(await Controller(database.Object, issuer.Object).Login(Request(Convert.ToBase64String(Binding))));
        issuer.VerifyNoOtherCalls();
    }
    [Theory] [InlineData("bad-base64")] [InlineData("")] [InlineData("AA==")]
    public async Task Malformed_binding_cannot_issue(string value)
    {
        var database = new Mock<IAccountApiDbService>(); var issuer = new Mock<IOriginalLoginIssuer>(MockBehavior.Strict);
        database.Setup(x => x.ValidateUserAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(User());
        Assert.IsType<BadRequestObjectResult>(await Controller(database.Object, issuer.Object).Login(Request(value)));
        issuer.VerifyNoOtherCalls();
    }
    [Fact] public async Task Missing_authority_does_not_fall_back_to_auth_SQL_credentials()
    {
        var database = new Mock<IAccountApiDbService>();
        database.Setup(x => x.ValidateUserAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(User());
        Assert.Equal(503, Assert.IsType<ObjectResult>(await Controller(database.Object, null).Login(Request(Convert.ToBase64String(Binding)))).StatusCode);
    }
    [Fact] public async Task Existing_API_consumers_can_authenticate_without_requesting_issuance()
    {
        var database = new Mock<IAccountApiDbService>(); var issuer = new Mock<IOriginalLoginIssuer>(MockBehavior.Strict);
        database.Setup(x => x.ValidateUserAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(User());
        var response = Assert.IsType<AuthResult>(Assert.IsType<OkObjectResult>(await Controller(database.Object, issuer.Object).Login(Request())).Value);
        Assert.True(response.Success); Assert.Null(response.OriginalLogin); issuer.VerifyNoOtherCalls();
    }
    [Theory] [InlineData(-1)] [InlineData(1)]
    public void Remote_ticket_cannot_change_the_absolute_lifetime(long ticks)
    {
        var response = Response(); response.OriginalLogin = response.OriginalLogin! with { ExpiresUtc = response.OriginalLogin.ExpiresUtc.AddTicks(ticks) };
        Assert.Throws<InvalidOperationException>(() => MvcLogin.AcceptRemoteTicket(response, Binding));
    }
    [Fact] public void Missing_remote_ticket_is_not_reconstructed_locally()
    {
        var response = Response(); response.OriginalLogin = null;
        Assert.Throws<InvalidOperationException>(() => MvcLogin.AcceptRemoteTicket(response, Binding));
    }
    [Fact] public async Task MVC_cannot_issue_when_remote_authority_is_required()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["OriginalLogin:RemoteAuthority"] = "true", ["ConnectionStrings:DefaultConnection"] = "offline-only" }).Build();
        await Assert.ThrowsAsync<InvalidOperationException>(() => new MvcLogin(config).CreateAsync(1, 1, Guid.NewGuid(), Binding));
    }
    private static Proyecto_Final.Models.AuthResult Response()
    {
        var now = DateTime.UtcNow;
        return new() { Success = true, UserId = 1, ProfileId = 2, SecurityStamp = Guid.NewGuid().ToString("D"),
            OriginalLogin = new(Guid.NewGuid(), now, now.AddHours(8)) };
    }
    [Fact] public void Authority_accepts_distinct_restricted_Auth_and_Issuer()
    {
        OriginalLoginAuthorityConfiguration.Validate(AuthorityConfiguration());
    }
    [Theory] [InlineData("same-identity")] [InlineData("Windows-owner")] [InlineData("other-target")] [InlineData("app-secret")]
    public void Authority_rejects_mixed_privileges_or_destinations(string mutation)
    {
        var configuration = AuthorityConfiguration();
        if (mutation == "same-identity") configuration["ConnectionStrings:OriginalLoginIssuer"] = configuration.GetConnectionString("DefaultConnection");
        if (mutation == "Windows-owner") configuration["ConnectionStrings:DefaultConnection"] = "Data Source=(localdb)\\Offline;Initial Catalog=Offline;Integrated Security=True";
        if (mutation == "other-target") configuration["ConnectionStrings:OriginalLoginIssuer"] = "Data Source=(localdb)\\Other;Initial Catalog=Offline;User ID=issuer;Password=<runtime-secret>";
        if (mutation == "app-secret") configuration["ConnectionStrings:ChatV2App"] = configuration.GetConnectionString("DefaultConnection");
        Assert.Throws<InvalidOperationException>(() => OriginalLoginAuthorityConfiguration.Validate(configuration));
    }
    private static IConfigurationRoot AuthorityConfiguration() => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
    {
        ["OriginalLogin:AuthorityEnabled"] = "true",
        ["ConnectionStrings:DefaultConnection"] = "Data Source=(localdb)\\Offline;Initial Catalog=Offline;User ID=auth;Password=<runtime-secret>",
        ["ConnectionStrings:OriginalLoginIssuer"] = "Data Source=(localdb)\\Offline;Initial Catalog=Offline;User ID=issuer;Password=<runtime-secret>"
    }).Build();
}
