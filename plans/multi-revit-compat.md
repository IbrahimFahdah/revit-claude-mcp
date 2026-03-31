# Plan: Multi-Revit Version Compatibility

**Branch:** `feature/multi-revit-compat`
**Goal:** Support Revit 2024 and below (`.NET Framework 4.8`) alongside the existing Revit 2025/2026 target (`.NET 8`) from a single codebase, and ship both variants in the release zip.

---

## Background

Revit 2025 moved from .NET Framework 4.8 to .NET 8. The plugin currently targets `net8.0-windows` only, making it incompatible with Revit 2024 and below. The approach is multi-targeting: one codebase builds two outputs — one per runtime — with conditional compilation handling the few incompatible APIs.

---

## Release Artifact Structure (target state)

```
revit-claude-connector-vX.Y.Z.zip
├── Revit2025Plus/
│   ├── RevitClaudeConnector/        ← net8.0-windows build
│   │   ├── RevitClaudePlugIn.dll
│   │   ├── plugin_settings.json
│   │   ├── Tools/Packages/BuiltIn/
│   │   └── ...
│   ├── RevitClaudeConnector.addin
│   └── server/
├── Revit2024/
│   ├── RevitClaudeConnector/        ← net48 build
│   │   ├── RevitClaudePlugIn.dll
│   │   ├── plugin_settings.json
│   │   ├── Tools/Packages/BuiltIn/
│   │   └── ...
│   ├── RevitClaudeConnector.addin
│   └── server/
└── INSTALL.md
```

---

## Tasks

### Task 1 — `ToolALC`: Replace `AssemblyLoadContext` with `AppDomain` for `net48`

**File:** `src/RevitClaudePlugIn/ToolHandler/ToolALC.cs`

This is the only hard engineering task. `AssemblyLoadContext` does not exist on .NET Framework; the equivalent isolation mechanism is `AppDomain`.

- Wrap the existing implementation in `#if !NET48`
- Add an `#if NET48` implementation that:
  - Creates a child `AppDomain` with a custom `AssemblyResolve` handler
  - Resolves shared assemblies (RevitAPI, Newtonsoft, IRevitTool contracts) from the host domain
  - Falls back to loading from the tool's package directory
- **Known limitation:** `AppDomain` on .NET Framework does not support unloading assemblies in the same way as collectible ALCs. Tools load correctly but require a Revit restart to pick up DLL changes. Document this as a known Revit 2024 limitation.

---

### Task 2 — Port `BuiltInTools` away from `System.Text.Json.Nodes`

**Files:** All `.cs` files in `src/BuiltInTools/`

`System.Text.Json.Nodes` (`JsonObject`, `JsonArray`, `JsonValue`) is a .NET 6+ API not available on `net48`. Newtonsoft.Json (`JObject`, `JArray`, `JToken`) is already a dependency and works on both targets.

- Replace all `using System.Text.Json.Nodes` with `using Newtonsoft.Json.Linq`
- Replace `JsonObject` → `JObject`, `JsonArray` → `JArray`, `JsonValue` → `JToken`
- Replace `.GetValue<T>()` with `.Value<T>()`
- Drop `System.ClientModel` from `BuiltInTools.csproj` if it is only pulled in for `System.Text.Json` transitively

---

### Task 3 — Update all `.csproj` files for multi-targeting

**Files:**
- `src/RevitClaudePlugIn/RevitClaudePlugIn.csproj`
- `src/BuiltInTools/BuiltInTools.csproj`
- `src/CustomTools/CustomTools.csproj`

Changes per project:

**RevitClaudePlugIn + BuiltInTools:**
```xml
<!-- Change single target to multi-target -->
<TargetFrameworks>net8.0-windows;net48</TargetFrameworks>

<!-- Revit API: version conditional on framework -->
<ItemGroup Condition="'$(TargetFramework)' == 'net8.0-windows'">
  <PackageReference Include="Nice3point.Revit.Api.RevitAPI" Version="2026.*" ExcludeAssets="runtime" />
  <PackageReference Include="Nice3point.Revit.Api.RevitAPIUI" Version="2026.*" ExcludeAssets="runtime" />
</ItemGroup>
<ItemGroup Condition="'$(TargetFramework)' == 'net48'">
  <PackageReference Include="Nice3point.Revit.Api.RevitAPI" Version="2024.*" ExcludeAssets="runtime" />
  <PackageReference Include="Nice3point.Revit.Api.RevitAPIUI" Version="2024.*" ExcludeAssets="runtime" />
</ItemGroup>
```

**CustomTools:**
```xml
<TargetFrameworks>net8.0;net48</TargetFrameworks>

<!-- Direct file references, conditional on framework -->
<ItemGroup Condition="'$(TargetFramework)' == 'net8.0'">
  <Reference Include="RevitAPI">
    <HintPath>$(ProgramFiles)\Autodesk\Revit 2026\RevitAPI.dll</HintPath>
    <Private>False</Private>
  </Reference>
</ItemGroup>
<ItemGroup Condition="'$(TargetFramework)' == 'net48'">
  <Reference Include="RevitAPI">
    <HintPath>$(ProgramFiles)\Autodesk\Revit 2024\RevitAPI.dll</HintPath>
    <Private>False</Private>
  </Reference>
</ItemGroup>
```

---

### Task 4 — Add `IsExternalInit` polyfill

**New file:** `src/RevitClaudePlugIn/Polyfills.cs`

Required to compile `record` types and `init`-only properties (C# 9 features) when targeting `net48`:

```csharp
#if NET48
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
#endif
```

---

### Task 5 — Update the GitHub Actions release workflow

**File:** `.github/workflows/release.yml`

Replace the current single-build workflow with a two-build workflow that produces separate `Revit2025Plus` and `Revit2024` folders in the release zip.

Key changes:
- Add `.NET Framework 4.8` SDK availability note (it is pre-installed on `windows-latest`)
- Run `dotnet publish` twice — once per framework target
- Stage `net8.0-windows` output into `dist/Revit2025Plus/RevitClaudeConnector/`
- Stage `net48` output into `dist/Revit2024/RevitClaudeConnector/`
- Copy `.addin` and `server/` into both variant folders
- Stamp `plugin_settings.json` version in both
- Zip the entire `dist/` as before

Sketch of new staging logic:
```powershell
# Build both targets
dotnet publish src/RevitClaudePlugIn/RevitClaudePlugIn.csproj -c Release -f net8.0-windows -o dist/Revit2025Plus/RevitClaudeConnector --no-self-contained
dotnet publish src/RevitClaudePlugIn/RevitClaudePlugIn.csproj -c Release -f net48        -o dist/Revit2024/RevitClaudeConnector    --no-self-contained

# BuiltInTools — both frameworks
dotnet build src/BuiltInTools/BuiltInTools.csproj -c Release

# Stage BuiltIn tools for each variant
foreach ($variant in @("Revit2025Plus", "Revit2024")) {
    $fw = if ($variant -eq "Revit2025Plus") { "net8.0-windows" } else { "net48" }
    $dest = "dist/$variant/RevitClaudeConnector/Tools/Packages/BuiltIn"
    New-Item -ItemType Directory -Path "$dest/runners" -Force | Out-Null
    Copy-Item "src/BuiltInTools/bin/Release/$fw/BuiltInTools.dll" "$dest/runners/"
    Copy-Item src/BuiltInTools/Schemas "$dest/Schemas" -Recurse -Force
    Copy-Item src/BuiltInTools/manifest.json "$dest/"
    # Stamp version in plugin_settings.json
    $settings = Get-Content src/RevitClaudePlugIn/Resources/plugin_settings.json | ConvertFrom-Json
    $settings.version = "${{ github.ref_name }}".TrimStart('v')
    $settings | ConvertTo-Json | Set-Content "dist/$variant/RevitClaudeConnector/plugin_settings.json"
    # Copy addin and server
    Copy-Item src/RevitClaudePlugIn/Resources/RevitClaudeConnector.addin "dist/$variant/"
    New-Item -ItemType Directory -Path "dist/$variant/server" -Force | Out-Null
    Copy-Item src/Connector.Mcp.Server/Revit.Claude.Connector.Mcp.Server.mcpb "dist/$variant/server/"
}
```

---

## Implementation Order

1. **Task 4** — polyfill (5 min, unblocks compilation)
2. **Task 3** — csproj multi-targeting (30 min, get the build scaffolding in place first)
3. **Task 2** — port BuiltInTools to Newtonsoft.Json (1 hr, fix compilation errors that surface from Task 3)
4. **Task 1** — `ToolALC` AppDomain implementation (2–4 hrs, the core engineering)
5. **Task 5** — workflow update (30 min, do last once local builds are green)

---

## Definition of Done

- [ ] `dotnet build src/ -c Release` compiles cleanly for both `net8.0-windows` and `net48` with zero errors
- [ ] Plugin loads and tools invoke correctly in Revit 2026 (net8 build)
- [ ] Plugin loads and tools invoke correctly in Revit 2024 (net48 build)
- [ ] Release zip contains `Revit2025Plus/` and `Revit2024/` folders, each self-contained
- [ ] Custom tool hot-reload limitation on Revit 2024 is noted in INSTALL.md
