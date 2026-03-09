#!/usr/bin/env node
"use strict";

// TokenSqueeze MCP Server — stdio transport
// Thin wrapper around the CLI binary, exposing outline/extract/find/list as MCP tools.
// Implements MCP protocol (JSON-RPC 2.0) directly — zero dependencies.

const { execFileSync } = require("child_process");
const path = require("path");
const os = require("os");

// ---------------------------------------------------------------------------
// Platform binary detection (mirrors skills/scripts pattern)
// ---------------------------------------------------------------------------

const rawRoot = process.env.CLAUDE_PLUGIN_ROOT;
const PLUGIN_ROOT =
  rawRoot && !rawRoot.includes("${") ? rawRoot : path.resolve(__dirname);

function getBinaryPath() {
  const platform = os.platform();
  if (platform === "win32") {
    return path.join(PLUGIN_ROOT, "bin", "win-x64", "token-squeeze.exe");
  }
  // macOS and Linux fallback
  return path.join(PLUGIN_ROOT, "bin", "osx-arm64", "token-squeeze");
}

const BINARY = getBinaryPath();

function runCli(args) {
  try {
    const stdout = execFileSync(BINARY, args, {
      encoding: "utf-8",
      timeout: 30000,
      stdio: ["pipe", "pipe", "pipe"],
    });
    return { ok: true, data: stdout.trim() };
  } catch (err) {
    const stderr = err.stderr ? err.stderr.toString().trim() : err.message;
    return { ok: false, error: stderr || "CLI command failed" };
  }
}

// ---------------------------------------------------------------------------
// Tool definitions
// ---------------------------------------------------------------------------

const TOOLS = [
  {
    name: "list_projects",
    description: "List all indexed projects. Returns name, sourcePath, and indexedAt for each project.",
    inputSchema: {
      type: "object",
      properties: {},
      required: [],
    },
  },
  {
    name: "read_file_outline",
    description:
      "Get all symbols (functions, classes, methods, types) in a file with their signatures. Pass project name and file path (e.g. 'src/main.py'). Use after list_projects to get project names.",
    inputSchema: {
      type: "object",
      properties: {
        project: {
          type: "string",
          description: "Project name (from list_projects)",
        },
        file: {
          type: "string",
          description:
            "File path relative to project root (e.g. 'src/Parser/SymbolExtractor.cs')",
        },
      },
      required: ["project", "file"],
    },
  },
  {
    name: "read_symbol_source",
    description:
      "Get the full source code of specific symbols by ID. Use after read_file_outline or search_symbols to retrieve exact function/class/method bodies. Symbol ID format: '{filePath}::{QualifiedName}#{Kind}' where QualifiedName uses dots for nesting (e.g. 'MyClass.MyMethod') and Kind is PascalCase (Function, Class, Method, Constant, Type). Example: 'src/Foo.cs::MyClass.Run#Method'. Construct IDs from outline data: combine the top-level 'file', dot-joined parent-to-child 'name' chain, and PascalCase 'kind'.",
    inputSchema: {
      type: "object",
      properties: {
        project: {
          type: "string",
          description: "Project name (from list_projects)",
        },
        ids: {
          type: "array",
          items: { type: "string" },
          description:
            "Symbol IDs — format: '{filePath}::{QualifiedName}#{Kind}'. Build from outline: file + dot-joined name hierarchy + PascalCase kind.",
        },
      },
      required: ["project", "ids"],
    },
  },
  {
    name: "search_symbols",
    description:
      "Search for symbols matching a query across an entire indexed project. Returns matches with signatures, locations, and symbol IDs.",
    inputSchema: {
      type: "object",
      properties: {
        project: {
          type: "string",
          description: "Project name (from list_projects)",
        },
        query: {
          type: "string",
          description:
            "Search query — matches symbol names, signatures, and docstrings",
        },
        kind: {
          type: "string",
          description: "Filter by symbol kind",
          enum: ["function", "class", "method", "constant", "type"],
        },
        path: {
          type: "string",
          description: "Filter by file path glob pattern (e.g. 'src/**/*.cs')",
        },
      },
      required: ["project", "query"],
    },
  },
];

// ---------------------------------------------------------------------------
// Tool handlers
// ---------------------------------------------------------------------------

function handleToolCall(name, args) {
  switch (name) {
    case "list_projects": {
      const result = runCli(["list"]);
      if (!result.ok) return errorResult(result.error);
      return textResult(result.data);
    }

    case "read_file_outline": {
      const result = runCli(["outline", args.project, args.file]);
      if (!result.ok) return errorResult(result.error);
      return textResult(result.data);
    }

    case "read_symbol_source": {
      const ids = args.ids || [];
      if (ids.length === 0) return errorResult("No symbol IDs provided");
      const cliArgs = ["extract", args.project];
      if (ids.length === 1) {
        cliArgs.push(ids[0]);
      } else {
        for (const id of ids) cliArgs.push("--batch", id);
      }
      const result = runCli(cliArgs);
      if (!result.ok) return errorResult(result.error);
      return textResult(result.data);
    }

    case "search_symbols": {
      const cliArgs = ["find", args.project, args.query];
      if (args.kind) cliArgs.push("--kind", args.kind);
      if (args.path) cliArgs.push("--path", args.path);
      const result = runCli(cliArgs);
      if (!result.ok) return errorResult(result.error);
      return textResult(result.data);
    }

    default:
      return errorResult(`Unknown tool: ${name}`);
  }
}

function textResult(text) {
  return { content: [{ type: "text", text }] };
}

function errorResult(message) {
  return { content: [{ type: "text", text: message }], isError: true };
}

// ---------------------------------------------------------------------------
// MCP JSON-RPC protocol (stdio)
// ---------------------------------------------------------------------------

const SERVER_INFO = {
  name: "token-squeeze",
  version: "1.2.0",
};

const CAPABILITIES = {
  tools: {},
};

function handleRequest(method, params) {
  switch (method) {
    case "initialize":
      return {
        protocolVersion: "2024-11-05",
        capabilities: CAPABILITIES,
        serverInfo: SERVER_INFO,
      };

    case "notifications/initialized":
      return null; // notification, no response

    case "tools/list":
      return { tools: TOOLS };

    case "tools/call": {
      const { name, arguments: toolArgs } = params;
      return handleToolCall(name, toolArgs || {});
    }

    default:
      return undefined; // unknown method
  }
}

// ---------------------------------------------------------------------------
// stdio transport — auto-detect framing from first message
//   Content-Length framed (LSP-style): older MCP clients
//   Newline-delimited JSON: Claude Code and newer clients
// ---------------------------------------------------------------------------

let buffer = "";
let useFraming = null; // null = not yet detected, true = Content-Length, false = newline-delimited

process.stdin.setEncoding("utf-8");
process.stdin.on("data", (chunk) => {
  buffer += chunk;

  // Auto-detect framing from first chunk
  if (useFraming === null) {
    const trimmed = buffer.trimStart();
    useFraming = trimmed.startsWith("Content-Length");
  }

  if (useFraming) {
    parseFramed();
  } else {
    parseNewlineDelimited();
  }
});

function parseFramed() {
  while (buffer.length > 0) {
    const headerEnd = buffer.indexOf("\r\n\r\n");
    if (headerEnd === -1) break;

    const header = buffer.substring(0, headerEnd);
    const match = header.match(/Content-Length:\s*(\d+)/i);
    if (!match) {
      buffer = buffer.substring(headerEnd + 4);
      continue;
    }

    const contentLength = parseInt(match[1], 10);
    const bodyStart = headerEnd + 4;

    if (buffer.length < bodyStart + contentLength) break;

    const body = buffer.substring(bodyStart, bodyStart + contentLength);
    buffer = buffer.substring(bodyStart + contentLength);

    try {
      processMessage(JSON.parse(body));
    } catch (err) {
      console.error("Failed to parse message:", err.message);
    }
  }
}

function parseNewlineDelimited() {
  let newlineIdx;
  while ((newlineIdx = buffer.indexOf("\n")) !== -1) {
    const line = buffer.substring(0, newlineIdx).trim();
    buffer = buffer.substring(newlineIdx + 1);
    if (line.length === 0) continue;

    try {
      processMessage(JSON.parse(line));
    } catch (err) {
      console.error("Failed to parse message:", err.message);
    }
  }
}

process.stdin.on("end", () => process.exit(0));

function processMessage(msg) {
  // Notification (no id) — handle silently
  if (msg.id === undefined || msg.id === null) {
    handleRequest(msg.method, msg.params || {});
    return;
  }

  const result = handleRequest(msg.method, msg.params || {});

  if (result === undefined) {
    sendResponse(msg.id, null, {
      code: -32601,
      message: `Method not found: ${msg.method}`,
    });
    return;
  }

  sendResponse(msg.id, result, null);
}

function sendResponse(id, result, error) {
  const response = { jsonrpc: "2.0", id };
  if (error) {
    response.error = error;
  } else {
    response.result = result;
  }

  const body = JSON.stringify(response);
  if (useFraming) {
    process.stdout.write(`Content-Length: ${Buffer.byteLength(body)}\r\n\r\n${body}`);
  } else {
    process.stdout.write(body + "\n");
  }
}
