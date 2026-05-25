# Explicit push command for Markdown → OneNote

Markdown files can be pushed to OneNote via an explicit `--push` CLI command. Push is on-demand (not automatic), takes a file or folder path, and targets a specific notebook and section. Existing pages are matched by page ID from the sync manifest and overwritten; new files create new pages.

We considered automatic bi-directional sync (detect Obsidian-side changes and push them back automatically) but chose explicit push because: automatic reverse sync requires tracking file modification times on the Obsidian side which is fragile; the risk of accidentally overwriting team content in OneNote is high without explicit user intent; and the primary use case is occasional sharing, not continuous two-way collaboration. OneNote remains the primary source of truth — when both sides have changed, OneNote wins.

The push pipeline reuses the existing domain model: Markdown → MarkdownReader → ContentElement tree → OneNoteXmlWriter → COM `UpdatePageContent`. V1 supports headings, paragraphs with formatting, lists, and tables. Images and code blocks are deferred.
