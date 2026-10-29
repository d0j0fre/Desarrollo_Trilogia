using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Proyecto_Final.Services;
using Proyecto_Final.Services.ChatV2;

namespace Proyecto_Final.Tests;

// Focused preconnection/availability checks. SQL/HTTP evidence is recorded separately.
public sealed class ChatV2PersonalPersistenceTests
{
    private static OriginalLoginContext Login(bool expired = false)
    {
        var issued = DateTime.UtcNow.AddHours(expired ? -9 : -1);
        return new(Guid.NewGuid(), 7, 2, Guid.NewGuid(), issued, issued.AddHours(8), new byte[32]);
    }
    private static SqlPersonalChatV2Persistence Adapter(Dictionary<string, string?>? values = null) =>
        new(new ConfigurationBuilder().AddInMemoryCollection(values ?? []).Build(),
            new EphemeralDataProtectionProvider(), TimeProvider.System);

    [Fact]
    public async Task Missing_restricted_reader_is_closed_before_connecting()
    {
        var error = await Assert.ThrowsAsync<ChatV2Exception>(() => Adapter().ResolveAsync(Login(), default).AsTask());
        Assert.Equal(ChatV2Failure.Dependency, error.Failure);
    }
    [Fact]
    public async Task Integrated_bootstrap_credentials_cannot_back_the_chat_reader()
    {
        var adapter = Adapter(new() { ["ConnectionStrings:ChatV2Reader"] =
            "Data Source=unused;Initial Catalog=unused;Integrated Security=True" });
        var error = await Assert.ThrowsAsync<ChatV2Exception>(() => adapter.ResolveAsync(Login(), default).AsTask());
        Assert.Equal(ChatV2Failure.Dependency, error.Failure);
    }
    [Fact]
    public async Task Expired_original_login_is_rejected_before_configuration_or_sql()
    {
        var error = await Assert.ThrowsAsync<ChatV2Exception>(() => Adapter().ResolveAsync(Login(true), default).AsTask());
        Assert.Equal(ChatV2Failure.Session, error.Failure);
    }
    [Theory]
    [InlineData(ChatV2Domain.Department)]
    [InlineData(ChatV2Domain.Customer)]
    public async Task Unsupported_domains_cannot_read_personal_storage(ChatV2Domain domain)
    {
        var error = await Assert.ThrowsAsync<ChatV2Exception>(() => Adapter().HistoryAsync(Login(), new(domain, 1), null, default).AsTask());
        Assert.Equal(ChatV2Failure.Dependency, error.Failure);
    }
    [Fact]
    public async Task Unavailable_composition_cannot_open_a_personal_conversation()
    {
        IChatV2IntentPersistence unavailable = new UnavailableChatV2IntentPersistence();
        var error = await Assert.ThrowsAsync<ChatV2Exception>(() => unavailable.OpenPrivateAsync(Login(), 8, default).AsTask());
        Assert.Equal(ChatV2Failure.Dependency, error.Failure);
    }
    [Fact]
    public void Internal_permission_does_not_advertise_department_sources()
    {
        var view = new ChatV2ContextView("opaque", true, false, false);
        Assert.True(view.InternalChat);
        Assert.False(view.DepartmentChat);
        Assert.False(view.Attention);
    }
}
