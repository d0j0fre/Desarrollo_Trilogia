using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace JJ.Runtime;

/// <summary>Graceful single-host shutdown through a role-private, trusted configuration path.</summary>
public static class LocalRuntimeControl
{
    public static void Register(IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var configured = configuration["LocalControl:StopSignalPath"];
        if (string.IsNullOrWhiteSpace(configured)) return;
        if (!environment.IsEnvironment("Local") || !Path.IsPathFullyQualified(configured)
            || Path.GetFileName(configured) != "stop-request")
            throw new InvalidOperationException("El control local requiere un destino privado acreditado.");
        var path = Path.GetFullPath(configured);
        if (!Directory.Exists(Path.GetDirectoryName(path)))
            throw new InvalidOperationException("La carpeta de control local no está preparada.");
        services.AddHostedService(provider => new StopSignalService(path,
            provider.GetRequiredService<IHostApplicationLifetime>()));
    }

    private sealed class StopSignalService(string path, IHostApplicationLifetime lifetime) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var watcher = new FileSystemWatcher(Path.GetDirectoryName(path)!, Path.GetFileName(path))
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite
            };
            void Check() { if (File.Exists(path)) signal.TrySetResult(); }
            watcher.Created += (_, _) => Check();
            watcher.Changed += (_, _) => Check();
            watcher.Renamed += (_, _) => Check();
            watcher.EnableRaisingEvents = true;
            Check();
            try { await signal.Task.WaitAsync(stoppingToken); lifetime.StopApplication(); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        }
    }
}
