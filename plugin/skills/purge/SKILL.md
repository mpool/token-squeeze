---
name: token-squeeze:purge
description: Delete the TokenSqueeze index for a project
argument-hint: "<project-name>"
allowed-tools: Bash
disable-model-invocation: true
---

# Purge a Project Index

Delete the stored index data for a named project. This is destructive and cannot be undone.

## Platform Binary Detection

Detect the platform and set the binary path:

- **Windows:** `${CLAUDE_PLUGIN_ROOT}/bin/win-x64/token-squeeze.exe`
- **macOS:** `${CLAUDE_PLUGIN_ROOT}/bin/osx-arm64/token-squeeze`

## Action

Run:

```
<binary> purge <project-name>
```

The project name argument is required. If not provided, tell the user to specify one. They can run `/token-squeeze:list` to see available project names.

## Output

Confirm what was deleted: "Purged index for project '<name>'."
