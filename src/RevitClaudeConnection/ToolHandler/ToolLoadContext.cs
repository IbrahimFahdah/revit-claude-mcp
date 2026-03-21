using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
namespace RevitClaudeConnector.ToolHandler
{
    public sealed class ToolLoadContext : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver _resolver;
        private static readonly HashSet<string> Shared = new(StringComparer.OrdinalIgnoreCase)
        {
            "Autodesk.RevitAPI",
            "Autodesk.RevitAPIUI",
        };

        public ToolLoadContext(string mainAsmPath) : base(isCollectible: true)
        {
            _resolver = new AssemblyDependencyResolver(mainAsmPath);
        }

        protected override Assembly Load(AssemblyName name)
        {
            if (Shared.Contains(name.Name!))
            {
                // Reuse Default’s Revit assemblies to avoid duplicates
                var inDefault = Default.Assemblies
                    .FirstOrDefault(a => a.GetName().Name!.Equals(name.Name, StringComparison.OrdinalIgnoreCase));
                if (inDefault != null) return inDefault;
                return null; // let Default resolve normally
            }

            var path = _resolver.ResolveAssemblyToPath(name);
            return path != null ? LoadFromAssemblyPath(path) : null;
        }

        protected override nint LoadUnmanagedDll(string unmanagedDllName)
        {
            var path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
            return path != null ? LoadUnmanagedDllFromPath(path) : nint.Zero;
        }
    }
}
