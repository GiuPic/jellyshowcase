using System.Reflection;
using System.Text.Json;
using Jellyfin.Plugin.JellyBridge.BridgeModels;
using Jellyfin.Plugin.JellyBridge.Services;
using Jellyfin.Plugin.JellyBridge.Utils;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JellyBridge.Controllers;

/// <summary>
/// Endpoints behind the "Richiedi" button that the web client script adds to discover items:
/// the request status of an item and the request itself, made as the logged-in user (X-API-User),
/// so it follows that user's permissions and stays pending until an admin approves it.
/// </summary>
[ApiController]
[Route("JellyShowcase")]
public class RequestController : ControllerBase
{
    private readonly DebugLogger<RequestController> _logger;
    private readonly ILibraryManager _libraryManager;
    private readonly ApiService _apiService;
    private readonly FavoriteService _favoriteService;

    public RequestController(ILoggerFactory loggerFactory, ILibraryManager libraryManager, ApiService apiService, FavoriteService favoriteService)
    {
        _logger = new DebugLogger<RequestController>(loggerFactory.CreateLogger<RequestController>());
        _libraryManager = libraryManager;
        _apiService = apiService;
        _favoriteService = favoriteService;
    }

    /// <summary>
    /// The web client script, injected into index.html by ShowcaseScriptStartupFilter.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("client.js")]
    public IActionResult GetClientScript()
    {
        var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(typeof(Plugin).Namespace + ".Web.client.js");
        if (stream == null)
        {
            return NotFound();
        }
        Response.Headers["Cache-Control"] = "no-cache";
        return File(stream, "application/javascript; charset=utf-8");
    }

    [Authorize]
    [HttpGet("Status/{itemId}")]
    public async Task<IActionResult> GetStatus([FromRoute] Guid itemId)
    {
        var media = ResolveShowcaseItem(itemId);
        if (media == null)
        {
            return Ok(new { showcase = false });
        }

        try
        {
            using var doc = await _apiService.GetJsonAsync($"/api/v1/{media.Value.kind}/{media.Value.tmdbId}");
            return Ok(new { showcase = true, status = ReadStatus(doc) });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read request status for item {ItemId}", itemId);
            return Ok(new { showcase = true, status = "unknown" });
        }
    }

    [Authorize]
    [HttpPost("Request/{itemId}")]
    public async Task<IActionResult> CreateRequest([FromRoute] Guid itemId)
    {
        var media = ResolveShowcaseItem(itemId);
        if (media == null)
        {
            return BadRequest(new { message = "Questo titolo non è nella Vetrina." });
        }

        var userId = GetJellyfinUserId();
        var users = await _favoriteService.GetJellyseerrUsersAsync();
        var seerrUser = users.FirstOrDefault(u => Guid.TryParse(u.JellyfinUserGuid, out var g) && g == userId);
        if (seerrUser == null)
        {
            return BadRequest(new { message = "Il tuo utente non è collegato a Seerr: chiedi all'amministratore." });
        }

        var parameters = new Dictionary<string, object>
        {
            ["mediaType"] = media.Value.kind,
            ["mediaId"] = int.Parse(media.Value.tmdbId, System.Globalization.CultureInfo.InvariantCulture),
        };
        if (media.Value.kind == "tv")
        {
            parameters["seasons"] = "all";
        }

        // CallEndpointAsync logs failures and returns an empty result instead of throwing:
        // a request only counts as created when Seerr returned it with an id.
        var result = await _apiService.CallEndpointAsync(JellyseerrEndpoint.CreateRequest, parameters: parameters, actAsUserId: seerrUser.Id);
        if (result is not JellyseerrMediaRequest { Id: > 0 })
        {
            _logger.LogWarning("Request from the web button failed: {Kind} {TmdbId} by {User}", media.Value.kind, media.Value.tmdbId, seerrUser.DisplayName);
            return BadRequest(new { message = "Richiesta non riuscita: forse hai raggiunto il limite di richieste. Riprova più tardi." });
        }
        _logger.LogInformation("Request created from the web button: {Kind} {TmdbId} by {User}", media.Value.kind, media.Value.tmdbId, seerrUser.DisplayName);

        using var doc = await _apiService.GetJsonAsync($"/api/v1/{media.Value.kind}/{media.Value.tmdbId}");
        return Ok(new { showcase = true, status = ReadStatus(doc) });
    }

    /// <summary>
    /// Returns the Seerr media type and TMDB id when the item is a discover placeholder.
    /// </summary>
    private (string kind, string tmdbId)? ResolveShowcaseItem(Guid itemId)
    {
        var item = _libraryManager.GetItemById(itemId);
        if (item is not (Movie or Series) || !FolderUtils.IsPathInSyncDirectory(item.Path))
        {
            return null;
        }
        var tmdbId = item.GetProviderId(MediaBrowser.Model.Entities.MetadataProvider.Tmdb);
        return string.IsNullOrEmpty(tmdbId) ? null : (item is Movie ? "movie" : "tv", tmdbId);
    }

    private Guid GetJellyfinUserId()
    {
        var value = User.FindFirst("Jellyfin-UserId")?.Value;
        return Guid.TryParse(value, out var id) ? id : Guid.Empty;
    }

    /// <summary>
    /// Maps Seerr media/request status to: none, pending, processing, partial, available, declined.
    /// </summary>
    private static string ReadStatus(JsonDocument? doc)
    {
        if (doc == null || !doc.RootElement.TryGetProperty("mediaInfo", out var info) || info.ValueKind != JsonValueKind.Object)
        {
            return "none";
        }

        // MediaStatus: 1 unknown, 2 pending, 3 processing, 4 partially available, 5 available.
        var mediaStatus = info.TryGetProperty("status", out var s) ? s.GetInt32() : 1;
        if (mediaStatus == 5) return "available";
        if (mediaStatus == 4) return "partial";

        // RequestStatus: 1 pending approval, 2 approved, 3 declined, 4 failed, 5 completed.
        var requests = info.TryGetProperty("requests", out var r) && r.ValueKind == JsonValueKind.Array
            ? r.EnumerateArray().Select(x => x.GetProperty("status").GetInt32()).ToList()
            : new List<int>();
        if (requests.Contains(1)) return "pending";
        if (mediaStatus == 3 || requests.Contains(2)) return "processing";
        if (requests.Count > 0 && requests.All(x => x == 3)) return "declined";
        return "none";
    }
}
