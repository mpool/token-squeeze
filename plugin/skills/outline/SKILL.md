---
name: token-squeeze:outline
description: Show symbols (functions, classes, methods, types) in a file from the index
argument-hint: "<file-path>"
allowed-tools: Bash
disable-model-invocation: false
---

# Outline Symbols in a File

Show all symbols defined in a specific file from the project index.

## Platform Binary Detection

Detect the platform and set the binary path:

- **Windows:** `${CLAUDE_PLUGIN_ROOT}/bin/win-x64/token-squeeze.exe`
- **macOS:** `${CLAUDE_PLUGIN_ROOT}/bin/osx-arm64/token-squeeze`

## Project Name Auto-Resolution

The user only provides a file path. You must resolve the project name automatically:

1. Run `<binary> list` and parse the JSON output.
2. Match the current working directory against the indexed project paths.
3. Use the matching project name for the outline command.

If no matching project is found, tell the user to run `/token-squeeze:index` first.

## Action

Run:

```
<binary> outline <project-name> <file-path>
```

## Output

Present a hierarchical list of symbols showing:
- Symbol name and kind (function, class, method, type, constant)
- Signature (parameters and return type where available)
- Nested symbols indented under their parent (e.g., methods under classes)
