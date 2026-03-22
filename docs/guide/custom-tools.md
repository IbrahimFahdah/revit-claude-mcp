# Custom Tools : ⚡ Supercharge with Your Own Tools!

You can also create and add your own tools to the connecter. Custom tools are installed per user and per Revit major version under `%LOCALAPPDATA%`.

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

## Next Step

→ *(Coming soon)* Accessing Revit data from your tool — parameters, elements, transactions
