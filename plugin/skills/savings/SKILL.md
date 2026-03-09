---
name: token-squeeze:savings
description: Estimate how many tokens token-squeeze saved in the current context window
argument-hint: ""
disable-model-invocation: false
---

# Estimate Token Savings

Produce a rough "bar napkin" estimate of how many tokens token-squeeze saved in this context window compared to reading full source files.

## Step 1 — Review This Conversation

Scan your conversation history for token-squeeze usage in this session. Count:

- **Outline calls:** How many times you ran `outline` (via skill or CLI). Each outline replaces reading the full file — you got a compact symbol listing instead.
- **Extract calls:** How many times you ran `extract` (via skill or CLI) and how many symbol IDs were requested total. Each extract pulled only a specific symbol body instead of reading the surrounding file.
- **Find calls:** How many times you ran `find`. Each find avoided grepping through full source files.

If you used no token-squeeze commands this session, say so and give a hypothetical estimate instead ("if you had used outline + extract instead of Read, here's what you'd save").

## Step 2 — Napkin Math

Use these rough heuristics:

| Metric | Estimate |
|--------|----------|
| Average tokens per source file (full read) | ~800 tokens |
| Average tokens per outline response | ~120 tokens |
| Average tokens per extract response (one symbol) | ~100 tokens |
| Average tokens per find response | ~80 tokens |

For each outline call, the savings = **~680 tokens** (avoided reading ~800, paid ~120).
For each extract call, the savings = **~700 tokens per symbol** (avoided reading ~800, paid ~100).
For each find call, the savings = **~720 tokens** (avoided grepping full files, paid ~80).

Calculate:

- **Tokens saved this session** = sum of savings from all calls above
- **Percentage** = tokens saved / (tokens saved + tokens actually consumed by token-squeeze responses)

## Step 3 — Present Results

Present a concise summary like:

```
Token Squeeze Savings (estimated)
─────────────────────────────────
This session:
  Outline calls:  X  → ~Y tokens saved
  Extract calls:  X  → ~Y tokens saved
  Find calls:     X  → ~Y tokens saved
  ─────────────────────────────
  Est. total saved:  ~Z tokens (~P% reduction)

That's roughly equivalent to skipping N full file reads.
```

Keep the tone light — this is an estimate, not an audit. Remind the user these are rough numbers.
