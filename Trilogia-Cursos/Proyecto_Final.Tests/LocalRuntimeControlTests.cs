using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Proyecto_Final.Controllers;

namespace Proyecto_Final.Tests;

public sealed class LocalRuntimeControlTests
{
    private static void Register(IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var type = typeof(ExpensesController).Assembly.GetType("JJ.Runtime.LocalRuntimeControl", throwOnError: true)!;
        try { type.GetMethod("Register")!.Invoke(null, new object[] { services, configuration, environment }); }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception.InnerException).Throw(); }
    }

    [Fact]
    public async Task Private_signal_requests_graceful_host_shutdown()
    {
        var directory = Path.Combine(Path.GetTempPath(), "jj-stop-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "stop-request");
        try
        {
            using var host = new HostBuilder().UseEnvironment("Local")
                .ConfigureAppConfiguration(config => config.AddInMemoryCollection(
                    new Dictionary<string, string?> { ["LocalControl:StopSignalPath"] = path }))
                .ConfigureServices((context, services) => Register(services, context.Configuration, context.HostingEnvironment)).Build();
            var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var registration = host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping.Register(() => stopped.TrySetResult());
            await host.StartAsync();
            await File.WriteAllTextAsync(path, "stop");
            await stopped.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await host.StopAsync();
        }
        finally
        {
            // Delete only this test's exact file and its now-empty GUID directory.
            if (File.Exists(path)) File.Delete(path);
            Directory.Delete(directory, recursive: false);
        }
    }

    [Theory]
    [InlineData("Production", "stop-request")]
    [InlineData("Local", "other-file")]
    public void Signal_is_not_registered_outside_the_local_contract(string environment, string filename)
    {
        var path = Path.Combine(Path.GetTempPath(), filename);
        var config = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["LocalControl:StopSignalPath"] = path }).Build();
        var hostEnvironment = new Moq.Mock<IHostEnvironment>();
        hostEnvironment.SetupGet(x => x.EnvironmentName).Returns(environment);
        Assert.Throws<InvalidOperationException>(() => Register(new ServiceCollection(), config, hostEnvironment.Object));
    }
}
