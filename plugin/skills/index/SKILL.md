---
name: token-squeeze:index
description: Index a local directory for symbol retrieval
argument-hint: "[path]"
allowed-tools: Bash
disable-model-invocation: true
---

# Index a Directory

Index a local directory so its symbols (functions, classes, methods, types, constants) can be queried with outline, extract, and find commands.

## Platform Binary Detection

Detect the platform and set the binary path:

- **Windows:** `${CLAUDE_PLUGIN_ROOT}/bin/win-x64/token-squeeze.exe`
- **macOS:** `${CLAUDE_PLUGIN_ROOT}/bin/osx-arm64/token-squeeze`

## Action

Run the index command:

```
<binary> index <path>
```

If no path argument is provided, default to the current working directory.

## Output

The command outputs JSON. Present a summary to the user showing:
- Number of files indexed
- Languages detected
- Cache location

Then check the project's `.gitignore` file (if it exists) to see whether `.cache/` is already covered — either by a literal `.cache/` entry or by a broader pattern like `*.Cache` or similar. Only offer to add `.cache/` to `.gitignore` if it is NOT already covered.
