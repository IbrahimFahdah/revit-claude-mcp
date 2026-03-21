using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitStartup.Base;
using System;
namespace RevitClaudeConnector.ToolHandler
{
    public class UiHandler : UiHandlerBase, IExternalEventHandler, IUIHandler
    {
        ToolRegistry toolRegistry;

        public void Execute(UIApplication uiapp)
        {
            try
            {
                var uidoc = uiapp.ActiveUIDocument;
                var doc = uidoc?.Document;

                // ---------- /ping ----------
                if (_path == "/ping")
                {
                    var o = new JObject
                    {
                        ["ok"] = true,
                        ["hasActiveDoc"] = doc != null
                    };
                    _tcs.TrySetResult(o.ToString());
                    return;
                }

                toolRegistry ??= ToolRegistry.LoadForCurrentRevit(uiapp);

                var ctx = new ToolContext(uiapp);

                // ---------- /tools (GET) ----------
                if (_path == "/tools" && _method == "GET")
                {
                    var arr = new JArray();
                    foreach (var t in toolRegistry.Tools)
                        arr.Add(new JObject { ["name"] = t.Value.ToolSchema.Name, ["description"] = t.Value.ToolSchema.Description, ["inputSchema"] = t.Value.ToolSchema.InputSchema });

                    _tcs.TrySetResult(arr.ToString());
                    return;
                }

                // For everything below, we need an active document
                if (doc == null)
                {
                    _tcs.TrySetResult(@"{""error"":""No active document""}");
                    return;
                }

                // ---------- /call (POST) ----------
                if (_path == "/call" && _method == "POST")
                {
                    var req = JObject.Parse(_body);
                    string name = (string)(req["name"] ?? "");
                    //var args = (JObject)(req["arguments"] ?? new JObject());

                    if (!toolRegistry.Tools.TryGetValue(name, out var tool)) { _tcs.TrySetResult(@"{""error"":""Unknown tool""}"); return; }
                    if (tool.ToolSchema.NeedsActiveDocument && ctx.Doc == null) { _tcs.TrySetResult(@"{""error"":""No active document""}"); return; }

                    var root = JObject.Parse(_body);
                    string argsJson = root["arguments"]?.ToString(Newtonsoft.Json.Formatting.None);
                    var ret = toolRegistry.Invoke(name, uiapp, ctx.UIDoc, argsJson);

                    try { _tcs.TrySetResult((JObject.Parse(ret) ?? new JObject { ["ok"] = true }).ToString()); }
                    catch (Exception ex) { _tcs.TrySetResult(@"{""error"":""" + ex.Message.Replace("\"", "'") + @"""}"); }
                    return;
                }

                _tcs.TrySetResult(@"{""error"":""Unknown path""}");
            }
            catch (Exception ex)
            {
                _tcs.TrySetResult(@"{""error"":""" + ex.Message.Replace("\"", "'") + @"""}");
            }
        }

        public string GetName() => "Claude Revit Bridge";
    }
}
