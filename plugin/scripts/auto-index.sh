#!/usr/bin/env bash
# TokenSqueeze auto-index hook script
# Runs on SessionStart to ensure the current directory is indexed.

set -euo pipefail

PLUGIN_ROOT="${CLAUDE_PLUGIN_ROOT:-$(cd "$(dirname "$0")/.." && pwd)}"

# Platform binary detection
if [[ "$OSTYPE" == "msys" || "$OSTYPE" == "cygwin" || "$OSTYPE" == "win32" ]]; then
  BINARY="${PLUGIN_ROOT}/bin/win-x64/token-squeeze.exe"
elif [[ "$OSTYPE" == "darwin"* ]]; then
  BINARY="${PLUGIN_ROOT}/bin/osx-arm64/token-squeeze"
else
  # Linux or other Unix - try osx-arm64 as fallback (user may have built for their platform)
  BINARY="${PLUGIN_ROOT}/bin/osx-arm64/token-squeeze"
fi

# Check binary exists
if [[ ! -x "$BINARY" ]]; then
  echo "TokenSqueeze: binary not found at ${BINARY}, run build.sh first" >&2
  exit 0
fi

# Read auto_reindex setting from settings.json
SETTINGS_FILE="${PLUGIN_ROOT}/settings.json"
AUTO_REINDEX="false"
if [[ -f "$SETTINGS_FILE" ]]; then
  # Extract auto_reindex value (simple grep, no jq dependency)
  if grep -q '"auto_reindex"[[:space:]]*:[[:space:]]*true' "$SETTINGS_FILE" 2>/dev/null; then
    AUTO_REINDEX="true"
  fi
fi

CWD="$(pwd)"

if [[ "$AUTO_REINDEX" == "false" ]]; then
  # Check if cwd is already indexed
  LIST_OUTPUT=$("$BINARY" list 2>/dev/null) || {
    echo "TokenSqueeze: auto-index failed" >&2
    exit 0
  }

  # Check if any project path matches cwd
  if echo "$LIST_OUTPUT" | grep -q "$(printf '%s' "$CWD" | sed 's/[[\.*^$()+?{}|]/\\&/g')"; then
    # Already indexed, exit silently
    exit 0
  fi
fi

# Run index on cwd
INDEX_OUTPUT=$("$BINARY" index "$CWD" 2>/dev/null) || {
  echo "TokenSqueeze: auto-index failed" >&2
  exit 0
}

# Parse JSON output for summary
# Extract filesIndexed and filesUpdated counts if available
FILES_INDEXED=$(echo "$INDEX_OUTPUT" | grep -o '"filesIndexed"[[:space:]]*:[[:space:]]*[0-9]*' | grep -o '[0-9]*' || echo "0")
FILES_UPDATED=$(echo "$INDEX_OUTPUT" | grep -o '"filesUpdated"[[:space:]]*:[[:space:]]*[0-9]*' | grep -o '[0-9]*' || echo "0")

if [[ "$FILES_INDEXED" == "0" && "$FILES_UPDATED" == "0" ]]; then
  echo "TokenSqueeze: index up to date"
else
  echo "TokenSqueeze: indexed ${FILES_INDEXED} files (${FILES_UPDATED} updated)"
fi
