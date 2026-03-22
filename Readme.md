# Revit Claude Connector

An open source bridge that lets **Claude AI** talk directly to **Autodesk Revit** using the [Model Context Protocol](https://modelcontextprotocol.io/).

Ask Claude things like:
> *"How many walls are in this model?"*
> *"List all families used on Level 2."*
> *"Export the structural elements to CSV."*

---

## How It Works

```
Claude Desktop
     │
     │  MCP (stdio JSON-RPC)
     ▼
MCP Server  (Node.js)
     │
     │  HTTP · 127.0.0.1:5578
     ▼
Revit Add-in  (C#)
     │
     │  Revit API
     ▼
Your Revit Model
```

---

## Project Structure

| Folder | Description |
|---|---|
| `src/Connector.Mcp.Server/` | Node.js MCP server — packaged as a `.mcpb` Claude Desktop extension |
| `src/RevitClaudePlugIn/` | C# WPF Revit add-in (plugin entry point and UI) |
| `src/BuiltInTools/` | 46 pre-built Revit tools (query, modify, export, visualise) |

---

## Installation

See the [Installation Guide](https://ibrahimfahdah.github.io/revit-claude-mcp/guide/installation) for full instructions.

**Quick start:**
1. Download the latest release ZIP from [Releases](https://github.com/IbrahimFahdah/revit-claude-mcp/releases)
2. Copy `RevitClaudeConnector.addin` and the `RevitClaudeConnector\` folder to `%APPDATA%\Autodesk\Revit\Addins\2026\`
3. Install `server/Revit.Claude.Connector.Mcp.Server.mcpb` via Claude Desktop → Settings → Extensions

---

## Building from Source

**Requirements:** .NET 8 SDK, Revit 2026

```bash
dotnet build src/RevitClaudeConnector.sln -c Release
```

**Rebuilding the MCP server bundle:**
```bash
npm install -g @anthropic-ai/mcpb
cd src/Connector.Mcp.Server
mcpb pack
```

---

## Contributing

Contributions are welcome! There are three main ways to get involved:

- **Enhance the built-in tools** — improve or extend the 46 tools in `src/BuiltInTools/` (better queries, new parameters, richer output, bug fixes).
- **Publish custom tools** — build and share your own tool packages so other users can drop them into their local `Packages\` folder. See the [Custom Tools guide](https://ibrahimfahdah.github.io/revit-claude-mcp/guide/custom-tools).
- **Improve the plugin itself** — work on the MCP server, the Revit add-in, the UI, documentation, or anything else that makes the connector better.

Open an issue to discuss ideas, or submit a pull request directly.

---

## Documentation

Full documentation at **https://ibrahimfahdah.github.io/revit-claude-mcp/**

---
