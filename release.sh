#!/usr/bin/env bash
# Pubblica una versione di JellyShowcase: compila, crea lo zip, aggiorna manifest.json,
# fa commit, tag e push, e crea la Release su GitHub con lo zip.
#   ./release.sh "Note di rilascio"
# La versione è quella di <AssemblyVersion> in JellyBridge.csproj.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")" && pwd)"
cd "$ROOT"
REPO="GiuPic/jellyshowcase"
GUID="3797d820-4353-4556-9cd9-b43c40777ae9"
TARGET_ABI="12.0.0.0"
NOTES="${1:?uso: ./release.sh \"note di rilascio\"}"
VERSION="$(grep -oP '(?<=<AssemblyVersion>)[^<]+' src/Jellyfin.Plugin.JellyBridge/JellyBridge.csproj)"
TAG="v$VERSION"
ZIP="jellyshowcase_$VERSION.zip"

[ -z "$(git status --porcelain)" ] || { echo "Ci sono modifiche non committate"; exit 1; }
git rev-parse -q --verify "refs/tags/$TAG" >/dev/null && { echo "Il tag $TAG esiste già: aggiorna la versione"; exit 1; }

./build.sh
rm -rf release && mkdir release
python3 -c "import sys,zipfile; z=zipfile.ZipFile(sys.argv[1],'w',zipfile.ZIP_DEFLATED); z.write(sys.argv[2],'JellyShowcase.dll'); z.close()" "release/$ZIP" "bin/Release/12.0.0/JellyShowcase.dll"
CHECKSUM="$(md5sum "release/$ZIP" | cut -d' ' -f1)"

python3 - "$VERSION" "$CHECKSUM" "$NOTES" "$REPO" "$GUID" "$TARGET_ABI" "$ZIP" <<'EOF'
import datetime, json, os, sys
version, checksum, notes, repo, guid, abi, zipname = sys.argv[1:8]
path = 'manifest.json'
manifest = json.load(open(path)) if os.path.exists(path) else [{
    "guid": guid,
    "name": "JellyShowcase",
    "description": "Browse and request Seerr discover content from any Jellyfin client.",
    "overview": "Discover library from Seerr with request button, streaming platforms and collections.",
    "owner": repo.split('/')[0],
    "category": "General",
    "imageUrl": f"https://raw.githubusercontent.com/{repo}/main/Screenshots/Icon.svg",
    "versions": [],
}]
plugin = manifest[0]
plugin["versions"] = [v for v in plugin["versions"] if v["version"] != version]
plugin["versions"].insert(0, {
    "version": version,
    "changelog": notes,
    "targetAbi": abi,
    "sourceUrl": f"https://github.com/{repo}/releases/download/v{version}/{zipname}",
    "checksum": checksum,
    "timestamp": datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
})
json.dump(manifest, open(path, "w"), indent=2, ensure_ascii=False)
open(path, "a").write("\n")
EOF

git add manifest.json
git commit -q -m "Release $TAG"
git tag "$TAG"
git push -q origin main "$TAG"
gh release create "$TAG" "release/$ZIP" --repo "$REPO" --title "JellyShowcase $VERSION" --notes "$NOTES"
echo "Pubblicata $TAG ($ZIP, md5 $CHECKSUM)"
