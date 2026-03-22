# Contributing

Contributions are welcome — whether you're fixing a bug, improving an existing tool, or building something entirely new. Here are the three main ways to get involved.

---

## 1. Enhance the Built-In Tools

The `src/BuiltInTools/` project contains 46 pre-built Revit tools covering queries, modifications, exports, and visualisation. There is plenty of room to make them better:

- Add missing parameters or richer output to existing tools
- Fix bugs or edge cases in how elements are queried
- Improve tool descriptions so Claude picks the right tool more reliably
- Add entirely new tools to cover gaps in the current set

See [Built-In Tools](./built-in-tools) for an overview of what exists today.

**To contribute:**
1. Fork the repository and create a branch
2. Make your changes in `src/BuiltInTools/`
3. Build with `dotnet build src/RevitClaudeConnector.sln -c Release` and verify in Revit
4. Submit a pull request with a clear description of what changed and why

---

## 2. Publish Custom Tools

Custom tools are the easiest way to share domain-specific Revit capabilities with the community. You write a C# class library, a manifest, and a JSON schema — no changes to the core plugin required.

See the [Custom Tools guide](./custom-tools) for a full walkthrough. Once your package works locally, you can share it by:

- Publishing the package folder as a GitHub repository so others can clone it
- Opening a discussion in this repo to list your package in a community directory (coming soon)

The plugin's drop-in model means users install your tools simply by copying your folder into their `Packages\` directory.

---

## 3. Improve the Plugin Itself

Beyond tools, the connector has several areas that could benefit from contributions:

- **MCP Server** (`src/Connector.Mcp.Server/`) — the Node.js layer that bridges Claude and the add-in
- **Revit Add-in** (`src/RevitClaudePlugIn/`) — the C# WPF plugin, including the UI and HTTP server
- **Documentation** (`docs/`) — corrections, new guides, better examples
- **CI / release workflow** — improving the GitHub Actions pipeline

---

## Getting Started

```bash
# Clone the repo
git clone https://github.com/IbrahimFahdah/revit-claude-mcp.git

# Build the plugin
dotnet build src/RevitClaudeConnector.sln -c Release

# Rebuild the MCP server bundle (optional)
npm install -g @anthropic-ai/mcpb
cd src/Connector.Mcp.Server
mcpb pack
```

Open an issue to discuss your idea before investing significant effort — it helps avoid duplicate work and makes reviews faster.
