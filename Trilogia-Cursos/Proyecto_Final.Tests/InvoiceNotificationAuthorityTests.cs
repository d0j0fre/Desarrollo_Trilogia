using System.Data;
using System.Net;
using System.Net.Mail;
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

public sealed class InvoiceNotificationAuthorityTests
{
    private static CommerceSession Session() => new(Guid.NewGuid(), 123, 7, Guid.NewGuid(), Enumerable.Repeat((byte)1,32).ToArray(), DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(-1).AddHours(8));
    private static InvoiceNotificationCommand Command()
    {
        var issued = DateTime.UtcNow.AddMinutes(-1);
        return new(new(Guid.NewGuid(),123,7,Guid.NewGuid(),Enumerable.Repeat((byte)1,32).ToArray(),issued,issued.AddHours(8)), 456, Guid.NewGuid(), true);
    }
    private static IConfiguration Configuration(bool enabled = true) => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
    {
        ["InvoiceNotifications:Enabled"] = enabled.ToString(), ["Commerce:Enabled"] = "true",
        ["InvoiceNotifications:ExpectedSqlUser"] = "purpose-user", ["InvoiceNotifications:ExpectedDataSource"] = @"(localdb)\OwnCopy",
        ["InvoiceNotifications:ExpectedDatabase"] = "OwnedCopy",
        ["ConnectionStrings:InvoiceNotificationAuthority"] = @"Server=(localdb)\OwnCopy;Database=OwnedCopy;User ID=purpose-user;Password=<TEST_ONLY>;TrustServerCertificate=true"
    }).Build();
    private static InvoiceNotificationSnapshot Snapshot(string address="owner@example.invalid") => new("F-QA-1",999,"<script>customer</script>",address,DateTime.SpecifyKind(new DateTime(2026,10,24),DateTimeKind.Utc),100.13m);
    private static Mock<EmailService> Email() => new(Configuration());
    private static InvoiceNotificationSender Sender(IInvoiceNotificationStore store, EmailService email) => new(store,email,Options.Create(new CompanyOptions { BrandName="<JJ>" }), TimeProvider.System);

    [Theory]
    [InlineData("To")][InlineData("Subject")][InlineData("Html")][InlineData("Amount")][InlineData("ClaimToken")]
    public void Closed_JSON_rejects_relay_fields(string field) => Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<InvoiceNotificationCommand>("{\""+field+"\":\"bad\"}"));

    [Theory]
    [InlineData("One <owner@example.invalid>")][InlineData("owner@example.invalid,other@example.invalid")]
    [InlineData("owner@example.invalid\r\nBcc:other@example.invalid")]
    public void Canonical_snapshot_rejects_multidestination_or_header_injection(string address) => Assert.Throws<InvalidOperationException>(()=>InvoiceNotificationSender.BuildHtml(Snapshot(address),new CompanyOptions{BrandName="JJ"}));

    [Fact]
    public void HTML_encodes_all_canonical_text_and_preserves_money()
    {
        var html=InvoiceNotificationSender.BuildHtml(Snapshot(),new CompanyOptions{BrandName="<JJ>"});
        Assert.Contains("&lt;script&gt;",html); Assert.DoesNotContain("<script>",html);Assert.Contains("&lt;JJ&gt;",html);Assert.Contains("100,13",html);
    }

    [Fact]
    public void SQL_temporal_parameters_are_datetime2_scale7_without_rounding()
    {
        var value=Command(); using var connection=new SqlConnection();
        using var sql=InvoiceNotificationStore.SessionCommand(connection,"Claim",value.Session,value.InvoiceId,value.OperationId);
        foreach(var name in new[]{"@IssuedUtc","@ExpiresUtc"})
        { Assert.Equal(SqlDbType.DateTime2,sql.Parameters[name].SqlDbType);Assert.Equal(7,sql.Parameters[name].Scale); }
        Assert.Equal(value.Session.ExpiresUtc,sql.Parameters["@ExpiresUtc"].Value);
        Assert.Equal(SqlDbType.Binary,sql.Parameters["@BindingHash"].SqlDbType);Assert.Equal(32,sql.Parameters["@BindingHash"].Size);
    }

    [Fact]
    public void Configuration_requires_separate_named_identity_and_pinned_local_destination()
    {
        InvoiceNotificationConfiguration.Validate(Configuration());
        Assert.False(InvoiceNotificationConfiguration.Connection(Configuration()).Enlist);
        foreach(var pair in new[]{("Commerce:Enabled","false"),("InvoiceNotifications:ExpectedSqlUser","other"),("InvoiceNotifications:ExpectedDatabase","other"),("ConnectionStrings:DefaultConnection",@"Server=local;User ID=purpose-user;Password=<TEST_ONLY>")})
        {
            var config=new ConfigurationBuilder().AddConfiguration(Configuration()).AddInMemoryCollection(new Dictionary<string,string?>{[pair.Item1]=pair.Item2}).Build();
            Assert.Throws<InvalidOperationException>(()=>InvoiceNotificationConfiguration.Validate(config));
        }
    }

    [Fact]
    public async Task Unauthenticated_mTLS_boundary_never_reaches_SQL_or_SMTP()
    {
        var sender=new Mock<IInvoiceNotificationSender>(); var http=new DefaultHttpContext();http.Request.Scheme="https";http.Connection.RemoteIpAddress=IPAddress.Loopback;
        var controller=new InvoiceNotificationsController(Configuration(),sender.Object,TimeProvider.System,NullLogger<InvoiceNotificationsController>.Instance){ControllerContext=new(){HttpContext=http}};
        var result=Assert.IsType<StatusCodeResult>(await controller.Send(Command(),default)); Assert.Equal(403,result.StatusCode);
        sender.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(59304,404)][InlineData(59303,409)][InlineData(59301,400)][InlineData(56801,403)][InlineData(56802,403)][InlineData(-2,503)]
    public void SQL_errors_have_closed_opaque_mapping(int code,int status) => Assert.Equal(status,InvoiceNotificationsController.SqlStatus(code));

    [Theory]
    [InlineData("AlreadyRequested")][InlineData("Sending")][InlineData("SmtpAccepted")][InlineData("Unknown")]
    public async Task Existing_or_ambiguous_attempt_never_sends_again(string state)
    {
        var command=Command();var store=new Mock<IInvoiceNotificationStore>();var receipt=new InvoiceNotificationReceipt(command.OperationId,state);
        store.Setup(x=>x.RequestAsync(command,It.IsAny<CancellationToken>())).ReturnsAsync(receipt);
        store.Setup(x=>x.ClaimAsync(command,It.IsAny<byte[]>(),It.IsAny<CancellationToken>())).ReturnsAsync(new InvoiceNotificationClaim(receipt,false,null));
        var mail=Email();Assert.Equal(receipt,await Sender(store.Object,mail.Object).SendAsync(command,default));
        mail.Verify(x=>x.SendEmailAsync(It.IsAny<string>(),It.IsAny<string>(),It.IsAny<string>(),It.IsAny<CancellationToken>()),Times.Never);
        store.Verify(x=>x.CompleteAsync(It.IsAny<Guid>(),It.IsAny<byte[]>(),It.IsAny<string>(),It.IsAny<CancellationToken>()),Times.Never);
    }

    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task Exactly_one_attempt_records_acceptance_or_uncertainty_even_when_caller_cancelled(bool lostResponse)
    {
        var command=Command(); var store=new Mock<IInvoiceNotificationStore>();var mail=Email();using var cancelled=new CancellationTokenSource();
        store.Setup(x=>x.RequestAsync(command,It.IsAny<CancellationToken>())).ReturnsAsync(new InvoiceNotificationReceipt(command.OperationId,"Pending"));
        byte[]? observedToken=null;
        store.Setup(x=>x.ClaimAsync(command,It.IsAny<byte[]>(),It.IsAny<CancellationToken>())).Callback<InvoiceNotificationCommand,byte[],CancellationToken>((_,token,_)=>observedToken=token)
            .ReturnsAsync(new InvoiceNotificationClaim(new(command.OperationId,"Sending"),true,Snapshot()));
        mail.Setup(x=>x.SendEmailAsync(It.IsAny<string>(),It.IsAny<string>(),It.IsAny<string>(),It.IsAny<CancellationToken>()))
            .Returns<string,string,string,CancellationToken>((_,_,_,_)=>{if(lostResponse){cancelled.Cancel();throw new OperationCanceledException();}return Task.CompletedTask;});
        var expected=lostResponse?"Unknown":"SmtpAccepted";
        store.Setup(x=>x.CompleteAsync(command.OperationId,It.IsAny<byte[]>(),expected,It.IsAny<CancellationToken>()))
            .Callback<Guid,byte[],string,CancellationToken>((_,token,_,ct)=>{Assert.Equal(32,token.Length);Assert.Contains(token,x=>x!=0);Assert.False(ct.IsCancellationRequested);Assert.True(ct.CanBeCanceled);})
            .ReturnsAsync(new InvoiceNotificationReceipt(command.OperationId,expected));
        Assert.Equal(expected,(await Sender(store.Object,mail.Object).SendAsync(command,cancelled.Token)).State);
        mail.Verify(x=>x.SendEmailAsync("owner@example.invalid",It.IsAny<string>(),It.IsAny<string>(),It.IsAny<CancellationToken>()),Times.Once);
        store.Verify(x=>x.CompleteAsync(command.OperationId,It.IsAny<byte[]>(),expected,It.IsAny<CancellationToken>()),Times.Once);
        Assert.NotNull(observedToken);Assert.All(observedToken,x=>Assert.Equal(0,x));
    }

    [Fact]
    public async Task Expired_session_and_empty_send_UUID_never_access_store()
    {
        var value=Command();var store=new Mock<IInvoiceNotificationStore>(); var sender=Sender(store.Object,Email().Object);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>sender.SendAsync(value with{Session=value.Session with{IssuedUtc=DateTime.UtcNow.AddHours(-9),ExpiresUtc=DateTime.UtcNow.AddHours(-1)}},default));
        await Assert.ThrowsAsync<ArgumentException>(()=>sender.SendAsync(value with{OperationId=Guid.Empty},default));store.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Empty_receipt_UUID_resolves_only_by_SQL_context_and_accepts_None()
    {
        var command=Command();var lookup=new InvoiceNotificationReceiptCommand(command.Session,command.InvoiceId,Guid.Empty);
        var store=new Mock<IInvoiceNotificationStore>();store.Setup(x=>x.ReceiptAsync(lookup,It.IsAny<CancellationToken>())).ReturnsAsync(new InvoiceNotificationReceipt(Guid.Empty,"None"));
        Assert.Equal("None",(await Sender(store.Object,Email().Object).ReceiptAsync(lookup,default)).State);
        Assert.True(new InvoiceNotificationReceipt(Guid.Empty,"None").IsValid);Assert.False(new InvoiceNotificationReceipt(Guid.Empty,"SmtpAccepted").IsValid);
        Assert.False(new InvoiceNotificationReceipt(Guid.NewGuid(),"None").IsValid);
    }
}
