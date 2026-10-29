using System.Data;
using System.Net;
using System.Text.Json;
using JJ.Commerce;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Proyecto_FinalAPI.Controllers;
using Proyecto_FinalAPI.Services;

namespace Proyecto_Final.Tests;

public sealed class RouteDispatchNotificationAuthorityTests
{
    private static IConfiguration Configuration() => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["RouteDispatchNotifications:Enabled"] = "true", ["Commerce:Enabled"] = "true",
        ["RouteDispatchNotifications:ExpectedUser"] = "dispatch-only", ["RouteDispatchNotifications:ExpectedDataSource"] = @"(localdb)\OwnedCopy",
        ["RouteDispatchNotifications:ExpectedDatabase"] = "OwnedCopy",
        ["RouteDispatchNotifications:ConnectionString"] = @"Server=(localdb)\OwnedCopy;Database=OwnedCopy;User ID=dispatch-only;Password=<TEST_ONLY>;TrustServerCertificate=true"
    }).Build();
    private static RouteDispatchNotificationCommand Command()
    {
        var issued = DateTime.UtcNow.AddMinutes(-1);
        return new(new(Guid.NewGuid(), 123, 7, Guid.NewGuid(), Enumerable.Repeat((byte)1, 32).ToArray(), issued, issued.AddHours(8)), 5, Guid.NewGuid(), 55, Guid.NewGuid(), Guid.NewGuid());
    }
    private static RouteDispatchNotificationSnapshot Snapshot(string recipient = "owner@example.invalid") => new(42, new DateTime(2026, 10, 25, 14, 30, 0, DateTimeKind.Utc), "<script>Cliente</script>", recipient);
    private static RouteDispatchNotificationSender Sender(IRouteDispatchNotificationStore store, EmailService email) => new(store, email, Options.Create(new CompanyOptions { BrandName = "<JJ>" }), TimeProvider.System);

    [Theory]
    [InlineData("To")][InlineData("Body")][InlineData("Subject")][InlineData("ClaimToken")][InlineData("Url")]
    public void Command_does_not_accept_relay_fields(string field) => Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<RouteDispatchNotificationCommand>("{\"" + field + "\":\"bad\"}"));

    [Theory]
    [InlineData("NoEvent", false, false, true)][InlineData("None", false, true, true)][InlineData("NoRecipient", false, true, true)]
    [InlineData("SmtpAccepted", true, true, true)][InlineData("Unknown", true, true, true)][InlineData("AlreadyRequested", true, true, true)]
    [InlineData("NoRecipient", true, true, false)][InlineData("Sending", false, true, false)][InlineData("None", false, false, false)]
    public void Receipt_shape_is_closed(string state, bool operation, bool eventGuid, bool valid) => Assert.Equal(valid, new RouteDispatchNotificationReceipt(operation ? Guid.NewGuid() : Guid.Empty, eventGuid ? Guid.NewGuid() : Guid.Empty, state).IsValid);

    [Fact]
    public void Template_records_dispatch_without_claiming_physical_delivery()
    {
        var html = RouteDispatchNotificationSender.BuildHtml(Snapshot(), new CompanyOptions { BrandName = "<JJ>" });
        Assert.Contains("&lt;script&gt;", html); Assert.DoesNotContain("<script>", html);
        Assert.Contains("2026-10-25 14:30:00 UTC", html); Assert.Contains("despacho", html);
        Assert.Contains("no acredita entrega física", System.Net.WebUtility.HtmlDecode(html)); Assert.DoesNotContain("href=", html);
    }

    [Theory]
    [InlineData("A <owner@example.invalid>")][InlineData("owner@example.invalid,other@example.invalid")][InlineData("owner@example.invalid\r\nBcc: other@example.invalid")]
    public void Recipient_must_be_one_canonical_address(string address) => Assert.Throws<InvalidOperationException>(() => RouteDispatchNotificationSender.BuildHtml(Snapshot(address), new CompanyOptions()));

    [Fact]
    public void Temporal_parameters_and_bigint_event_are_explicit()
    {
        var value = Command(); using var connection = new SqlConnection();
        using var command = RouteDispatchNotificationStore.SessionCommand(connection, "Claim", value.Session, value.RouteId, value.DispatchId, value.EventId, value.EventGuid, value.OperationId);
        foreach (var key in new[] { "@IssuedUtc", "@ExpiresUtc" }) { Assert.Equal(SqlDbType.DateTime2, command.Parameters[key].SqlDbType); Assert.Equal(7, command.Parameters[key].Scale); }
        Assert.Equal(SqlDbType.BigInt, command.Parameters["@EventId"].SqlDbType);
        Assert.Equal(value.EventGuid, command.Parameters["@EventGuid"].Value);
        Assert.Equal(value.DispatchId, command.Parameters["@DispatchId"].Value);
    }

    [Fact]
    public void Configuration_requires_pinned_distinct_authority_and_disables_ambient_enlistment()
    {
        RouteDispatchNotificationConfiguration.Validate(Configuration()); Assert.False(RouteDispatchNotificationConfiguration.Connection(Configuration()).Enlist);
        foreach (var value in new[] { ("Commerce:Enabled", "false"), ("RouteDispatchNotifications:ExpectedUser", "other"), ("RouteDispatchNotifications:ExpectedDataSource", "other"), ("RouteDispatchNotifications:ExpectedDatabase", "other"), ("ConnectionStrings:DefaultConnection", "Server=other;User ID=dispatch-only;Password=<TEST_ONLY>") })
        {
            var config = new ConfigurationBuilder().AddConfiguration(Configuration()).AddInMemoryCollection(new Dictionary<string, string?> { [value.Item1] = value.Item2 }).Build();
            Assert.Throws<InvalidOperationException>(() => RouteDispatchNotificationConfiguration.Validate(config));
        }
    }

    [Fact]
    public async Task Unaccredited_composition_does_not_open_SQL() => await Assert.ThrowsAsync<InvalidOperationException>(() => RouteDispatchNotificationManifest.VerifyAsync(new ConfigurationBuilder().Build()));

    [Theory]
    [InlineData(59501, 400)][InlineData(59502, 404)][InlineData(59503, 409)][InlineData(59504, 400)][InlineData(59505, 409)][InlineData(56801, 403)][InlineData(56802, 403)][InlineData(-2, 503)]
    public void SQL_status_is_numeric_and_opaque(int code, int status) => Assert.Equal(status, RouteDispatchNotificationsController.SqlStatus(code));

    [Fact]
    public async Task Missing_mTLS_never_invokes_sender()
    {
        var sender = new Mock<IRouteDispatchNotificationSender>(); var http = new DefaultHttpContext(); http.Request.Scheme = "https"; http.Connection.RemoteIpAddress = IPAddress.Loopback;
        var controller = new RouteDispatchNotificationsController(Configuration(), sender.Object, TimeProvider.System, NullLogger<RouteDispatchNotificationsController>.Instance) { ControllerContext = new() { HttpContext = http } };
        Assert.Equal(403, Assert.IsType<StatusCodeResult>(await controller.Send(Command(), default)).StatusCode); sender.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("AlreadyRequested")][InlineData("Unknown")][InlineData("SmtpAccepted")][InlineData("Sending")]
    public async Task Durable_existing_or_uncertain_attempt_never_sends_again(string state)
    {
        var value = Command(); var receipt = new RouteDispatchNotificationReceipt(value.OperationId, value.EventGuid, state);
        var store = new Mock<IRouteDispatchNotificationStore>(); var email = new Mock<EmailService>(Configuration());
        store.Setup(x => x.RequestAsync(value, It.IsAny<CancellationToken>())).ReturnsAsync(receipt);
        store.Setup(x => x.ClaimAsync(value, It.IsAny<byte[]>(), It.IsAny<CancellationToken>())).ReturnsAsync(new RouteDispatchNotificationClaim(receipt, false, null));
        Assert.Equal(receipt, await Sender(store.Object, email.Object).SendAsync(value, default)); email.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task No_recipient_has_no_claim_or_SMTP_effect()
    {
        var value = Command(); var store = new Mock<IRouteDispatchNotificationStore>(); var email = new Mock<EmailService>(Configuration());
        store.Setup(x => x.RequestAsync(value, It.IsAny<CancellationToken>())).ReturnsAsync(new RouteDispatchNotificationReceipt(Guid.Empty, value.EventGuid, "NoRecipient"));
        Assert.Equal("NoRecipient", (await Sender(store.Object, email.Object).SendAsync(value, default)).State);
        store.Verify(x => x.ClaimAsync(It.IsAny<RouteDispatchNotificationCommand>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Never); email.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task SMTP_result_has_one_attempt_and_token_is_cleared(bool unknown)
    {
        var value = Command(); var store = new Mock<IRouteDispatchNotificationStore>(); var email = new Mock<EmailService>(Configuration()); byte[]? observed = null;
        store.Setup(x => x.RequestAsync(value, It.IsAny<CancellationToken>())).ReturnsAsync(new RouteDispatchNotificationReceipt(value.OperationId, value.EventGuid, "Pending"));
        store.Setup(x => x.ClaimAsync(value, It.IsAny<byte[]>(), It.IsAny<CancellationToken>())).Callback<RouteDispatchNotificationCommand, byte[], CancellationToken>((_, token, _) => observed = token)
            .ReturnsAsync(new RouteDispatchNotificationClaim(new(value.OperationId, value.EventGuid, "Sending"), true, Snapshot()));
        email.Setup(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(() => unknown ? Task.FromException(new System.Net.Mail.SmtpException("Synthetic uncertain acceptance")) : Task.CompletedTask);
        string state = unknown ? "Unknown" : "SmtpAccepted";
        store.Setup(x => x.CompleteAsync(value.OperationId, It.IsAny<byte[]>(), state, It.IsAny<CancellationToken>())).ReturnsAsync(new RouteDispatchNotificationReceipt(value.OperationId, value.EventGuid, state));
        Assert.Equal(state, (await Sender(store.Object, email.Object).SendAsync(value, default)).State);
        email.Verify(x => x.SendEmailAsync("owner@example.invalid", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.NotNull(observed); Assert.All(observed, x => Assert.Equal(0, x));
    }

    [Fact]
    public async Task Dispatch_or_event_UUID_cannot_become_notification_UUID()
    {
        var value = Command(); var store = new Mock<IRouteDispatchNotificationStore>(); var sender = Sender(store.Object, new Mock<EmailService>(Configuration()).Object);
        await Assert.ThrowsAsync<ArgumentException>(() => sender.SendAsync(value with { OperationId = value.EventGuid }, default));
        await Assert.ThrowsAsync<ArgumentException>(() => sender.SendAsync(value with { OperationId = value.DispatchId }, default));
        store.VerifyNoOtherCalls();
    }
}
