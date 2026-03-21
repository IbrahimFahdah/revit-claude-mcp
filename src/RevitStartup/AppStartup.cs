using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using RevitStartup.Base;
using System.Diagnostics;
using System.Net;
using System.Text;

namespace RevitStartup
{
    public class AppStartup
    {
        private HttpListener _listener;
        protected IUIHandler Handler;

        public AppStartup(IUIHandler UiHandler)
        {
            Handler = UiHandler;
        }

        public bool Run(string assemblyPath)
        {
            try
            {
                var bm = new BundleManager();

                // Read plugin settings and get checkUrl
                var (pluginVersion, checkUrl) = bm.ReadCurrentPluginVersion();
                if (!string.IsNullOrWhiteSpace(checkUrl))
                {
                    var re = bm.RequirePlugInUpdate(new Uri(checkUrl));
                    if (re.updateRequired)
                    {
                        var isYes = MessageBox("PlugIn Update", "A new version of the connector is available." +
                            "  Do you want to download the latest version?");
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


                //var packageUrl = new Uri(accountDetails.PackageUrl);
                //if (bm.RequireToolsUpdate(packageUrl))
                //{
                //    var isYes = MessageBox("Tools Update",
                //        "A new version of AI Tools is available. It will be downloaded and installed now.");
                //    if (isYes)
                //    {
                //        var active = bm.EnsureLatest(packageUrl);
                //    }
                //}

                _listener = new HttpListener();
                _listener.Prefixes.Add("http://127.0.0.1:5578/");

                _listener.Start();
                TaskDialog("RevitClaudeConnector", "RevitClaudeConnector started successfully");

                // Initialize Application Insights with your instrumentation key
                var config = new TelemetryConfiguration("a2067467-fe1f-486e-9954-b086a713ae35");
                var telemetryClient = new TelemetryClient(config);

                //  // Optional: add details (e.g., username, machine name)
                // telemetryClient.TrackTrace($"App started by {Environment.UserName} on licence: {terms.UserName}");

                // Send telemetry immediately
                telemetryClient.Flush();
            }
            catch (HttpListenerException ex)
            {
                TaskDialog(
                    "RevitClaudeConnector ERROR",
                    "Failed to start HTTP bridge on 127.0.0.1:5578.\n\n" +
                    "Run as Administrator once:\n" +
                    "  netsh http add urlacl url=http://127.0.0.1:5578/ user=%USERNAME%\n\n" +
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

        protected virtual void RaiseEvent()
        {
            // throw new NotImplementedException();
        }

        protected virtual bool MessageBox(string title, string message)
        {
            return false;
            // throw new NotImplementedException();
        }

        protected virtual void TaskDialog(string title, string message)
        {
            //throw new NotImplementedException();
        }
    }
}
