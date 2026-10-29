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

public sealed class DeliveryNotificationAuthorityTests
{
    private static IConfiguration Configuration(bool enabled=true)=>new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
    {
        ["DeliveryNotifications:Enabled"]=enabled.ToString(),["Commerce:Enabled"]="true",
        ["DeliveryNotifications:ExpectedSqlUser"]="delivery-only",["DeliveryNotifications:ExpectedDataSource"]=@"(localdb)\OwnedCopy",
        ["DeliveryNotifications:ExpectedDatabase"]="OwnedCopy",
        ["ConnectionStrings:DeliveryNotificationAuthority"]=@"Server=(localdb)\OwnedCopy;Database=OwnedCopy;User ID=delivery-only;Password=<TEST_ONLY>;TrustServerCertificate=true"
    }).Build();
    private static DeliveryNotificationCommand Command()
    {
        var issued=DateTime.UtcNow.AddMinutes(-1);
        return new(new(Guid.NewGuid(),123,7,Guid.NewGuid(),Enumerable.Repeat((byte)1,32).ToArray(),issued,issued.AddHours(8)),55,Guid.NewGuid(),Guid.NewGuid());
    }
    private static DeliveryNotificationSnapshot Snapshot(string recipient="owner@example.invalid",string state="EnRuta")=>new(42,state,new DateTime(2026,10,25,8,30,0,DateTimeKind.Unspecified),"<script>Cliente</script>",recipient);
    private static DeliveryNotificationSender Sender(IDeliveryNotificationStore store,EmailService email)=>new(store,email,Options.Create(new CompanyOptions{BrandName="<JJ>"}),TimeProvider.System);

    [Theory]
    [InlineData("To")][InlineData("Body")][InlineData("Subject")][InlineData("State")][InlineData("ClaimToken")][InlineData("Url")]
    public void Closed_command_rejects_relay_fields(string field)=>Assert.Throws<JsonException>(()=>JsonSerializer.Deserialize<DeliveryNotificationCommand>("{\""+field+"\":\"bad\"}"));

    [Theory]
    [InlineData("NoEvent",false,false,true)][InlineData("None",false,true,true)]
    [InlineData("SmtpAccepted",true,true,true)][InlineData("AlreadyRequested",true,true,true)]
    [InlineData("NoEvent",false,true,false)][InlineData("None",true,true,false)]
    [InlineData("Unknown",false,true,false)][InlineData("Sending",true,false,false)]
    public void Receipt_shape_is_closed(string state,bool operation,bool sync,bool valid)=>Assert.Equal(valid,new DeliveryNotificationReceipt(operation?Guid.NewGuid():Guid.Empty,sync?Guid.NewGuid():Guid.Empty,state).IsValid);

    [Fact]
    public void Template_describes_a_past_recorded_event_without_UTC_conversion_or_current_delivery_claim()
    {
        var html=DeliveryNotificationSender.BuildHtml(Snapshot(),new CompanyOptions{BrandName="<JJ>"});
        Assert.Contains("&lt;script&gt;",html);Assert.DoesNotContain("<script>",html);Assert.Contains("2026-10-25 08:30:00",html);
        Assert.Contains("hora local registrada",html);Assert.Contains("evento registrado",html);Assert.Contains("En ruta",html);
        Assert.DoesNotContain("ahora está",html);Assert.DoesNotContain("UTC",html);Assert.DoesNotContain("href=",html);
    }
    [Theory]
    [InlineData("A <owner@example.invalid>")][InlineData("owner@example.invalid,other@example.invalid")][InlineData("owner@example.invalid\r\nBcc: other@example.invalid")]
    public void Canonical_destination_must_be_one_address_without_header_injection(string address)=>Assert.Throws<InvalidOperationException>(()=>DeliveryNotificationSender.BuildHtml(Snapshot(address),new CompanyOptions()));

    [Theory]
    [InlineData("Facturado")][InlineData("Fallido")][InlineData("Pendiente")]
    public void Only_notification_events_EnRuta_or_Entregado_are_accepted(string state)=>Assert.Throws<InvalidOperationException>(()=>DeliveryNotificationSender.BuildHtml(Snapshot(state:state),new CompanyOptions()));

    [Fact]
    public void UTC_is_not_a_valid_interpretation_of_the_recorded_local_event_time()=>Assert.Throws<InvalidOperationException>(()=>DeliveryNotificationSender.BuildHtml(Snapshot() with{RecordedLocalTime=DateTime.UtcNow},new CompanyOptions()));

    [Fact]
    public void Explicit_SQL_parameters_preserve_temporal_precision_and_both_distinct_UUIDs()
    {
        var value=Command();using var connection=new SqlConnection();using var command=DeliveryNotificationStore.SessionCommand(connection,"Claim",value.Session,value.RouteOrderId,value.SyncGuid,value.OperationId);
        foreach(var key in new[]{"@IssuedUtc","@ExpiresUtc"}){Assert.Equal(SqlDbType.DateTime2,command.Parameters[key].SqlDbType);Assert.Equal(7,command.Parameters[key].Scale);}
        Assert.Equal(value.Session.ExpiresUtc,command.Parameters["@ExpiresUtc"].Value);Assert.Equal(value.SyncGuid,command.Parameters["@SyncGuid"].Value);
        Assert.Equal(value.OperationId,command.Parameters["@OperationId"].Value);
    }
    [Fact]
    public void Configuration_disables_ambient_enlistment_and_requires_distinct_pinned_SQL_identity()
    {
        DeliveryNotificationConfiguration.Validate(Configuration());Assert.False(DeliveryNotificationConfiguration.Connection(Configuration()).Enlist);
        foreach(var item in new[]{("Commerce:Enabled","false"),("DeliveryNotifications:ExpectedSqlUser","other"),("DeliveryNotifications:ExpectedDatabase","other"),("ConnectionStrings:InvoiceNotificationAuthority",@"Server=local;User ID=delivery-only;Password=<TEST_ONLY>")})
        {
            var config=new ConfigurationBuilder().AddConfiguration(Configuration()).AddInMemoryCollection(new Dictionary<string,string?>{[item.Item1]=item.Item2}).Build();
            Assert.Throws<InvalidOperationException>(()=>DeliveryNotificationConfiguration.Validate(config));
        }
    }
    [Fact]
    public async Task Manifest_cannot_open_SQL_without_an_accredited_authority_configuration()=>await Assert.ThrowsAsync<InvalidOperationException>(()=>DeliveryNotificationManifest.VerifyAsync(new ConfigurationBuilder().Build()));

    [Theory]
    [InlineData(59401,400)][InlineData(59403,409)][InlineData(59404,404)][InlineData(56801,403)][InlineData(56802,403)][InlineData(-2,503)]
    public void SQL_error_mapping_is_opaque_and_purpose_bound(int code,int expected)=>Assert.Equal(expected,DeliveryNotificationsController.SqlStatus(code));

    [Fact]
    public async Task Missing_client_certificate_never_invokes_authority()
    {
        var sender=new Mock<IDeliveryNotificationSender>();var http=new DefaultHttpContext();http.Request.Scheme="https";http.Connection.RemoteIpAddress=IPAddress.Loopback;
        var controller=new DeliveryNotificationsController(Configuration(),sender.Object,TimeProvider.System,NullLogger<DeliveryNotificationsController>.Instance){ControllerContext=new(){HttpContext=http}};
        Assert.Equal(403,Assert.IsType<StatusCodeResult>(await controller.Send(Command(),default)).StatusCode);sender.VerifyNoOtherCalls();
    }
    [Theory]
    [InlineData("AlreadyRequested")][InlineData("Unknown")][InlineData("SmtpAccepted")][InlineData("Sending")]
    public async Task Existing_or_ambiguous_request_never_sends_again(string state)
    {
        var value=Command();var receipt=new DeliveryNotificationReceipt(value.OperationId,value.SyncGuid,state);var store=new Mock<IDeliveryNotificationStore>();var email=new Mock<EmailService>(Configuration());
        store.Setup(x=>x.RequestAsync(value,It.IsAny<CancellationToken>())).ReturnsAsync(receipt);
        store.Setup(x=>x.ClaimAsync(value,It.IsAny<byte[]>(),It.IsAny<CancellationToken>())).ReturnsAsync(new DeliveryNotificationClaim(receipt,false,null));
        Assert.Equal(receipt,await Sender(store.Object,email.Object).SendAsync(value,default));email.VerifyNoOtherCalls();
    }
    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task One_attempt_records_acceptance_or_unknown_with_bounded_completion_and_zeroed_token(bool lostAck)
    {
        var value=Command();var store=new Mock<IDeliveryNotificationStore>();var email=new Mock<EmailService>(Configuration());using var cancelled=new CancellationTokenSource();byte[]? observed=null;
        store.Setup(x=>x.RequestAsync(value,It.IsAny<CancellationToken>())).ReturnsAsync(new DeliveryNotificationReceipt(value.OperationId,value.SyncGuid,"Pending"));
        store.Setup(x=>x.ClaimAsync(value,It.IsAny<byte[]>(),It.IsAny<CancellationToken>())).Callback<DeliveryNotificationCommand,byte[],CancellationToken>((_,token,_)=>observed=token)
            .ReturnsAsync(new DeliveryNotificationClaim(new(value.OperationId,value.SyncGuid,"Sending"),true,Snapshot()));
        email.Setup(x=>x.SendEmailAsync(It.IsAny<string>(),It.IsAny<string>(),It.IsAny<string>(),It.IsAny<CancellationToken>())).Returns<string,string,string,CancellationToken>((_,_,_,_)=>
        {if(lostAck){cancelled.Cancel();throw new OperationCanceledException();}return Task.CompletedTask;});
        var expected=lostAck?"Unknown":"SmtpAccepted";
        store.Setup(x=>x.CompleteAsync(value.OperationId,It.IsAny<byte[]>(),expected,It.IsAny<CancellationToken>())).Callback<Guid,byte[],string,CancellationToken>((_,token,_,ct)=>
        {Assert.Equal(32,token.Length);Assert.Contains(token,x=>x!=0);Assert.True(ct.CanBeCanceled);Assert.False(ct.IsCancellationRequested);}).ReturnsAsync(new DeliveryNotificationReceipt(value.OperationId,value.SyncGuid,expected));
        Assert.Equal(expected,(await Sender(store.Object,email.Object).SendAsync(value,cancelled.Token)).State);
        email.Verify(x=>x.SendEmailAsync("owner@example.invalid",It.IsAny<string>(),It.IsAny<string>(),It.IsAny<CancellationToken>()),Times.Once);
        store.Verify(x=>x.CompleteAsync(value.OperationId,It.IsAny<byte[]>(),expected,It.IsAny<CancellationToken>()),Times.Once);
        Assert.NotNull(observed);Assert.All(observed,x=>Assert.Equal(0,x));
    }
    [Fact]
    public async Task Send_does_not_use_the_business_UUID_as_notification_ownership()
    {
        var value=Command();var store=new Mock<IDeliveryNotificationStore>();var sender=Sender(store.Object,new Mock<EmailService>(Configuration()).Object);
        await Assert.ThrowsAsync<ArgumentException>(()=>sender.SendAsync(value with{OperationId=value.SyncGuid},default));
        await Assert.ThrowsAsync<ArgumentException>(()=>sender.SendAsync(value with{OperationId=Guid.Empty},default));
        await Assert.ThrowsAsync<ArgumentException>(()=>sender.SendAsync(value with{SyncGuid=Guid.Empty},default));store.VerifyNoOtherCalls();
    }
    [Fact]
    public async Task Receipt_empty_selectors_resolve_only_through_SQL_without_sending()
    {
        var value=Command();var lookup=new DeliveryNotificationReceiptCommand(value.Session,value.RouteOrderId,Guid.Empty,Guid.Empty);var store=new Mock<IDeliveryNotificationStore>();
        store.Setup(x=>x.ReceiptAsync(lookup,It.IsAny<CancellationToken>())).ReturnsAsync(new DeliveryNotificationReceipt(Guid.Empty,Guid.Empty,"NoEvent"));
        var email=new Mock<EmailService>(Configuration());Assert.Equal("NoEvent",(await Sender(store.Object,email.Object).ReceiptAsync(lookup,default)).State);email.VerifyNoOtherCalls();
    }
    [Fact]
    public async Task Receipt_does_not_adopt_foreign_or_wrong_event_result()
    {
        var value=Command();var lookup=new DeliveryNotificationReceiptCommand(value.Session,value.RouteOrderId,value.SyncGuid,value.OperationId);var store=new Mock<IDeliveryNotificationStore>();
        store.Setup(x=>x.ReceiptAsync(lookup,It.IsAny<CancellationToken>())).ReturnsAsync(new DeliveryNotificationReceipt(Guid.NewGuid(),value.SyncGuid,"SmtpAccepted"));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>Sender(store.Object,new Mock<EmailService>(Configuration()).Object).ReceiptAsync(lookup,default));
    }
}
