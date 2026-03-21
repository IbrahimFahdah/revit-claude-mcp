# Building the `.mcpb` Extension

A `.mcpb` file (Model Context Protocol Bundle) is a single compressed archive that Claude Desktop can install in one click. It packages your MCP server and its metadata so users never have to run `npm install` or edit config files manually.

This page explains what is inside the bundle, what each file does, and how to build it.

---

## What Is a `.mcpb` File?

Before `.mcpb` existed, installing an MCP server meant:
1. Cloning a repo
2. Running `npm install`
3. Manually editing Claude Desktop's `claude_desktop_config.json` to add the server path

A `.mcpb` file eliminates all of that. Claude Desktop reads it, unpacks the server, and registers it automatically.

Under the hood, a `.mcpb` is a ZIP archive with a specific structure and a `manifest.json` at its root.

---

## The Files That Make the Bundle

All source files live in `src/Revit.Claude.Connector.Mcp.Server/`:

```
Revit.Claude.Connector.Mcp.Server/
├── manifest.json          ← Extension metadata (read by Claude Desktop)
├── package.json           ← Node.js package descriptor
├── icon.png               ← Extension icon (shown in Claude Desktop UI)
├── server/
│   └── index.js           ← The MCP server entry point
└── node_modules/          ← Installed dependencies (created by npm install)
```

After running `mcpb pack`, one new file is created in the same directory:

```
└── Revit.Claude.Connector.Mcp.Server.mcpb   ← The distributable bundle
```

---

## Understanding `manifest.json`

`manifest.json` is the most important file in the bundle. Claude Desktop reads it to know how to launch the server.

```json
{
  "dxt_version": "0.1",
  "name": "RevitClaudeConnector",
  "display_name": "RevitClaudeConnector",
  "version": "1.0.0",
  "description": "Let Claude access your Revit models.",
  "long_description": "This extension allows Claude to interact with Revit models and call your Revit C# tools.",
  "author": {
    "name": "IFADAH"
  },
  "icon": "icon.png",
  "server": {
    "type": "node",
    "entry_point": "server/index.js",
    "mcp_config": {
      "command": "node",
      "args": [
        "${__dirname}/server/index.js"
      ]
    }
  },
  "keywords": ["api", "automation", "productivity"],
  "license": "MIT",
  "compatibility": {
    "claude_desktop": ">=0.10.0",
    "platforms": ["darwin", "win32", "linux"],
    "runtimes": {
      "node": ">=16.0.0"
    }
  }
}
```

### Field-by-field breakdown

| Field | What it means |
|-------|---------------|
| `dxt_version` | The DXT (Desktop Extension) schema version. Use `"0.1"` for current Claude Desktop. |
| `name` | Machine-readable identifier. No spaces. Used internally. |
| `display_name` | Human-readable name shown in the Claude Desktop Extensions panel. |
| `version` | Semantic version of your extension. Bump this when you publish updates. |
| `description` | One-line summary shown in the extensions list. |
| `long_description` | Longer description shown on the extension detail page. |
| `author.name` | Your name or organisation, shown in the UI. |
| `icon` | Path (relative to `manifest.json`) to the extension icon. Must be a PNG. |
| `server.type` | The runtime. `"node"` tells Claude Desktop this is a Node.js server. |
| `server.entry_point` | The main JS file, relative to the bundle root. Claude Desktop uses this for display. |
| `server.mcp_config.command` | The executable to launch — `"node"` here. |
| `server.mcp_config.args` | Arguments passed to the command. `${__dirname}` is a special token that Claude Desktop replaces with the path where it unpacked the bundle. |
| `compatibility.claude_desktop` | Minimum Claude Desktop version required. |
| `compatibility.platforms` | Operating systems the extension supports. |
| `compatibility.runtimes.node` | Minimum Node.js version required on the user's machine. |

### Why `${__dirname}` matters

When Claude Desktop installs the `.mcpb`, it unpacks it to an internal folder with an unpredictable path (something like `C:\Users\you\AppData\Roaming\Claude\extensions\RevitClaudeConnector-1.0.0\`). You cannot know this path at build time.

`${__dirname}` is replaced at runtime with that unpacked path, so `"${__dirname}/server/index.js"` always resolves correctly regardless of where Claude Desktop puts it.

---

## Understanding `package.json`

```json
{
  "name": "ant.dir.ant.anthropic.filesystem",
  "version": "0.1.6",
  "type": "module",
  "private": true,
  "main": "server/index.js",
  "dependencies": {
    "@modelcontextprotocol/server-filesystem": "2025.1.14"
  }
}
```

| Field | What it means |
|-------|---------------|
| `type: "module"` | Tells Node.js to treat `.js` files as ES Modules (allowing `import` syntax). |
| `private: true` | Prevents accidentally publishing this package to the npm registry. |
| `main` | Entry point for Node.js `require()` / direct execution. |
| `dependencies` | The packages this server needs at runtime. These are bundled into the `.mcpb` via `node_modules/`. |

::: tip
Unlike a typical web project, the `node_modules/` folder **is** included in the `.mcpb`. This is intentional — users do not need to run `npm install` after installing the extension.
:::

---

## Step-by-Step: Building the `.mcpb`

### Prerequisites

You need **Node.js ≥ 16** and **npm** installed. Check with:

```bash
node --version
npm --version
```

### Step 1 — Install the `mcpb` packager

`mcpb` is Anthropic's official tool for building `.mcpb` files. Install it globally once:

```bash
npm install -g @anthropic-ai/mcpb
```

Verify the install:

```bash
mcpb --version
```

### Step 2 — Install the server's dependencies

Navigate into the server source folder and run `npm install` to populate `node_modules/`:

```bash
cd src/Revit.Claude.Connector.Mcp.Server
npm install
```

This creates (or updates) `node_modules/` with everything listed under `dependencies` in `package.json`. The `mcpb` tool will include this folder in the bundle.

::: warning
You must run `npm install` every time you add or update a dependency. If `node_modules/` is missing or outdated, the bundled extension will fail at runtime.
:::

### Step 3 — Run `mcpb pack`

From the same folder, run:

```bash
mcpb pack
```

`mcpb` reads `manifest.json`, validates the structure, and zips everything (including `node_modules/`) into a single `.mcpb` file.

The output looks like this:

```
Packing extension...
  ✓ Validated manifest.json
  ✓ Included server/index.js
  ✓ Included node_modules/ (42 packages)
  ✓ Included icon.png
Output: Revit.Claude.Connector.Mcp.Server.mcpb (1.2 MB)
```

The `.mcpb` file is created in the same directory.

### Step 4 — What `mcpb pack` does internally

Understanding the internals helps when things go wrong.

`mcpb pack`:
1. Reads and validates `manifest.json` against the DXT 0.1 schema.
2. Resolves `server.entry_point` and checks the file exists.
3. Recursively includes everything in the folder **except** files listed in `.mcpbignore` (if present) or `.gitignore`.
4. Compresses the result into a ZIP archive and renames it with the `.mcpb` extension.

The `.mcpb` file is just a ZIP. You can verify its contents with:

```bash
# On Windows (PowerShell)
Rename-Item Revit.Claude.Connector.Mcp.Server.mcpb bundle.zip
Expand-Archive bundle.zip -DestinationPath bundle-contents

# On macOS/Linux
cp Revit.Claude.Connector.Mcp.Server.mcpb bundle.zip
unzip bundle.zip -d bundle-contents
```

---

## Step 5 — Installing the `.mcpb` in Claude Desktop

1. Open **Claude Desktop**.
2. Go to **Settings** → **Extensions**.
3. Click **Advanced Settings** (bottom of the panel).
4. Click **Install Extension...** and select the `.mcpb` file.
5. Claude Desktop unpacks the bundle, registers the server, and shows it in the Extensions list.
6. The extension is now active. Open a new conversation and the tools from your Revit add-in will be available.

::: info First-time use
After installing, Claude will not have any Revit tools available until the **Revit add-in** is also installed and Revit is running. The MCP server acts as a proxy — if there is nothing at `http://127.0.0.1:5578`, it cannot return any tools.
:::

---

## Rebuilding After Changes

Whenever you change `server/index.js` or `manifest.json`, rebuild the bundle:

```bash
cd src/Revit.Claude.Connector.Mcp.Server
mcpb pack
```

Then in Claude Desktop:
1. Go to **Settings** → **Extensions**.
2. Remove the old extension.
3. Install the new `.mcpb`.

There is no hot-reload. Claude Desktop must be restarted (or the extension removed and re-added) to pick up a new version.

---

## Troubleshooting

### "Method not found" errors in Claude
The MCP server started but could not reach the Revit add-in. Check that:
- Revit is running with the `RevitClaudeBridge` add-in loaded.
- The add-in successfully started on port `5578` (Revit shows a startup dialog).
- No firewall is blocking `127.0.0.1:5578`.

### The extension installs but shows no tools
Open Claude Desktop's developer console (if available) or check stderr output. The most common cause is that `node_modules/` was not present when `mcpb pack` ran.

### `mcpb: command not found`
The global install did not add the binary to your PATH. Try:
```bash
npx @anthropic-ai/mcpb pack
```

### Port conflict on 5578
Another process is using port 5578. Either stop that process or launch the MCP server with a different base URL:
```bash
node server/index.js --base=http://127.0.0.1:6000
```
And update the Revit add-in to listen on the same port.
