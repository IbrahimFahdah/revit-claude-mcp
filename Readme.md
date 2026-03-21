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
2. Copy the `plugin/` contents to `%APPDATA%\Autodesk\Revit\Addins\2026\`
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

## Documentation

Full documentation at **https://ibrahimfahdah.github.io/revit-claude-mcp/**

---

## License

MIT
