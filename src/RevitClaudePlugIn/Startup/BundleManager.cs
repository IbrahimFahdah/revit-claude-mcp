using Newtonsoft.Json;
using RevitClaudePlugIn.Common;
using System;
using System.IO;
using System.Net.Http;
using System.Reflection;

namespace RevitClaudePlugIn.Startup
{
    /// <summary>
    /// Checks whether a newer version of the plugin is available on GitHub Releases.
    /// </summary>
    internal sealed class BundleManager
    {
        public (bool updateRequired, string downloadUrl) RequirePlugInUpdate(Uri latestJsonUrl)
        {
            var (currentVersion, _, _) = ReadCurrentPluginVersion();
            var latest = DownloadLatestPlugIn(latestJsonUrl);

            var latestVersion = latest.tag_name.TrimStart('v', 'V');

            if (Version.TryParse(currentVersion, out var cv) &&
                Version.TryParse(latestVersion, out var lv) &&
                lv <= cv)
            {
                return (false, null);
            }

            var asset = latest.assets.Find(a => a.name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
            var downloadUrl = asset?.browser_download_url
                ?? "https://github.com/IbrahimFahdah/revit-claude-mcp/releases/latest";

            return (true, downloadUrl);
        }

        public (string? version, string? checkUrl, string? bridgeUrl) ReadCurrentPluginVersion()
        {
            var assemblyPath = Assembly.GetExecutingAssembly().Location;
            var folder = Path.GetDirectoryName(assemblyPath);
            var settingsFile = Path.Combine(folder!, "plugin_settings.json");
            if (!File.Exists(settingsFile))
                return (null, null, null);

            var json = File.ReadAllText(settingsFile);
            var settings = JsonConvert.DeserializeObject<PluginSettings>(json);
            return (settings?.Version, settings?.CheckUrl, settings?.BridgeUrl);
        }

        private static GitHubRelease DownloadLatestPlugIn(Uri url)
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.Add("User-Agent", "RevitClaudeConnector");
            var json = http.GetStringAsync(url).GetAwaiter().GetResult();
            var obj = JsonConvert.DeserializeObject<GitHubRelease>(json)
                      ?? throw new InvalidDataException("Unable to parse GitHub release response.");
            if (string.IsNullOrWhiteSpace(obj.tag_name))
                throw new InvalidDataException("GitHub release response missing tag_name.");
            return obj;
        }
    }
}
