# How the MCP Connector Server Works

This page explains **exactly what `server/index.js` does**, line by line.
By the end you will understand how Claude Desktop launches the server, how the two sides exchange messages, and why the code is structured the way it is.

---

## Big Picture

When Claude Desktop needs to call a Revit tool, the following sequence happens:

```
Claude Desktop
  │
  │  1. Spawns "node server/index.js" as a child process
  │  2. Writes a JSON-RPC request to the process's stdin
  │
  ▼
index.js  (your MCP server)
  │
  │  3. Reads the request from stdin
  │  4. Makes an HTTP request to the Revit add-in
  │
  ▼
Revit Add-in  (http://127.0.0.1:5578)
  │
  │  5. Runs the tool against the open Revit model
  │  6. Returns a JSON result over HTTP
  │
  ▼
index.js
  │
  │  7. Writes the JSON-RPC response to stdout
  │
  ▼
Claude Desktop  (reads the result and continues the conversation)
```

The critical insight is the **communication channel**:
- Claude ↔ MCP server: **stdin / stdout** (text lines, one JSON object per line)
- MCP server ↔ Revit add-in: **plain HTTP** on localhost

This design means the MCP server has no open ports of its own. Claude Desktop manages its lifetime — it starts the process when needed and kills it when done.

---

## The MCP Protocol in One Paragraph

**MCP (Model Context Protocol)** is a JSON-based protocol defined by Anthropic.
Every message is a [JSON-RPC 2.0](https://www.jsonrpc.org/specification) object written as a single line on stdout or stdin.

Claude always initiates. It sends a *request* (an object with an `id` field) and expects a *response* (an object with the same `id`). There are also *notifications* (objects without an `id`) that do not expect a response.

The three methods this server implements are:

| Method | Direction | What it does |
|--------|-----------|--------------|
| `initialize` | Claude → Server | Handshake. Server declares its capabilities. |
| `tools/list` | Claude → Server | Claude asks: "what tools do you have?" |
| `tools/call` | Claude → Server | Claude says: "run this tool with these arguments." |

---

## Walking Through the Code

### 1. The Shebang and Module Header

```js
#!/usr/bin/env node
```

The shebang line tells the OS to run this file with Node.js when it is executed directly (e.g., `./server/index.js`). Claude Desktop actually runs it as `node server/index.js`, so the shebang is a convenience for manual testing.

---

### 2. Configuration — BASE URL and Timeout

```js
const argv = process.argv.slice(2);
const arg = (k, def) => {
    const hit = argv.find(a => a.startsWith(`--${k}=`));
    return hit ? hit.split("=", 2)[1] : def;
};
const BASE = arg("base", process.env.REVIT_BRIDGE_BASE || "http://127.0.0.1:5578").replace(/\/+$/, "");
const TIMEOUT_MS = parseInt(arg("timeout", process.env.REVIT_BRIDGE_TIMEOUT || "60000"), 10);
```

`BASE` is the root URL of the Revit add-in's local HTTP server.
`TIMEOUT_MS` is how long to wait for Revit before giving up (default 60 seconds — long operations like exporting to IFC can take time).

Both values can be overridden in three ways, in order of priority:

1. **Command-line argument**: `node server/index.js --base=http://127.0.0.1:9999`
2. **Environment variable**: `REVIT_BRIDGE_BASE=http://127.0.0.1:9999`
3. **Hardcoded default**: `http://127.0.0.1:5578`

The `.replace(/\/+$/, "")` strips any trailing slash so that every URL built later (e.g., `BASE + "/tools"`) is always clean.

---

### 3. I/O Helpers

```js
const rl = readline.createInterface({ input: process.stdin, crlfDelay: Infinity });
const log = (...a) => console.error("[MCP]", new Date().toTimeString().slice(0, 8), ...a);
const write = obj => { process.stdout.write(JSON.stringify(obj) + "\n"); };
```

**`rl` (readline interface)** — reads stdin one line at a time. MCP uses newline-delimited JSON, so every incoming message from Claude is exactly one line.

**`log`** — writes diagnostic messages to **stderr**. This is important: stdout is reserved exclusively for MCP responses to Claude. Anything written to stdout that isn't valid JSON-RPC will confuse Claude Desktop. Logging to stderr keeps the channel clean.

**`write`** — serialises a JavaScript object to JSON and writes it to stdout followed by a newline. This is the only way responses are sent back to Claude.

---

### 4. The HTTP Helpers

```js
function httpGet(path) { ... }
function httpPost(path, json) { ... }
```

These are thin wrappers around Node's built-in `http` module. They return Promises that resolve to `{ status, body }` where `body` is a raw string.

A few important details:

- **`timeout`** is set on the request options. If Revit doesn't respond in time, `req.destroy()` is called with an error, which rejects the promise and surfaces as a JSON-RPC error back to Claude.
- **`Content-Length`** is calculated with `Buffer.byteLength(data)` (not `data.length`) to handle multi-byte UTF-8 characters correctly.
- The HTTP library used is Node's built-in `http` — no external dependencies. This keeps the package small and avoids version conflicts.

---

### 5. The Main Event Loop

```js
rl.on("line", async line => {
    let req;
    try { req = JSON.parse(line); }
    catch (e) { log("bad json:", e?.message); return; }
    ...
```

Every time Claude writes a line to stdin, this handler fires. The line is parsed as JSON. If parsing fails (e.g., empty line, garbled input), the error is logged and the line is silently ignored — there is no `id` to respond to.

The rest of the handler is a series of `if` blocks, one per MCP method.

---

### 6. `initialize` — The Handshake

```js
if (method === "initialize") {
    initialized = true;
    write({
        jsonrpc: "2.0",
        id,
        result: {
            protocolVersion: params.protocolVersion || "2025-06-18",
            capabilities: { tools: {} },
            serverInfo: { name: "revit-bridge-js", version: "1.0.0" },
            sessionId: randomUUID()
        }
    });
    return;
}
```

`initialize` is the first thing Claude sends. The server must respond with:

- **`protocolVersion`** — the MCP spec version both sides agree to use.
- **`capabilities`** — what the server supports. `{ tools: {} }` means "I support the tools subsystem." Other capabilities (resources, prompts, etc.) are not declared here because this server doesn't implement them.
- **`serverInfo`** — a human-readable name and version, shown in Claude Desktop's UI.
- **`sessionId`** — a random UUID for this session. Each run of the server gets a unique ID.

The `initialized` flag prevents Claude from calling `initialize` twice (an error per the MCP spec).

---

### 7. `tools/list` — Advertising Available Tools

```js
if (method === "tools/list") {
    const { body, status } = await httpGet("/tools");
    let tools = [];
    try { tools = JSON.parse(body); } catch { /* keep empty */ }
    write({ jsonrpc: "2.0", id, result: { tools } });
    return;
}
```

Claude calls `tools/list` when it needs to know what it can do. The server:

1. Makes a `GET /tools` request to the Revit add-in.
2. The add-in returns a JSON array of tool descriptors, each with `name`, `description`, and `inputSchema`.
3. The server forwards this array directly to Claude.

The tool list is **fetched live from Revit** on every call. This means you can load new tool DLLs into the running add-in and Claude will see them immediately — no restart required.

A `tools/list` response for a single tool looks like this:

```json
{
  "jsonrpc": "2.0",
  "id": 1,
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
      }
    ]
  }
}
```

Claude reads the `description` field to decide *when* to call each tool. Writing clear, specific descriptions is one of the most important things you can do when building tools.

---

### 8. `tools/call` — Running a Tool

```js
if (method === "tools/call") {
    const name = params?.name ?? "";
    const args = params?.arguments ?? {};
    const { body, status } = await httpPost("/call", { name, arguments: args });
    write({
        jsonrpc: "2.0",
        id,
        result: { content: [{ type: "text", text: body }] }
    });
    return;
}
```

When Claude decides to use a tool, it sends a `tools/call` message with:
- `params.name` — the tool name (e.g., `"GetAllUsedFamilies"`)
- `params.arguments` — a JSON object of inputs (e.g., `{ "categoryName": "Walls" }`)

The server posts both to `POST /call` on the Revit add-in, which runs the corresponding C# tool and returns the result as a JSON string.

The MCP response wraps the result in a `content` array:

```json
{
  "result": {
    "content": [
      { "type": "text", "text": "{ \"families\": [\"Basic Wall\", \"Curtain Wall\"] }" }
    ]
  }
}
```

The `content` array is MCP's way of supporting rich responses — you could return `"type": "image"` alongside `"type": "text"` if you wanted to send screenshots back to Claude. For now, all results are plain text JSON.

---

### 9. Error Handling

Every `try/catch` in the handler writes an MCP error response back to Claude:

```js
catch (e) {
    if (!isNote) write({ jsonrpc: "2.0", id, error: { code: -32000, message: String(e?.message || e) } });
    log("handler error:", e?.stack || e);
}
```

Error codes follow the JSON-RPC 2.0 standard:
- `-32700` Parse error
- `-32600` Invalid request
- `-32601` Method not found
- `-32000` to `-32099` Server error (application-defined)

Claude reads error responses and will typically tell the user what went wrong in plain English.

---

### 10. Shutdown

```js
rl.on("close", () => { log("stdin closed; exiting"); process.exit(0); });
```

When Claude Desktop closes or kills the server process, stdin closes. The readline interface fires the `"close"` event and the process exits cleanly. No orphan processes.

---

## The Full Message Flow — An Example

Here is what actually travels over stdin/stdout when you ask Claude *"What families are used in this model?"*

**Step 1 — Initialize** (happens once per session)

Claude → server (stdin):
```json
{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{}}}
```

Server → Claude (stdout):
```json
{"jsonrpc":"2.0","id":1,"result":{"protocolVersion":"2025-06-18","capabilities":{"tools":{}},"serverInfo":{"name":"revit-bridge-js","version":"1.0.0"},"sessionId":"a3f2..."}}
```

**Step 2 — List Tools**

Claude → server:
```json
{"jsonrpc":"2.0","id":2,"method":"tools/list","params":{}}
```

Server → Revit HTTP bridge:
```
GET http://127.0.0.1:5578/tools
```

Revit → server:
```json
[{"name":"GetAllUsedFamilies","description":"...","inputSchema":{...}}, ...]
```

Server → Claude:
```json
{"jsonrpc":"2.0","id":2,"result":{"tools":[{"name":"GetAllUsedFamilies",...}]}}
```

**Step 3 — Call the Tool**

Claude → server:
```json
{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"GetAllUsedFamilies","arguments":{}}}
```

Server → Revit:
```
POST http://127.0.0.1:5578/call
{"name":"GetAllUsedFamilies","arguments":{}}
```

Revit → server:
```json
{"families":["Basic Wall","Curtain Wall - Storefront","M_Door - 0915 x 2134mm"]}
```

Server → Claude:
```json
{"jsonrpc":"2.0","id":3,"result":{"content":[{"type":"text","text":"{\"families\":[...]}"}]}}
```

Claude reads the families list and writes a natural-language answer to the user.

---

## Summary

`index.js` is intentionally minimal — under 150 lines. Its only job is to translate between two protocols:

| From | Protocol | To |
|------|----------|----|
| Claude Desktop | MCP (JSON-RPC over stdio) | index.js |
| index.js | Plain HTTP | Revit add-in |

All the Revit knowledge lives in C#. The MCP server stays dumb and reusable.

---

## Next Step

Now that you understand how the server works, use MCP Inspector to verify it live — before touching Claude Desktop at all.

→ [Testing with MCP Inspector](./mcp-inspector)
