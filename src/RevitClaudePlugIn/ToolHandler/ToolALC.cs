#if !NET48
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
namespace RevitClaudePlugIn.ToolHandler
{
    /// <summary>
    /// Collectible ALC for tool assemblies. Shares the host contracts and any already-loaded
    /// assemblies from Default ALC to preserve type identity (IRevitTool, RevitAPI, Newtonsoft).
    /// Falls back to plugin folder for loose dependencies if needed.
    /// </summary>
    sealed class ToolALC : AssemblyLoadContext
    {
        private readonly string _baseDir;
        private readonly Assembly _hostContracts;

        public ToolALC(string baseDir, Assembly hostContracts = null)
            : base(isCollectible: true)
        {
            _baseDir = baseDir;
            _hostContracts = hostContracts;

            Resolving += OnResolving;
        }

        private Assembly OnResolving(AssemblyLoadContext ctx, AssemblyName name)
        {
            // 1) Ensure we share the host contracts assembly (IRevitTool)
            if (name.Name == _hostContracts?.GetName().Name)
                return _hostContracts;

            // 2) Reuse anything already loaded in Default (RevitAPI, Newtonsoft, etc.)
            var loaded = AppDomain.CurrentDomain
                                  .GetAssemblies()
                                  .FirstOrDefault(a => a.GetName().Name == name.Name);
            if (loaded != null) return loaded;

            // 3) Try plugin folder (next to the add-in DLL)
            var candidate = Path.Combine(Path.GetDirectoryName(_baseDir), name.Name + ".dll");
            if (File.Exists(candidate))
                return LoadFromAssemblyPath(candidate);

            return null!;
        }
    }
}
#else
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace RevitClaudePlugIn.ToolHandler
{
    /// <summary>
    /// .NET Framework 4.8 equivalent of ToolALC.
    /// AssemblyLoadContext is not available on net48; this implementation uses Assembly.LoadFrom
    /// with a shared AppDomain.AssemblyResolve handler instead.
    ///
    /// Known limitation: assemblies loaded via Assembly.LoadFrom cannot be unloaded.
    /// Calling Reload() on ToolRegistry drops the cached references but the DLLs remain
    /// in the AppDomain until Revit restarts. Custom tool hot-reload is not supported on
    /// Revit 2024 and below.
    /// </summary>
    sealed class ToolALC
    {
        private static readonly object _lock = new object();
        private static bool _resolverRegistered;
        private static readonly List<string> _searchDirs = new List<string>();

        public ToolALC(string asmPath)
        {
            var dir = Path.GetDirectoryName(asmPath);
            lock (_lock)
            {
                if (dir != null && !_searchDirs.Contains(dir, StringComparer.OrdinalIgnoreCase))
                    _searchDirs.Add(dir);

                if (!_resolverRegistered)
                {
                    AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
                    _resolverRegistered = true;
                }
            }
        }

        public Assembly LoadFromAssemblyPath(string path)
        {
            return Assembly.LoadFrom(path);
        }

        private static Assembly OnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            var name = new AssemblyName(args.Name).Name;

            // Reuse any already-loaded assembly with the same simple name
            var loaded = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == name);
            if (loaded != null) return loaded;

            // Try all registered package directories
            lock (_lock)
            {
                foreach (var dir in _searchDirs)
                {
                    var candidate = Path.Combine(dir, name + ".dll");
                    if (File.Exists(candidate))
                        return Assembly.LoadFrom(candidate);
                }
            }
            return null;
        }
    }
}
#endif
