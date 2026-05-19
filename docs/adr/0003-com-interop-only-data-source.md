# COM Interop as the sole OneNote data source

OneNote data is accessed exclusively through the OneNote COM API (`OneNote.Application`), which calls `GetHierarchy` and `GetPageContent` to retrieve XML. This requires the Win32 desktop version of OneNote to be installed.

We considered also supporting direct `.one` file parsing (undocumented binary format — fragile and labour-intensive) and the Microsoft Graph REST API (different auth model, limited binary attachment support). Both are fundamentally different data sources. The COM approach is proven by the POC, gives full access to all content including binary data, and is the only option that doesn't require significant R&D. The architecture isolates COM behind `IOneNoteSource`, so alternative sources can be added later without touching the parser or writer.
