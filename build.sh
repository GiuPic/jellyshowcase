#!/usr/bin/env bash
# Compila JellyShowcase con il .NET SDK in Docker e, con --install, lo installa nel Jellyfin locale.
#   ./build.sh             compila soltanto
#   ./build.sh --install   compila, installa e riavvia Jellyfin
set -euo pipefail

ROOT="$(cd "$(dirname "$0")" && pwd)"
JELLYFIN_VERSION="12.0.0"   # versione dei pacchetti NuGet Jellyfin.Controller/Model (ABI minima)
PROJECT="src/Jellyfin.Plugin.JellyBridge/JellyBridge.csproj"
PLUGINS_DIR="/path/to/jellyfin/config/plugins"
CONTAINER="jellyfin"

docker run --rm \
  -v "$ROOT":/src -w /src \
  -v jellyshowcase-nuget:/root/.nuget/packages \
  mcr.microsoft.com/dotnet/sdk:10.0 \
  dotnet build "$PROJECT" -c Release -p:JellyfinVersion="$JELLYFIN_VERSION" -nologo -clp:ErrorsOnly

OUT="$ROOT/bin/Release/$JELLYFIN_VERSION"
DLL="$(ls "$OUT"/*.dll | grep -v -E '/(Jellyfin\.|MediaBrowser\.|Microsoft\.)' | head -1)"
VERSION="$(grep -oP '(?<=<AssemblyVersion>)[^<]+' "$ROOT/$PROJECT")"
NAME="$(grep -oP '(?<=public override string Name => ")[^"]+' "$ROOT/src/Jellyfin.Plugin.JellyBridge/Plugin.cs")"
echo "Compilato: $DLL (v$VERSION)"

[ "${1:-}" = "--install" ] || exit 0

docker stop "$CONTAINER" >/dev/null
rm -rf "$PLUGINS_DIR/${NAME}_"*
DEST="$PLUGINS_DIR/${NAME}_${VERSION}"
mkdir -p "$DEST"
cp "$DLL" "$DEST/"
docker start "$CONTAINER" >/dev/null
echo "Installato in $DEST, Jellyfin riavviato"
