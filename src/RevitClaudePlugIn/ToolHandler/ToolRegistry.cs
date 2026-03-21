using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitClaudePlugIn.Common;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
namespace RevitClaudePlugIn.ToolHandler
{
    /// <summary>
    /// Discovers and invokes tools from two roots:
    ///   1. Built-in  — {PluginDir}\Tools\Packages\{Package}\manifest.json
    ///   2. Custom    — %LOCALAPPDATA%\RevitClaudeConnector\{RevitMajor}\Tools\Packages\{Package}\manifest.json
    /// Custom packages are loaded after built-ins; name collisions favour the custom tool.
    /// Each manifest.json sits directly in the package folder — no versions/ subfolder or current.txt.
    /// </summary>
    public sealed class ToolRegistry
    {
        private static readonly ConcurrentDictionary<string, CachedTool> _loadedTools = new();

        public static ToolRegistry LoadForCurrentRevit(UIApplication uiapp, string pluginDir = null)
        {
            var major = GetRevitMajor(uiapp);
            return LoadForRevitMajor(major, pluginDir);
        }

        public static ToolRegistry LoadForRevitMajor(string revitMajor, string pluginDir = null)
        {
            var reg = new ToolRegistry(revitMajor, pluginDir);
            reg.ScanAndLoad();
            return reg;
        }

        /// <summary>All discovered tools keyed by unique name.</summary>
        public IReadOnlyDictionary<string, ToolDescriptor> Tools => _tools;

        /// <summary>Convenience list of tool names.</summary>
        public IEnumerable<string> ToolNames => _tools.Keys;

        /// <summary>
        /// Invoke a tool by name.
        /// The tool's entry point must be: Execute(UIApplication, UIDocument, string) -> string (JSON).
        /// Returns the JSON string produced by the tool.
        /// </summary>
        public string Invoke(string toolName, UIApplication uiapp, UIDocument uidoc, string argsJson)
        {
            if (!_tools.TryGetValue(toolName, out var td))
                throw new KeyNotFoundException(
                    $"Tool '{toolName}' not found. Available: {string.Join(", ", _tools.Keys)}");

            var asmPath = Path.Combine(td.PackageDir, td.RunnerAssemblyRelPath);
            if (!File.Exists(asmPath))
                throw new FileNotFoundException($"Runner assembly not found for tool '{toolName}'.", asmPath);

            var cacheKey = $"{asmPath}::{td.RunnerTypeName}";

            var cached = _loadedTools.GetOrAdd(cacheKey, _ =>
            {
                var alc = new ToolALC(asmPath);
                var asm = alc.LoadFromAssemblyPath(asmPath);

                var type = asm.GetType(td.RunnerTypeName, throwOnError: true)!;
                var methodName = string.IsNullOrWhiteSpace(td.RunnerMethodName) ? "Execute" : td.RunnerMethodName;
                var mi = type.GetMethod(methodName,
                    BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance);

                if (mi == null)
                    throw new MissingMethodException($"{td.RunnerTypeName}.{methodName}(UIApplication, UIDocument, string) not found.");

                return new CachedTool(alc, asm, type, mi);
            });

            object instance = null;
            if (!cached.Method.IsStatic)
                instance = Activator.CreateInstance(cached.Type);

            var result = (string)cached.Method.Invoke(instance, new object[] { uiapp, uidoc, argsJson })!;
            return result;
        }

        private sealed record CachedTool(ToolALC ALC, Assembly Assembly, Type Type, MethodInfo Method);

        // ---------- Implementation ----------

        private readonly string _revitMajor;
        private readonly string _pluginDir;
        private readonly Dictionary<string, ToolDescriptor> _tools = new(StringComparer.OrdinalIgnoreCase);

        private ToolRegistry(string revitMajor, string pluginDir)
        {
            _revitMajor = revitMajor;
            _pluginDir = pluginDir
                ?? Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
                ?? AppDomain.CurrentDomain.BaseDirectory;
        }

        private void ScanAndLoad()
        {
            // 1. Built-in tools ship alongside the plugin DLL
            var builtInPackages = Path.Combine(_pluginDir, "Tools", "Packages");
            ScanPackagesRoot(builtInPackages);

            // 2. Custom/user-installed tools per Revit version (overwrite built-ins on collision)
            var customPackages = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                Constants.AppDataFolder, _revitMajor, "Tools", "Packages");
            ScanPackagesRoot(customPackages);
        }

        private void ScanPackagesRoot(string packagesRoot)
        {
            if (!Directory.Exists(packagesRoot)) return;

            foreach (var pkgDir in Directory.EnumerateDirectories(packagesRoot))
            {
                var packageName = Path.GetFileName(pkgDir);
                var manifestPath = Path.Combine(pkgDir, "manifest.json");
                if (!File.Exists(manifestPath)) continue;

                JObject manifest;
                try
                {
                    manifest = JObject.Parse(File.ReadAllText(manifestPath));
                }
                catch
                {
                    continue;
                }

                var version = manifest.Value<string>("version") ?? "";
                var tools = manifest["tools"] as JArray ?? new JArray();

                foreach (var t in tools.OfType<JObject>())
                {
                    var name = t.Value<string>("name");
                    var runner = (JObject)t["runner"];
                    if (string.IsNullOrWhiteSpace(name) || runner == null) continue;

                    var asmRel = runner.Value<string>("assembly") ?? "";
                    var type = runner.Value<string>("type") ?? "";
                    var method = runner.Value<string>("method") ?? "Execute";
                    var schemaRel = (t.Value<string>("schema") ?? "").Replace('/', Path.DirectorySeparatorChar);

                    if (string.IsNullOrWhiteSpace(asmRel) || string.IsNullOrWhiteSpace(type)) continue;

                    McpToolSchema mcp = null;
                    if (!string.IsNullOrWhiteSpace(schemaRel))
                    {
                        var schemaPath = Path.Combine(pkgDir, schemaRel);
                        if (File.Exists(schemaPath))
                        {
                            try
                            {
                                var jo = JObject.Parse(File.ReadAllText(schemaPath));

                                var mName = (string)jo["name"] ?? name;
                                var mDesc = (string)jo["description"] ?? "";
                                var input = jo["input_schema"] as JObject;

                                if (input == null)
                                    input = jo; // legacy: treat whole file as input_schema

                                mcp = new McpToolSchema
                                {
                                    Name = mName,
                                    Description = mDesc,
                                    InputSchema = input
                                };
                            }
                            catch
                            {
                                // leave mcp null on parse error
                            }
                        }
                    }

                    _tools[name] = new ToolDescriptor
                    {
                        Name = name,
                        Package = packageName,
                        Version = version,
                        PackageDir = pkgDir,
                        RunnerAssemblyRelPath = asmRel.Replace('/', Path.DirectorySeparatorChar),
                        RunnerTypeName = type,
                        RunnerMethodName = method,
                        SchemaRelPath = string.IsNullOrWhiteSpace(schemaRel) ? null : schemaRel,
                        ToolSchema = mcp
                    };
                }
            }
        }

        private static string GetRevitMajor(UIApplication uiapp)
        {
            var s = uiapp.Application?.VersionNumber ?? "";
            if (!string.IsNullOrWhiteSpace(s)) return s;
            return DateTime.Now.Year.ToString();
        }

        // ---------- Types ----------

        public sealed class ToolDescriptor
        {
            public string Name { get; init; } = "";
            public string Package { get; init; } = "";
            public string Version { get; init; } = "";
            public string PackageDir { get; init; } = "";
            public string RunnerAssemblyRelPath { get; init; } = "";
            public string RunnerTypeName { get; init; } = "";
            public string RunnerMethodName { get; init; } = "Execute";

            /// <summary>Relative schema file path from the package root (optional, for debugging).</summary>
            public string SchemaRelPath { get; init; }

            /// <summary>Parsed MCP schema served to Claude.</summary>
            public McpToolSchema ToolSchema { get; init; }
        }

        public sealed class McpToolSchema
        {
            public string Name { get; init; } = "";
            public string Description { get; init; } = "";
            public JObject InputSchema { get; init; } = new JObject
            {
                ["type"] = "object",
                ["additionalProperties"] = true
            };

            public bool NeedsActiveDocument { get; set; } = true;
        }
    }
}
