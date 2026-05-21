# OneNoteUtils

Export OneNote notebooks to Obsidian-compatible Markdown with incremental sync. Preserves hierarchy, formatting, images, tables, and attachments.

## Features

- **Incremental sync** — only fetches and exports pages that changed since the last run
- **Full content export** — headings, paragraphs, bold/italic/strikethrough, hyperlinks, underline
- **Images & attachments** — extracted into `_attachments/` subfolder with Obsidian `![[embed]]` syntax
- **Tables** — rendered as markdown tables; single-column "container" tables rendered as content blocks
- **Nested lists** — bullet and numbered lists with correct indentation and standard markdown markers
- **Page hierarchy** — parent/child pages become nested folders with subpage links
- **YAML frontmatter** — page title, OneNote page ID, section name
- **Section filtering** — export specific sections via CLI or config
- **Rename & delete detection** — renames clean up old files, deleted pages are removed
- **Skip-and-continue** — individual page failures don't block the rest of the export
- **Structured logging** — progress tracking with per-page logging

## Prerequisites

- **Windows** — required for OneNote COM interop
- **OneNote desktop** (Win32, not UWP/Store) — must be installed and running with notebooks open
- **.NET 8.0 SDK** or later — to build and run

## Quick Start

```bash
# Clone the repo
git clone https://github.com/ciarancoady/OneNoteUtils.git
cd OneNoteUtils

# Build
dotnet build OneNoteUtils.slnx

# Sync a notebook (incremental — default)
dotnet run --project src/OneNoteUtils.Cli -- -n "My Notebook" -o "C:\Export"

# Sync a specific section
dotnet run --project src/OneNoteUtils.Cli -- -n "My Notebook" -o "C:\Export" -s "Daily Notes"

# Force full re-export (ignores sync manifest)
dotnet run --project src/OneNoteUtils.Cli -- -n "My Notebook" -o "C:\Export" --full

# Verbose logging
dotnet run --project src/OneNoteUtils.Cli -- -n "My Notebook" -o "C:\Export" -v
```

### Sync Behaviour

By default, the tool performs an **incremental sync**:
1. First run: no manifest exists → full export + creates `.onenote-sync.json`
2. Subsequent runs: compares `lastModifiedTime` → only syncs changed pages
3. Nothing changed? Exits instantly after one hierarchy call (~5 seconds)

New pages are exported. Modified pages are re-exported. Deleted/renamed pages have their old files cleaned up. Use `--full` to force a clean re-export.

## CLI Reference

```
Usage:
  OneNoteUtils.Cli --notebook <name> --output <path> [options]

Required:
  -n, --notebook <name>    Notebook name or folder path
  -o, --output <path>      Output directory

Options:
  -s, --section <name>     Only export this section (repeatable)
      --full               Force full export (skip incremental sync)
  -c, --config <path>      Path to a JSON config file (default: appsettings.json)
  -v, --verbose            Enable debug logging
  -h, --help               Show this help message
```

## Configuration

Default settings can be overridden in `appsettings.json` or via `--config`:

```json
{
  "ExportOptions": {
    "SectionFilter": [],
    "IncludeFrontmatter": true,
    "UseObsidianWikilinks": true,
    "EmbedImages": true,
    "EmbedPdfs": false,
    "UseAliasLinks": true,
    "AlwaysSuffixWithShortId": false,
    "ShortIdLength": 8
  }
}
```

## Architecture

The solution is split into four projects with enforced layer boundaries (see [ADR-0002](docs/adr/0002-four-project-solution-structure.md)):

```
OneNoteUtils.slnx
├── src/
│   ├── OneNoteUtils.Core/              — Domain models, parsers, interfaces
│   ├── OneNoteUtils.OneNote/           — COM interop via dedicated STA thread (net8.0-windows)
│   ├── OneNoteUtils.Writers.Obsidian/  — Obsidian Markdown writer
│   └── OneNoteUtils.Cli/              — Entry point, config, DI wiring
└── tests/
    ├── OneNoteUtils.Core.Tests/        — Parser and utility tests (42 tests)
    └── OneNoteUtils.Writers.Obsidian.Tests/ — Writer output tests (15 tests)
```

**Pipeline:** OneNote COM → raw XML → domain model (Notebook → Section → Page → ContentElement tree) → Markdown files on disk. Incremental sync uses a `.onenote-sync.json` manifest to track state between runs (see [ADR-0004](docs/adr/0004-incremental-sync-via-json-manifest.md)).

See [CONTEXT.md](CONTEXT.md) for the domain glossary and [docs/adr/](docs/adr/) for architectural decisions.

## Running Tests

```bash
dotnet test OneNoteUtils.slnx
```

## AI Agent Skills

This repo uses [Matt Pocock's Skills](https://github.com/mattpocock/skills) for AI-assisted development. Skills are installed in `.agents/skills/` and configured via `AGENTS.md` and `docs/agents/`.

Available skills include:
- `/grill-with-docs` — stress-test plans against the domain model and sharpen terminology
- `/tdd` — test-driven development with red-green-refactor loop
- `/diagnose` — disciplined debugging loop for hard bugs
- `/to-issues` — break plans into GitHub Issues
- `/triage` — triage issues through a state machine
- `/improve-codebase-architecture` — find deepening opportunities in the codebase

See [AGENTS.md](AGENTS.md) for the full configuration.

## License

See [LICENSE](LICENSE) for details.
