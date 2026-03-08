---
name: token-squeeze:list
description: List all indexed projects with symbol counts and languages
argument-hint: ""
allowed-tools: Bash
disable-model-invocation: false
---

# List Indexed Projects

Show all projects that have been indexed by TokenSqueeze.

## Platform Binary Detection

Detect the platform and set the binary path:

- **Windows:** `${CLAUDE_PLUGIN_ROOT}/bin/win-x64/token-squeeze.exe`
- **macOS:** `${CLAUDE_PLUGIN_ROOT}/bin/osx-arm64/token-squeeze`

## Action

Run:

```
<binary> list
```

## Output

The command outputs JSON. Present the results as a table with columns:
- Project name
- Path
- File count
- Languages

If no projects are indexed, tell the user they can run `/token-squeeze:index` to index a directory.
