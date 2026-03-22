using Autodesk.Revit.UI;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using RevitClaudePlugIn.ToolHandler;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace RevitClaudePlugIn.Startup
{
    public class AppStartup
    {
        private HttpListener _listener;
        protected UiHandler Handler;
        private ExternalEvent _extEvent;

        public AppStartup(UiHandler UiHandler)
        {
            Handler = UiHandler;
            _extEvent = ExternalEvent.Create((IExternalEventHandler)Handler);
        }

        public bool Run(string assemblyPath)
        {
            var listenerUrl = "http://127.0.0.1:5578/";
            try
            {
                var bm = new BundleManager();

                // Read plugin settings and get checkUrl
                var (pluginVersion, checkUrl, bridgeUrl) = bm.ReadCurrentPluginVersion();
                if (!string.IsNullOrWhiteSpace(checkUrl))
                {
                    var re = bm.RequirePlugInUpdate(new Uri(checkUrl));
                    if (re.updateRequired)
                    {
                        var isYes = MessageBox("PlugIn Update",
                            "A new version of the connector is available. Do you want to download it?\n\n" +
                            re.releaseNotes);
                        if (isYes)
                        {
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = re.downloadUrl,
                                UseShellExecute = true
                            });
                            return false;
                        }
                    }
                }

                listenerUrl = !string.IsNullOrWhiteSpace(bridgeUrl) ? bridgeUrl : "http://127.0.0.1:5578/";
                _listener = new HttpListener();
                _listener.Prefixes.Add(listenerUrl);

                _listener.Start();
                TaskDialog("RevitClaudeConnector", "RevitClaudeConnector started successfully");

                // Initialize Application Insights with your instrumentation key
                var config = new TelemetryConfiguration("a2067467-fe1f-486e-9954-b086a713ae35");
                var telemetryClient = new TelemetryClient(config);
                telemetryClient.TrackTrace($"App session started by {Environment.UserName}");
                telemetryClient.Flush();
            }
            catch (HttpListenerException ex)
            {
                TaskDialog(
                    "RevitClaudeConnector ERROR",
                    $"Failed to start HTTP bridge on {listenerUrl}\n\n" +
                    "Run as Administrator once:\n" +
                    $"  netsh http add urlacl url={listenerUrl} user=%USERNAME%\n\n" +
                    "Details:\n" + ex.Message);
                return false;
            }
            catch (Exception ex)
            {
                TaskDialog(
                    "RevitClaudeConnector ERROR", "Something went wrong" + "Details:\n" + ex.Message);
                return false;
            }

            _ = Task.Run(() => Loop());

            return true;
        }

        public void Stop()
        {
            try { _listener?.Stop(); } catch { }
            try { _listener?.Close(); } catch { }
        }

        private async Task Loop()
        {
            while (_listener.IsListening)
            {
                HttpListenerContext ctx = null;
                try { ctx = await _listener.GetContextAsync(); }
                catch { break; }
                if (ctx == null) break;
                _ = Handle(ctx);
            }
        }

        private async Task Handle(HttpListenerContext ctx)
        {
            var path = ctx.Request.Url?.AbsolutePath ?? "/";
            var method = ctx.Request.HttpMethod?.ToUpperInvariant() ?? "GET";

            // read body if any; otherwise treat as "{}" to avoid 411 issues
            var body = "{}";
            if (ctx.Request.HasEntityBody)
            {
                using (var sr = new StreamReader(ctx.Request.InputStream, ctx.Request.ContentEncoding))
                    body = await sr.ReadToEndAsync();
                if (string.IsNullOrWhiteSpace(body)) body = "{}";
            }

            var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            Handler?.Set(path, method, body, tcs);
            RaiseEvent();
            var result = await tcs.Task;

            var payload = Encoding.UTF8.GetBytes(result);
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "application/json; charset=utf-8";
            await ctx.Response.OutputStream.WriteAsync(payload, 0, payload.Length);
            ctx.Response.OutputStream.Close();
        }

        protected void RaiseEvent()
        {
            _extEvent.Raise();
        }

        protected void TaskDialog(string title, string message)
        {
            Autodesk.Revit.UI.TaskDialog.Show(title, message);
        }

        protected bool MessageBox(string title, string message)
        {
            var res = Autodesk.Revit.UI.TaskDialog.Show(title, message, TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No);
            return res == TaskDialogResult.Yes;
        }
    }
}
