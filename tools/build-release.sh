#!/usr/bin/env bash
# Build Linux + Windows self-contained binaries AND release-ready ZIPs.
#
# Output (everything under one tidy folder, ready to attach to a GitHub release):
#   build/linux-x64/AnnoMapEditor                          (~95 MB)
#   build/win-x64/AnnoMapEditor.exe                        (~99 MB)
#   build/AnnoMapEditor-v<version>-linux-x64.zip
#   build/AnnoMapEditor-v<version>-win-x64.zip
#
# Run from repo root:  ./tools/build-release.sh
set -euo pipefail

cd "$(dirname "$0")/.."

PROJECT="AnnoMapEditor/AnnoMapEditor.csproj"
PUBLISH_DIR="build"

# Read version from csproj so the ZIP filenames track the actual build.
VERSION="$(grep -oP '(?<=<Version>)[^<]+' "$PROJECT" | head -1)"
[ -z "$VERSION" ] && VERSION="0.0.0"

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

# Zip the published folder for a given runtime identifier. Uses `zip -j` so the
# archive flat-extracts (no `linux-x64/` prefix inside the ZIP), which is what
# users expect when they download a release asset.
zip_release() {
    local rid="$1"
    local zip_name="AnnoMapEditor-v${VERSION}-${rid}.zip"
    local zip_path="$PUBLISH_DIR/$zip_name"
    rm -f "$zip_path"
    (cd "$PUBLISH_DIR/$rid" && zip -q -9 "../$zip_name" *)
    echo "Packed $zip_path"
}

build linux-x64
build win-x64

zip_release linux-x64
zip_release win-x64

# Build AppImage
APPIMAGETOOL="$(command -v appimagetool || true)"
if [ -z "$APPIMAGETOOL" ]; then
    APPIMAGETOOL="$PUBLISH_DIR/appimagetool-x86_64.AppImage"
    if [ ! -x "$APPIMAGETOOL" ]; then
        echo "==> Downloading appimagetool"
        curl -L -o "$APPIMAGETOOL" \
            https://github.com/AppImage/AppImageKit/releases/download/continuous/appimagetool-x86_64.AppImage
        chmod +x "$APPIMAGETOOL"
    fi
fi

APPDIR="$PUBLISH_DIR/AppDir"
rm -rf "$APPDIR"
mkdir -p "$APPDIR/usr/bin"
cp "$PUBLISH_DIR/linux-x64/AnnoMapEditor" "$APPDIR/usr/bin/"
chmod +x "$APPDIR/usr/bin/AnnoMapEditor"

cat > "$APPDIR/AppRun" <<'EOF'
#!/bin/sh
HERE="$(dirname "$(readlink -f "$0")")"
exec "$HERE/usr/bin/AnnoMapEditor" "$@"
EOF
chmod +x "$APPDIR/AppRun"

cat > "$APPDIR/AnnoMapEditor.desktop" <<EOF
[Desktop Entry]
Type=Application
Name=Anno Map Editor
Exec=AnnoMapEditor
Icon=AnnoMapEditor
Categories=Game;Utility;
Terminal=false
EOF

# Use existing icon if available, else placeholder
for icon in AnnoMapEditor/Assets/icon.png AnnoMapEditor/Assets/Icons/icon.png; do
    if [ -f "$icon" ]; then
        cp "$icon" "$APPDIR/AnnoMapEditor.png"
        break
    fi
done
[ ! -f "$APPDIR/AnnoMapEditor.png" ] && printf '\x89PNG\r\n\x1a\n\x00\x00\x00\rIHDR\x00\x00\x00\x01\x00\x00\x00\x01\x08\x06\x00\x00\x00\x1f\x15\xc4\x89\x00\x00\x00\rIDATx\x9cc\x00\x01\x00\x00\x05\x00\x01\x0d\n-\xb4\x00\x00\x00\x00IEND\xaeB`\x82' > "$APPDIR/AnnoMapEditor.png"

echo "==> Building AppImage"
ARCH=x86_64 "$APPIMAGETOOL" "$APPDIR" "$PUBLISH_DIR/AnnoMapEditor-v${VERSION}-x86_64.AppImage"

echo
echo "Done. Release artefacts:"
ls -lh "$PUBLISH_DIR/linux-x64/AnnoMapEditor" \
       "$PUBLISH_DIR/win-x64/AnnoMapEditor.exe" \
       "$PUBLISH_DIR"/AnnoMapEditor-v"$VERSION"-*.zip \
       "$PUBLISH_DIR"/AnnoMapEditor-v"$VERSION"-x86_64.AppImage
