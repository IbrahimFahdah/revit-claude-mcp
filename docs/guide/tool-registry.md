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

---

## Built-In Tools

Built-in tools ship **alongside the plugin DLL** in a `Tools\Packages` subfolder.
At runtime, the registry resolves the plugin folder from the location of the executing assembly, so no configuration is needed.

**On-disk location after installation:**

```
{RevitAddInsFolder}\RevitClaudeConnector\
    RevitClaudeConnector.dll
    Tools\
        Packages\
            TestTools\
                manifest.json
                runners\
                    Tools.dll
                schemas\
                    get_elements_by_category.schema.json
                    ...
```

The connector ships with **46 ready-made tools** in the `TestTools` package covering querying, modifying, exporting, and visualising Revit models.

Because built-in tools are loaded from the plugin folder, they are upgraded automatically whenever the plugin itself is updated. You do not need to touch the `%LOCALAPPDATA%` folder to get new built-in tools.

---

## Custom Tools

Custom tools are installed per user and per Revit major version under `%LOCALAPPDATA%`.

**Root path:**

```
%LOCALAPPDATA%\RevitClaudeConnector\{RevitMajor}\Tools\Packages\
```

For example, for Revit 2025:

```
C:\Users\YourName\AppData\Local\RevitClaudeConnector\2025\Tools\Packages\
    MyCompanyTools\
        manifest.json
        runners\
            MyCompanyTools.dll
        schemas\
            my_tool.schema.json
```

**Key rules:**

- Each subdirectory of `Packages\` is treated as one package.
- There is no registration step — drop the folder in and restart Revit.
- If a custom tool has the same `name` as a built-in tool, **the custom tool takes precedence**.
- Tools are isolated with a dedicated `AssemblyLoadContext`, so your DLL's dependencies cannot conflict with the plugin's.

---

## Writing a Custom Tool — Minimal Example

**1. Create a class library** targeting `net8.0-windows` and reference `RevitAPI.dll`.

**2. Implement the tool:**

```csharp
using Autodesk.Revit.UI;

namespace MyCompanyTools
{
    public class GetActiveViewName
    {
        public string Execute(UIApplication uiapp, UIDocument uidoc, string requestJson)
        {
            var viewName = uidoc?.ActiveView?.Name ?? "No active document";
            return $"{{\"viewName\":\"{viewName}\"}}";
        }
    }
}
```

**3. Write the manifest:**

```json
{
  "version": "1.0.0",
  "tools": [
    {
      "name": "get_active_view_name",
      "runner": {
        "assembly": "runners/MyCompanyTools.dll",
        "type": "MyCompanyTools.GetActiveViewName"
      },
      "schema": "schemas/get_active_view_name.schema.json"
    }
  ]
}
```

**4. Write the schema:**

```json
{
  "name": "get_active_view_name",
  "description": "Returns the name of the currently active view in Revit.",
  "input_schema": {
    "type": "object",
    "properties": {},
    "required": []
  }
}
```

**5. Copy the output to the custom packages folder:**

```
%LOCALAPPDATA%\RevitClaudeConnector\2025\Tools\Packages\
    MyCompanyTools\
        manifest.json
        runners\
            MyCompanyTools.dll
        schemas\
            get_active_view_name.schema.json
```

**6. Restart Revit.** The tool appears immediately in `tools/list` and Claude can call it.

---

## Summary

| | Built-in tools | Custom tools |
|---|---|---|
| **Location** | `{PluginDir}\Tools\Packages\` | `%LOCALAPPDATA%\RevitClaudeConnector\{RevitMajor}\Tools\Packages\` |
| **Who manages them** | Updated with the plugin | You |
| **Collision behaviour** | Loaded first | Override built-ins on name clash |
| **Requires restart?** | Only when plugin updates | Yes, once after adding a package |

---

## Next Step

→ *(Coming soon)* Accessing Revit data from your tool — parameters, elements, transactions
