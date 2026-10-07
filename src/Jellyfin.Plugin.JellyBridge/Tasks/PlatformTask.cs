using Jellyfin.Plugin.JellyBridge.Configuration;
using Jellyfin.Plugin.JellyBridge.JellyfinModels;
using Jellyfin.Plugin.JellyBridge.Services;
using Jellyfin.Plugin.JellyBridge.Utils;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JellyBridge.Tasks;

/// <summary>
/// Applies streaming platforms (tags, studios, collections) to the discover items.
/// Runs daily and is queued by the sync task once the library refresh has finished.
/// </summary>
public class PlatformTask : IScheduledTask
{
    private readonly DebugLogger<PlatformTask> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public PlatformTask(ILogger<PlatformTask> logger, IServiceScopeFactory scopeFactory)
    {
        _logger = new DebugLogger<PlatformTask>(logger);
        _scopeFactory = scopeFactory;
    }

    public string Name => "JellyShowcase Platforms";
    public string Key => "JellyShowcasePlatforms";
    public string Description => "Adds streaming platforms as tags, studios and collections to the discover items.";
    public string Category => "JellyShowcase";

    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        if (!Plugin.GetConfigOrDefault<bool>(nameof(PluginConfiguration.IsEnabled)))
        {
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var refreshService = scope.ServiceProvider.GetRequiredService<RefreshService>();
        var platformService = scope.ServiceProvider.GetRequiredService<PlatformService>();

        // New placeholders must exist in Jellyfin before they can be tagged and collected: give the
        // background scan queued by the sync a moment to start, then wait for it to finish.
        await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
        await refreshService.WaitForTaskRefreshLibrary();
        progress.Report(5);
        // The bridge library refresh runs outside the scheduled-task queue: wait for the placeholders themselves.
        await platformService.WaitForPlaceholdersAsync(TimeSpan.FromMinutes(20), TimeSpan.FromMinutes(2), cancellationToken);
        progress.Report(10);
        await platformService.ApplyAsync(cancellationToken);
        progress.Report(100);
    }

    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        return new[] { JellyfinTaskTrigger.Interval(TimeSpan.FromHours(24)) };
    }
}
