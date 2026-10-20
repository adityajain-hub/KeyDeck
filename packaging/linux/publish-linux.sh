#!/usr/bin/env bash
set -e

echo "=========================================================="
echo " Building KeyDeck for Linux (linux-x64)                   "
echo "=========================================================="

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
OUTPUT_DIR="$ROOT_DIR/publish/linux"

mkdir -p "$OUTPUT_DIR"

echo "[1/3] Publishing single-file self-contained binary..."
dotnet publish "$ROOT_DIR/src/KeyDeck.UI.Avalonia/KeyDeck.UI.Avalonia.csproj" \
    -c Release \
    -r linux-x64 \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:EnableCompressionInSingleFile=true \
    -o "$OUTPUT_DIR"

echo "[2/3] Copying packaging assets..."
cp "$ROOT_DIR/packaging/linux/99-keydeck.rules" "$OUTPUT_DIR/"
cp "$ROOT_DIR/packaging/linux/setup-permissions.sh" "$OUTPUT_DIR/"
cp "$ROOT_DIR/packaging/linux/keydeck.desktop" "$OUTPUT_DIR/"
chmod +x "$OUTPUT_DIR/KeyDeck.UI.Avalonia"
chmod +x "$OUTPUT_DIR/setup-permissions.sh"

echo "[3/3] Creating distribution archive..."
cd "$OUTPUT_DIR"
tar -czvf "$ROOT_DIR/publish/KeyDeck-Linux-x64.tar.gz" *

echo "=========================================================="
echo " ✅ Build complete!"
echo " Archive: publish/KeyDeck-Linux-x64.tar.gz"
echo "=========================================================="
