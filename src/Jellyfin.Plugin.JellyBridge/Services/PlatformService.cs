using Jellyfin.Data.Enums;
using Jellyfin.Plugin.JellyBridge.BridgeModels;
using Jellyfin.Plugin.JellyBridge.Configuration;
using Jellyfin.Plugin.JellyBridge.Utils;
using MediaBrowser.Controller.Collections;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JellyBridge.Services;

/// <summary>
/// Applies the streaming platforms of each discover item to Jellyfin: as tags and studios on the
/// item (locked, so metadata refreshes keep them) and as one collection per platform, so the
/// discover library can be browsed by platform from every client, TVs included.
/// </summary>
public class PlatformService
{
    private readonly DebugLogger<PlatformService> _logger;
    private readonly MetadataService _metadataService;
    private readonly ILibraryManager _libraryManager;
    private readonly ICollectionManager _collectionManager;

    public PlatformService(ILogger<PlatformService> logger, MetadataService metadataService,
        ILibraryManager libraryManager, ICollectionManager collectionManager)
    {
        _logger = new DebugLogger<PlatformService>(logger);
        _metadataService = metadataService;
        _libraryManager = libraryManager;
        _collectionManager = collectionManager;
    }

    public async Task<(int updatedItems, int collections)> ApplyAsync(CancellationToken cancellationToken)
    {
        var networks = Plugin.GetConfigOrDefault<List<JellyseerrNetwork>>(nameof(PluginConfiguration.NetworkMap)) ?? new();
        var networkNames = networks.Select(n => n.Name).ToList();
        var (movies, shows) = await _metadataService.ReadMetadataAsync();

        var members = new Dictionary<string, List<Guid>>(StringComparer.OrdinalIgnoreCase);
        var updated = 0;
        foreach (var item in movies.Cast<IJellyseerrItem>().Concat(shows))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var dir = _metadataService.GetJellyBridgeItemDirectory(item);
            if (File.Exists(Path.Combine(dir, BridgeService.IgnoreFileName)))
            {
                continue;
            }

            var jfItem = item is JellyseerrMovie
                ? _libraryManager.FindByPath(Path.Combine(dir, PlaceholderVideoGenerator.MoviePlaceholderFileName), false)
                : _libraryManager.FindByPath(dir, true);
            if (jfItem == null)
            {
                continue;
            }

            var providers = item.Providers ?? new List<string>();
            foreach (var provider in providers)
            {
                if (!members.TryGetValue(provider, out var ids))
                {
                    members[provider] = ids = new List<Guid>();
                }
                ids.Add(jfItem.Id);
            }

            if (ApplyToItem(jfItem, providers, networkNames))
            {
                await _libraryManager.UpdateItemAsync(jfItem, jfItem.GetParent(), ItemUpdateType.MetadataEdit, cancellationToken);
                updated++;
            }
        }

        var collections = 0;
        if (Plugin.GetConfigOrDefault<bool>(nameof(PluginConfiguration.EnablePlatformCollections)))
        {
            collections = await SyncCollectionsAsync(members, cancellationToken);
        }

        _logger.LogInformation("Platforms applied: {Updated} items updated, {Collections} collections in sync", updated, collections);
        return (updated, collections);
    }

    /// <summary>
    /// Replaces the platform tags/studios of the item (keeping the others). Returns true when it changed.
    /// </summary>
    private static bool ApplyToItem(BaseItem item, List<string> providers, List<string> networkNames)
    {
        var tags = item.Tags.Where(t => !PlatformNames.IsPlatformName(t, networkNames)).Concat(providers)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var studios = providers.Concat(item.Studios.Where(s => !PlatformNames.IsPlatformName(s, networkNames)))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var locked = item.LockedFields.Union(new[] { MetadataField.Tags, MetadataField.Studios }).ToArray();

        if (tags.SequenceEqual(item.Tags) && studios.SequenceEqual(item.Studios) && locked.Length == item.LockedFields.Length)
        {
            return false;
        }

        item.Tags = tags;
        item.Studios = studios;
        item.LockedFields = locked;
        return true;
    }

    private async Task<int> SyncCollectionsAsync(Dictionary<string, List<Guid>> members, CancellationToken cancellationToken)
    {
        var prefix = Plugin.GetConfigOrDefault<string>(nameof(PluginConfiguration.PlatformCollectionPrefix)) ?? string.Empty;
        var existing = _libraryManager.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = new[] { BaseItemKind.BoxSet },
                Recursive = true,
            })
            .OfType<BoxSet>()
            .Where(b => prefix.Length > 0 && b.Name.StartsWith(prefix, StringComparison.Ordinal))
            .ToDictionary(b => b.Name, StringComparer.Ordinal);

        foreach (var (platform, ids) in members)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var name = prefix + platform;
            if (!existing.TryGetValue(name, out var boxSet))
            {
                boxSet = await _collectionManager.CreateCollectionAsync(new CollectionCreationOptions
                {
                    Name = name,
                    IsLocked = true,
                    ItemIdList = ids.Select(i => i.ToString("N")).ToArray(),
                });
                _logger.LogInformation("Created collection {Name} with {Count} items", name, ids.Count);
                continue;
            }

            existing.Remove(name);
            var current = boxSet.GetLinkedChildren().Select(c => c.Id).ToHashSet();
            var toAdd = ids.Where(id => !current.Contains(id)).ToList();
            var toRemove = current.Where(id => !ids.Contains(id)).ToList();
            if (toAdd.Count > 0)
            {
                await _collectionManager.AddToCollectionAsync(boxSet.Id, toAdd);
            }
            if (toRemove.Count > 0)
            {
                await _collectionManager.RemoveFromCollectionAsync(boxSet.Id, toRemove);
            }
        }

        // Platforms no longer present: empty their collection instead of deleting it (no destructive deletes).
        foreach (var stale in existing.Values)
        {
            var current = stale.GetLinkedChildren().Select(c => c.Id).ToList();
            if (current.Count > 0)
            {
                await _collectionManager.RemoveFromCollectionAsync(stale.Id, current);
            }
        }

        return members.Count;
    }
}
