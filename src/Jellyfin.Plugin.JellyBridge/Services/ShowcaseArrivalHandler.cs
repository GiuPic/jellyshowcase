using System.Collections.Concurrent;
using Jellyfin.Plugin.JellyBridge.Configuration;
using Jellyfin.Plugin.JellyBridge.Utils;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JellyBridge.Services;

/// <summary>
/// Hides a discover placeholder as soon as the real media arrives in a main library.
/// With KeepRequestedUntilAvailable, requested items stay visible while pending/downloading;
/// this handler removes them the moment the downloaded movie or series gets its TMDB id.
/// The .ignore marker keeps the placeholder out of later scans; the discover sync remains the fallback.
/// </summary>
public sealed class ShowcaseArrivalHandler : IHostedService
{
    private readonly DebugLogger<ShowcaseArrivalHandler> _logger;
    private readonly ILibraryManager _libraryManager;
    private readonly ConcurrentDictionary<string, byte> _handled = new();

    public ShowcaseArrivalHandler(ILogger<ShowcaseArrivalHandler> logger, ILibraryManager libraryManager)
    {
        _logger = new DebugLogger<ShowcaseArrivalHandler>(logger);
        _libraryManager = libraryManager;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // ItemAdded often fires before metadata (and the TMDB id) is known, so ItemUpdated is needed too.
        _libraryManager.ItemAdded += OnItemChanged;
        _libraryManager.ItemUpdated += OnItemChanged;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _libraryManager.ItemAdded -= OnItemChanged;
        _libraryManager.ItemUpdated -= OnItemChanged;
        return Task.CompletedTask;
    }

    private void OnItemChanged(object? sender, ItemChangeEventArgs e)
    {
        var item = e.Item;
        if (item is not (Movie or Series) || string.IsNullOrEmpty(item.Path))
        {
            return;
        }

        try
        {
            if (!Plugin.GetConfigOrDefault<bool>(nameof(PluginConfiguration.KeepRequestedUntilAvailable))
                || FolderUtils.IsPathInSyncDirectory(item.Path))
            {
                return;
            }

            var tmdbId = item.GetProviderId(MetadataProvider.Tmdb);
            if (string.IsNullOrEmpty(tmdbId))
            {
                return;
            }

            var isMovie = item is Movie;
            var key = (isMovie ? "movie:" : "tv:") + tmdbId;
            if (!_handled.TryAdd(key, 0))
            {
                return;
            }

            HidePlaceholders(tmdbId, isMovie, item.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error hiding discover placeholder for {ItemName}", item.Name);
        }
    }

    private void HidePlaceholders(string tmdbId, bool isMovie, string itemName)
    {
        var baseDirectory = FolderUtils.GetBaseDirectory();
        // Placeholder folders are named "Title (Year) [tmdbid-123] [imdbid]" for movies and
        // "... [tvdbid]" for shows (TMDB ids of movies and shows overlap, so the suffix matters).
        var suffix = isMovie ? "[imdbid]" : "[tvdbid]";
        var folders = Directory.EnumerateDirectories(baseDirectory, $"*[[]tmdbid-{tmdbId}[]]*", new EnumerationOptions
            {
                RecurseSubdirectories = true,
                MaxRecursionDepth = 2,
                MatchType = MatchType.Win32,
            })
            .Where(d => Path.GetFileName(d).Contains($"[tmdbid-{tmdbId}]", StringComparison.Ordinal)
                && Path.GetFileName(d).EndsWith(suffix, StringComparison.Ordinal));

        foreach (var folder in folders)
        {
            var ignorePath = Path.Combine(folder, BridgeService.IgnoreFileName);
            if (File.Exists(ignorePath))
            {
                continue;
            }

            File.WriteAllText(ignorePath, $"{{\"reason\":\"available\",\"name\":\"{itemName.Replace("\"", "'")}\",\"date\":\"{DateTimeOffset.Now:O}\"}}");

            // Remove only this placeholder from the database (files stay on disk): instant, no library scan.
            var placeholder = isMovie
                ? _libraryManager.FindByPath(Path.Combine(folder, PlaceholderVideoGenerator.MoviePlaceholderFileName), false)
                : _libraryManager.FindByPath(folder, true);
            if (placeholder != null)
            {
                _libraryManager.DeleteItem(placeholder, new DeleteOptions { DeleteFileLocation = false }, true);
            }

            _logger.LogInformation("'{ItemName}' is now in the library: hid its discover placeholder {Folder}", itemName, folder);
        }
    }
}
