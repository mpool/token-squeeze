---
name: token-squeeze:explore
description: Explore, research, or trace how code works — how features are implemented, how components connect, how data flows through the system. Use when the user asks to understand, investigate, map, or walk through any part of the codebase.
argument-hint: "[question or topic]"
---

# Explore Codebase

You are exploring a codebase using token-squeeze for token-efficient symbol retrieval. Your goal is to thoroughly answer the user's question by exploring the indexed codebase and producing a clear, referenced explanation.

## Your job is to DOCUMENT what exists — not critique, improve, or recommend changes.

## Step 1 — Validate Index (MUST complete before any other tool calls)

Call `search_symbols` with a broad query (e.g. the user's topic) FIRST — as a single, standalone call. Do NOT call `read_file_outline` or `read_symbol_source` in parallel with this initial call.

If the response contains `"hint"` mentioning "No index exists", tell the user to run `/token-squeeze:index` first and **stop — do not proceed to further steps**.

Only after `search_symbols` returns actual results should you continue to Step 2.

## Step 2 — Understand the Question

If the user provided a question or topic as an argument, use it directly. If not, ask what they want to research.

Break the question into specific things to investigate:
- What components are involved?
- What entry points should be traced?
- What connections need to be mapped?

## Step 3 — Locate

Spawn the **symbol-locator** agent to find all relevant files and symbols. Give it a clear prompt describing what to search for based on the user's question.

Wait for results before proceeding.

## Step 4 — Analyze

Using the locator's results, spawn the **codebase-explorer** agent to trace the implementation in detail. Feed it the specific files and symbols the locator found so it starts from the right places.

If the research topic has multiple independent areas (e.g., "how does auth and billing work"), spawn multiple codebase-explorer agents in parallel — one per area.

## Step 5 — Synthesize

Combine all agent findings into a structured research summary:

```
## Research: [Topic]

### Summary
[2-3 sentence answer to the user's question]

### Components Found
[Key files and symbols from the locator, grouped by purpose]

### How It Works
[Detailed trace from the explorer — entry points, call chains, data flow]

### Architecture
[Patterns, conventions, and design decisions documented]

### Code References
[Key file:line references for navigation]

### Open Questions
[Anything that couldn't be fully resolved]
```

## Step 6 — Present

Present the synthesis to the user. Offer to dig deeper into any specific area.

## Guidelines

- Use token-squeeze MCP tools as the primary way to read code — not Read or Grep
- Always trace actual code, never guess
- Include file:line references for all claims
- Document what IS, not what SHOULD BE
- If agents return incomplete results, spawn follow-up agents with more specific prompts
- Keep the research focused on the user's question — don't explore tangentially
