# Hot Reload

You do not need to restart Revit or Claude when adding or updating tools. Hot Reload works for both built-in and custom tools — any change to the packages folder is picked up on demand.

## Steps

**Step 1 — Reload in Revit**

Click **Reload Tools** in the Revit ribbon under **Claude Connector → Manage**. This rescans the packages folder and reloads all tool DLLs immediately, without restarting Revit.

**Step 2 — Refresh Claude's tool list**

Open Claude Desktop and go to **Settings → Developer**, find the connector MCP server entry, and toggle it **off then back on**. Claude will re-fetch the tool list and any new or updated tools will be available straight away.

> If toggling doesn't pick up the change, use the **Restart Claude** button in the Revit ribbon (**Claude Connector → Manage**) as a fallback — it hard-restarts Claude Desktop and re-attaches it to the panel.
