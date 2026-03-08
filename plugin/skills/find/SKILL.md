---
name: token-squeeze:find
description: Search symbols by name, signature, or docstring in the index
argument-hint: "<query> [--kind function|class|method|constant|type] [--path glob]"
allowed-tools: Bash
disable-model-invocation: false
---

# Find Symbols by Query

Search for symbols across all indexed files by name, signature, or docstring content.

## Platform Binary Detection

Detect the platform and set the binary path:

- **Windows:** `${CLAUDE_PLUGIN_ROOT}/bin/win-x64/token-squeeze.exe`
- **macOS:** `${CLAUDE_PLUGIN_ROOT}/bin/osx-arm64/token-squeeze`

## Project Name Auto-Resolution

The user provides a search query. You must resolve the project name automatically:

1. Run `<binary> list` and parse the JSON output.
2. Match the current working directory against the indexed project paths.
3. Use the matching project name for the find command.

If no matching project is found, tell the user to run `/token-squeeze:index` first.

## Action

Run:

```
<binary> find <project-name> <query> [--kind function|class|method|constant|type] [--path glob]
```

Optional filters:
- `--kind`: Filter by symbol kind (function, class, method, constant, type)
- `--path`: Filter by file path glob pattern

## Output

Present search results showing:
- Symbol name and kind
- File path and line number
- Signature (parameters and return type)

If no results found, suggest broadening the query or trying different filters.
