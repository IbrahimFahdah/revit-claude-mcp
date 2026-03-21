using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitClaudeConnector.Common;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
namespace RevitClaudeConnector.ToolLoading
{
    /// <summary>
    /// Discovers and invokes tools for the current Revit major version.
    /// Looks under:
    /// %LOCALAPPDATA%\IFADAH\RevitTools\revit-{MAJOR}\packages\{Package}\versions\{current.txt}\manifest.json
    /// Each tool entry must specify: runner.assembly, runner.type, runner.method (default "Execute").
    /// </summary>
    public sealed class ToolRegistry
    {
        private static readonly ConcurrentDictionary<string, CachedTool> _loadedTools = new();

        public static ToolRegistry LoadForCurrentRevit(UIApplication uiapp, string rootDir = null)
        {
            var major = GetRevitMajor(uiapp);
            return LoadForRevitMajor(major, rootDir);
        }

        public static ToolRegistry LoadForRevitMajor(string revitMajor, string rootDir = null)
        {
            var reg = new ToolRegistry(revitMajor, rootDir);
            reg.ScanAndLoad();
            return reg;
        }

        /// <summary>All discovered tools keyed by unique name.</summary>
        public IReadOnlyDictionary<string, ToolDescriptor> Tools => _tools;

        /// <summary>Convenience list of tool names.</summary>
        public IEnumerable<string> ToolNames => _tools.Keys;

        /// <summary>
        /// Invoke a tool by name.
        /// The tool’s static entry must be: Execute(UIApplication, UIDocument, string) -> string (JSON).
        /// Returns the JSON string produced by the tool.
        /// </summary>
        public string Invoke(string toolName, UIApplication uiapp, UIDocument uidoc, string argsJson)
        {
            if (!_tools.TryGetValue(toolName, out var td))
                throw new KeyNotFoundException(
                    $"Tool '{toolName}' not found. Available: {string.Join(", ", _tools.Keys)}");

            var asmPath = Path.Combine(td.PackageVersionDir, td.RunnerAssemblyRelPath);
            if (!File.Exists(asmPath))
                throw new FileNotFoundException($"Runner assembly not found for tool '{toolName}'.", asmPath);

            // Get or load the tool
            // Use a composite key combining asmPath and RunnerTypeName
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
        private readonly string _rootDir;
        private readonly Dictionary<string, ToolDescriptor> _tools = new(StringComparer.OrdinalIgnoreCase);

        private ToolRegistry(string revitMajor, string rootDir)
        {
            _revitMajor = revitMajor;
            _rootDir = rootDir ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                Constants.Company, "RevitTools", "app");
        }

        private void ScanAndLoad()
        {
            var packagesRoot = Path.Combine(_rootDir, $"revit-{_revitMajor}", "packages");
            if (!Directory.Exists(packagesRoot)) return;

            foreach (var pkgDir in Directory.EnumerateDirectories(packagesRoot))
            {
                var packageName = Path.GetFileName(pkgDir);
                var currentFile = Path.Combine(pkgDir, "current.txt");
                if (!File.Exists(currentFile)) continue;

                var currentVersion = File.ReadAllText(currentFile).Trim();
                if (string.IsNullOrWhiteSpace(currentVersion)) continue;

                var versionDir = Path.Combine(pkgDir, "versions", currentVersion);
                var manifestPath = Path.Combine(versionDir, "manifest.json");
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

                    // Try to read MCP schema file if provided
                    McpToolSchema mcp = null;
                    if (!string.IsNullOrWhiteSpace(schemaRel))
                    {
                        var schemaPath = Path.Combine(versionDir, schemaRel);
                        if (File.Exists(schemaPath))
                        {
                            try
                            {
                                var jo = JObject.Parse(File.ReadAllText(schemaPath));

                                // Minimal validation for MCP shape
                                var mName = (string)jo["name"] ?? name; // default to tool name
                                var mDesc = (string)jo["description"] ?? "";
                                var input = jo["input_schema"] as JObject;

                                if (input == null)
                                {
                                    // If the file was a raw JSON Schema (legacy), wrap it into MCP
                                    input = jo; // treat whole file as input_schema
                                }

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
                        Version = currentVersion,
                        PackageVersionDir = versionDir,
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
            // Application.VersionNumber returns e.g. "2025"
            var s = uiapp.Application?.VersionNumber ?? "";
            if (!string.IsNullOrWhiteSpace(s)) return s;
            // Fallback: use current year if needed (unlikely)
            return DateTime.Now.Year.ToString();
        }

        // ---------- Types ----------

        public sealed class ToolDescriptor
        {
            public string Name { get; init; } = "";
            public string Package { get; init; } = "";
            public string Version { get; init; } = "";
            public string PackageVersionDir { get; init; } = "";
            public string RunnerAssemblyRelPath { get; init; } = "";
            public string RunnerTypeName { get; init; } = "";
            public string RunnerMethodName { get; init; } = "Execute";

            // Relative schema file path from manifest.json (optional, for debugging)
            public string SchemaRelPath { get; init; }

            // Parsed MCP schema (preferred for serving to Claude MCP)
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
