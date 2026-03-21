# Testing with MCP Inspector

Before packaging your server into a `.mcpb` and installing it in Claude Desktop, you should test it in isolation. **MCP Inspector** is an official tool from Anthropic that gives you a visual interface to send MCP messages to your server and see the raw responses — exactly what Claude sees when it talks to the server.

This page shows you how to set it up and use it to verify that your Revit connector is working correctly.

---

## Why Test with MCP Inspector?

When something doesn't work inside Claude Desktop, the failure could be in:
- The MCP server (`index.js`)
- The connection to the Revit add-in (HTTP bridge)
- The tool itself (C# logic)
- Claude's interpretation of the tool description

MCP Inspector lets you eliminate the first two immediately. If `tools/list` returns tools and `tools/call` returns sensible results in the Inspector, then the MCP server and HTTP bridge are working. Any remaining problem is in Claude Desktop or the C# tool.

```
Without Inspector:   Claude Desktop → MCP Server → Revit  (3 things to debug)
With Inspector:      Inspector      → MCP Server → Revit  (isolates the MCP layer)
```

---

## Prerequisites

- **Node.js ≥ 18** installed
- The Revit add-in running inside Revit (so the HTTP bridge on `127.0.0.1:5578` is live)
- The `server/index.js` source file accessible

---

## Launching MCP Inspector

MCP Inspector ships as an npm package and can be run directly with `npx` — no install needed.

From the repository root, point it at your server entry point:

```bash
npx @modelcontextprotocol/inspector node src/Revit.Claude.Connector.Mcp.Server/server/index.js
```

You will see output like:

```
Starting MCP inspector...
Proxy server listening on port 6277
Visit http://localhost:6174 to use the inspector
```

Open `http://localhost:6174` in your browser. You will see the Inspector UI with a connection panel on the left and a message area on the right.

::: tip Running with a custom base URL
If your Revit add-in is listening on a different port, pass it as an argument:
```bash
npx @modelcontextprotocol/inspector node src/Revit.Claude.Connector.Mcp.Server/server/index.js -- --base=http://127.0.0.1:9000
```
The `--` separator is required — everything after it is passed to your server, not to the Inspector.
:::

---

## The Inspector UI

The interface has three main panels:

```
┌─────────────────────────┬──────────────────────────────────────────┐
│  Connection             │  Messages                                │
│  ─────────────          │  ──────────                              │
│  Status: Connected      │  ← jsonrpc responses appear here        │
│                         │                                          │
│  [Initialize]           │                                          │
│  [List Tools]           │                                          │
│  [Call Tool ▾]          │                                          │
│                         │                                          │
└─────────────────────────┴──────────────────────────────────────────┘
```

- **Connection panel** — shows whether the Inspector is connected to your server and provides action buttons.
- **Messages panel** — shows the raw JSON-RPC request/response pairs, exactly as exchanged over stdio.

---

## Step 1 — Initialize

Click **Connect** (or **Send Initialize**). The Inspector sends:

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "initialize",
  "params": {
    "protocolVersion": "2025-06-18",
    "capabilities": {},
    "clientInfo": { "name": "mcp-inspector", "version": "0.1.0" }
  }
}
```

Your server should respond with:

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "result": {
    "protocolVersion": "2025-06-18",
    "capabilities": { "tools": {} },
    "serverInfo": { "name": "revit-bridge-js", "version": "1.0.0" },
    "sessionId": "a3f29c..."
  }
}
```

**What to check:**
- `capabilities.tools` is present — confirms the server declared tool support.
- `serverInfo.name` is `"revit-bridge-js"` — confirms the right server started.

If you see a connection error instead, the server process failed to start. Check the terminal where you ran `npx` for error output.

---

## Step 2 — List Tools

Click **List Tools**. The Inspector sends:

```json
{ "jsonrpc": "2.0", "id": 2, "method": "tools/list", "params": {} }
```

If Revit is running with the add-in loaded, you will get back the full array of available tools:

```json
{
  "jsonrpc": "2.0",
  "id": 2,
  "result": {
    "tools": [
      {
        "name": "GetAllUsedFamilies",
        "description": "Returns all families loaded in the active Revit model.",
        "inputSchema": {
          "type": "object",
          "properties": {},
          "required": []
        }
      },
      {
        "name": "GetElementsByCategory",
        "description": "Returns elements belonging to a given Revit category.",
        "inputSchema": {
          "type": "object",
          "properties": {
            "categoryName": {
              "type": "string",
              "description": "The Revit category name, e.g. 'Walls' or 'Doors'."
            }
          },
          "required": ["categoryName"]
        }
      }
    ]
  }
}
```

**What to check:**
- The tool list is not empty.
- Each tool has a `name`, `description`, and `inputSchema`.
- The `description` fields are clear — remember, Claude reads these to decide which tool to call.

**If the list is empty:** Revit is reachable but returned an empty array. The add-in may not have loaded any tools from disk yet. Check that the tools folder path is correct in the add-in settings.

**If you get an HTTP error:** The server started but could not reach `127.0.0.1:5578`. Revit may not be running, or the add-in failed to start. Revit shows a startup dialog (`"RevitClaudeBridge started on 127.0.0.1:5578"`) when the bridge is active — check whether that appeared.

---

## Step 3 — Call a Tool

Select a tool from the dropdown (populated automatically after `tools/list`), fill in any required arguments, and click **Call Tool**.

For example, calling `GetElementsByCategory` with `categoryName = "Walls"`:

Inspector sends:
```json
{
  "jsonrpc": "2.0",
  "id": 3,
  "method": "tools/call",
  "params": {
    "name": "GetElementsByCategory",
    "arguments": { "categoryName": "Walls" }
  }
}
```

Expected response:
```json
{
  "jsonrpc": "2.0",
  "id": 3,
  "result": {
    "content": [
      {
        "type": "text",
        "text": "{\"elements\": [{\"id\": 123456, \"name\": \"Basic Wall\", ...}]}"
      }
    ]
  }
}
```

**What to check:**
- `result.content[0].type` is `"text"` — the correct content type for this server.
- `result.content[0].text` is valid JSON from the Revit add-in.
- The data looks correct for the tool you called.

**If you get `{ "error": "No active document" }`:** Revit is running and the bridge is up, but no model is open. Open a Revit project file and try again.

**If you get `{ "error": "Unknown tool" }`:** The tool name in the call doesn't match any loaded tool. Double-check the name in the `tools/list` response.

---

## Reading the Raw Message Log

The Messages panel shows every exchange in chronological order. This is the most useful debugging feature — you can see exactly what the server received and what it sent back.

Each row shows:
- Direction (→ outgoing to server, ← incoming from server)
- Timestamp
- The full JSON, expandable

When filing a bug report or asking for help, copy the raw messages from here — they contain all the information needed to diagnose the problem.

---

## Testing Without Revit

You can run MCP Inspector against the server even when Revit is not running. The server will start and respond to `initialize` normally. When you call `tools/list`, it will attempt to reach `127.0.0.1:5578`, fail with a connection refused error, and return an MCP error response:

```json
{
  "jsonrpc": "2.0",
  "id": 2,
  "error": {
    "code": -32000,
    "message": "connect ECONNREFUSED 127.0.0.1:5578"
  }
}
```

This confirms the MCP server itself is healthy — the error is expected and comes from the HTTP layer, not from `index.js`. Once you start Revit, the same `tools/list` call will return results without restarting the Inspector.

---

## Quick Reference

| What you want to verify | Inspector action | Expected result |
|------------------------|------------------|-----------------|
| Server starts at all | Run `npx @modelcontextprotocol/inspector ...` | No crash in terminal |
| MCP handshake works | Initialize | `capabilities.tools` in response |
| Revit bridge is reachable | List Tools | Non-empty tools array |
| A specific tool runs | Call Tool | JSON result in `content[0].text` |
| Error handling works | Call Tool with bad args | MCP error with descriptive message |

---

## Next Step

Once `tools/list` returns your tools and `tools/call` returns sensible results, you are ready to package the server for distribution.

→ [Building the `.mcpb` Extension](./building-mcpb)
