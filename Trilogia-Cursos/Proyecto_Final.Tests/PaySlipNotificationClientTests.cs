using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using JJ.Commerce;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Proyecto_Final.Services;

namespace Proyecto_Final.Tests;

public sealed class PaySlipNotificationClientTests
{
    private static OriginalLoginContext Login()
    {
        var issued = DateTime.UtcNow.AddMinutes(-1);
        return new(Guid.NewGuid(), 1040, 14, Guid.NewGuid(), issued, issued.AddHours(8),
            System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
    }

    private static (PaySlipNotificationClient Client, Handler Handler) Client(OriginalLoginContext? login,
        Func<HttpRequestMessage, Task<HttpResponseMessage>> response, bool enabled = true)
    {
        var context = new DefaultHttpContext();
        if (login is not null) context.Items[OriginalLoginSession.ValidatedContextItem] = login;
        var handler = new Handler(response);
        return (new(new HttpClient(handler) { BaseAddress = new("https://localhost:7444/") },
            new HttpContextAccessor { HttpContext = context }, new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["PaySlipNotifications:Enabled"] = enabled.ToString() }).Build()), handler);
    }

    [Fact]
    public async Task Only_original_login_and_calculation_UUID_cross_the_boundary()
    {
        var login = Login(); var operation = Guid.NewGuid();
        const long calculation = (long)int.MaxValue + 17;
        var (client, handler) = Client(login, async request =>
        {
            Assert.Equal("/internal/notifications/pay-slips/send", request.RequestUri!.AbsolutePath);
            Assert.Equal(HttpMethod.Post, request.Method);
            var json = await request.Content!.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            Assert.Equal(new[] { "calculationId", "operationId", "session" }, doc.RootElement.EnumerateObject().Select(x => x.Name).Order().ToArray());
            var command = JsonSerializer.Deserialize<PaySlipNotificationCommand>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
            Assert.Equal(login.LoginId, command.Session.LoginId);
            Assert.Equal(login.BindingHash.ToArray(), command.Session.BindingHash);
            Assert.Equal(login.UserId, command.Session.UserId);
            Assert.Equal(calculation, command.CalculationId); Assert.Equal(operation, command.OperationId);
            return Ok(operation, "SmtpAccepted");
        });
        Assert.Equal("SmtpAccepted", (await client.SendAsync(calculation, operation, default)).State);
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData("Pending")] [InlineData("Sending")] [InlineData("SmtpAccepted")] [InlineData("Unknown")]
    public async Task Receipt_recovers_the_same_original_attempt_without_sending(string state)
    {
        var operation = Guid.NewGuid();
        var (client, handler) = Client(Login(), async request =>
        {
            Assert.Equal("/internal/notifications/pay-slips/receipt", request.RequestUri!.AbsolutePath);
            var command = await request.Content!.ReadFromJsonAsync<PaySlipNotificationReceiptCommand>();
            Assert.Equal(operation, command!.OperationId); Assert.Equal(71, command.CalculationId);
            return Ok(operation, state);
        });
        Assert.Equal(state, (await client.ReceiptAsync(71, operation, default)).State);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Lost_send_response_is_not_replayed_and_can_be_followed_only_by_explicit_receipt()
    {
        var operation = Guid.NewGuid();
        var (client, handler) = Client(Login(), request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/send", StringComparison.Ordinal))
                throw new HttpRequestException("Synthetic acknowledgement loss");
            Assert.EndsWith("/receipt", request.RequestUri.AbsolutePath);
            return Task.FromResult(Ok(operation, "Unknown"));
        });
        await Assert.ThrowsAsync<HttpRequestException>(() => client.SendAsync(71, operation, default));
        Assert.Equal(1, handler.Calls);
        Assert.Equal("Unknown", (await client.ReceiptAsync(71, operation, default)).State);
        Assert.Equal(2, handler.Calls);
    }

    [Theory]
    [InlineData("None")] [InlineData("Fallido")] [InlineData("Delivered")] [InlineData("AlreadyRequested")]
    [InlineData("")] [InlineData("unknown")]
    public async Task Unapproved_states_are_never_presented_as_a_confirmed_receipt(string state)
    {
        var operation = Guid.NewGuid();
        var (client, handler) = Client(Login(), _ => Task.FromResult(Ok(operation, state)));
        var error = await Assert.ThrowsAsync<PaySlipNotificationTransportException>(() => client.ReceiptAsync(71, operation, default));
        Assert.Equal(503, error.Status); Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Empty_or_other_UUID_in_response_is_rejected(bool empty)
    {
        var (client, handler) = Client(Login(), _ => Task.FromResult(Ok(empty ? Guid.Empty : Guid.NewGuid(), "Unknown")));
        await Assert.ThrowsAsync<PaySlipNotificationTransportException>(() => client.SendAsync(71, Guid.NewGuid(), default));
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("not-json")]
    public async Task Malformed_responses_fail_closed(string json)
    {
        var (client, _) = Client(Login(), _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StringContent(json, Encoding.UTF8, "application/json") }));
        await Assert.ThrowsAsync<PaySlipNotificationTransportException>(() => client.ReceiptAsync(71, Guid.NewGuid(), default));
    }

    [Fact]
    public async Task Extra_response_field_is_rejected_even_with_the_correct_UUID()
    {
        var operation = Guid.NewGuid();
        var json = JsonSerializer.Serialize(new { operationId = operation, state = "Unknown", recipient = "forbidden@example.invalid" });
        var (client, handler) = Client(Login(), _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StringContent(json, Encoding.UTF8, "application/json") }));
        await Assert.ThrowsAsync<PaySlipNotificationTransportException>(() => client.ReceiptAsync(71, operation, default));
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Oversized_declared_response_is_rejected_before_body_read()
    {
        var body = new CountingNonSeekableStream(new byte[8192]);
        var (client, handler) = Client(Login(), _ =>
        {
            var content = new StreamContent(body);
            content.Headers.ContentLength = 8192;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        });
        await Assert.ThrowsAsync<PaySlipNotificationTransportException>(() => client.SendAsync(71, Guid.NewGuid(), default));
        Assert.Equal(0, body.BytesRead); Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Oversized_response_without_length_reads_only_the_bounded_limit_plus_one_byte()
    {
        var body = new CountingNonSeekableStream(new byte[8192]);
        var (client, handler) = Client(Login(), _ =>
        {
            var content = new StreamContent(body);
            Assert.Null(content.Headers.ContentLength);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        });
        await Assert.ThrowsAsync<PaySlipNotificationTransportException>(() => client.ReceiptAsync(71, Guid.NewGuid(), default));
        Assert.Equal(4097, body.BytesRead); Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Valid_unknown_receipt_without_length_is_recovered_within_the_limit()
    {
        var operation = Guid.NewGuid();
        var body = new CountingNonSeekableStream(JsonSerializer.SerializeToUtf8Bytes(new PaySlipNotificationReceipt(operation, "Unknown")));
        var (client, handler) = Client(Login(), _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StreamContent(body) }));
        Assert.Equal("Unknown", (await client.ReceiptAsync(71, operation, default)).State);
        Assert.InRange(body.BytesRead, 1, 4096); Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Exact_limit_response_is_not_mistaken_for_truncated_oversized_content()
    {
        var operation = Guid.NewGuid();
        string json = JsonSerializer.Serialize(new PaySlipNotificationReceipt(operation, "Unknown"));
        var body = new CountingNonSeekableStream(Encoding.UTF8.GetBytes(json.PadRight(4096)));
        var (client, handler) = Client(Login(), _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StreamContent(body) }));
        Assert.Equal(operation, (await client.ReceiptAsync(71, operation, default)).OperationId);
        Assert.Equal(4096, body.BytesRead); Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData(403)] [InlineData(404)] [InlineData(409)] [InlineData(503)]
    public async Task Numeric_authority_rejections_are_preserved_without_retry(int status)
    {
        var (client, handler) = Client(Login(), _ => Task.FromResult(new HttpResponseMessage((HttpStatusCode)status)));
        Assert.Equal(status, (await Assert.ThrowsAsync<PaySlipNotificationTransportException>(() => client.ReceiptAsync(71, Guid.NewGuid(), default))).Status);
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Missing_or_disabled_validated_context_never_calls_authority(bool disabled)
    {
        var (client, handler) = Client(null, _ => throw new InvalidOperationException("Must not call"), !disabled);
        if (disabled) await Assert.ThrowsAsync<PaySlipNotificationTransportException>(() => client.SendAsync(71, Guid.NewGuid(), default));
        else await Assert.ThrowsAsync<UnauthorizedAccessException>(() => client.SendAsync(71, Guid.NewGuid(), default));
        Assert.Equal(0, handler.Calls);
    }

    [Theory]
    [InlineData(0, false)] [InlineData(-1, false)] [InlineData(71, true)]
    public async Task Both_operations_reject_invalid_selectors_before_HTTP(long calculation, bool emptyUuid)
    {
        var (client, handler) = Client(Login(), _ => throw new InvalidOperationException("Must not call"));
        var operation = emptyUuid ? Guid.Empty : Guid.NewGuid();
        await Assert.ThrowsAsync<ArgumentException>(() => client.SendAsync(calculation, operation, default));
        await Assert.ThrowsAsync<ArgumentException>(() => client.ReceiptAsync(calculation, operation, default));
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task Cancellation_is_propagated_without_send_retry()
    {
        var (client, handler) = Client(Login(), _ => throw new OperationCanceledException("Synthetic cancelled handoff"));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.SendAsync(71, Guid.NewGuid(), default));
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Cancellation_during_response_body_ends_the_original_call_without_retry()
    {
        using var cancelled = new CancellationTokenSource();
        var body = new BlockedBodyStream();
        var (client, handler) = Client(Login(), _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StreamContent(body) }));
        var request = client.SendAsync(71, Guid.NewGuid(), cancelled.Token);
        await body.ReadEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(body.ReadToken.CanBeCanceled);
        Assert.NotEqual(cancelled.Token, body.ReadToken); // Body receives the linked total-deadline token.
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request);
        Assert.True(body.ReadToken.IsCancellationRequested);
        Assert.Equal(1, handler.Calls); Assert.Equal(1, body.ReadCalls);
        Assert.True(body.Disposed);
    }

    private static HttpResponseMessage Ok(Guid operation, string state) => new(HttpStatusCode.OK)
    { Content = JsonContent.Create(new PaySlipNotificationReceipt(operation, state)) };

    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> response) : HttpMessageHandler
    {
        public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        { Calls++; return response(request); }
    }

    private sealed class CountingNonSeekableStream(byte[] bytes) : MemoryStream(bytes, writable: false)
    {
        public int BytesRead;
        public override bool CanSeek => false;
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var result = base.ReadAsync(buffer, cancellationToken);
            if (!result.IsCompletedSuccessfully) return CountAsync(result);
            int count = result.Result;
            BytesRead += count;
            return ValueTask.FromResult(count);
        }
        private async ValueTask<int> CountAsync(ValueTask<int> read)
        {
            int count = await read;
            BytesRead += count;
            return count;
        }
    }

    private sealed class BlockedBodyStream : Stream
    {
        public TaskCompletionSource<bool> ReadEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public CancellationToken ReadToken { get; private set; }
        public int ReadCalls;
        public bool Disposed;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            ReadCalls++; ReadToken = cancellationToken; ReadEntered.TrySetResult(true);
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return 0;
        }
        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
