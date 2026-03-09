#!/bin/bash
# publish.sh -- Build, tag, release, and publish token-squeeze
#
# Usage:
#   ./publish.sh          # bumps minor version (1.0.0 -> 1.1.0)
#   ./publish.sh 2.0.0    # explicit version
#   ./publish.sh patch     # bumps patch (1.0.0 -> 1.0.1)
#   ./publish.sh major     # bumps major (1.0.0 -> 2.0.0)
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
cd "$SCRIPT_DIR"

# ── Prerequisites ──────────────────────────────────────────────
for cmd in dotnet node npm gh git tar; do
  if ! command -v "$cmd" &>/dev/null; then
    echo "Error: '$cmd' is required but not installed." >&2
    exit 1
  fi
done

# Check npm auth
if ! npm whoami &>/dev/null; then
  echo "Error: Not logged in to npm. Run 'npm login' first." >&2
  exit 1
fi

# Check gh auth
if ! gh auth status &>/dev/null 2>&1; then
  echo "Error: Not logged in to GitHub CLI. Run 'gh auth login' first." >&2
  exit 1
fi

# Check clean working tree
if [[ -n "$(git status --porcelain)" ]]; then
  echo "Error: Working tree is not clean. Commit or stash changes first." >&2
  git status --short
  exit 1
fi

# ── Version ────────────────────────────────────────────────────
CURRENT_VERSION=$(node -p "require('./package.json').version")

if [[ -n "${1:-}" ]]; then
  case "$1" in
    major)
      IFS='.' read -r major minor patch <<< "$CURRENT_VERSION"
      NEW_VERSION="$((major + 1)).0.0"
      ;;
    minor)
      IFS='.' read -r major minor patch <<< "$CURRENT_VERSION"
      NEW_VERSION="${major}.$((minor + 1)).0"
      ;;
    patch)
      IFS='.' read -r major minor patch <<< "$CURRENT_VERSION"
      NEW_VERSION="${major}.${minor}.$((patch + 1))"
      ;;
    *)
      # Treat as explicit version
      if [[ ! "$1" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
        echo "Error: Invalid version '$1'. Use semver (e.g. 2.0.0) or major/minor/patch." >&2
        exit 1
      fi
      NEW_VERSION="$1"
      ;;
  esac
else
  # Default: bump minor
  IFS='.' read -r major minor patch <<< "$CURRENT_VERSION"
  NEW_VERSION="${major}.$((minor + 1)).0"
fi

TAG="v${NEW_VERSION}"

# Check tag doesn't already exist
if git rev-parse "$TAG" &>/dev/null; then
  echo "Error: Tag $TAG already exists." >&2
  exit 1
fi

echo "╔══════════════════════════════════════╗"
echo "║  token-squeeze publish               ║"
echo "╠══════════════════════════════════════╣"
echo "║  Version: $CURRENT_VERSION -> $NEW_VERSION"
echo "║  Tag:     $TAG"
echo "╚══════════════════════════════════════╝"
echo ""

# ── Build ──────────────────────────────────────────────────────
echo "▸ Building binaries..."
PROJECT="$SCRIPT_DIR/src/TokenSqueeze/TokenSqueeze.csproj"
BIN_DIR="$SCRIPT_DIR/plugin/bin"

dotnet publish "$PROJECT" \
  -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:IncludeAllContentForSelfExtract=true \
  -o "$BIN_DIR/win-x64" \
  -v quiet

dotnet publish "$PROJECT" \
  -c Release -r osx-arm64 --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:IncludeAllContentForSelfExtract=true \
  -o "$BIN_DIR/osx-arm64" \
  -v quiet

echo "  ✓ Binaries built"

# ── Package binaries ───────────────────────────────────────────
echo "▸ Packaging release archives..."
RELEASE_DIR="$SCRIPT_DIR/.release"
rm -rf "$RELEASE_DIR"
mkdir -p "$RELEASE_DIR"

# Windows zip
if [[ "$OSTYPE" == msys* || "$OSTYPE" == mingw* || "$OSTYPE" == cygwin* ]]; then
  WIN_DEST="$(cygpath -w "$RELEASE_DIR/token-squeeze-win-x64.zip")"
  (cd "$BIN_DIR/win-x64" && powershell -Command "Compress-Archive -Path 'token-squeeze.exe' -DestinationPath '$WIN_DEST' -Force")
else
  (cd "$BIN_DIR/win-x64" && zip "$RELEASE_DIR/token-squeeze-win-x64.zip" token-squeeze.exe)
fi

# macOS tar.gz
(cd "$BIN_DIR/osx-arm64" && tar -czf "$RELEASE_DIR/token-squeeze-osx-arm64.tar.gz" token-squeeze)

echo "  ✓ Archives created"

# ── Update versions ───────────────────────────────────────────
echo "▸ Updating version to $NEW_VERSION..."
node -e "
  const fs = require('fs');
  for (const f of ['package.json', 'plugin/.claude-plugin/plugin.json']) {
    const pkg = JSON.parse(fs.readFileSync(f, 'utf8'));
    pkg.version = '$NEW_VERSION';
    fs.writeFileSync(f, JSON.stringify(pkg, null, 2) + '\n');
  }
"
echo "  ✓ package.json and plugin.json updated"

# ── Confirmation ───────────────────────────────────────────────
echo ""
echo "Ready to publish. This will:"
echo "  1. Commit version bump"
echo "  2. Create tag $TAG"
echo "  3. Push to origin (with tags)"
echo "  4. Create GitHub release with binary archives"
echo "  5. Publish to npm registry"
echo ""
read -p "Proceed? [y/N] " -n 1 -r
echo ""

if [[ ! "$REPLY" =~ ^[Yy]$ ]]; then
  echo "Aborted. Version files were updated but not committed."
  echo "Run 'git checkout -- package.json plugin/.claude-plugin/plugin.json' to revert."
  exit 1
fi

# ── Git ────────────────────────────────────────────────────────
echo "▸ Committing and tagging..."
git add package.json plugin/.claude-plugin/plugin.json
git commit -m "release: v${NEW_VERSION}"
git tag -a "$TAG" -m "v${NEW_VERSION}"
git push origin main --follow-tags

echo "  ✓ Pushed $TAG"

# ── GitHub Release ─────────────────────────────────────────────
echo "▸ Creating GitHub release..."
gh release create "$TAG" \
  "$RELEASE_DIR/token-squeeze-win-x64.zip" \
  "$RELEASE_DIR/token-squeeze-osx-arm64.tar.gz" \
  --title "v${NEW_VERSION}" \
  --generate-notes

echo "  ✓ GitHub release created"

# ── npm publish ────────────────────────────────────────────────
echo "▸ Publishing to npm..."
npm publish

echo "  ✓ Published to npm"

# ── Cleanup ────────────────────────────────────────────────────
rm -rf "$RELEASE_DIR"

echo ""
echo "╔══════════════════════════════════════╗"
echo "║  Published token-squeeze $NEW_VERSION"
echo "║  npm:    npx token-squeeze@latest    ║"
echo "║  github: $TAG                        ║"
echo "╚══════════════════════════════════════╝"
