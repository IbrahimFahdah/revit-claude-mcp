# Installation

## Requirements

- Autodesk Revit 2026
- [Claude Desktop](https://claude.ai/download) (latest)

---

## What's in the ZIP

```
revit-claude-connector-vX.X.X.zip
├── plugin/
│   ├── RevitClaudeConnector.addin        ← Revit add-in manifest
│   └── RevitClaudeConnector/             ← Add-in binaries
│       ├── RevitClaudePlugIn.dll
│       ├── BuiltInTools.dll
│       └── ...
├── server/
│   └── Revit.Claude.Connector.Mcp.Server.mcpb   ← Claude Desktop extension
└── INSTALL.md
```

---

## Step 1 — Install the Revit Plugin

1. Open File Explorer and navigate to your Revit add-ins folder:
   ```
   %APPDATA%\Autodesk\Revit\Addins\2026\
   ```
   *(Paste that path directly into the File Explorer address bar)*

2. Copy **both** of the following from the `plugin/` folder into that directory:
   - `RevitClaudeConnector.addin`
   - `RevitClaudeConnector\` (the entire subfolder)

   The final layout should look like:
   ```
   %APPDATA%\Autodesk\Revit\Addins\2026\
   ├── RevitClaudeConnector.addin
   └── RevitClaudeConnector\
       ├── RevitClaudePlugIn.dll
       ├── BuiltInTools.dll
       └── ...
   ```

3. Launch (or restart) Revit 2026. A **Claude** panel will appear in the ribbon.

---

## Step 2 — Install the MCP Server

1. Open **Claude Desktop**.
2. Go to **Settings → Extensions** (or drag and drop the `.mcpb` file onto the Claude Desktop window).
3. Select `server/Revit.Claude.Connector.Mcp.Server.mcpb` and confirm the installation.
4. Restart Claude Desktop when prompted.

---

## Step 3 — Connect

1. In Revit, open a project and click the **Claude** button in the ribbon to start the bridge.
2. In Claude Desktop, you can now ask Claude to query and manipulate your Revit model.

---

## Troubleshooting

| Problem | Fix |
|---|---|
| Plugin doesn't appear in Revit | Verify the `.addin` file and `RevitClaudeConnector\` folder are both in the `Addins\2026\` directory |
| MCP server not connecting | Ensure Claude Desktop has been restarted after the extension was installed |
| Revit shows a security warning | Click **Always Load** to trust the add-in |
