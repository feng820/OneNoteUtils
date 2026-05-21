# Incremental sync via JSON manifest with lastModifiedTime comparison

Sync is the default CLI mode. It compares OneNote's per-page `lastModifiedTime` from the hierarchy XML against a `.onenote-sync.json` manifest stored in the output directory. Only new, modified, renamed, or deleted pages trigger work. If no manifest exists, the first run behaves as a full export and creates one.

We considered timestamp-only detection (no manifest — just compare file modification times on disk) but rejected it because: file mtime can drift from OneNote's modification time due to system clock differences or manual edits; renamed pages can't be detected without tracking the original page ID → filename mapping; and cleanup of associated images/attachments requires knowing which files belong to which page.

A database (SQLite) was considered instead of JSON but rejected as overkill for a flat page-ID-to-metadata mapping that rarely exceeds a few thousand entries.
