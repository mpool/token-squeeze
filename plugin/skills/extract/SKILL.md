---
name: token-squeeze:extract
description: Get the full source code of a symbol by its ID
argument-hint: "<symbol-id> [--batch <id1> <id2> ...]"
allowed-tools: Bash
disable-model-invocation: false
---

# Extract Symbol Source Code

Retrieve the full source code of one or more symbols by their IDs.

## Platform Binary Detection

Detect the platform and set the binary path:

- **Windows:** `${CLAUDE_PLUGIN_ROOT}/bin/win-x64/token-squeeze.exe`
- **macOS:** `${CLAUDE_PLUGIN_ROOT}/bin/osx-arm64/token-squeeze`

## Project Name Auto-Resolution

The user provides symbol IDs. You must resolve the project name automatically:

1. Run `<binary> list` and parse the JSON output.
2. Match the current working directory against the indexed project paths.
3. Use the matching project name for the extract command.

If no matching project is found, tell the user to run `/token-squeeze:index` first.

## Action

For a single symbol:

```
<binary> extract <project-name> <symbol-id>
```

For multiple symbols (batch):

```
<binary> extract <project-name> --batch <id1> <id2> ...
```

## Output

Present the extracted source code in a fenced code block with the appropriate language identifier (python, typescript, csharp, c, cpp, javascript). Include the symbol name, file path, and line range as context above the code block.
