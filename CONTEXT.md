# OneNoteUtils

A tool that exports OneNote notebooks to Obsidian-compatible Markdown, preserving hierarchy, formatting, images, tables, and attachments.

## Language

**Notebook**:
A OneNote notebook — the top-level container identified by name or folder path.
_Avoid_: binder, project

**Section**:
A named grouping of pages inside a **Notebook** (maps to a OneNote section/tab).
_Avoid_: folder, tab, category

**Page**:
A single OneNote page within a **Section**. Has a title, content, and a level indicating its depth in the parent–child hierarchy.
_Avoid_: note, document, file

**Page Level**:
An integer (1 = root) that encodes the parent–child nesting of **Pages** within a **Section**. A level-2 page is a child of the nearest preceding level-1 page.
_Avoid_: indent, depth

**Content Element**:
A discrete unit of content inside a **Page** — a heading, paragraph, list, table, image, or attachment. Content elements are ordered and can nest recursively (e.g. an image inside a list item).
_Avoid_: block, node, component

**Run**:
A span of inline text within a paragraph with uniform formatting (bold, italic, strikethrough, hyperlink). A paragraph contains one or more **Runs**.
_Avoid_: span, fragment, chunk

**Source**:
The component that reads raw data from OneNote (currently via COM Interop). Produces hierarchy and page XML.
_Avoid_: reader, provider, connector

**Parser**:
The component that transforms raw OneNote XML into the domain model (**Notebook** → **Section** → **Page** → **Content Element** tree).
_Avoid_: transformer, converter, mapper

**Writer**:
The component that serializes the domain model to a specific output format (currently Obsidian Markdown). Responsible for file layout, link syntax, and presentation concerns like subpage link sections.
_Avoid_: exporter, renderer, formatter

**Export**:
The end-to-end process of reading a **Notebook** from a **Source**, parsing it, and writing it via a **Writer** to disk.
_Avoid_: sync, conversion, migration

## Relationships

- A **Notebook** contains one or more **Sections**
- A **Section** contains zero or more **Pages**
- A **Page** contains an ordered list of **Content Elements**
- A **Content Element** may recursively contain other **Content Elements** (e.g. a table cell or list item containing images and formatted text)
- A paragraph **Content Element** contains one or more **Runs**
- **Page Level** determines the parent–child tree of **Pages** within a **Section**

## Example dialogue

> **Dev:** "When the **Parser** encounters a page with **Page Level** 2, how does it know which page is the parent?"
> **Domain expert:** "It walks the preceding pages in document order — the parent is the nearest earlier page whose **Page Level** is one less."

> **Dev:** "Should the **Writer** flatten all **Pages** into one folder?"
> **Domain expert:** "No — a **Page** with children becomes a subfolder. The **Writer** also appends subpage links, but that's a presentation concern, not part of the domain model."

## Flagged ambiguities

- "export" vs "sync" — resolved: this tool does a full **Export** (overwrite). Incremental sync is a future concern.
- "converter" was used to mean both the XML-to-model transformation and the model-to-Markdown serialization — resolved: the former is the **Parser**, the latter is the **Writer**.
