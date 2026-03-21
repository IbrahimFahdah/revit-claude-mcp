using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
namespace RevitClaudeConnector.ToolLoading
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
