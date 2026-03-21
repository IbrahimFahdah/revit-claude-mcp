using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RevitClaudeConnector.Common;
using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;

namespace RevitClaudeConnector.Startup
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

            if (string.Equals(currentVersion, latest.version, StringComparison.OrdinalIgnoreCase))
            {
                return (false, null);
            }

            return (true, latest.zip_url);
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

        /// <summary>
        /// Ensure local content is at latest version; download+replace if needed.
        /// Returns the active version after the check.
        /// </summary>
        public string EnsureLatest(Uri latestJsonUrl, bool requireSha256 = true)
        {
            using var _ = AcquireGlobalLock();

            var current = ReadCurrent();
            var latest = DownloadLatestTools(latestJsonUrl);

            if (requireSha256 && string.IsNullOrWhiteSpace(latest.sha256))
                throw new InvalidOperationException("latest.json missing required 'sha256'.");

            if (string.Equals(current, latest.version, StringComparison.OrdinalIgnoreCase))
            {
                // Already current; ensure app folder exists
                Directory.CreateDirectory(ContentRoot);
                return current!;
            }

            // Download the single ZIP
            var zipBytes = DownloadBytes(new Uri(latest.zip_url));

            // Verify hash
            //if (!string.IsNullOrWhiteSpace(latest.sha256))
            //    VerifySha256(zipBytes, latest.sha256!);

            // Stage: unzip to temp
            var staging = Path.Combine(RootDir, "bundles", "tmp", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(staging);

            using (var ms = new MemoryStream(zipBytes, writable: false))
            using (var za = new ZipArchive(ms, ZipArchiveMode.Read))
                za.ExtractToDirectory(staging, overwriteFiles: true);

            UnblockRecursively(staging);

            // Optional: sanity check a manifest inside the ZIP (if you include one)
            var manifestPath = Path.Combine(staging, "manifest.json");
            if (File.Exists(manifestPath))
            {
                var jo = JObject.Parse(File.ReadAllText(manifestPath));
                var ver = (string?)jo["version"];
                if (!string.IsNullOrWhiteSpace(ver) &&
                    !string.Equals(ver, latest.version, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException($"manifest.json version '{ver}' != latest '{latest.version}'");
            }

            // Prepare replacement
            var appDir = ContentRoot;
            Directory.CreateDirectory(Path.GetDirectoryName(appDir)!);

            // Keep a backup of the previous app folder (one copy)
            if (Directory.Exists(appDir))
            {
                var prev = ReadCurrent();
                var backupDir = Path.Combine(RootDir, "backup");
                Directory.CreateDirectory(backupDir);
                var dst = Path.Combine(backupDir, string.IsNullOrWhiteSpace(prev) ? "previous" : prev!);

                // Clear old backup if same name
                SafeDeleteDirectory(dst);
                Directory.Move(appDir, dst);
            }

            // Move staged → app
            Directory.CreateDirectory(Path.GetDirectoryName(appDir)!);
            Directory.Move(staging, appDir);

            // Atomically write new current.txt
            WriteAllTextAtomic(CurrentFile, latest.version + Environment.NewLine);

            // Cleanup: remove other temp roots left behind
            SafeDeleteDirectory(Path.GetDirectoryName(staging)!);

            return latest.version!;
        }

        /// <summary>
        /// Returns current installed version (or null if none).
        /// </summary>
        public string? GetCurrentVersion() => ReadCurrent();

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

        private static void VerifySha256(byte[] data, string expectedHex)
        {
            using var sha = SHA256.Create();
            var hex = Convert.ToHexString(sha.ComputeHash(data)).ToLowerInvariant();
            var norm = expectedHex.Replace(" ", "").Replace("-", "").Trim().ToLowerInvariant();
            if (!hex.Equals(norm, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"SHA256 mismatch. expected={expectedHex} actual={hex}");
        }

        private static void UnblockRecursively(string root)
        {
            foreach (var f in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                try { File.Delete(f + ":Zone.Identifier"); } catch { /* ignore */ }
            }
        }

        private static void WriteAllTextAtomic(string path, string content)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, content);
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
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

        private static LatestPluginJson DownloadLatestPlugIn(Uri url)
        {
            using var http = new HttpClient();
            var json = http.GetStringAsync(url).GetAwaiter().GetResult();
            var obj = JsonConvert.DeserializeObject<LatestPluginJson>(json)
                      ?? throw new InvalidDataException("Unable to parse latest.json.");
            if (string.IsNullOrWhiteSpace(obj.version) || string.IsNullOrWhiteSpace(obj.zip_url))
                throw new InvalidDataException("latest.json missing required fields (version, zip_url).");
            return obj;
        }

        private static byte[] DownloadBytes(Uri url)
        {
            using var http = new HttpClient();
            return http.GetByteArrayAsync(url).GetAwaiter().GetResult();
        }

        private static void SafeDeleteDirectory(string path)
        {
            if (!Directory.Exists(path)) return;
            try { Directory.Delete(path, true); } catch { /* ignore */ }
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
