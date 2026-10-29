using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Proyecto_Final.Services;
using Proyecto_Final.Services.ChatV2;

namespace Proyecto_Final.Tests;

public sealed class PersonalChatV2TransportTests
{
    private static OriginalLoginContext Login()
    {
        var issued = DateTime.UtcNow.AddMinutes(-1);
        return new(Guid.NewGuid(), 42, 7, Guid.NewGuid(), issued, issued.AddHours(8), RandomNumberGenerator.GetBytes(32));
    }
    private static ChatV2LiveIdentity Actor(OriginalLoginContext l) => new(l.LoginId, l.LoginId, l.UserId, l.ProfileId,
        ChatV2Kind.Internal, l.SecurityStamp, 3, new(l.ExpiresUtc), ChatV2Capabilities.InternalUse);
    private static HttpRequest Request(string? origin = null)
    {
        var request = new DefaultHttpContext().Request; request.Scheme = "https"; request.Host = new("localhost:7013");
        if (origin is not null) request.Headers.Origin = origin;
        return request;
    }
    [Fact] public async Task Authentication_before_original_session_validation_caches_a_denial_for_that_request()
    {
        var services = new ServiceCollection(); services.AddLogging(); services.AddSingleton(TimeProvider.System);
        services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, PersonalChatV2Authentication>(PersonalChatV2Authentication.SchemeName, _ => { });
        await using var provider = services.BuildServiceProvider(); using var scope = provider.CreateScope();
        var context = Request().HttpContext; context.RequestServices = scope.ServiceProvider;
        Assert.False((await context.AuthenticateAsync(PersonalChatV2Authentication.SchemeName)).Succeeded);
        context.Items[OriginalLoginSession.ValidatedContextItem] = Login();
        Assert.False((await context.AuthenticateAsync(PersonalChatV2Authentication.SchemeName)).Succeeded);
    }
    [Fact] public async Task Authentication_after_original_session_validation_accepts_only_that_canonical_login()
    {
        var services = new ServiceCollection(); services.AddLogging(); services.AddSingleton(TimeProvider.System);
        services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, PersonalChatV2Authentication>(PersonalChatV2Authentication.SchemeName, _ => { });
        await using var provider = services.BuildServiceProvider(); using var scope = provider.CreateScope();
        var context = Request().HttpContext; context.RequestServices = scope.ServiceProvider; var login = Login();
        context.Items[OriginalLoginSession.ValidatedContextItem] = login;
        var authentication = await context.AuthenticateAsync(PersonalChatV2Authentication.SchemeName);
        Assert.True(authentication.Succeeded);
        Assert.Equal(login.LoginId.ToString("D"), authentication.Principal!.FindFirst("original_login")!.Value);
    }
    [Theory]
    [InlineData("https://localhost:7013", true)]
    [InlineData(null, true)]
    [InlineData("https://evil.example", false)]
    [InlineData("https://localhost:7014", false)]
    [InlineData("http://localhost:7013", false)]
    [InlineData("https://localhost:7013/other", false)]
    [InlineData("https://user@localhost:7013", false)]
    public void Origin_is_bound_to_the_actual_HTTPS_application(string? origin, bool expected)
        => Assert.Equal(expected, PersonalChatV2Authentication.OriginAllowed(Request(origin)));
    [Fact] public void Tokens_in_URL_and_unencrypted_transport_are_rejected()
    {
        var request = Request(); request.QueryString = new("?access_token=not-used");
        Assert.False(PersonalChatV2Authentication.OriginAllowed(request));
        request = Request(); request.Scheme = "http"; Assert.False(PersonalChatV2Authentication.OriginAllowed(request));
    }
    [Fact] public void Session_capture_is_copied_cannot_be_rebound_and_is_removed_on_disconnect()
    {
        var store = new PersonalChatV2TransportSessions(); var login = Login(); store.Add("a", login);
        var capture = store.Capture(login.LoginId); Assert.Equal(login.BindingHash.ToArray(), capture.BindingHash.ToArray());
        PersonalChatV2TransportSessions.Clear(capture);
        Assert.Contains(login.BindingHash.ToArray(), x => x != 0);
        Assert.Throws<ChatV2Exception>(() => store.Add("b", login with { UserId = 99 }));
        Assert.Throws<ChatV2Exception>(() => store.Add("c", login with { BindingHash = RandomNumberGenerator.GetBytes(32) }));
        store.Remove("a"); Assert.Throws<ChatV2Exception>(() => store.Capture(login.LoginId));
    }
    [Fact] public void Per_original_session_connection_count_is_bounded()
    {
        var store = new PersonalChatV2TransportSessions(); var login = Login();
        for (int i = 0; i < 8; i++) store.Add("c" + i, login);
        Assert.Throws<ChatV2Exception>(() => store.Add("excess", login));
        store.Remove("c0"); store.Add("replacement", login);
    }
    private static (PersonalChatV2ConnectionValidator Validator, Mock<IChatV2IntentPersistence> Persistence, ServiceProvider Provider)
        Validator(OriginalLoginContext login, Func<ChatV2LiveIdentity, ChatV2LiveIdentity>? actor = null, bool canRead = true)
    {
        var store = new PersonalChatV2TransportSessions(); store.Add("connection", login);
        var persistence = new Mock<IChatV2IntentPersistence>(MockBehavior.Strict);
        persistence.Setup(x => x.ResolveAsync(It.IsAny<OriginalLoginContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(actor?.Invoke(Actor(login)) ?? Actor(login));
        persistence.Setup(x => x.MembershipAsync(It.IsAny<OriginalLoginContext>(), It.IsAny<ChatV2Resource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatV2ResourceAccess(canRead, false, 17));
        var services = new ServiceCollection().AddScoped(_ => persistence.Object).BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        return (new(store, services.GetRequiredService<IServiceScopeFactory>(), TimeProvider.System), persistence, services);
    }
    [Fact] public async Task Real_binding_metadata_is_revalidated_through_the_persistence_boundary()
    {
        var login = Login(); var fixture = Validator(login); using var provider = fixture.Provider;
        var context = await fixture.Validator.ValidateAsync(login.LoginId, new(ChatV2Domain.Private, 4), default);
        Assert.Equal(login.LoginId, context.SessionId); Assert.Equal(17, context.ResourceEpoch);
        fixture.Persistence.Verify(x => x.MembershipAsync(It.Is<OriginalLoginContext>(c => c.UserId == 42 && c.LoginId == login.LoginId),
            new ChatV2Resource(ChatV2Domain.Private, 4), It.IsAny<CancellationToken>()), Times.Once);
    }
    [Theory]
    [InlineData("other-session")]
    [InlineData("other-actor")]
    [InlineData("other-profile")]
    [InlineData("stamp")]
    [InlineData("expired")]
    [InlineData("capability")]
    [InlineData("client")]
    public async Task Changed_canonical_identity_never_keeps_a_live_connection(string change)
    {
        var login = Login(); var fixture = Validator(login, a => change switch
        {
            "other-session" => a with { SessionId = Guid.NewGuid() }, "other-actor" => a with { Actor = 99 },
            "other-profile" => a with { Profile = 99 }, "stamp" => a with { SecurityStamp = Guid.NewGuid() },
            "expired" => a with { ExpiresUtc = DateTimeOffset.UtcNow.AddSeconds(-1) },
            "capability" => a with { Capabilities = ChatV2Capabilities.None }, _ => a with { Kind = ChatV2Kind.Client }
        });
        using var provider = fixture.Provider;
        await Assert.ThrowsAsync<ChatV2Exception>(async () => await fixture.Validator.ValidateAsync(login.LoginId, new(ChatV2Domain.Private, 4), default));
        fixture.Persistence.Verify(x => x.MembershipAsync(It.IsAny<OriginalLoginContext>(), It.IsAny<ChatV2Resource>(), It.IsAny<CancellationToken>()), Times.Never);
    }
    [Fact] public async Task Missing_membership_and_unimplemented_domains_stay_closed()
    {
        var login = Login(); var fixture = Validator(login, canRead: false); using var provider = fixture.Provider;
        await Assert.ThrowsAsync<ChatV2Exception>(async () => await fixture.Validator.ValidateAsync(login.LoginId, new(ChatV2Domain.Private, 4), default));
        await Assert.ThrowsAsync<ChatV2Exception>(async () => await fixture.Validator.ValidateAsync(login.LoginId, new(ChatV2Domain.Customer, 4), default));
    }
    [Fact] public void Hub_has_an_explicit_authentication_scheme_and_no_message_or_identity_arguments()
    {
        var attribute = Assert.Single(typeof(PersonalChatV2Hub).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>());
        Assert.Equal(PersonalChatV2Authentication.SchemeName, attribute.AuthenticationSchemes);
        Assert.Null(typeof(PersonalChatV2Hub).GetMethod("Send"));
        Assert.Equal(new[] { typeof(string), typeof(int) }, typeof(PersonalChatV2Hub).GetMethod("Subscribe")!.GetParameters().Select(x => x.ParameterType));
    }
    [Fact] public async Task Notification_contains_only_fixed_event_name_and_zero_arguments()
    {
        var proxy = new Mock<ISingleClientProxy>(); var clients = new Mock<IHubClients>(); var hub = new Mock<IHubContext<PersonalChatV2Hub>>();
        clients.Setup(x => x.Client("connection")).Returns(proxy.Object); hub.SetupGet(x => x.Clients).Returns(clients.Object);
        await new PersonalChatV2GenericNotifications(hub.Object).InvalidateAsync("connection", default);
        proxy.Verify(x => x.SendCoreAsync("Invalidate", It.Is<object?[]>(a => a.Length == 0), It.IsAny<CancellationToken>()), Times.Once);
    }
}
