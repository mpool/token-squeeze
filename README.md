# TokenSqueeze

A codebase indexing and symbol retrieval for [Claude Code](https://docs.anthropic.com/en/docs/claude-code) token optimization.

Index your project once, then let Claude retrieve precise function signatures, class outlines, and full symbol source — without dumping entire files into context.

## Install

```bash
npx token-squeeze@latest
```

This installs the Claude Code plugin and downloads the correct binary for your platform. Restart Claude Code after installing.

### Manual install

If you prefer to build from source (requires [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)):

```bash
git clone https://github.com/mpool/token-squeeze.git
cd token-squeeze
./plugin/build.sh
```

Then copy the `plugin/` contents to `~/.claude/plugins/token-squeeze/`.

## What it does

TokenSqueeze gives Claude Code two interfaces:

### MCP Tools (automatic)

The plugin bundles an MCP server (stdio transport) that Claude calls directly — no slash commands needed:

| Tool | Description |
|------|-------------|
| `token_squeeze_list` | List indexed projects with symbol counts and languages |
| `token_squeeze_outline` | Show symbols in a file (functions, classes, methods, types) |
| `token_squeeze_extract` | Get the full source of one or more symbols by ID |
| `token_squeeze_find` | Search symbols by name, signature, or docstring |

### Skills (slash commands)

| Skill | Description |
|-------|-------------|
| `/token-squeeze:index` | Index a directory's symbols |
| `/token-squeeze:savings` | Estimate how many tokens were saved this session |
| `/token-squeeze:purge` | Remove an index |

On session start, the plugin auto-indexes the current directory (if not already indexed).

## Supported Languages

Python, JavaScript, TypeScript, C#, C, C++

## How it works

```
Source files  -->  tree-sitter parser  -->  Symbol index (JSON)
                                              |
Claude Code  <--  CLI query commands  <-------+
```

Symbols are stored in `~/.token-squeeze/` as JSON indexes alongside raw source snapshots. Claude queries them through the CLI, getting just the symbols it needs instead of reading entire files.
