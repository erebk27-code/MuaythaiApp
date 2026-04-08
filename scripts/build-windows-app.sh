#!/bin/zsh

set -euo pipefail

PROJECT_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
APP_NAME="MuaythaiApp"
RUNTIME="${1:-win-x64}"
PUBLISH_DIR="$PROJECT_ROOT/bin/Release/net8.0/$RUNTIME/publish"
OUTPUT_DIR="${2:-$HOME/Desktop/${APP_NAME}-Windows-$RUNTIME}"
SOURCE_DB="$PROJECT_ROOT/muaythai.db"
UPDATE_REPO_SOURCE="$PROJECT_ROOT/update-repo-url.txt"

echo "Publishing $APP_NAME for $RUNTIME..."
dotnet publish "$PROJECT_ROOT/MuaythaiApp.csproj" -c Release -r "$RUNTIME" --self-contained true

echo "Preparing Windows bundle at $OUTPUT_DIR..."
rm -rf "$OUTPUT_DIR"
mkdir -p "$OUTPUT_DIR"
cp -R "$PUBLISH_DIR"/. "$OUTPUT_DIR/"

if [[ -f "$SOURCE_DB" ]]; then
    cp "$SOURCE_DB" "$OUTPUT_DIR/muaythai.db"
fi

if [[ -f "$UPDATE_REPO_SOURCE" ]]; then
    cp "$UPDATE_REPO_SOURCE" "$OUTPUT_DIR/update-repo-url.txt"
fi

cat > "$OUTPUT_DIR/README-WINDOWS.txt" <<EOF
MuaythaiApp Windows package

1. Copy this entire folder to your Windows computer.
2. Run MuaythaiApp.exe
3. On first launch, the app will copy muaythai.db into the Windows user data folder automatically.

Keep all files in this folder together.
EOF

echo "Windows bundle ready: $OUTPUT_DIR"
echo "Run on Windows: $OUTPUT_DIR/$APP_NAME.exe"
