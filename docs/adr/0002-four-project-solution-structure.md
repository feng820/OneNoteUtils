# Four-project solution structure with layer isolation

The solution is split into four projects: `OneNoteUtils.Core` (domain models + parser), `OneNoteUtils.OneNote` (COM interop behind `IOneNoteSource`), `OneNoteUtils.Writers.Obsidian` (Obsidian Markdown writer), and `OneNoteUtils.Cli` (entry point + DI wiring). Two test projects mirror the testable libraries.

We considered a simpler 2-project split (one library + one console app) but chose 4 projects to enforce layer boundaries at the dependency level. `Core` has no dependency on COM or any output format. `Writers.Obsidian` depends only on `Core`. `OneNote` depends only on `Core`. `Cli` wires them together. This makes it impossible for the parser to accidentally call COM, or for the writer to depend on OneNote internals — the compiler enforces what code review would otherwise have to catch.
