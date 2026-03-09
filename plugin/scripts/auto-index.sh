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
    # Already indexed
    INDEXED=true
  fi
fi

if [[ "${INDEXED:-}" != "true" ]]; then
  # Run index on cwd
  INDEX_OUTPUT=$("$BINARY" index "$CWD" 2>/dev/null) || {
    echo "TokenSqueeze: auto-index failed" >&2
    exit 0
  }

  # Parse JSON output for summary
  FILES_INDEXED=$(echo "$INDEX_OUTPUT" | grep -o '"filesIndexed"[[:space:]]*:[[:space:]]*[0-9]*' | grep -o '[0-9]*' || echo "0")
  FILES_UPDATED=$(echo "$INDEX_OUTPUT" | grep -o '"filesUpdated"[[:space:]]*:[[:space:]]*[0-9]*' | grep -o '[0-9]*' || echo "0")

  if [[ "$FILES_INDEXED" != "0" || "$FILES_UPDATED" != "0" ]]; then
    echo "TokenSqueeze: indexed ${FILES_INDEXED} files (${FILES_UPDATED} updated)" >&2
  fi
fi

# Always output tool guidance to stdout (injected into conversation context)
cat <<'GUIDANCE'
MANDATORY: When exploring code, use TokenSqueeze MCP tools as your PRIMARY code exploration method for optimal token performance. If you launch codebase-analyzer or explorer agents, instruct them to prefer TokenSqueeze (search_symbols, read_file_outline, read_symbol_source) over Read/Grep for symbol lookups.
- read_file_outline: get all symbols in a file (replaces reading the full file)
- search_symbols: find functions/classes/methods by name (replaces grep for symbol searches)
- read_symbol_source: get the source of a specific symbol by ID (replaces reading entire files for one function)
Workflow: list_projects → search_symbols / read_file_outline → read_symbol_source
GUIDANCE
