# Installation

## Requirements

- Autodesk Revit 2025 or above (older versions require compiling the plugin)
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
   %APPDATA%\Autodesk\Revit\Addins\2025\
   ```
   *(Paste that path directly into the File Explorer address bar)*

2. Copy **both** of the following from the `plugin/` folder into that directory:
   - `RevitClaudeConnector.addin`
   - `RevitClaudeConnector\` (the entire subfolder)

   The final layout should look like:
   ```
   %APPDATA%\Autodesk\Revit\Addins\2025\
   ├── RevitClaudeConnector.addin
   └── RevitClaudeConnector\
       ├── RevitClaudePlugIn.dll
       ├── BuiltInTools.dll
       └── ...
   ```

3. Launch (or restart) Revit 2025. A **Claude Connector** tab will appear in the ribbon.

---

## Step 2 — Install the MCP Server

1. Open **Claude Desktop**.
2. Go to **Settings → Extensions** (or drag and drop the `.mcpb` file onto the Claude Desktop window).
3. Select `server/Revit.Claude.Connector.Mcp.Server.mcpb` and confirm the installation.
4. Restart Claude Desktop when prompted.

In Claude Desktop, you can now ask Claude to query and manipulate your Revit model.

**NOTE:** Once both the plugin and the MCP server are installed, open Revit and click the **Status** button in the **Claude Connector** ribbon tab. The status should read `Running`.

![Tools](/ConnectorStatus.png)

---

## Troubleshooting

| Problem | Fix |
|---|---|
| Plugin doesn't appear in Revit | Verify the `.addin` file and `RevitClaudeConnector\` folder are both in the `Addins\2025\` directory |
| MCP server not connecting | Ensure Claude Desktop has been restarted after the extension was installed |
| Revit shows a security warning | Click **Always Load** to trust the add-in |
