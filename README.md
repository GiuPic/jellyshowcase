# JellyShowcase

A Jellyfin plugin that turns your [Seerr](https://seerr.dev) (or Jellyseerr / Overseerr) discover content into a
browsable Jellyfin library, so users can find new movies and series and request them from any Jellyfin client.

JellyShowcase is a fork of [JellyBridge](https://github.com/kinggeorges12/JellyBridge) by kinggeorges12, with
these changes:

- **Requests stay pending.** Requests are created *as the user* (`X-API-User`), so they follow that user's
  permissions and quotas and wait for an administrator's approval (in JellyBridge they were created with the
  admin's permissions and auto-approved).
- **Authenticated API.** All plugin endpoints require a Jellyfin administrator (in JellyBridge they were
  reachable without login, including the plugin configuration with the Seerr API key).
- **"Request" button** on discover items in the web client and web-based apps, showing the live status
  (pending approval, downloading, available). Favoriting an item (❤️) still works as a request, e.g. on TV apps.
- **Streaming platforms.** Each item gets the platforms where it is actually available in your region
  (subscription, free or with ads) as tags and studios, and one collection per platform
  ("Discover - Netflix", ...), browsable from every client.
- **Requested items stay visible** in the discover library until the real media arrives in a main library,
  then the placeholder is hidden automatically.

## Requirements

- Jellyfin **12.0** or newer
- Seerr, Jellyseerr or Overseerr, with an API key
- A folder for the discover library that Jellyfin can read and write

## Installation

1. In Jellyfin: **Dashboard → Plugins → Repositories → +** and add
   `https://raw.githubusercontent.com/GiuPic/jellyshowcase/main/manifest.json`
2. Install **JellyShowcase** from the catalog and restart Jellyfin.
3. Open the plugin page, set the Seerr URL, API key, library folder, region and networks, then create the
   discover library and run the sync.

Users must exist in Seerr (import them from Jellyfin in Seerr's user settings) to request from the button.

## Building

`./build.sh` builds the plugin with the .NET 10 SDK in Docker. `./build.sh --install` also copies it into a
local Jellyfin (set `PLUGINS_DIR` and `CONTAINER` in a `.build.env` file). `./release.sh "notes"` publishes a
GitHub release and updates `manifest.json`.

The documentation of the features inherited from JellyBridge is in [README.JellyBridge.md](README.JellyBridge.md).

## License

GNU General Public License v3.0, like the original project. See [LICENSE](LICENSE).
