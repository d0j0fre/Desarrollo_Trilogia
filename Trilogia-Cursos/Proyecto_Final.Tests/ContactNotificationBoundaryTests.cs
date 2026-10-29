using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Proyecto_Final.Services;
using Proyecto_Final.Models.Store;
using Proyecto_FinalAPI.Models;
using Proyecto_FinalAPI.Controllers;
using Proyecto_FinalAPI.Services;
using ApiEmail = Proyecto_FinalAPI.Services.EmailService;
using ApiCompany = Proyecto_FinalAPI.Services.CompanyOptions;

namespace Proyecto_Final.Tests;

public sealed class ContactNotificationBoundaryTests
{
    [Fact]
    public void Historical_synchronous_mail_caller_propagates_a_real_cancellation_deadline()
    {
        var email=new Mock<ApiEmail>(Configuration()){CallBase=true};
        email.Setup(x=>x.SendEmailAsync(It.IsAny<string>(),It.IsAny<string>(),It.IsAny<string>(),It.IsAny<CancellationToken>()))
            .Returns<string,string,string,CancellationToken>((to,subject,html,ct)=>{Assert.True(ct.CanBeCanceled);Assert.False(ct.IsCancellationRequested);return Task.CompletedTask;});
        email.Object.SendEmail("owner@example.invalid","Synthetic","Synthetic");
        email.Verify(x=>x.SendEmailAsync(It.IsAny<string>(),It.IsAny<string>(),It.IsAny<string>(),It.Is<CancellationToken>(ct=>ct.CanBeCanceled)),Times.Once);
    }
    static IConfiguration Configuration(bool enabled=true) => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
    { ["Contact:NotificationsEnabled"]=enabled.ToString(), ["Contact:NotificationRecipient"]="owner@example.invalid", ["Commerce:Enabled"]="true" }).Build();

    [Theory]
    [InlineData("To")]
    [InlineData("Html")]
    [InlineData("Attachments")]
    public void Public_contract_rejects_general_relay_fields(string field)
    {
        Assert.Throws<JsonException>(()=>JsonSerializer.Deserialize<ContactNotificationRequest>("{\""+field+"\":\"untrusted\"}"));
    }

    [Fact]
    public async Task Sender_uses_only_fixed_API_destination_and_encodes_contact_text()
    {
        var email=new Mock<ApiEmail>(Configuration());
        email.Setup(x=>x.SendEmailAsync(It.IsAny<string>(),It.IsAny<string>(),It.IsAny<string>(),It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var sender=new ContactNotificationSender(email.Object,Configuration(),Options.Create(new ApiCompany{BrandName="Distribuidora JJ"}));
        await sender.SendAsync(new(){ConsultationId=123,Name="<script>bad</script>",Email="visitor@example.invalid",Subject="<b>subject</b>",Message="<img src=x>"},default);
        email.Verify(x=>x.SendEmailAsync("owner@example.invalid","Consulta web #123 — Distribuidora JJ",It.Is<string>(s=>s.Contains("&lt;script&gt;")&&s.Contains("&lt;img")&&!s.Contains("<script>bad")),It.IsAny<CancellationToken>()),Times.Once);
    }

    [Fact]
    public async Task API_without_client_certificate_never_calls_SMTP()
    {
        var sender=new Mock<IContactNotificationSender>();var http=new DefaultHttpContext();http.Request.Scheme="https";http.Connection.RemoteIpAddress=IPAddress.Loopback;
        var controller=new ContactNotificationsController(Configuration(),sender.Object,TimeProvider.System,NullLogger<ContactNotificationsController>.Instance){ControllerContext=new(){HttpContext=http}};
        var result=Assert.IsType<StatusCodeResult>(await controller.Send(new(){ConsultationId=123},default));
        Assert.Equal(403,result.StatusCode);sender.Verify(x=>x.SendAsync(It.IsAny<ContactNotificationRequest>(),It.IsAny<CancellationToken>()),Times.Never);
    }

    [Fact]
    public void Enabled_transport_requires_fixed_destination()
    {
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["Contact:NotificationsEnabled"]="true",["Commerce:Enabled"]="true"}).Build();
        Assert.Throws<InvalidOperationException>(()=>ContactNotificationConfiguration.Validate(config));
    }

    [Theory]
    [InlineData(503,123,"smtpAccepted")]
    [InlineData(200,999,"smtpAccepted")]
    [InlineData(200,123,"not-confirmed")]
    public async Task MVC_rejects_unknown_or_wrong_receipt_without_retry(int status,int id,string state)
    {
        var handler=new NotificationHandler(status,id,state);using var http=new HttpClient(handler){BaseAddress=new("https://localhost:7444/")};var client=new ContactNotificationClient(http,Configuration());
        await Assert.ThrowsAsync<HttpRequestException>(()=>client.NotifyAsync(123,new(){Nombre="Synthetic",Correo="visitor@example.invalid",Asunto="QA",Mensaje="Synthetic only"},default));
        Assert.Equal(1,handler.Calls);Assert.DoesNotContain("To",handler.Json);Assert.DoesNotContain("Html",handler.Json);
    }

    [Fact]
    public async Task MVC_accepts_only_receipt_for_the_saved_consultation()
    {
        var handler=new NotificationHandler(200,123,"smtpAccepted");using var http=new HttpClient(handler){BaseAddress=new("https://localhost:7444/")};
        await new ContactNotificationClient(http,Configuration()).NotifyAsync(123,new(){Nombre="Synthetic",Correo="visitor@example.invalid",Asunto="QA",Mensaje="Synthetic only"},default);
        Assert.Equal(1,handler.Calls);
    }

    sealed class NotificationHandler(int status,int id,string state) : HttpMessageHandler
    {
        public int Calls;public string Json="";
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {Calls++;Assert.Equal("/internal/notifications/contact",request.RequestUri!.AbsolutePath);Json=await request.Content!.ReadAsStringAsync(ct);return new((HttpStatusCode)status){Content=JsonContent.Create(new{consultationId=id,status=state})};}
    }
}
