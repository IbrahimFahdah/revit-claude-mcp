# Token Efficiency Guide

## The Core Idea

When Claude connects to your Revit model via MCP, it loads tool definitions the first time it needs them. That initial load is the most expensive part of a conversation. Everything after that is relatively cheap.

---

## The Most Important Rule

**Do your Revit work in one conversation whenever possible.**

The first time Claude uses an MCP tool in a conversation, it loads the tool schema. Subsequent uses of that tool in the same conversation reuse what's already loaded — no extra cost.

Starting a new conversation means loading tools again from scratch.

---

## Simple Tips

### Be specific in your requests
Tell Claude exactly what you want. Vague questions cause Claude to explore multiple tools before settling on one.

✅ "Get elements in room 2177792"  
❌ "What can you tell me about rooms?"

### Break complex tasks into steps
Ask one thing at a time. Each step builds on tools already loaded.

✅
```
Step 1: "Get all furniture in room 200"
Step 2: "Get the Family and Type for those elements"
Step 3: "Export to CSV"
```

❌ "Get all furniture in every room with all their parameters and export to CSV organized by level"

### Tell Claude to be efficient
If you have many queries, say so upfront:

> "I have 20 Revit queries to run. Be token-efficient and only load what's needed for each."

---

## What Actually Costs Tokens

| Operation | Relative Cost |
|-----------|--------------|
| First tool load in a conversation | High (one-time) |
| Reusing an already-loaded tool | Very low |
| Simple query (get selection, active view) | Very low |
| Getting parameters from one element | Low |
| Getting all elements in a room | Medium |
| Large data exports | Medium–High |

---

## Prompt Caching

Claude supports **prompt caching** at the API level. When the same tool definitions are sent repeatedly, Claude can serve them from cache instead of processing them as fresh input.

### Cost model (documented by Anthropic)

| Token type | Cost multiplier |
|------------|----------------|
| Normal input | 1.0× |
| Cache write (first time a prefix is cached) | 1.25× |
| Cache read (reusing a cached prefix) | **0.1×** — 90% cheaper |

### Cache TTL
- Default: **5 minutes** after last use
- Extendable to **1 hour** via explicit cache control settings

### What this means in practice

MCP tool schemas are the same content every conversation — same source, same structure. At the API level, that makes them a good candidate for caching. If the cache is still warm when you start a new conversation, Claude reads the schemas at 10% of normal cost instead of full price.

**However:** Whether Claude Desktop explicitly enables cache_control on MCP tool definitions depends on the platform's implementation. The behavior above is how Anthropic's caching API works — the actual savings you see may vary depending on how Claude Desktop is configured.

### The safe assumption
Don't rely on cross-conversation caching as your primary strategy. Treat it as a bonus when it kicks in. **Staying in one conversation is always the most predictable and efficient approach.**

---

## Bottom Line

- One long conversation > many short ones
- Ask for exactly what you need
- Build on previous steps rather than repeating context
