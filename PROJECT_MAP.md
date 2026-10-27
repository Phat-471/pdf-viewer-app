# PDF Pro: AI project map

This file is the starting point for AI agents and developers exploring this repository. It links to the detailed architecture, feature map, and generated code graph.

## Read in this order

1. [Project roadmap and architecture](docs/PROJECT_ROADMAP.md) — system boundaries, feature flows, source locations, risks, and proposed work.
2. [Interactive code relationship graph](graphify-out/graph.html) — browse code symbols and extracted relationships; use [graph data](graphify-out/graph.json) for programmatic search.
3. [Generated architecture report](graphify-out/GRAPH_REPORT.md) — graph clusters, high-connectivity nodes, and extraction limits.
4. [Function test and diagnostics guide](docs/FUNCTION_TEST_DIAGNOSTICS.md) — test one operation at a time and copy its trace when it fails.
5. [Release and update workflow](docs/RELEASE_UPDATES.md) — packaging and update behavior. Check [AGENTS.md](AGENTS.md) before edits.

## Repository scope

The Git repository contains PDF Pro and other adjacent WordPress/site material. For PDF Pro application work, start with `src/PdfViewerApp`, `src/PdfCore`, `src/PdfViewerApp.Tests`, `app-upgrade`, `tools`, `.github/workflows`, and relevant files in `docs`. Do not assume every top-level directory belongs to the desktop application.

## Ground rules for AI

- Treat current source and tests as authoritative. Generated graphs are navigation aids and may include inferred relationships; verify important claims in source.
- Treat [project_audit_report.md](project_audit_report.md) as a historical audit snapshot. Its date and paths predate current source organization; re-check each finding before acting on it.
- Keep unrelated working-tree changes out of commits. Never stage publish tokens or local publish configuration.
- Follow [AGENTS.md](AGENTS.md) and [DEVELOPMENT_RULES.md](DEVELOPMENT_RULES.md) for backups, versioning, release, and verification.
- App version at this map's creation: `1.13.7` (`VERSION.txt`). Re-read it before work because it changes over time.

## Quick navigation

| Task | Start here |
|---|---|
| App startup, tabs, keyboard shortcuts, ribbon wiring | `src/PdfViewerApp/UI/Windows/MainWindow.xaml.cs`; `src/PdfViewerApp/UI/Controls/MainRibbon.xaml.cs` |
| PDF page view, mouse tools, annotations, text editing | `src/PdfViewerApp/UI/Controls/PdfDocumentTab.xaml.cs` and `PdfDocumentTab.*.cs` |
| PDFium rendering and native lifetime | `src/PdfViewerApp/Core/PdfiumEngine.cs`; `PdfRenderSession.cs`; `Services/Cache/` |
| PDF mutation (merge, rotate, insert, delete, extract, text edits) | `src/PdfCore/src/lib.rs`; `src/PdfCore/src/text_editor.rs`; `src/PdfViewerApp/Services/PdfCoreInterop.cs` |
| Annotation data and print/export | `src/PdfViewerApp/Models/Annotations/`; `src/PdfViewerApp/Core/PdfDocumentPaginator.cs` |
| OCR and AI snapshot | `src/PdfViewerApp/Core/PdfOcrService.cs`; `src/PdfViewerApp/PdfViewerApp.Ai/`; `src/PdfViewerApp/UI/Controls/AiPanelControl.xaml.cs` |
| Activation, settings, cache, update | `src/PdfViewerApp/Services/`; `src/PdfViewerApp/Models/` |
| Tests | `src/PdfViewerApp.Tests/` |
| Build, installer, in-app update ZIP | `package_project.ps1`; `installer.iss`; `app-upgrade/`; `tools/release.ps1` |
| CI release and WordPress update notification | `.github/workflows/release.yml`; `tools/check_update_endpoint.ps1` |
