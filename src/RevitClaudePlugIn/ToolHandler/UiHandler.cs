using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;
namespace RevitClaudePlugIn.ToolHandler
{
    public class UiHandler : IExternalEventHandler
    {
        ToolRegistry toolRegistry;
        protected string _path = "/";
        protected string _method = "GET";
        protected string _body = "{}";
        protected TaskCompletionSource<string> _tcs;

        public void Set(string path, string method, string body, TaskCompletionSource<string> tcs)
        {
            _path = path; _method = method; _body = body; _tcs = tcs;
        }

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
                    var name = (string)(req["name"] ?? "");
                    //var args = (JObject)(req["arguments"] ?? new JObject());

                    if (!toolRegistry.Tools.TryGetValue(name, out var tool)) { _tcs.TrySetResult(@"{""error"":""Unknown tool""}"); return; }
                    if (tool.ToolSchema.NeedsActiveDocument && ctx.Doc == null) { _tcs.TrySetResult(@"{""error"":""No active document""}"); return; }

                    var root = JObject.Parse(_body);
                    var argsJson = root["arguments"]?.ToString(Newtonsoft.Json.Formatting.None);
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

        /// <summary>
        /// Reloads all tool packages from disk. If the registry has not been initialised yet
        /// it is created now. Returns the number of tools available after the reload.
        /// </summary>
        public int ReloadTools(UIApplication uiapp)
        {
            if (toolRegistry == null)
                toolRegistry = ToolRegistry.LoadForCurrentRevit(uiapp);
            else
                toolRegistry.Reload();

            return toolRegistry.Tools.Count;
        }

        public int ToolCount => toolRegistry?.Tools.Count ?? 0;

        public void EnsureInitialized(UIApplication uiapp)
        {
            toolRegistry ??= ToolRegistry.LoadForCurrentRevit(uiapp);
        }

        public string GetName() => "Claude Revit Bridge";
    }
}
