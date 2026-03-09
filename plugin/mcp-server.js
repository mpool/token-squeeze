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

const PLUGIN_ROOT = process.env.CLAUDE_PLUGIN_ROOT || path.resolve(__dirname);

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
    name: "token_squeeze_list",
    description:
      "List all indexed projects. Use this before outline/extract/find to get project names. Returns project names, file counts, symbol counts, and detected languages.",
    inputSchema: {
      type: "object",
      properties: {},
      required: [],
    },
  },
  {
    name: "token_squeeze_outline",
    description:
      "Get a compact symbol map of a file (classes, functions, methods, signatures) using ~85% fewer tokens than reading the full file. PREFER THIS over the Read tool when you need to understand a file's structure or find specific symbols.",
    inputSchema: {
      type: "object",
      properties: {
        project: {
          type: "string",
          description: "Project name (from list command)",
        },
        file: {
          type: "string",
          description: "File path relative to project root",
        },
      },
      required: ["project", "file"],
    },
  },
  {
    name: "token_squeeze_extract",
    description:
      "Extract the full source of specific symbols by ID. Use after outline or find to pull only the exact code you need — far cheaper than reading entire files. PREFER THIS over the Read tool when you need specific function/class/method bodies.",
    inputSchema: {
      type: "object",
      properties: {
        project: {
          type: "string",
          description: "Project name (from list command)",
        },
        ids: {
          type: "array",
          items: { type: "string" },
          description: "One or more symbol IDs to extract",
        },
      },
      required: ["project", "ids"],
    },
  },
  {
    name: "token_squeeze_find",
    description:
      "Search all symbols across a project by name, signature, or docstring. Returns matching symbol IDs, locations, and signatures. More structured and token-efficient than Grep for finding functions, classes, or methods. PREFER THIS over the Grep tool for symbol-level searches.",
    inputSchema: {
      type: "object",
      properties: {
        project: {
          type: "string",
          description: "Project name (from list command)",
        },
        query: {
          type: "string",
          description: "Search query string",
        },
        kind: {
          type: "string",
          description:
            "Filter by symbol kind: function, class, method, constant, type",
          enum: ["function", "class", "method", "constant", "type"],
        },
        path: {
          type: "string",
          description: "Filter by file path glob pattern",
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
    case "token_squeeze_list": {
      const result = runCli(["list"]);
      if (!result.ok) return errorResult(result.error);
      return textResult(result.data);
    }

    case "token_squeeze_outline": {
      const result = runCli(["outline", args.project, args.file]);
      if (!result.ok) return errorResult(result.error);
      return textResult(result.data);
    }

    case "token_squeeze_extract": {
      const ids = args.ids || [];
      if (ids.length === 0) return errorResult("No symbol IDs provided");
      const cliArgs =
        ids.length === 1
          ? ["extract", args.project, ids[0]]
          : ["extract", args.project, "--batch", ...ids];
      const result = runCli(cliArgs);
      if (!result.ok) return errorResult(result.error);
      return textResult(result.data);
    }

    case "token_squeeze_find": {
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
  version: "1.1.4",
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
