using System.Net;
using System.Net.Mail;
using System.Security.Cryptography.X509Certificates;
using JJ.Commerce;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Proyecto_FinalAPI.Controllers;
using Proyecto_FinalAPI.Services;

namespace Proyecto_Final.Tests;

public sealed class PaySlipNotificationAuthorityTests
{
    private static readonly DateTime Now = new(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc);
    private static PaySlipNotificationCommand Command()
        => new(new(Guid.NewGuid(), 123, 7, Guid.NewGuid(), Enumerable.Repeat((byte)1, 32).ToArray(),
            Now.AddMinutes(-1), Now.AddMinutes(-1).AddHours(8)), 4_000_000_000L, Guid.NewGuid());
    private static IConfiguration Configuration(bool enabled = true, string url = "https://localhost:7013/")
        => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["PaySlipNotifications:Enabled"] = enabled.ToString(),
            ["PaySlipNotifications:PublicBaseUrl"] = url
        }).Build();
    private static Mock<EmailService> Email()
    {
        var mail = new Mock<EmailService>(Configuration());
        mail.Setup(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return mail;
    }
    private static PaySlipNotificationSender Sender(IPaySlipNotificationStore store, EmailService email, IConfiguration? configuration = null)
        => new(store, email, configuration ?? Configuration(), Options.Create(new CompanyOptions { BrandName = "<JJ>\r\n" }), new FixedClock());
    private static PaySlipNotificationSnapshot Snapshot(long calculation = 4_000_000_000L, string email = "owner@example.invalid")
        => new(calculation, 456, email);
    private static void Pending(Mock<IPaySlipNotificationStore> store, PaySlipNotificationCommand command)
        => store.Setup(x => x.RequestAsync(command, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaySlipNotificationReceipt(command.OperationId, "Pending"));

    [Fact]
    public async Task Disabled_controller_activates_without_new_DI_registration_and_returns_404()
    {
        using var services = new ServiceCollection().AddSingleton(Configuration(false))
            .AddSingleton<TimeProvider>(new FixedClock())
            .AddSingleton<Microsoft.Extensions.Logging.ILogger<PaySlipNotificationsController>>(NullLogger<PaySlipNotificationsController>.Instance)
            .BuildServiceProvider();
        var controller = ActivatorUtilities.CreateInstance<PaySlipNotificationsController>(services);
        Assert.IsType<NotFoundResult>(await controller.Send(Command(), default));
    }

    [Fact]
    public async Task Enabled_endpoint_without_client_certificate_never_reaches_sender()
    {
        var sender = new Mock<IPaySlipNotificationSender>();
        var http = new DefaultHttpContext(); http.Request.Scheme = "https"; http.Connection.RemoteIpAddress = IPAddress.Loopback;
        var controller = new PaySlipNotificationsController(Configuration(), new FixedClock(),
            NullLogger<PaySlipNotificationsController>.Instance, sender.Object) { ControllerContext = new() { HttpContext = http } };
        Assert.Equal(403, Assert.IsType<StatusCodeResult>(await controller.Send(Command(), default)).StatusCode);
        sender.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Certificate_observation_is_cancelled_by_deadline_or_request_abort(bool abortRequest)
    {
        using var deadline = new CancellationTokenSource(); using var aborted = new CancellationTokenSource();
        var tls = new BlockedTlsFeature();
        var http = new DefaultHttpContext(); http.Request.Scheme = "https";
        http.Connection.RemoteIpAddress = IPAddress.Loopback; http.RequestAborted = aborted.Token;
        http.Features.Set<ITlsConnectionFeature>(tls);
        Task<bool> operation = CommercialAuthorityBoundary.IsAuthorizedAsync(http, Configuration(), Now, deadline.Token);
        try
        {
            await tls.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
            (abortRequest ? aborted : deadline).Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation.WaitAsync(TimeSpan.FromSeconds(2)));
            Assert.True(tls.ObservedToken.IsCancellationRequested); Assert.Equal(1, tls.Calls);
        }
        finally { tls.Pending.TrySetResult(null); }
    }

    [Fact]
    public async Task Controller_cancellation_while_waiting_for_certificate_never_reaches_sender()
    {
        var tls = new BlockedTlsFeature(); var sender = new Mock<IPaySlipNotificationSender>();
        var http = new DefaultHttpContext(); http.Request.Scheme = "https"; http.Connection.RemoteIpAddress = IPAddress.Loopback;
        http.Features.Set<ITlsConnectionFeature>(tls); using var cancelled = new CancellationTokenSource();
        var controller = new PaySlipNotificationsController(Configuration(), new FixedClock(),
            NullLogger<PaySlipNotificationsController>.Instance, sender.Object) { ControllerContext = new() { HttpContext = http } };
        Task<IActionResult> operation = controller.Send(Command(), cancelled.Token);
        try
        {
            await tls.Started.Task.WaitAsync(TimeSpan.FromSeconds(2)); cancelled.Cancel();
            Assert.Equal(503, Assert.IsType<StatusCodeResult>(await operation.WaitAsync(TimeSpan.FromSeconds(2))).StatusCode);
            Assert.True(tls.ObservedToken.IsCancellationRequested); sender.VerifyNoOtherCalls();
        }
        finally { tls.Pending.TrySetResult(null); }
    }

    [Theory]
    [InlineData(59601, 400)] [InlineData(59603, 409)] [InlineData(59604, 404)]
    [InlineData(56801, 403)] [InlineData(56802, 403)] [InlineData(59610, 503)] [InlineData(-2, 503)]
    public void SQL_errors_use_opaque_statuses(int code, int expected)
        => Assert.Equal(expected, PaySlipNotificationsController.SqlStatus(code));

    [Theory]
    [InlineData("http://localhost:7013/")] [InlineData("//localhost:7013/")]
    [InlineData("https://person@localhost:7013/")] [InlineData("https://localhost:7013/?to=other")]
    [InlineData("https://localhost:7013/#other")] [InlineData("javascript:alert(1)")]
    public async Task Invalid_server_portal_never_creates_an_attempt(string url)
    {
        var store = new Mock<IPaySlipNotificationStore>();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Sender(store.Object, Email().Object, Configuration(url: url)).SendAsync(Command(), default));
        store.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Disabled_authority_invalid_session_and_empty_UUID_never_access_store()
    {
        var command = Command(); var store = new Mock<IPaySlipNotificationStore>(); var email = Email();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Sender(store.Object, email.Object, Configuration(false)).SendAsync(command, default));
        var sender = Sender(store.Object, email.Object);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => sender.SendAsync(command with { Session = command.Session with { IssuedUtc = Now.AddHours(-9), ExpiresUtc = Now.AddHours(-1) } }, default));
        await Assert.ThrowsAsync<ArgumentException>(() => sender.SendAsync(command with { OperationId = Guid.Empty }, default));
        await Assert.ThrowsAsync<ArgumentException>(() => sender.SendAsync(command with { CalculationId = 0 }, default));
        store.VerifyNoOtherCalls(); email.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Already_cancelled_caller_cannot_prepare_SQL_or_SMTP()
    {
        var store = new Mock<IPaySlipNotificationStore>(); using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Sender(store.Object, Email().Object).SendAsync(Command(), cancelled.Token));
        store.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("Sending")] [InlineData("SmtpAccepted")] [InlineData("Unknown")]
    public async Task Existing_attempt_returns_receipt_without_claiming_or_sending(string state)
    {
        var command = Command(); var store = new Mock<IPaySlipNotificationStore>(); var mail = Email();
        var receipt = new PaySlipNotificationReceipt(command.OperationId, state);
        store.Setup(x => x.RequestAsync(command, It.IsAny<CancellationToken>())).ReturnsAsync(receipt);
        Assert.Equal(receipt, await Sender(store.Object, mail.Object).SendAsync(command, default));
        store.Verify(x => x.ClaimAsync(It.IsAny<PaySlipNotificationCommand>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Never);
        store.Verify(x => x.CompleteAsync(It.IsAny<Guid>(), It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        mail.Verify(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Concurrent_claim_loser_never_sends_or_completes()
    {
        var command = Command(); var store = new Mock<IPaySlipNotificationStore>(); Pending(store, command); var mail = Email();
        store.Setup(x => x.ClaimAsync(command, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaySlipNotificationClaim(new(command.OperationId, "Sending"), false, null));
        Assert.Equal("Sending", (await Sender(store.Object, mail.Object).SendAsync(command, default)).State);
        mail.Verify(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        store.Verify(x => x.CompleteAsync(It.IsAny<Guid>(), It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Single_SMTP_handoff_records_acceptance_or_unknown_and_zeroes_claim_token(bool lostResponse)
    {
        var command = Command(); var store = new Mock<IPaySlipNotificationStore>(); Pending(store, command); var mail = Email();
        using var caller = new CancellationTokenSource(); byte[]? claimToken = null;
        store.Setup(x => x.ClaimAsync(command, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Callback<PaySlipNotificationCommand, byte[], CancellationToken>((_, token, _) => claimToken = token)
            .ReturnsAsync(new PaySlipNotificationClaim(new(command.OperationId, "Sending"), true, Snapshot()));
        mail.Setup(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, string, string, CancellationToken>((_, _, _, _) =>
            {
                caller.Cancel();
                return lostResponse ? Task.FromException(new OperationCanceledException()) : Task.CompletedTask;
            });
        string state = lostResponse ? "Unknown" : "SmtpAccepted";
        store.Setup(x => x.CompleteAsync(command.OperationId, It.IsAny<byte[]>(), state, It.IsAny<CancellationToken>()))
            .Callback<Guid, byte[], string, CancellationToken>((_, token, _, ct) =>
            {
                Assert.Equal(32, token.Length); Assert.Contains(token, value => value != 0);
                Assert.True(ct.CanBeCanceled); Assert.False(ct.IsCancellationRequested);
            }).ReturnsAsync(new PaySlipNotificationReceipt(command.OperationId, state));
        Assert.Equal(state, (await Sender(store.Object, mail.Object).SendAsync(command, caller.Token)).State);
        mail.Verify(x => x.SendEmailAsync("owner@example.invalid", It.Is<string>(s => !s.Contains('\r') && !s.Contains('\n')),
            It.Is<string>(s => s.Contains("https://localhost:7013/MyPaySlips/Details/4000000000") && s.Contains("&lt;JJ&gt;")), It.IsAny<CancellationToken>()), Times.Once);
        store.Verify(x => x.CompleteAsync(command.OperationId, It.IsAny<byte[]>(), state, It.IsAny<CancellationToken>()), Times.Once);
        Assert.NotNull(claimToken); Assert.All(claimToken, value => Assert.Equal(0, value));
    }

    [Fact]
    public async Task Lost_SQL_completion_propagates_without_reclassifying_or_replaying_SMTP()
    {
        var command = Command(); var store = new Mock<IPaySlipNotificationStore>(); Pending(store, command); var mail = Email();
        store.Setup(x => x.ClaimAsync(command, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaySlipNotificationClaim(new(command.OperationId, "Sending"), true, Snapshot()));
        store.Setup(x => x.CompleteAsync(command.OperationId, It.IsAny<byte[]>(), "SmtpAccepted", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("Synthetic completion loss"));
        await Assert.ThrowsAsync<IOException>(() => Sender(store.Object, mail.Object).SendAsync(command, default));
        mail.Verify(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        store.Verify(x => x.CompleteAsync(command.OperationId, It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Completion_with_a_different_state_fails_without_another_send()
    {
        var command = Command(); var store = new Mock<IPaySlipNotificationStore>(); Pending(store, command); var mail = Email();
        store.Setup(x => x.ClaimAsync(command, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaySlipNotificationClaim(new(command.OperationId, "Sending"), true, Snapshot()));
        store.Setup(x => x.CompleteAsync(command.OperationId, It.IsAny<byte[]>(), "SmtpAccepted", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaySlipNotificationReceipt(command.OperationId, "Unknown"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Sender(store.Object, mail.Object).SendAsync(command, default));
        mail.Verify(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        store.Verify(x => x.CompleteAsync(command.OperationId, It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("One <owner@example.invalid>")] [InlineData("owner@example.invalid,other@example.invalid")]
    [InlineData("owner@example.invalid\r\nBcc:other@example.invalid")]
    public async Task Invalid_canonical_recipient_conserves_claim_as_unknown_without_sending(string address)
    {
        var command = Command(); var store = new Mock<IPaySlipNotificationStore>(); Pending(store, command); var mail = Email();
        store.Setup(x => x.ClaimAsync(command, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaySlipNotificationClaim(new(command.OperationId, "Sending"), true, Snapshot(email: address)));
        store.Setup(x => x.CompleteAsync(command.OperationId, It.IsAny<byte[]>(), "Unknown", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaySlipNotificationReceipt(command.OperationId, "Unknown"));
        Assert.Equal("Unknown", (await Sender(store.Object, mail.Object).SendAsync(command, default)).State);
        mail.Verify(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Wrong_calculation_snapshot_is_never_sent()
    {
        var command = Command(); var store = new Mock<IPaySlipNotificationStore>(); Pending(store, command); var mail = Email();
        store.Setup(x => x.ClaimAsync(command, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaySlipNotificationClaim(new(command.OperationId, "Sending"), true, Snapshot(123)));
        store.Setup(x => x.CompleteAsync(command.OperationId, It.IsAny<byte[]>(), "Unknown", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaySlipNotificationReceipt(command.OperationId, "Unknown"));
        Assert.Equal("Unknown", (await Sender(store.Object, mail.Object).SendAsync(command, default)).State);
        mail.Verify(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Receipt_recovers_only_exact_operation_and_never_calls_SMTP()
    {
        var command = Command(); var lookup = new PaySlipNotificationReceiptCommand(command.Session, command.CalculationId, command.OperationId);
        var store = new Mock<IPaySlipNotificationStore>(); var mail = Email();
        store.Setup(x => x.ReceiptAsync(lookup, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaySlipNotificationReceipt(command.OperationId, "Unknown"));
        Assert.Equal("Unknown", (await Sender(store.Object, mail.Object).ReceiptAsync(lookup, default)).State);
        mail.Verify(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        store.Verify(x => x.RequestAsync(It.IsAny<PaySlipNotificationCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        store.Verify(x => x.ClaimAsync(It.IsAny<PaySlipNotificationCommand>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("None")] [InlineData("Fallido")] [InlineData("AlreadyRequested")]
    public async Task Unknown_receipt_states_fail_closed(string state)
    {
        var command = Command(); var lookup = new PaySlipNotificationReceiptCommand(command.Session, command.CalculationId, command.OperationId);
        var store = new Mock<IPaySlipNotificationStore>();
        store.Setup(x => x.ReceiptAsync(lookup, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaySlipNotificationReceipt(command.OperationId, state));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Sender(store.Object, Email().Object).ReceiptAsync(lookup, default));
    }

    [Fact]
    public async Task Another_UUID_receipt_fails_closed()
    {
        var command = Command(); var lookup = new PaySlipNotificationReceiptCommand(command.Session, command.CalculationId, command.OperationId);
        var store = new Mock<IPaySlipNotificationStore>();
        store.Setup(x => x.ReceiptAsync(lookup, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaySlipNotificationReceipt(Guid.NewGuid(), "SmtpAccepted"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Sender(store.Object, Email().Object).ReceiptAsync(lookup, default));
    }

    [Fact]
    public void Server_portal_prefix_and_large_calculation_cannot_change_destination()
    {
        var portal = PaySlipNotificationSender.PublicBase(Configuration(url: "https://localhost:7013/product"));
        string html = PaySlipNotificationSender.BuildHtml(Snapshot(long.MaxValue), portal, new CompanyOptions { BrandName = "<JJ>" });
        Assert.Contains("https://localhost:7013/product/MyPaySlips/Details/9223372036854775807", html);
        Assert.Contains("&lt;JJ&gt;", html); Assert.DoesNotContain("owner@example.invalid", html);
        Assert.DoesNotContain("456", html); Assert.DoesNotContain("<JJ>", html);
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(Now);
    }

    private sealed class BlockedTlsFeature : ITlsConnectionFeature
    {
        public X509Certificate2? ClientCertificate { get; set; }
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<X509Certificate2?> Pending { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public CancellationToken ObservedToken { get; private set; }
        public int Calls { get; private set; }
        public Task<X509Certificate2?> GetClientCertificateAsync(CancellationToken cancellationToken)
        {
            Calls++; ObservedToken = cancellationToken; Started.TrySetResult();
            // Deliberately ignore the token to prove the boundary's own wait remains finite.
            return Pending.Task;
        }
    }
}
