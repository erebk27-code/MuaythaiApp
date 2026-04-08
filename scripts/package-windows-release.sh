#!/bin/zsh

set -euo pipefail

PROJECT_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
APP_NAME="MuaythaiApp"
APP_ID="com.muaythai.desktop"
VELOPACK_VERSION="0.0.1298"
RUNTIME="${2:-win-x64}"
VERSION="${1:-}"
PUBLISH_DIR="$PROJECT_ROOT/bin/Release/net8.0/$RUNTIME/publish"
RELEASES_DIR="$PROJECT_ROOT/Releases/$RUNTIME"
UPDATE_REPO_SOURCE="$PROJECT_ROOT/update-repo-url.txt"

if [[ -z "$VERSION" ]]; then
    echo "Usage: $0 <version> [runtime]"
    exit 1
fi

echo "Publishing $APP_NAME $VERSION for $RUNTIME..."
dotnet publish "$PROJECT_ROOT/MuaythaiApp.csproj" -c Release -r "$RUNTIME" --self-contained true -p:Version="$VERSION"

if [[ -f "$UPDATE_REPO_SOURCE" ]]; then
    cp "$UPDATE_REPO_SOURCE" "$PUBLISH_DIR/update-repo-url.txt"
fi

mkdir -p "$RELEASES_DIR"

echo "Packing Velopack release into $RELEASES_DIR..."
dnx "vpk@$VELOPACK_VERSION" --yes --allow-roll-forward pack \
    --channel win \
    --packId "$APP_ID" \
    --packVersion "$VERSION" \
    --runtime "$RUNTIME" \
    --packDir "$PUBLISH_DIR" \
    --mainExe "$APP_NAME.exe" \
    --packTitle "$APP_NAME" \
    --outputDir "$RELEASES_DIR"

echo "Velopack release ready: $RELEASES_DIR"
