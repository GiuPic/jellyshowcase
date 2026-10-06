namespace Jellyfin.Plugin.JellyBridge.Utils;

/// <summary>
/// Short display names for streaming platforms, as shown in tags, studios and collections.
/// </summary>
public static class PlatformNames
{
    private static readonly Dictionary<int, string> ById = new()
    {
        [8] = "Netflix",
        [119] = "Prime Video",
        [9] = "Prime Video",
        [337] = "Disney+",
        [350] = "Apple TV+",
        [29] = "Sky Go",
        [39] = "NOW",
        [531] = "Paramount+",
        [109] = "Timvision",
        [283] = "Crunchyroll",
        [359] = "Mediaset Infinity",
        [110] = "Infinity+",
        [222] = "RaiPlay",
        [524] = "Discovery+",
        [11] = "MUBI",
    };

    /// <summary>
    /// Gets the display name for a TMDB watch provider.
    /// </summary>
    public static string Display(int providerId, string? providerName)
        => ById.TryGetValue(providerId, out var name) ? name : (providerName ?? providerId.ToString());

    /// <summary>
    /// Every name a platform tag may have had (current display names plus the TMDB/legacy spellings
    /// written by older syncs), so stale platform tags can be replaced.
    /// </summary>
    public static bool IsPlatformName(string name, IEnumerable<string> configuredNetworkNames)
        => ById.ContainsValue(name)
            || configuredNetworkNames.Contains(name, StringComparer.OrdinalIgnoreCase);
}
