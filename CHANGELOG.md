# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.5] - 2026-03-30

### Fixed
- Claude panel now fills the full dockable pane area on all display scale settings (125 %, 150 %, etc.). Previously the embedded Claude window was sized using WPF device-independent pixels instead of physical pixels, leaving a black gap along the right and bottom edges on high-DPI machines. `MoveWindow` calls in both the initial embed and the resize handler now convert DIPs to physical pixels via the panel's `HwndSource.CompositionTarget.TransformToDevice` matrix.

[1.0.5]: https://github.com/IbrahimFahdah/revit-claude-mcp/releases/tag/v1.0.5

## [1.0.4] - 2026-03-24

### Fixed
- Release workflow now stamps `plugin_settings.json` with the release version so users are not prompted to update immediately after installing the latest release

[1.0.4]: https://github.com/IbrahimFahdah/revit-claude-mcp/releases/tag/v1.0.4

## [1.0.3] - 2026-03-23

### Changed
- Minor improvements to the UI Ribbon bar
- Updated documentation

[1.0.3]: https://github.com/IbrahimFahdah/revit-claude-mcp/releases/tag/v1.0.3

## [1.0.2] - 2026-03-22

### Changed
- UI improvements across the Revit add-in panel

[1.0.2]: https://github.com/IbrahimFahdah/revit-claude-mcp/releases/tag/v1.0.2

## [1.0.1] - 2026-03-22

### Changed
- Update check now uses the GitHub Releases API instead of Google Drive, providing stable download URLs and automatic release notes in the update dialog
- Version comparison upgraded to `System.Version` for correct semantic ordering

[1.0.1]: https://github.com/IbrahimFahdah/revit-claude-mcp/releases/tag/v1.0.1

## [1.0.0] - 2026-03-21

Initial open-source release of the Claude–Revit AI Connector using the Model Context Protocol (MCP).

### Added
- **MCP Server** — Node.js MCP server (compiled `.mcpb` bundle) that exposes Revit as tools to Claude
- **Revit Add-in** — C# WPF plugin (`RevitClaudePlugIn`) as the Revit-side entry point
- **Dynamic tool execution** — Roslyn-based C# script compiler (`RevitClaudeBridge`) for runtime tool evaluation without restarting Revit
- **48 built-in tools** covering:
  - Query: elements by category, level, family, type, view, workset, selection
  - Parameters: read and write element parameters and additional properties
  - Geometry: bounding boxes, locations, boundary lines, material layers
  - Visualization: graphic overrides, category/element visibility, view filters, isolate modes
  - Export: CSV export, IFC export, family size analysis
  - Model info: units, warnings, schedules, viewports on sheets
  - AI integration: image generation via Gemini Flash
- **GitHub Actions** release workflow — builds and packages a ZIP matching the Revit Addins folder layout
- **GitHub Pages** — VitePress documentation site deployed via CI
- **MPL-2.0 license**

### Changed
- Restructured release ZIP so it installs directly into the Revit Addins directory without extra nesting
- Flattened `Resources/` into the plugin root for correct add-in loading
- Set canonical repository URLs to `IbrahimFahdah/revit-claude-mcp`

### Fixed
- VitePress base path for GitHub Pages deployment
- CI runner directory creation before copying `BuiltInTools.dll`
- `.nojekyll` file added so GitHub Pages does not process VitePress output through Jekyll

[1.0.0]: https://github.com/IbrahimFahdah/revit-claude-mcp/releases/tag/v1.0.0
