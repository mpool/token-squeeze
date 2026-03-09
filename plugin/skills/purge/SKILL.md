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

The project name argument is required. If not provided, run `<binary> list` and show the user the available projects so they can choose.

Then run:

```
<binary> purge <project-name>
```

## Output

Confirm what was deleted: "Purged index for project '<name>'."
