using Newtonsoft.Json;
using RevitClaudePlugIn.Common;
using System;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Threading;

namespace RevitClaudePlugIn.Startup
{
    /// <summary>
    /// Global bundle manager for a single ZIP + single latest.json.
    /// Installs under: %LOCALAPPDATA%\IFADAH\RevitTools
    ///
    /// Layout:
    ///   RootDir\
    ///     app\                <-- active unpacked content (replaced wholesale on update)
    ///     current.txt         <-- "1.2.3"
    ///     backup\1.2.2\...    <-- optional previous (one copy kept)
    ///     bundles\tmp\{guid}\ <-- staging
    ///     lockfiles\global.lck
    ///
    /// Usage at startup:
    ///   var mgr = new BundleManager();
    ///   mgr.EnsureLatest(new Uri("https://cdn.example.com/latest.json"));
    ///   var contentRoot = mgr.ContentRoot; // ...\RevitTools\app
    /// </summary>
    internal sealed partial class BundleManager
    {
        public string RootDir { get; }
        public string ContentRoot => Path.Combine(RootDir, "app");
        private string CurrentFile => Path.Combine(RootDir, "current.txt");

        public BundleManager(string? rootDir = null)
        {
            RootDir = rootDir ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
              Constants.Company, "RevitTools");
            Directory.CreateDirectory(RootDir);
        }

        public (bool updateRequired, string downloadUrl) RequirePlugInUpdate(Uri latestJsonUrl)
        {
            using var _ = AcquireGlobalLock();
            var (currentVersion, _, _) = ReadCurrentPluginVersion();
            var latest = DownloadLatestPlugIn(latestJsonUrl);

            // tag_name is typically "v1.2.0" — strip leading 'v' for comparison
            var latestVersion = latest.tag_name.TrimStart('v', 'V');

            if (Version.TryParse(currentVersion, out var cv) &&
                Version.TryParse(latestVersion, out var lv) &&
                lv <= cv)
            {
                return (false, null);
            }

            var asset = latest.assets.Find(a => a.name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
            var downloadUrl = asset?.browser_download_url
                ?? $"https://github.com/IbrahimFahdah/revit-claude-mcp/releases/latest";

            return (true, downloadUrl);
        }

        public bool RequireToolsUpdate(Uri latestJsonUrl)
        {
            using var _ = AcquireGlobalLock();
            var current = ReadCurrent();
            var latest = DownloadLatestTools(latestJsonUrl);

            if (string.Equals(current, latest.version, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }

        // --------------- Internals ---------------

        private string? ReadCurrent()
            => File.Exists(CurrentFile) ? File.ReadAllText(CurrentFile).Trim() : null;

        /// <summary>
        /// Reads plugin settings JSON next to the DLL and returns version, check URL, and download URL.
        /// </summary>
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

        private static LatestToolsJson DownloadLatestTools(Uri url)
        {
            using var http = new HttpClient();
            var json = http.GetStringAsync(url).GetAwaiter().GetResult();
            var obj = JsonConvert.DeserializeObject<LatestToolsJson>(json)
                      ?? throw new InvalidDataException("Unable to parse latest.json.");
            if (string.IsNullOrWhiteSpace(obj.version) || string.IsNullOrWhiteSpace(obj.zip_url))
                throw new InvalidDataException("latest.json missing required fields (version, zip_url).");
            return obj;
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

        // Global lock so two Revit instances don’t update at the same time
        private sealed class GlobalLock : IDisposable
        {
            private readonly FileStream _fs;
            public GlobalLock(FileStream fs) => _fs = fs;
            public void Dispose() { try { _fs.Dispose(); } catch { } }
        }

        private GlobalLock AcquireGlobalLock()
        {
            var lockDir = Path.Combine(RootDir, "lockfiles");
            Directory.CreateDirectory(lockDir);
            var path = Path.Combine(lockDir, "global.lck");

            FileStream fs;
            while (true)
            {
                try
                {
                    fs = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                    break;
                }
                catch (IOException)
                {
                    Thread.Sleep(100);
                }
            }
            return new GlobalLock(fs);
        }
    }
}
