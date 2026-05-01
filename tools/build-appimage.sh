#!/usr/bin/env bash
# Wrap the linux-x64 publish into a portable AppImage.
# Run AFTER ./tools/build-release.sh (it expects build/linux-x64/AnnoMapEditor to exist).
#
# Downloads appimagetool on first run if it's not on PATH.
# Output:  build/AnnoMapEditor-<version>-x86_64.AppImage
set -euo pipefail

cd "$(dirname "$0")/.."

LINUX_BIN="build/linux-x64/AnnoMapEditor"
if [ ! -x "$LINUX_BIN" ]; then
    echo "Missing $LINUX_BIN — run ./tools/build-release.sh first."
    exit 1
fi

# Read version from csproj so the AppImage filename matches.
VERSION="$(grep -oP '(?<=<Version>)[^<]+' AnnoMapEditor/AnnoMapEditor.csproj | head -1)"
[ -z "$VERSION" ] && VERSION="0.0.0"

APPDIR="build/AppDir"
rm -rf "$APPDIR"
mkdir -p "$APPDIR/usr/bin"

cp "$LINUX_BIN" "$APPDIR/usr/bin/AnnoMapEditor"
chmod +x "$APPDIR/usr/bin/AnnoMapEditor"

# AppRun trampoline — what the AppImage runs.
cat > "$APPDIR/AppRun" <<'EOF'
#!/bin/sh
HERE="$(dirname "$(readlink -f "$0")")"
exec "$HERE/usr/bin/AnnoMapEditor" "$@"
EOF
chmod +x "$APPDIR/AppRun"

# Minimal .desktop (required by appimagetool).
cat > "$APPDIR/AnnoMapEditor.desktop" <<EOF
[Desktop Entry]
Type=Application
Name=Anno Map Editor
Exec=AnnoMapEditor
Icon=AnnoMapEditor
Categories=Game;Utility;
Terminal=false
EOF

# Use the existing window icon if available, otherwise generate a placeholder so
# appimagetool doesn't fail on missing icon.
ICON_SRC=""
for c in AnnoMapEditor/Assets/icon.png AnnoMapEditor/Assets/Icons/icon.png; do
    [ -f "$c" ] && ICON_SRC="$c" && break
done
if [ -n "$ICON_SRC" ]; then
    cp "$ICON_SRC" "$APPDIR/AnnoMapEditor.png"
else
    # 1x1 transparent PNG as a stand-in. Replace with a real icon later.
    printf '\x89PNG\r\n\x1a\n\x00\x00\x00\rIHDR\x00\x00\x00\x01\x00\x00\x00\x01\x08\x06\x00\x00\x00\x1f\x15\xc4\x89\x00\x00\x00\rIDATx\x9cc\x00\x01\x00\x00\x05\x00\x01\x0d\n-\xb4\x00\x00\x00\x00IEND\xaeB`\x82' \
        > "$APPDIR/AnnoMapEditor.png"
fi

# Locate or download appimagetool.
APPIMAGETOOL="$(command -v appimagetool || true)"
if [ -z "$APPIMAGETOOL" ]; then
    APPIMAGETOOL="build/appimagetool-x86_64.AppImage"
    if [ ! -x "$APPIMAGETOOL" ]; then
        echo "==> Downloading appimagetool"
        curl -L -o "$APPIMAGETOOL" \
            https://github.com/AppImage/AppImageKit/releases/download/continuous/appimagetool-x86_64.AppImage
        chmod +x "$APPIMAGETOOL"
    fi
fi

OUT="build/AnnoMapEditor-${VERSION}-x86_64.AppImage"
echo "==> Building $OUT"
ARCH=x86_64 "$APPIMAGETOOL" "$APPDIR" "$OUT"
ls -lh "$OUT"
