#!/bin/zsh

set -euo pipefail

PROJECT_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
VELOPACK_VERSION="0.0.1298"
RUNTIME="${1:-win-x64}"
RELEASES_DIR="$PROJECT_ROOT/Releases/$RUNTIME"
REPO_FILE="$PROJECT_ROOT/update-repo-url.txt"
REPO_URL="${VPK_REPO_URL:-}"
TOKEN="${VPK_TOKEN:-}"

if [[ -z "$REPO_URL" && -f "$REPO_FILE" ]]; then
    REPO_URL="$(tr -d '\r' < "$REPO_FILE" | xargs)"
fi

if [[ -z "$REPO_URL" ]]; then
    echo "Set VPK_REPO_URL or create $REPO_FILE with your GitHub repository URL."
    exit 1
fi

if [[ -z "$TOKEN" ]]; then
    echo "Set VPK_TOKEN to a GitHub token with permission to create releases."
    exit 1
fi

echo "Uploading Velopack release from $RELEASES_DIR to $REPO_URL..."
dnx "vpk@$VELOPACK_VERSION" --yes --allow-roll-forward upload github \
    --outputDir "$RELEASES_DIR" \
    --repoUrl "$REPO_URL" \
    --token "$TOKEN" \
    --publish

echo "Upload complete."
