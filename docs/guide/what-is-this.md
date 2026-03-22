# What Is the Claude–Revit AI Connector?

This project is an open source bridge that lets **Claude AI** talk directly to **Autodesk Revit**.

Once installed, you can open a conversation in Claude Desktop and ask things like:

> *"How many walls are in this model?"*
> *"List all the families used on Level 2."*
> *"Export the structural elements to CSV."*

Claude reads your question, picks the right Revit tool, runs it inside your live Revit session, and answers you — all without you writing a single line of code.

---

## AI Assistance in Revit

![AI Assistance in Revit](/AI-Assistance-in-Revit.png)

Working with Revit daily? You've probably hit that moment where a repetitive task makes you think "there has to be a better way." Here's how the automation options stack up:

**📊 Dynamo** — The visual programming powerhouse. Drag nodes, connect wires, see your logic flow. Perfect for BIM managers who want to build reusable workflows without deep coding knowledge. Great for geometric operations and data manipulation. The catch? Complex logic can turn into spaghetti nodes fast.

**🐍 pyRevit** — Python meets Revit. Write custom tools that live right in your ribbon. Speed is excellent since it runs native C# under the hood. Trade-off: requires Python programming skills.

**⚡ Revit API (C#)** — The fastest, most powerful option with direct access to everything Revit can do. Trade-off: steep learning curve, you often need to build your own UI, and plugins typically serve one specialized purpose.

**🤖 AI Connector (e.g. Claude + MCP)** — The newest player, and the one that shines where the others struggle. 
- Natural language commands with the flexibility to chain tools and automate multi-step tasks. 
- The real differentiator isn't just *getting* data out of Revit: it's what happens next. An AI connector keeps going. It can summarize findings, flag anomalies, generate a formatted report, produce a CSV, draft an email to your team, or answer follow-up questions, all in the same conversation. 
- Trade-off: slower than native commands. Best for complex one-off operations, cross-discipline analysis, exploration, and anywhere the presentation of results matters as much as the results themselves.

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
