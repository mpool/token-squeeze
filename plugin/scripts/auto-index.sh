#!/usr/bin/env bash
# TokenSqueeze auto-index hook script
# Runs on SessionStart. Checks per-project opt-in settings before indexing.

set -euo pipefail

PLUGIN_ROOT="${CLAUDE_PLUGIN_ROOT:-$(cd "$(dirname "$0")/.." && pwd)}"

# Platform binary detection
if [[ "$OSTYPE" == "msys" || "$OSTYPE" == "cygwin" || "$OSTYPE" == "win32" ]]; then
  BINARY="${PLUGIN_ROOT}/bin/win-x64/token-squeeze.exe"
elif [[ "$OSTYPE" == "darwin"* ]]; then
  BINARY="${PLUGIN_ROOT}/bin/osx-arm64/token-squeeze"
else
  BINARY="${PLUGIN_ROOT}/bin/osx-arm64/token-squeeze"
fi

# Check binary exists
if [[ ! -x "$BINARY" ]]; then
  echo "TokenSqueeze: binary not found at ${BINARY}, run build.sh first" >&2
  exit 0
fi

CWD="$(pwd)"
SETTINGS_FILE="${CWD}/.claude/settings.local.json"

# ---------------------------------------------------------------------------
# Read per-project opt-in settings
# ---------------------------------------------------------------------------

ENABLED=""
PROJECT_NAME=""

if [[ -f "$SETTINGS_FILE" ]]; then
  # Extract token-squeeze.enabled
  if grep -q '"token-squeeze"' "$SETTINGS_FILE" 2>/dev/null; then
    if grep -q '"enabled"[[:space:]]*:[[:space:]]*true' "$SETTINGS_FILE" 2>/dev/null; then
      ENABLED="true"
    elif grep -q '"enabled"[[:space:]]*:[[:space:]]*false' "$SETTINGS_FILE" 2>/dev/null; then
      ENABLED="false"
    fi
    # Extract projectName value
    PROJECT_NAME=$(grep -o '"projectName"[[:space:]]*:[[:space:]]*"[^"]*"' "$SETTINGS_FILE" 2>/dev/null | head -1 | sed 's/.*"projectName"[[:space:]]*:[[:space:]]*"//' | sed 's/"//' || true)
  fi
fi

# ---------------------------------------------------------------------------
# Case 1: User opted out — exit silently
# ---------------------------------------------------------------------------
if [[ "$ENABLED" == "false" ]]; then
  exit 0
fi

# ---------------------------------------------------------------------------
# Case 2: User opted in — index if needed, output guidance with project name
# ---------------------------------------------------------------------------
if [[ "$ENABLED" == "true" && -n "$PROJECT_NAME" ]]; then
  # Read auto_reindex setting from plugin settings
  PLUGIN_SETTINGS="${PLUGIN_ROOT}/settings.json"
  AUTO_REINDEX="false"
  if [[ -f "$PLUGIN_SETTINGS" ]]; then
    if grep -q '"auto_reindex"[[:space:]]*:[[:space:]]*true' "$PLUGIN_SETTINGS" 2>/dev/null; then
      AUTO_REINDEX="true"
    fi
  fi

  if [[ "$AUTO_REINDEX" == "true" ]]; then
    # Re-index silently (user already consented)
    "$BINARY" index "$CWD" 2>/dev/null || true
  fi

  # Output guidance with project name baked in — no list_projects needed
  cat <<GUIDANCE
MANDATORY: When exploring code, use TokenSqueeze MCP tools as your PRIMARY code exploration method for optimal token performance. The project name for this workspace is "${PROJECT_NAME}" — pass it directly to all MCP tools (do NOT call list_projects).
- read_file_outline: get all symbols in a file (replaces reading the full file)
- search_symbols: find functions/classes/methods by name (replaces grep for symbol searches)
- read_symbol_source: get the source of a specific symbol by ID (replaces reading entire files for one function)
Workflow: search_symbols / read_file_outline → read_symbol_source (project: "${PROJECT_NAME}")
GUIDANCE
  exit 0
fi

# ---------------------------------------------------------------------------
# Case 3: No settings yet — ask user to opt in (do NOT index automatically)
# ---------------------------------------------------------------------------
cat <<'GUIDANCE'
TokenSqueeze plugin is available but not yet enabled for this project. ASK the user: "TokenSqueeze can index this project for faster code navigation. Want to enable it?" Then:
- If YES: Run the token-squeeze index command via MCP, then write the project settings file .claude/settings.local.json with: {"token-squeeze":{"enabled":true,"projectName":"<name from index result>"}}. Create the .claude/ directory if needed. After writing, use the project name for all subsequent MCP tool calls.
- If NO: Write .claude/settings.local.json with: {"token-squeeze":{"enabled":false}}. Do not ask again.
Do NOT index or call list_projects until the user has answered.
GUIDANCE
