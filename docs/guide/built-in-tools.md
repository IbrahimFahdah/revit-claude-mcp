# Built-In Tools

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
