# What Is the Claude–Revit AI Connector?

This project is an open source bridge that lets **Claude AI** talk directly to **Autodesk Revit**.

Once installed, you can open a conversation in Claude Desktop and ask things like:

> *"How many walls are in this model?"*
> *"List all the families used on Level 2."*
> *"Export the structural elements to CSV."*

Claude reads your question, picks the right Revit tool, runs it inside your live Revit session, and answers you — all without you writing a single line of code.

---

## The Two Moving Parts

The connector is made of exactly two pieces that talk to each other over the local network.

```
Claude Desktop
     │
     │  (MCP — stdio JSON-RPC)
     ▼
MCP Server  (Node.js  ·  index.js)
     │
     │  (HTTP  ·  127.0.0.1:5578)
     ▼
Revit Add-in  (C#  ·  RevitClaudeBridge.dll)
     │
     │  (Revit API)
     ▼
Your Revit Model
```

| Piece | Language | Role |
|-------|----------|------|
| MCP Server (`server/index.js`) | Node.js | Speaks MCP to Claude; proxies requests to the add-in |
| Revit Bridge (`RevitClaudeBridge.dll`) | C# | Runs inside Revit; executes tools against the open model |

The two pieces are deliberately separate so that the MCP Server stays simple and stateless, while all the Revit-specific logic lives in C# where the Revit API is available.

---

## What You Will Learn in This Guide

The next sections take you through every layer of the system:

1. **How the MCP Server works** — what each line of `index.js` does and why
2. **Building the `.mcpb` extension** — packaging the server so Claude Desktop can install it in one click
3. *(Coming soon)* How the Revit Bridge works
4. *(Coming soon)* Writing your own Revit tools
