# The Tool Registry

The **Tool Registry** is the part of the Revit add-in that discovers, loads, and invokes C# tools at runtime.
It reads tool packages from the filesystem, compiles a catalogue of every available tool, and hands that catalogue to the MCP bridge so Claude can call them.

This page explains how packages are structured, what the schema files look like, and where to put tools depending on whether they are built-in or written by you.

---

## How Discovery Works

When Revit starts, `ToolRegistry` scans two folder trees and merges the results into a single tool catalogue:

```
1. {PluginDir}\Tools\Packages\          ← built-in tools (ship with the plugin)
2. %LOCALAPPDATA%\RevitClaudeConnector\
       {RevitMajor}\Tools\Packages\     ← custom / user-installed tools
```

Both roots are scanned in order. If a tool name appears in both, the **custom tool wins** — this lets you override a built-in tool without modifying the plugin itself.

---

## Package Folder Structure

Every tool lives inside a **package** — a named folder that groups related tools, their compiled DLL, and their schema files.

```
Tools\
└── Packages\
    └── MyPackage\                 ← package folder (any name)
        ├── manifest.json          ← declares which tools are in this package
        ├── runners\
        │   └── Tools.dll          ← compiled C# assembly containing the tools
        └── schemas\
            └── my_tool.schema.json  ← MCP schema for a single tool (optional)
```

There is no `versions/` subfolder and no `current.txt` file. The package folder **is** the active version. The `manifest.json` may record a version string as metadata, but the folder layout does not depend on it.

---

## manifest.json

`manifest.json` sits at the root of every package and lists every tool the package provides.

```json
{
  "version": "1.0.0",
  "tools": [
    {
      "name": "get_elements_by_category",
      "runner": {
        "assembly": "runners/Tools.dll",
        "type": "Tools.GetElementsByCategory",
        "method": "Execute"
      },
      "schema": "schemas/get_elements_by_category.schema.json"
    }
  ]
}
```

| Field | Required | Description |
|---|---|---|
| `version` | No | Informational version string — not used for folder routing |
| `tools[].name` | **Yes** | Unique tool name used by Claude to call the tool |
| `tools[].runner.assembly` | **Yes** | Path to the DLL, relative to the package folder |
| `tools[].runner.type` | **Yes** | Fully-qualified C# class name inside the DLL |
| `tools[].runner.method` | No | Method name to call (default: `Execute`) |
| `tools[].schema` | No | Path to the MCP schema file, relative to the package folder |

Forward slashes in paths are normalised to the platform separator automatically.

---

## Tool Schema Files

Each `*.schema.json` file describes one tool to Claude: what the tool does and what arguments it accepts.
The format follows the [MCP tool schema](https://spec.modelcontextprotocol.io) convention.

```json
{
  "name": "get_elements_by_category",
  "description": "Returns all element IDs in the active model that belong to the given Revit category name.",
  "input_schema": {
    "type": "object",
    "properties": {
      "categoryName": {
        "type": "string",
        "description": "The built-in Revit category name, e.g. \"Walls\" or \"Doors\"."
      }
    },
    "required": ["categoryName"]
  }
}
```

| Field | Description |
|---|---|
| `name` | Canonical tool name (defaults to `tools[].name` from the manifest if omitted) |
| `description` | What the tool does — Claude reads this to decide *when* to call it. Write clearly. |
| `input_schema` | JSON Schema object describing the arguments the tool accepts |

::: tip Description quality matters
Claude uses the `description` field to decide which tool fits the user's request.
A vague description like `"Gets elements"` is much less useful than `"Returns all element IDs whose Revit category matches the given name"`.
:::

If a tool has no schema file, the registry registers it with a permissive schema that accepts any object.
Claude can still call it, but without a description it will rarely know when to do so.

---

## The Tool Entry Point

Every tool class must expose a public `Execute` method (or the name you set in `method`) with this signature:

```csharp
public string Execute(UIApplication uiapp, UIDocument uidoc, string requestJson)
```

- `uiapp` — the Revit `UIApplication`; always provided.
- `uidoc` — the active `UIDocument`; may be `null` if no document is open.
- `requestJson` — the arguments from Claude, serialised as a JSON string.
- **Return value** — a JSON string. Claude receives this as the tool's result.

The method can be instance or static. If it is an instance method, the registry creates a new instance of the class for each call via `Activator.CreateInstance`.

