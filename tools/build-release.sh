#!/usr/bin/env bash
# Build Linux + Windows self-contained binaries.
# Output (everything under one tidy folder, ready to attach to a GitHub release):
#   build/linux-x64/AnnoMapEditor        (linux executable, ~95 MB)
#   build/win-x64/AnnoMapEditor.exe      (windows executable, ~99 MB)
#
# Run from repo root:  ./tools/build-release.sh
set -euo pipefail

cd "$(dirname "$0")/.."

PROJECT="AnnoMapEditor/AnnoMapEditor.csproj"
PUBLISH_DIR="build"

rm -rf "$PUBLISH_DIR"
mkdir -p "$PUBLISH_DIR"

build() {
    local rid="$1"
    echo "==> Publishing $rid"
    dotnet publish "$PROJECT" \
        -c Release \
        -r "$rid" \
        --self-contained true \
        -p:PublishSingleFile=true \
        -p:IncludeNativeLibrariesForSelfExtract=true \
        -p:DebugType=embedded \
        -o "$PUBLISH_DIR/$rid" \
        > "$PUBLISH_DIR/$rid.log" 2>&1 || {
            echo "Build failed for $rid — see $PUBLISH_DIR/$rid.log"
            tail -20 "$PUBLISH_DIR/$rid.log"
            exit 1
        }
}

build linux-x64
build win-x64

echo
echo "Done. Binaries:"
ls -lh "$PUBLISH_DIR/linux-x64/AnnoMapEditor"
ls -lh "$PUBLISH_DIR/win-x64/AnnoMapEditor.exe"
