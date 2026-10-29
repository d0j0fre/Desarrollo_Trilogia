using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Proyecto_Final.Services.ChatV2;

namespace Proyecto_Final.Tests;

public sealed class LegacyChatTransitionTests
{
    [Theory]
    [InlineData("POST", "SendMessage")]
    [InlineData("POST", "OpenConversation")]
    [InlineData("POST", "SendDepartmentMessage")]
    [InlineData("POST", "FutureMutation")]
    [InlineData("DELETE", "Search")]
    public void Opt_in_blocks_old_and_future_writers_without_calling_legacy(string method, string action)
    {
        var context = Invoke(true, method, action);
        Assert.Equal(503, Assert.IsType<ObjectResult>(context.Result).StatusCode);
    }
    [Theory]
    [InlineData("GET", "GetMessages")]
    [InlineData("HEAD", "GetMessages")]
    public void Canonical_personal_history_remains_readable(string method, string action) =>
        Assert.Null(Invoke(true, method, action).Result);
    [Theory]
    [InlineData("GET", "GetDepartmentMessages")]
    [InlineData("GET", "GetDepartments")]
    [InlineData("GET", "Index")]
    [InlineData("POST", "Search")]
    public void Readers_without_canonical_department_authorization_remain_closed(string method, string action) =>
        Assert.Equal(503, Assert.IsType<ObjectResult>(Invoke(true, method, action).Result).StatusCode);
    [Fact] public void Rollback_retains_only_the_canonical_personal_reader()
    {
        Assert.Null(Invoke(false, "GET", "GetMessages", true).Result);
        Assert.Equal(503, Assert.IsType<ObjectResult>(Invoke(false, "GET", "GetDepartmentMessages", true).Result).StatusCode);
    }
    [Fact] public void Feature_off_preserves_existing_routes() => Assert.Null(Invoke(false, "POST", "SendMessage").Result);
    [Fact] public void Disabling_V2_does_not_reopen_legacy_writers_after_cutover() =>
        Assert.Equal(503, Assert.IsType<ObjectResult>(Invoke(false, "POST", "SendMessage", true).Result).StatusCode);
    private static ResourceExecutingContext Invoke(bool enabled, string method, string action, bool readOnly = false)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
        { ["ChatV2:PersonalSqlEnabled"] = enabled.ToString(), ["ChatV2:LegacyReadOnly"] = readOnly.ToString() }).Build();
        var http = new DefaultHttpContext(); http.Request.Method = method;
        var actionContext = new ActionContext(http, new RouteData(), new ControllerActionDescriptor { ActionName = action });
        var context = new ResourceExecutingContext(actionContext, new List<IFilterMetadata>(), new List<IValueProviderFactory>());
        new LegacyChatWriteGuard(config).OnResourceExecuting(context); return context;
    }
}
