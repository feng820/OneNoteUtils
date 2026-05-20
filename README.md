# OneNoteUtils

Export OneNote notebooks to Obsidian-compatible Markdown, preserving hierarchy, formatting, images, tables, and attachments.

## Features

- **Full content export** — headings, paragraphs, bold/italic/strikethrough, hyperlinks, underline
- **Images & attachments** — extracted as files with Obsidian `![[embed]]` syntax
- **Tables** — rendered as markdown tables; single-column "container" tables rendered as content blocks
- **Nested lists** — bullet and numbered lists with correct indentation and standard markdown markers
- **Page hierarchy** — parent/child pages become nested folders with subpage links
- **YAML frontmatter** — page title, OneNote page ID, section name
- **Section filtering** — export specific sections via CLI or config
- **Skip-and-continue** — individual page failures don't block the rest of the export
- **Structured logging** — progress tracking with per-page logging

## Prerequisites

- **Windows** — required for OneNote COM interop
- **OneNote desktop** (Win32, not UWP/Store) — must be installed and running with notebooks open
- **Windows PowerShell 5.1** — used as a bridge for COM interop (ships with Windows)
- **.NET 8.0 SDK** or later — to build and run

## Quick Start

```bash
# Clone the repo
git clone https://github.com/ciarancoady/OneNoteUtils.git
cd OneNoteUtils

# Build
dotnet build OneNoteUtils.slnx

# Export a notebook
dotnet run --project src/OneNoteUtils.Cli -- -n "My Notebook" -o "C:\Export"

# Export a specific section
dotnet run --project src/OneNoteUtils.Cli -- -n "My Notebook" -o "C:\Export" -s "Daily Notes"

# Export multiple sections
dotnet run --project src/OneNoteUtils.Cli -- -n "My Notebook" -o "C:\Export" -s "Section A" -s "Section B"

# Verbose logging
dotnet run --project src/OneNoteUtils.Cli -- -n "My Notebook" -o "C:\Export" -v
```

## CLI Reference

```
Usage:
  OneNoteUtils.Cli --notebook <name> --output <path> [options]

Required:
  -n, --notebook <name>    Notebook name or folder path
  -o, --output <path>      Output directory

Options:
  -s, --section <name>     Only export this section (repeatable)
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
│   ├── OneNoteUtils.OneNote/           — COM interop via PS5.1 bridge (net8.0-windows)
│   ├── OneNoteUtils.Writers.Obsidian/  — Obsidian Markdown writer
│   └── OneNoteUtils.Cli/              — Entry point, config, DI wiring
└── tests/
    ├── OneNoteUtils.Core.Tests/        — Parser and utility tests (42 tests)
    └── OneNoteUtils.Writers.Obsidian.Tests/ — Writer output tests (15 tests)
```

**Pipeline:** OneNote COM → raw XML → domain model (Notebook → Section → Page → ContentElement tree) → Markdown files on disk.

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
