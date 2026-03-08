#!/bin/bash
# build.sh -- Publish self-contained binaries for both platforms
set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
PROJECT="$REPO_ROOT/src/TokenSqueeze/TokenSqueeze.csproj"
OUTPUT_DIR="$SCRIPT_DIR/bin"

echo "Publishing win-x64..."
dotnet publish "$PROJECT" \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:IncludeAllContentForSelfExtract=true \
  -o "$OUTPUT_DIR/win-x64"

echo "Publishing osx-arm64..."
dotnet publish "$PROJECT" \
  -c Release \
  -r osx-arm64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:IncludeAllContentForSelfExtract=true \
  -o "$OUTPUT_DIR/osx-arm64"

echo "Build complete. Binaries in $OUTPUT_DIR"
