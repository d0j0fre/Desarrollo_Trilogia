using System.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Proyecto_Final.Controllers;
using Proyecto_Final.Filters;
using Proyecto_Final.Models.Admin;
using Proyecto_Final.Services;

namespace Proyecto_Final.Tests;

public sealed class DocumentDelegationBoundaryTests
{
    [Fact]
    public async Task Missing_original_login_denies_before_SQL_credentials_are_parsed()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["ConnectionStrings:DefaultConnection"] = "invalid credentials" }).Build();
        var service = new DocumentManagementDbService(configuration, new HttpContextAccessor { HttpContext = new DefaultHttpContext() });
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetOptionsAsync());
    }

    [Fact]
    public void Context_command_keeps_exact_UTC_precision_and_separates_write_actor_names()
    {
        var issued = new DateTime(2026, 10, 21, 0, 0, 0, DateTimeKind.Utc).AddTicks(1234567);
        var login = new OriginalLoginContext(Guid.NewGuid(), 19, 5, Guid.NewGuid(), issued, issued.AddHours(8), new byte[32]);
        using var connection = new SqlConnection();
        using var command = DocumentSqlContext.Command(connection, "Document_Update", login);
        Assert.Equal("DocumentSecurity.Document_Update", command.CommandText);
        Assert.Equal(login.UserId, command.Parameters["@ContextActor"].Value);
        Assert.Equal(login.UserId, command.Parameters["@Actor"].Value);
        Assert.DoesNotContain(command.Parameters.Cast<SqlParameter>(), x => x.ParameterName == "@UsuarioId");
        foreach (var name in new[] { "@ContextIssuedUtc", "@ContextExpiresUtc" })
        {
            Assert.Equal(SqlDbType.DateTime2, command.Parameters[name].SqlDbType);
            Assert.Equal((byte)7, command.Parameters[name].Scale);
        }
        Assert.Equal(issued, command.Parameters["@ContextIssuedUtc"].Value);
        Assert.Throws<ArgumentException>(() => DocumentSqlContext.Command(connection, "dbo.sp_Document_Update", login));
        Assert.Throws<UnauthorizedAccessException>(() => DocumentSqlContext.Command(connection, "Document_Update", login with { LoginId = Guid.Empty }));
    }

    [Fact]
    public void Management_and_document_actions_keep_separate_authorization_boundaries()
    {
        var management = Assert.Single(typeof(DocumentDelegationsController).GetCustomAttributes(typeof(AdminAuthorizeAttribute), true));
        Assert.NotNull(management);
        var save = typeof(DocumentDelegationsController).GetMethod(nameof(DocumentDelegationsController.Save))!;
        Assert.Single(save.GetCustomAttributes(typeof(ValidateAntiForgeryTokenAttribute), true));
        Assert.DoesNotContain("SetGrant", DocumentDelegationManifest.Operations);
        Assert.DoesNotContain("CanAccess", DocumentDelegationManifest.PublicOperations);
        Assert.False(new DocumentListItemViewModel().CanDownload);
        Assert.False(new DocumentListItemViewModel { CanRead = true }.CanEdit);
        Assert.False(new DocumentListItemViewModel { CanRead = true }.CanVersion);
    }

    [Fact]
    public void Version_publication_requires_the_reviewed_monotonic_contract()
    {
        Assert.Equal("2BB5FCDCA013FAFA324788E2A3BBF9AC143ADEB4A32112E0C50226F743CE5364",
            DocumentDelegationManifest.Ledger["0061_stage2_document_version_monotonic"]);
        Assert.Equal("61EC40F559383122E4E662460F2E0024644CBEFDFBEF9C5C15FAEB8E1E13C22D",
            DocumentDelegationManifest.Modules["DocumentSecurity.Document_MarkVersionReady"]);
        Assert.Contains("Document_MarkVersionReady", DocumentDelegationManifest.PublicOperations);
    }
}
